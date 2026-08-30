using System;
using UnityEngine;

namespace Bjorn.ThirdPerson
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource), typeof(Collider))]
    public sealed class RecordableMicrophone : MonoBehaviour, IGtaStyleInteractable
    {
        [Header("Microphone")]
        [SerializeField] private string displayName = "Recordable Microphone";
        [SerializeField] private MicrophoneQualityTier quality = MicrophoneQualityTier.Standard;
        [SerializeField, Range(5, 300)] private int maximumRecordingSeconds = 120;
        [SerializeField] private MicrophoneRecorderWindow recorderWindowPrefab;

        private AudioSource playbackSource;
        private AudioClip rawRecording;
        private AudioClip lastRecording;
        private string activeDevice;
        private int lastRecordedPosition;
        private float liveLevel;

        public string InteractionPrompt => $"Use {displayName}";
        public string DisplayName => displayName;
        public MicrophoneQualityTier Quality => quality;
        public MicrophoneQualityProfile QualityProfile => MicrophoneQualityProcessor.GetProfile(quality);
        public int MaximumRecordingSeconds => maximumRecordingSeconds;
        public bool IsRecording => rawRecording != null && !string.IsNullOrEmpty(activeDevice);
        public float RecordingSeconds => rawRecording != null && rawRecording.frequency > 0
            ? Mathf.Max(lastRecordedPosition, GetCurrentPosition()) / (float)rawRecording.frequency
            : 0f;
        public float LiveLevel => liveLevel;
        public AudioClip LastRecording => lastRecording;
        public MicrophoneRecorderWindow RecorderWindowPrefab => recorderWindowPrefab;

        private void Awake()
        {
            playbackSource = GetComponent<AudioSource>();
            playbackSource.playOnAwake = false;
            playbackSource.loop = false;
            playbackSource.spatialBlend = 0f;
        }

        private void Update()
        {
            if (string.IsNullOrEmpty(activeDevice) || rawRecording == null)
            {
                liveLevel = Mathf.MoveTowards(liveLevel, 0f, Time.unscaledDeltaTime * 4f);
                return;
            }

            int position = GetCurrentPosition();
            if (position > 0)
            {
                lastRecordedPosition = Mathf.Max(lastRecordedPosition, position);
                UpdateLiveLevel(position);
            }
        }

        public bool CanInteract(GtaStylePlayerInteractor interactor)
        {
            return interactor != null && !MicrophoneRecorderWindow.IsOpen;
        }

        public void Interact(GtaStylePlayerInteractor interactor)
        {
            if (recorderWindowPrefab == null)
            {
                Debug.LogError($"{name} has no recorder window prefab assigned.", this);
                return;
            }
            MicrophoneRecorderWindow.Show(recorderWindowPrefab, this, interactor);
        }

        public bool StartRecording(string deviceName, out string error)
        {
            error = string.Empty;
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                error = "No recording device was detected by Unity.";
                return false;
            }
            if (IsRecording)
            {
                error = "This microphone is already recording.";
                return false;
            }

            activeDevice = string.IsNullOrEmpty(deviceName) ? Microphone.devices[0] : deviceName;
            MicrophoneQualityProfile profile = QualityProfile;
            lastRecordedPosition = 0;
            liveLevel = 0f;
            int captureRate = profile.CaptureSampleRate;
            Microphone.GetDeviceCaps(activeDevice, out int minimumFrequency, out int maximumFrequency);
            if (minimumFrequency > 0 && maximumFrequency > 0)
            {
                captureRate = Mathf.Clamp(captureRate, minimumFrequency, maximumFrequency);
            }
            rawRecording = Microphone.Start(activeDevice, false, maximumRecordingSeconds, captureRate);
            if (rawRecording == null)
            {
                error = $"Unity could not start device '{activeDevice}'. Check OS microphone permission.";
                activeDevice = null;
                return false;
            }

            return true;
        }

        public AudioClip StopRecording(out string error)
        {
            error = string.Empty;
            if (rawRecording == null || string.IsNullOrEmpty(activeDevice))
            {
                error = "There is no active recording to stop.";
                return lastRecording;
            }

            int finalPosition = Mathf.Max(lastRecordedPosition, GetCurrentPosition());
            if (Microphone.IsRecording(activeDevice))
            {
                Microphone.End(activeDevice);
            }

            string completedDevice = activeDevice;
            activeDevice = null;
            liveLevel = 0f;
            if (finalPosition <= 0)
            {
                rawRecording = null;
                error = $"Device '{completedDevice}' returned no audio. Check microphone permission and input level.";
                return null;
            }

            try
            {
                if (lastRecording != null)
                {
                    Destroy(lastRecording);
                }
                lastRecording = MicrophoneQualityProcessor.Process(rawRecording, finalPosition, quality);
                playbackSource.clip = lastRecording;
            }
            catch (Exception exception)
            {
                error = $"Recording processing failed: {exception.Message}";
                Debug.LogException(exception, this);
            }
            finally
            {
                Destroy(rawRecording);
                rawRecording = null;
            }

            return lastRecording;
        }

        public void CancelRecording()
        {
            if (!string.IsNullOrEmpty(activeDevice) && Microphone.IsRecording(activeDevice))
            {
                Microphone.End(activeDevice);
            }
            activeDevice = null;
            liveLevel = 0f;
            if (rawRecording != null)
            {
                Destroy(rawRecording);
                rawRecording = null;
            }
        }

        public void PlayLastRecording()
        {
            if (lastRecording == null)
            {
                return;
            }
            playbackSource.Stop();
            playbackSource.clip = lastRecording;
            playbackSource.Play();
        }

        public void StopPlayback()
        {
            playbackSource?.Stop();
        }

        public string SaveLastRecording()
        {
            if (lastRecording == null)
            {
                throw new InvalidOperationException("Record something before exporting a WAV file.");
            }
            return WavFileWriter.SaveToRecordingsFolder(lastRecording, $"{displayName}_{QualityProfile.DisplayName}");
        }

        public float[] GetWaveform(int pointCount)
        {
            AudioClip clip = IsRecording ? rawRecording : lastRecording;
            int availableFrames = IsRecording ? Mathf.Max(lastRecordedPosition, GetCurrentPosition()) : (clip != null ? clip.samples : 0);
            if (clip == null || availableFrames <= 0 || pointCount <= 0)
            {
                return Array.Empty<float>();
            }

            availableFrames = Mathf.Clamp(availableFrames, 1, clip.samples);
            int framesToRead = Mathf.Min(availableFrames, Mathf.Min(clip.frequency * 2, pointCount * 128));
            int startFrame = Mathf.Max(0, availableFrames - framesToRead);
            float[] samples = new float[framesToRead * clip.channels];
            if (!clip.GetData(samples, startFrame))
            {
                return Array.Empty<float>();
            }

            float[] waveform = new float[pointCount];
            for (int point = 0; point < pointCount; point++)
            {
                int frameStart = point * framesToRead / pointCount;
                int frameEnd = Mathf.Max(frameStart + 1, (point + 1) * framesToRead / pointCount);
                float peak = 0f;
                float signedPeak = 0f;
                for (int frame = frameStart; frame < frameEnd && frame < framesToRead; frame++)
                {
                    float mono = 0f;
                    for (int channel = 0; channel < clip.channels; channel++)
                    {
                        mono += samples[frame * clip.channels + channel];
                    }
                    mono /= clip.channels;
                    if (Mathf.Abs(mono) > peak)
                    {
                        peak = Mathf.Abs(mono);
                        signedPeak = mono;
                    }
                }
                waveform[point] = signedPeak;
            }
            return waveform;
        }

        public void Configure(string microphoneName, MicrophoneQualityTier qualityTier, int maxSeconds,
            MicrophoneRecorderWindow windowPrefab)
        {
            displayName = microphoneName;
            quality = qualityTier;
            maximumRecordingSeconds = Mathf.Clamp(maxSeconds, 5, 300);
            recorderWindowPrefab = windowPrefab;
        }

        private int GetCurrentPosition()
        {
            if (string.IsNullOrEmpty(activeDevice))
            {
                return 0;
            }
            return Mathf.Max(0, Microphone.GetPosition(activeDevice));
        }

        private void UpdateLiveLevel(int position)
        {
            int frameCount = Mathf.Min(128, position);
            if (frameCount <= 0 || rawRecording == null)
            {
                return;
            }

            int startFrame = Mathf.Max(0, position - frameCount);
            float[] samples = new float[frameCount * rawRecording.channels];
            if (!rawRecording.GetData(samples, startFrame))
            {
                return;
            }

            float sumSquares = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                sumSquares += samples[i] * samples[i];
            }
            float rms = Mathf.Sqrt(sumSquares / samples.Length);
            liveLevel = Mathf.Lerp(liveLevel, Mathf.Clamp01(rms * 5f), 0.45f);
        }

        private void OnDisable()
        {
            CancelRecording();
        }
    }
}
