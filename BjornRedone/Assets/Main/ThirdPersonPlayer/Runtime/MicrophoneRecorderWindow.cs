using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Bjorn.ThirdPerson
{
    [DisallowMultipleComponent]
    public sealed class MicrophoneRecorderWindow : MonoBehaviour
    {
        [Header("Controls")]
        [SerializeField] private Button recordButton;
        [SerializeField] private Button stopButton;
        [SerializeField] private Button playButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button nextDeviceButton;
        [SerializeField] private Button closeButton;

        [Header("Display")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text deviceText;
        [SerializeField] private Text timeText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text effectsText;
        [SerializeField] private Image levelFill;
        [SerializeField] private WaveformGraphic waveform;

        private static MicrophoneRecorderWindow activeInstance;
        private RecordableMicrophone sourceMicrophone;
        private GtaStylePlayerInteractor playerInteractor;
        private string[] devices = Array.Empty<string>();
        private int selectedDeviceIndex;
        private float nextWaveformRefresh;

        public static bool IsOpen => activeInstance != null && activeInstance.gameObject.activeInHierarchy;

        private void Awake()
        {
            activeInstance = this;
            recordButton?.onClick.AddListener(Record);
            stopButton?.onClick.AddListener(Stop);
            playButton?.onClick.AddListener(Play);
            saveButton?.onClick.AddListener(Save);
            nextDeviceButton?.onClick.AddListener(SelectNextDevice);
            closeButton?.onClick.AddListener(Close);
        }

        private void OnDestroy()
        {
            if (activeInstance == this)
            {
                activeInstance = null;
            }
        }

        private void Update()
        {
            if (sourceMicrophone == null)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (sourceMicrophone.IsRecording)
            {
                timeText.text = FormatTime(sourceMicrophone.RecordingSeconds);
                levelFill.fillAmount = sourceMicrophone.LiveLevel;
                if (Time.unscaledTime >= nextWaveformRefresh)
                {
                    waveform.SetSamples(sourceMicrophone.GetWaveform(220));
                    nextWaveformRefresh = Time.unscaledTime + 0.075f;
                }

                if (sourceMicrophone.RecordingSeconds >= sourceMicrophone.MaximumRecordingSeconds - 0.05f)
                {
                    Stop();
                }
            }
            else
            {
                levelFill.fillAmount = Mathf.MoveTowards(levelFill.fillAmount, 0f, Time.unscaledDeltaTime * 2.5f);
            }
        }

        public static void Show(MicrophoneRecorderWindow prefab, RecordableMicrophone microphone,
            GtaStylePlayerInteractor interactor)
        {
            if (prefab == null || microphone == null)
            {
                return;
            }

            if (activeInstance == null)
            {
                activeInstance = Instantiate(prefab);
                activeInstance.name = "Microphone Recorder UI";
            }

            activeInstance.gameObject.SetActive(true);
            activeInstance.Open(microphone, interactor);
        }

        public static void CloseActive()
        {
            if (activeInstance != null)
            {
                activeInstance.Close();
            }
        }

        private void Open(RecordableMicrophone microphone, GtaStylePlayerInteractor interactor)
        {
            if (sourceMicrophone != null && sourceMicrophone != microphone && sourceMicrophone.IsRecording)
            {
                sourceMicrophone.StopRecording(out _);
            }

            sourceMicrophone = microphone;
            playerInteractor = interactor;
            playerInteractor?.SetModalUiOpen(true);
            EnsureEventSystem();

            devices = Microphone.devices ?? Array.Empty<string>();
            selectedDeviceIndex = Mathf.Clamp(selectedDeviceIndex, 0, Mathf.Max(0, devices.Length - 1));
            titleText.text = $"MICROPHONE RECORDER  —  {sourceMicrophone.QualityProfile.DisplayName}";
            effectsText.text = sourceMicrophone.QualityProfile.EffectsDescription;
            timeText.text = "00:00.0";
            levelFill.fillAmount = 0f;
            waveform.SetSamples(sourceMicrophone.LastRecording != null
                ? sourceMicrophone.GetWaveform(220)
                : Array.Empty<float>());
            UpdateDeviceLabel();
            SetStatus(devices.Length > 0
                ? "Ready. Recordings are processed by this microphone's hardware quality."
                : "No microphone detected. Connect one and allow OS microphone access.",
                devices.Length > 0);
            RefreshButtons();
        }

        private void Record()
        {
            if (sourceMicrophone == null || devices.Length == 0)
            {
                SetStatus("No microphone device is available.", false);
                return;
            }

            sourceMicrophone.StopPlayback();
            if (sourceMicrophone.StartRecording(devices[selectedDeviceIndex], out string error))
            {
                waveform.SetSamples(Array.Empty<float>());
                timeText.text = "00:00.0";
                SetStatus("Recording…", true);
            }
            else
            {
                SetStatus(error, false);
            }
            RefreshButtons();
        }

        private void Stop()
        {
            if (sourceMicrophone == null || !sourceMicrophone.IsRecording)
            {
                return;
            }

            AudioClip result = sourceMicrophone.StopRecording(out string error);
            if (result != null)
            {
                waveform.SetSamples(sourceMicrophone.GetWaveform(220));
                timeText.text = FormatTime(result.length);
                SetStatus($"Captured {result.length:F1}s. Quality effects are baked into playback and export.", true);
            }
            else
            {
                SetStatus(error, false);
            }
            RefreshButtons();
        }

        private void Play()
        {
            if (sourceMicrophone == null || sourceMicrophone.LastRecording == null)
            {
                return;
            }
            sourceMicrophone.PlayLastRecording();
            SetStatus("Playing processed recording…", true);
        }

        private void Save()
        {
            if (sourceMicrophone == null || sourceMicrophone.LastRecording == null)
            {
                return;
            }

            try
            {
                string path = sourceMicrophone.SaveLastRecording();
                SetStatus($"Saved WAV: {path}", true);
                Debug.Log($"[Microphone Recorder] Saved WAV to {path}", sourceMicrophone);
            }
            catch (Exception exception)
            {
                SetStatus(exception.Message, false);
                Debug.LogException(exception, sourceMicrophone);
            }
        }

        private void SelectNextDevice()
        {
            if (devices.Length <= 1 || (sourceMicrophone != null && sourceMicrophone.IsRecording))
            {
                return;
            }
            selectedDeviceIndex = (selectedDeviceIndex + 1) % devices.Length;
            UpdateDeviceLabel();
        }

        private void Close()
        {
            if (sourceMicrophone != null && sourceMicrophone.IsRecording)
            {
                sourceMicrophone.StopRecording(out _);
            }
            sourceMicrophone?.StopPlayback();
            playerInteractor?.SetModalUiOpen(false);
            playerInteractor = null;
            sourceMicrophone = null;
            gameObject.SetActive(false);
        }

        private void UpdateDeviceLabel()
        {
            deviceText.text = devices.Length == 0
                ? "INPUT:  NO DEVICE"
                : $"INPUT:  {devices[selectedDeviceIndex]}  ({selectedDeviceIndex + 1}/{devices.Length})";
        }

        private void RefreshButtons()
        {
            bool hasSource = sourceMicrophone != null;
            bool recording = hasSource && sourceMicrophone.IsRecording;
            bool hasRecording = hasSource && sourceMicrophone.LastRecording != null;
            recordButton.interactable = hasSource && devices.Length > 0 && !recording;
            stopButton.interactable = recording;
            playButton.interactable = hasRecording && !recording;
            saveButton.interactable = hasRecording && !recording;
            nextDeviceButton.interactable = devices.Length > 1 && !recording;
        }

        private void SetStatus(string message, bool positive)
        {
            statusText.text = message;
            statusText.color = positive ? new Color(0.57f, 0.9f, 0.66f) : new Color(1f, 0.52f, 0.45f);
        }

        private static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float remainder = seconds - minutes * 60f;
            return $"{minutes:00}:{remainder:00.0}";
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemObject.transform.SetAsFirstSibling();
        }

        public void Configure(Button record, Button stop, Button play, Button save, Button nextDevice, Button close,
            Text title, Text device, Text timer, Text status, Text effects, Image meterFill, WaveformGraphic waveformGraphic)
        {
            recordButton = record;
            stopButton = stop;
            playButton = play;
            saveButton = save;
            nextDeviceButton = nextDevice;
            closeButton = close;
            titleText = title;
            deviceText = device;
            timeText = timer;
            statusText = status;
            effectsText = effects;
            levelFill = meterFill;
            waveform = waveformGraphic;
        }
    }
}
