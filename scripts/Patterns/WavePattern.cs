using System.Collections.Generic;
using Godot;
using LazerSystem.Core;

namespace LazerSystem.Patterns
{
    /// <summary>
    /// Sine wave pattern. Animated via frequency, amplitude, and speed.
    /// Generates approximately 100 points along the wave.
    /// </summary>
    public class WavePattern : ILaserPattern
    {
        public string PatternName => "Wave";

        private const int PointCount = 100;

        public List<LaserPoint> Generate(float time, PatternParameters parameters)
        {
            var points = new List<LaserPoint>();
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
                    points.Add(LaserPoint.Blanked(px, py));
                }

                points.Add(LaserPoint.Colored(px, py, c.R, c.G, c.B));
            }

            return points;
        }
    }
}
