using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Sound.Services.Stores.States;
using Unity.AI.Generators.UI.Utilities;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Sound.Services.Utilities
{
    static class AudioClipExtensions
    {
        public static async Task Play(this AudioClip audioClip, CancellationToken token, Action<float> timeUpdate = null, SoundEnvelopeSettings envelopeSettings = null)
        {
            EditorUtility.audioMasterMute = false;

            var playClip = audioClip;
            var timeOffset = 0.0f;
            if (envelopeSettings != null)
            {
                audioClip.TryGetSamples(out var samples);
                audioClip.ApplyEnvelope(samples, envelopeSettings.controlPoints);
                // trimming before enveloping would be more efficient, but also, more error-prone
                samples = audioClip.ApplyTrim(samples, envelopeSettings.startPosition, envelopeSettings.endPosition);
                playClip = AudioClip.Create(audioClip.name + "_clone", samples.Length, audioClip.channels, audioClip.frequency, false);
                playClip.SetData(samples, 0);

                timeOffset = envelopeSettings.startPosition * audioClip.length;
            }

            var audioSource = new GameObject("AudioClipPlayer") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<AudioSource>();
            audioSource.clip = playClip;
            audioSource.Play();

            while (audioSource.isPlaying)
            {
                try { timeUpdate?.Invoke(timeOffset + audioSource.time); }
                catch { /* ignored */ }

                if (token.IsCancellationRequested)
                    break;
                await Task.Yield();
            }

            if (audioSource.isPlaying)
                audioSource.Stop();

            try { timeUpdate?.Invoke(0); }
            catch { /* ignored */ }

            audioSource.gameObject.SafeDestroy();
            if (envelopeSettings != null)
                playClip.SafeDestroy();
        }
        public static Task Play(this AudioClip audioClip, Action<float> timeUpdate = null, SoundEnvelopeSettings envelopeSettings = null) =>
            Play(audioClip, CancellationToken.None, timeUpdate, envelopeSettings);

        public static SoundEnvelopeSettings MakeDefaultEnvelope(this AudioClip audioClip, float startPosition = 0, float endPosition = 1)
        {
            // auto fade in-out
            const float fadeInOut = 0.005f; // seconds
            return new SoundEnvelopeSettings {
                startPosition = startPosition,
                endPosition = endPosition,
                controlPoints = new List<Vector2>
                {
                    new(Mathf.Max(0, startPosition), 0),
                    new(Mathf.Max(0, startPosition + GetNormalizedPositionAtTime(audioClip, fadeInOut)), 1),
                    new(Mathf.Min(1, endPosition - GetNormalizedPositionAtTime(audioClip, fadeInOut)), 1),
                    new(Mathf.Min(1, endPosition), 0)
                }};
        }

        static int GetSampleIndexAtPosition(this AudioClip audioClip, float position) => Mathf.FloorToInt(Mathf.Clamp01(position) * audioClip.samples);

        static int GetSampleIndexAtPositionUnclamped(this AudioClip audioClip, float position) => Mathf.FloorToInt(position * audioClip.samples);

        public static float GetNormalizedPositionAtSampleIndex(this AudioClip audioClip, int sample) => Mathf.Clamp01(sample / (float)audioClip.samples);

        public static float GetNormalizedPositionAtTime(this AudioClip audioClip, float time) => Mathf.Clamp01(time / audioClip.length);

        public static float GetNormalizedPositionAtTimeUnclamped(this AudioClip audioClip, float time) => audioClip.length < Mathf.Epsilon ? -1f : time / audioClip.length;

        public static void EncodeToWav(this AudioClip audioClip, Stream outputStream, SoundEnvelopeSettings envelopeSettings = null)
        {
            if (!TryGetSamples(audioClip, out var audioSamples))
                return;
            envelopeSettings ??= new SoundEnvelopeSettings();
            ApplyEnvelope(audioClip, audioSamples, envelopeSettings.controlPoints);
            var samples = GetSampleRange(audioClip, audioSamples, envelopeSettings.startPosition, envelopeSettings.endPosition);
            EncodeToWav(samples, outputStream, audioClip.channels, audioClip.frequency);
        }

        public static void EncodeToWav(this AudioClip audioClip, Stream outputStream, float[] audioSamples = null, SoundEnvelopeSettings envelopeSettings = null)
        {
            if (audioSamples == null && !audioClip.TryGetSamples(out audioSamples))
                return;

            envelopeSettings ??= new SoundEnvelopeSettings();
            ApplyEnvelope(audioClip, audioSamples, envelopeSettings.controlPoints);

            var samples = GetSampleRange(audioClip, audioSamples, envelopeSettings.startPosition, envelopeSettings.endPosition);
            EncodeToWav(samples, outputStream, audioClip.channels, audioClip.frequency);
        }

        public static void EncodeToWav(IReadOnlyCollection<float> samples, Stream outputStream, int numChannels = 1, int sampleRate = 44100)
        {
            // Audio Specifications
            const int bitsPerSample = 16; // Bits per sample
            const int bytesPerSample = bitsPerSample / 8;
            var blockAlign = numChannels * bytesPerSample;
            var byteRate = sampleRate * blockAlign;
            var numSamples = samples.Count * numChannels;
            var dataChunkSize = numSamples * bytesPerSample;
            const int subChunk1Size = 16; // PCM header size for 'fmt ' chunk
            var subChunk2Size = dataChunkSize;
            var chunkSize = 4 + (8 + subChunk1Size) + (8 + subChunk2Size);

            using var writer = new BinaryWriter(outputStream, System.Text.Encoding.ASCII, true);

            // Write the WAV file header
            // RIFF header
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(chunkSize);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });

            // 'fmt ' sub-chunk
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(subChunk1Size);
            writer.Write((short)1);                   // Audio format (1 = PCM)
            writer.Write((short)numChannels);         // Number of channels
            writer.Write(sampleRate);                 // Sample rate
            writer.Write(byteRate);                   // Byte rate
            writer.Write((short)blockAlign);          // Block align
            writer.Write((short)bitsPerSample);       // Bits per sample

            // 'data' sub-chunk
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(subChunk2Size);

            // Write audio data
            foreach (var sample in samples)
                writer.Write((short)(sample * short.MaxValue));

            writer.Flush();
        }

        internal const float silentSample = 0.0f;

        public static void EncodeToWavUnclamped(this AudioClip audioClip, Stream outputStream, float start = 0, float end = -1)
        {
            if (!audioClip.TryGetSamples(out var audioSamples))
                return;

            var samples = GetSampleRange(audioClip, audioSamples, start, end);

            var startSample = start == 0 ? 0 : audioClip.GetSampleIndexAtPosition(start);
            var endSample = (int)end == -1 ? audioClip.samples : audioClip.GetSampleIndexAtPosition(end);
            var sampleLength = endSample - startSample;

            var startSampleUnclamped = start == 0 ? 0 : audioClip.GetSampleIndexAtPositionUnclamped(start);
            var endSampleUnclamped = (int)end == -1 ? audioClip.samples : audioClip.GetSampleIndexAtPositionUnclamped(end);
            var sampleLengthUnclamped = endSampleUnclamped - startSampleUnclamped;
            if (sampleLengthUnclamped > sampleLength)
            {
                var previousLength = samples.Length;
                Array.Resize(ref samples, sampleLengthUnclamped * audioClip.channels);
                for (var i = previousLength - 1; i < samples.Length; i++)
                    samples[i] = silentSample;

                if (startSampleUnclamped < startSample)
                    OffsetArrayInPlace(samples, (startSample - startSampleUnclamped) * audioClip.channels);
            }

            EncodeToWav(samples, outputStream, audioClip.channels, audioClip.frequency);
        }

        public static void SaveAudioClipToWav(this AudioClip audioClip, string assetPath, SoundEnvelopeSettings settings)
        {
            settings ??= new SoundEnvelopeSettings();
            {
                using var fileStream = FileIO.OpenFileStream(assetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                EncodeToWav(audioClip, fileStream, settings);
            }
            AssetDatabase.ImportAsset(assetPath);
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath));
        }

        static void OffsetArrayInPlace(IList<float> array, int offsetCount)
        {
            for (var i = array.Count - 1; i >= offsetCount; i--)
                array[i] = array[i - offsetCount];

            for (var i = 0; i < offsetCount; i++)
                array[i] = silentSample;
        }

        public static float[] GetSampleRange(this AudioClip audioClip, float[] audioSamples = null, float start = 0, float end = -1)
        {
            if (audioSamples == null && !audioClip.TryGetSamples(out audioSamples))
                return null;
            var startSample = start == 0 ? 0 : GetSampleIndexAtPosition(audioClip, start);
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            var endSample = end == -1 ? audioClip.samples : GetSampleIndexAtPosition(audioClip, end);
            var sampleLength = endSample - startSample;

            var samples = new float[sampleLength * audioClip.channels];
            Array.Copy(audioSamples, startSample * audioClip.channels, samples, 0, samples.Length);

            return samples;
        }

        public static bool TryGetSamples(this AudioClip audioClip, out float[] samples)
        {
            samples = null;
            if (audioClip == null)
                return false;
            samples = new float[audioClip.samples * audioClip.channels];
            audioClip.GetData(samples, 0);
            return true;
        }

        static float InterpolateAmplitude(float time, Vector2 startKeyframe, Vector2 endKeyframe)
        {
            float amplitude;
            if (Mathf.Approximately(startKeyframe.x, endKeyframe.x))
                amplitude = startKeyframe.y;
            else if (time <= startKeyframe.x)
                amplitude = startKeyframe.y;
            else if (time >= endKeyframe.x)
                amplitude = endKeyframe.y;
            else
                amplitude = Mathf.Lerp(startKeyframe.y, endKeyframe.y, (time - startKeyframe.x) / (endKeyframe.x - startKeyframe.x));
            return amplitude;
        }

        static void ApplyEnvelope(this AudioClip audioClip, IList<float> audioData, IReadOnlyList<Vector2> controlPoints)
        {
            if (controlPoints == null || controlPoints.Count == 0)
                return;

            var pointA = controlPoints[0];
            var pointB = controlPoints[Math.Min(1, controlPoints.Count - 1)];

            var controlPointIndex = 0;
            for (var i = 0; i < audioClip.samples; i++)
            {
                var samplePosition = i / (float)audioClip.samples;
                while (samplePosition > pointB.x && controlPointIndex < controlPoints.Count - 1)
                {
                    controlPointIndex++;
                    pointA = controlPoints[controlPointIndex];
                    pointB = controlPoints[Math.Min(controlPointIndex + 1, controlPoints.Count - 1)];
                }

                var amplitudeScale = InterpolateAmplitude(samplePosition, pointA, pointB);
                for (var channel = 0; channel < audioClip.channels; channel++)
                {
                    var index = i * audioClip.channels + channel;
                    audioData[index] *= amplitudeScale;
                }
            }
        }

        static float[] ApplyTrim(this AudioClip audioClip, float[] samples, float start = 0, float end = -1)
        {
            var startSample = (start == 0) ? 0 : GetSampleIndexAtPosition(audioClip, start);
            var endSample = (end == -1) ? audioClip.samples : GetSampleIndexAtPosition(audioClip, end);
            var count = endSample - startSample;

            var subSamples = new float[count * audioClip.channels];
            Array.Copy(samples, startSample, subSamples, 0, count);
            return subSamples;
        }

        public static int FindPreviousSilentSample(this AudioClip audioClip, IList<float> audioData, float maxAmplitude,
            int startSample, float amplitudeThreshold = 0.02f, int durationThreshold = 100 /*0.05s*/)
        {
            var silentCount = durationThreshold;
            var silenceStartSample = -1;
            for (var i = startSample; i >= 0; i--)
            {
                for (var channel = 0; channel < audioClip.channels; channel++)
                {
                    var index = i * audioClip.channels + channel;
                    if (Mathf.Abs(audioData[index]) <= Mathf.Max(0, amplitudeThreshold * maxAmplitude - Mathf.Epsilon))
                    {
                        silentCount--;
                        if (silenceStartSample < 0)
                            silenceStartSample = i;
                        if (silentCount <= 0)
                            return silenceStartSample;
                    }
                    else
                    {
                        silentCount = durationThreshold;
                        silenceStartSample = -1;
                    }
                }
            }

            return 0;
        }

        public static int FindNextSilentSample(this AudioClip audioClip, IList<float> audioData, float maxAmplitude, int startSample,
            float amplitudeThreshold = 0.02f, int durationThreshold = 100 /*0.05s*/)
        {
            var silentCount = durationThreshold;
            var silenceStartSample = -1;
            for (var i = startSample; i < audioClip.samples; i++)
            {
                for (var channel = 0; channel < audioClip.channels; channel++)
                {
                    var index = i * audioClip.channels + channel;
                    if (Mathf.Abs(audioData[index]) <= Mathf.Max(0, amplitudeThreshold * maxAmplitude - Mathf.Epsilon))
                    {
                        silentCount--;
                        if (silenceStartSample < 0)
                            silenceStartSample = i;
                        if (silentCount <= 0)
                            return silenceStartSample;
                    }
                    else
                    {
                        silentCount = durationThreshold;
                        silenceStartSample = -1;
                    }
                }
            }

            return 0;
        }

        public static (float, int) FindMaxAmplitudeAndSample(this AudioClip audioClip, IList<float> audioData)
        {
            float maxAmplitude = 0;
            var maxAmplitudeSample = 0;
            for (var i = 0; i < audioClip.samples; i++)
            {
                for (var channel = 0; channel < audioClip.channels; channel++)
                {
                    var index = i * audioClip.channels + channel;
                    if (Mathf.Abs(audioData[index]) > maxAmplitude)
                    {
                        maxAmplitudeSample = i;
                        maxAmplitude = Mathf.Abs(audioData[index]);
                    }
                }
            }

            return (maxAmplitude, maxAmplitudeSample);
        }
    }
}
