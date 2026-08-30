using System;
using UnityEngine;

namespace Bjorn.ThirdPerson
{
    public enum MicrophoneQualityTier
    {
        Studio,
        Standard,
        Cheap,
        Broken
    }

    [Serializable]
    public readonly struct MicrophoneQualityProfile
    {
        public readonly string DisplayName;
        public readonly string EffectsDescription;
        public readonly int CaptureSampleRate;
        public readonly int BitDepth;
        public readonly float LowPassCutoff;
        public readonly float NoiseAmount;
        public readonly float SaturationDrive;
        public readonly int SampleHoldFrames;
        public readonly float CrackleChance;
        public readonly float DropoutChance;
        public readonly bool ForceMono;

        public MicrophoneQualityProfile(string displayName, string effectsDescription, int captureSampleRate,
            int bitDepth, float lowPassCutoff, float noiseAmount, float saturationDrive, int sampleHoldFrames,
            float crackleChance, float dropoutChance, bool forceMono)
        {
            DisplayName = displayName;
            EffectsDescription = effectsDescription;
            CaptureSampleRate = captureSampleRate;
            BitDepth = bitDepth;
            LowPassCutoff = lowPassCutoff;
            NoiseAmount = noiseAmount;
            SaturationDrive = saturationDrive;
            SampleHoldFrames = sampleHoldFrames;
            CrackleChance = crackleChance;
            DropoutChance = dropoutChance;
            ForceMono = forceMono;
        }
    }

    public static class MicrophoneQualityProcessor
    {
        public static MicrophoneQualityProfile GetProfile(MicrophoneQualityTier tier)
        {
            switch (tier)
            {
                case MicrophoneQualityTier.Studio:
                    return new MicrophoneQualityProfile("STUDIO", "48 kHz · clean capture · gentle peak protection",
                        48000, 16, 19000f, 0f, 1.02f, 1, 0f, 0f, false);
                case MicrophoneQualityTier.Standard:
                    return new MicrophoneQualityProfile("STANDARD", "44.1 kHz · light roll-off · subtle preamp grain",
                        44100, 14, 12500f, 0.0012f, 1.12f, 1, 0.000015f, 0f, false);
                case MicrophoneQualityTier.Cheap:
                    return new MicrophoneQualityProfile("CHEAP", "22 kHz · 8-bit crunch · narrow bandwidth · hiss",
                        22050, 8, 4600f, 0.0075f, 1.85f, 2, 0.00018f, 0.000012f, true);
                default:
                    return new MicrophoneQualityProfile("BROKEN", "11 kHz · 5-bit crush · heavy filter · static · dropouts",
                        11025, 5, 2350f, 0.022f, 2.8f, 4, 0.00065f, 0.000055f, true);
            }
        }

        public static AudioClip Process(AudioClip source, int sampleFrames, MicrophoneQualityTier tier)
        {
            if (source == null)
            {
                return null;
            }

            int frames = Mathf.Clamp(sampleFrames, 1, source.samples);
            int sourceChannels = Mathf.Max(1, source.channels);
            float[] sourceSamples = new float[frames * sourceChannels];
            if (!source.GetData(sourceSamples, 0))
            {
                throw new InvalidOperationException("Unity could not read the captured microphone samples.");
            }

            float[] processed = ProcessSamples(sourceSamples, frames, sourceChannels, source.frequency, tier, out int outputChannels);
            string clipName = $"{GetProfile(tier).DisplayName} Recording {DateTime.Now:HH-mm-ss}";
            AudioClip result = AudioClip.Create(clipName, frames, outputChannels, source.frequency, false);
            result.SetData(processed, 0);
            return result;
        }

        public static float[] ProcessSamples(float[] source, int frames, int sourceChannels, int sampleRate,
            MicrophoneQualityTier tier, out int outputChannels)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (frames <= 0 || sourceChannels <= 0 || source.Length < frames * sourceChannels)
            {
                throw new ArgumentException("The supplied sample buffer does not match its frame/channel metadata.");
            }

            MicrophoneQualityProfile profile = GetProfile(tier);
            outputChannels = profile.ForceMono ? 1 : sourceChannels;
            float[] mixed = new float[frames * outputChannels];

            if (outputChannels == sourceChannels)
            {
                Array.Copy(source, mixed, mixed.Length);
            }
            else
            {
                for (int frame = 0; frame < frames; frame++)
                {
                    float sum = 0f;
                    int sourceOffset = frame * sourceChannels;
                    for (int channel = 0; channel < sourceChannels; channel++)
                    {
                        sum += source[sourceOffset + channel];
                    }
                    mixed[frame] = sum / sourceChannels;
                }
            }

            float cutoff = Mathf.Clamp(profile.LowPassCutoff, 80f, sampleRate * 0.48f);
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float alpha = (1f / sampleRate) / (rc + 1f / sampleRate);
            float[] lowPassState = new float[outputChannels];
            float[] heldSample = new float[outputChannels];
            int quantizationSteps = Mathf.Max(1, (1 << Mathf.Clamp(profile.BitDepth - 1, 1, 20)) - 1);
            int sampleRateHold = Mathf.Max(1, Mathf.RoundToInt(sampleRate / (float)profile.CaptureSampleRate));
            int effectiveSampleHold = Mathf.Max(1, profile.SampleHoldFrames * sampleRateHold);
            float saturationNormalizer = Mathf.Max(0.0001f, (float)Math.Tanh(profile.SaturationDrive));
            System.Random random = new System.Random(frames * 17 + sourceChannels * 131 + (int)tier * 7919);
            int dropoutFramesRemaining = 0;

            for (int frame = 0; frame < frames; frame++)
            {
                if (dropoutFramesRemaining <= 0 && profile.DropoutChance > 0f && random.NextDouble() < profile.DropoutChance)
                {
                    dropoutFramesRemaining = random.Next(Mathf.Max(8, sampleRate / 180), Mathf.Max(16, sampleRate / 28));
                }

                for (int channel = 0; channel < outputChannels; channel++)
                {
                    int index = frame * outputChannels + channel;
                    float sample = mixed[index];
                    lowPassState[channel] += alpha * (sample - lowPassState[channel]);
                    sample = lowPassState[channel];

                    if (frame % effectiveSampleHold == 0)
                    {
                        heldSample[channel] = sample;
                    }
                    sample = heldSample[channel];

                    if (profile.NoiseAmount > 0f)
                    {
                        sample += ((float)random.NextDouble() * 2f - 1f) * profile.NoiseAmount;
                    }
                    if (profile.CrackleChance > 0f && random.NextDouble() < profile.CrackleChance)
                    {
                        sample += ((float)random.NextDouble() * 2f - 1f) * Mathf.Lerp(0.15f, 0.7f, (int)tier / 3f);
                    }
                    if (dropoutFramesRemaining > 0)
                    {
                        sample *= tier == MicrophoneQualityTier.Broken ? 0.025f : 0.18f;
                    }

                    sample = (float)Math.Tanh(sample * profile.SaturationDrive) / saturationNormalizer;
                    sample = Mathf.Round(sample * quantizationSteps) / quantizationSteps;
                    mixed[index] = Mathf.Clamp(sample, -1f, 1f);
                }

                if (dropoutFramesRemaining > 0)
                {
                    dropoutFramesRemaining--;
                }
            }

            return mixed;
        }
    }
}
