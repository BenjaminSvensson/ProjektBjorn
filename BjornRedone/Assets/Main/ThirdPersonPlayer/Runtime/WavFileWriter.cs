using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Bjorn.ThirdPerson
{
    public static class WavFileWriter
    {
        public static string SaveToRecordingsFolder(AudioClip clip, string suggestedName)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            string folder = Path.Combine(Application.persistentDataPath, "Recordings");
            Directory.CreateDirectory(folder);
            string safeName = SanitizeFileName(string.IsNullOrWhiteSpace(suggestedName) ? "Recording" : suggestedName);
            string path = Path.Combine(folder, $"{safeName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.wav");

            int sampleCount = clip.samples * clip.channels;
            float[] samples = new float[sampleCount];
            if (!clip.GetData(samples, 0))
            {
                throw new InvalidOperationException("Unity could not read the recording for WAV export.");
            }

            const int bitsPerSample = 16;
            int byteRate = clip.frequency * clip.channels * bitsPerSample / 8;
            int dataSize = sampleCount * sizeof(short);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)clip.channels);
                writer.Write(clip.frequency);
                writer.Write(byteRate);
                writer.Write((short)(clip.channels * bitsPerSample / 8));
                writer.Write((short)bitsPerSample);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);

                for (int i = 0; i < samples.Length; i++)
                {
                    short pcm = (short)Mathf.RoundToInt(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue);
                    writer.Write(pcm);
                }
            }

            return path;
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }
            return value.Trim();
        }
    }
}
