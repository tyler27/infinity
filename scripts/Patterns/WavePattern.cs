using System.Collections.Generic;
using Godot;
using LazerSystem.Core;

namespace LazerSystem.Patterns
{
    /// <summary>
    /// Sine wave pattern. Animated via frequency, amplitude, and speed.
    /// </summary>
    public class WavePattern : ILaserPattern
    {
        public string PatternName => "Wave";

        private const int PointCount = 64;

        public void Generate(float time, PatternParameters parameters, List<LaserPoint> output)
        {
            Color c = parameters.EffectiveColor();
            float halfWidth = parameters.size;
            float amplitude = parameters.amplitude;
            float frequency = parameters.frequency;
            float phase = time * parameters.speed;
            float angle = Mathf.DegToRad(parameters.rotation);
            float cx = parameters.position.X;
            float cy = parameters.position.Y;

            float cosA = Mathf.Cos(angle);
            float sinA = Mathf.Sin(angle);

            // Generate wave points in local space then rotate
            for (int i = 0; i <= PointCount; i++)
            {
                float t = (float)i / PointCount;
                float localX = Mathf.Lerp(-halfWidth, halfWidth, t);
                float localY = Mathf.Sin(localX * frequency * Mathf.Pi * 2f + phase) * amplitude;

                // Rotate around center
                float rotX = localX * cosA - localY * sinA;
                float rotY = localX * sinA + localY * cosA;

                float px = cx + rotX;
                float py = cy + rotY;

                if (i == 0)
                {
                    output.Add(LaserPoint.Blanked(px, py));
                }

                output.Add(LaserPoint.Colored(px, py, c.R, c.G, c.B));
            }
        }
    }
}
