using System.Collections.Generic;
using Godot;
using LazerSystem.Core;

namespace LazerSystem.Patterns
{
    /// <summary>
    /// Straight line pattern from point A to point B.
    /// Position defines the center, rotation sets the angle, and size sets the half-length.
    /// </summary>
    public class LinePattern : ILaserPattern
    {
        public string PatternName => "Line";

        private const int PointCount = 24;

        public void Generate(float time, PatternParameters parameters, List<LaserPoint> output)
        {
            Color c = parameters.EffectiveColor();
            float halfLength = parameters.size;
            float angle = Mathf.DegToRad(parameters.rotation) + time * parameters.speed;
            float cx = parameters.position.X;
            float cy = parameters.position.Y;

            float dx = Mathf.Cos(angle) * halfLength;
            float dy = Mathf.Sin(angle) * halfLength;

            float ax = cx - dx;
            float ay = cy - dy;
            float bx = cx + dx;
            float by = cy + dy;

            // Blank move to start
            output.Add(LaserPoint.Blanked(ax, ay));

            // Draw line from A to B
            for (int i = 0; i <= PointCount; i++)
            {
                float t = (float)i / PointCount;
                float px = Mathf.Lerp(ax, bx, t);
                float py = Mathf.Lerp(ay, by, t);
                output.Add(LaserPoint.Colored(px, py, c.R, c.G, c.B));
            }
        }
    }
}
