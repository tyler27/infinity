using Godot;

namespace LazerSystem.Timeline
{
    public static class WaveformGenerator
    {
        public static float[] GeneratePeaks(AudioStream stream, float pixelsPerSecond, float duration)
        {
            if (stream == null || duration <= 0f)
                return null;

            // Only support AudioStreamWav for PCM extraction
            if (stream is not AudioStreamWav wav)
                return null;

            byte[] data = wav.Data;
            if (data == null || data.Length == 0)
                return null;

            int totalPixels = Mathf.CeilToInt(duration * pixelsPerSecond);
            if (totalPixels <= 0)
                return null;

            var peaks = new float[totalPixels];

            int bitsPerSample = wav.Format switch
            {
                AudioStreamWav.FormatEnum.Format8Bits => 8,
                AudioStreamWav.FormatEnum.Format16Bits => 16,
                _ => 16
            };
            int channels = wav.Stereo ? 2 : 1;
            int bytesPerSample = bitsPerSample / 8;
            int bytesPerFrame = bytesPerSample * channels;
            int totalFrames = data.Length / bytesPerFrame;
            float sampleRate = (float)wav.MixRate;

            for (int px = 0; px < totalPixels; px++)
            {
                float timeStart = px / pixelsPerSecond;
                float timeEnd = (px + 1) / pixelsPerSecond;

                int frameStart = Mathf.Clamp((int)(timeStart * sampleRate), 0, totalFrames - 1);
                int frameEnd = Mathf.Clamp((int)(timeEnd * sampleRate), 0, totalFrames);

                float maxAmp = 0f;
                for (int f = frameStart; f < frameEnd; f++)
                {
                    int byteOffset = f * bytesPerFrame;
                    if (byteOffset + bytesPerSample > data.Length)
                        break;

                    float sample;
                    if (bitsPerSample == 8)
                    {
                        sample = (data[byteOffset] - 128) / 128f;
                    }
                    else // 16-bit
                    {
                        short s = (short)(data[byteOffset] | (data[byteOffset + 1] << 8));
                        sample = s / 32768f;
                    }

                    float abs = Mathf.Abs(sample);
                    if (abs > maxAmp)
                        maxAmp = abs;
                }

                peaks[px] = maxAmp;
            }

            return peaks;
        }
    }
}
