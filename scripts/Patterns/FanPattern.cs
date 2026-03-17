using System.Collections.Generic;
using Godot;
using LazerSystem.Core;

namespace LazerSystem.Patterns
{
    /// <summary>
    /// Fan pattern: N beams radiating outward from a center point, spread across a configurable angle.
    /// </summary>
    public class FanPattern : ILaserPattern
    {
        public string PatternName => "Fan";

        private const int PointsPerBeam = 10;

        public void Generate(float time, PatternParameters parameters, List<LaserPoint> output)
        {
            Color c = parameters.EffectiveColor();
            int beamCount = Mathf.Max(1, parameters.count);
            float spreadRad = Mathf.DegToRad(parameters.spread);
            float baseRotation = Mathf.DegToRad(parameters.rotation) + time * parameters.speed;
            float length = parameters.size;
            float cx = parameters.position.X;
            float cy = parameters.position.Y;

            for (int i = 0; i < beamCount; i++)
            {
                float t = beamCount == 1 ? 0f : (float)i / (beamCount - 1) - 0.5f;
                float angle = baseRotation + t * spreadRad;

                float dx = Mathf.Cos(angle);
                float dy = Mathf.Sin(angle);

                // Blank move to center
                output.Add(LaserPoint.Blanked(cx, cy));

                // Draw beam outward
                for (int p = 0; p < PointsPerBeam; p++)
                {
                    float frac = (float)p / (PointsPerBeam - 1);
                    float px = cx + dx * length * frac;
                    float py = cy + dy * length * frac;
                    output.Add(LaserPoint.Colored(px, py, c.R, c.G, c.B));
                }
            }
        }
    }
}
