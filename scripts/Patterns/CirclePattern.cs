using System.Collections.Generic;
using Godot;
using LazerSystem.Core;

namespace LazerSystem.Patterns
{
    /// <summary>
    /// Circle outline pattern made of points using sin/cos.
    /// Supports animated rotation and size modulation.
    /// </summary>
    public class CirclePattern : ILaserPattern
    {
        public string PatternName => "Circle";

        private const int PointCount = 64;

        public List<LaserPoint> Generate(float time, PatternParameters parameters)
        {
            var points = new List<LaserPoint>();
            Color c = parameters.EffectiveColor();
            float radius = parameters.size;
            float rotationOffset = Mathf.DegToRad(parameters.rotation) + time * parameters.speed;
            float cx = parameters.position.X;
            float cy = parameters.position.Y;

            // Animated radius pulsing via amplitude
            float animatedRadius = radius + Mathf.Sin(time * parameters.frequency) * parameters.amplitude * 0.1f;
            animatedRadius = Mathf.Max(0.01f, animatedRadius);

            // Blank move to first point
            float startAngle = rotationOffset;
            float startX = cx + Mathf.Cos(startAngle) * animatedRadius;
            float startY = cy + Mathf.Sin(startAngle) * animatedRadius;
            points.Add(LaserPoint.Blanked(startX, startY));

            // Draw circle outline
            for (int i = 0; i <= PointCount; i++)
            {
                float t = (float)i / PointCount;
                float angle = rotationOffset + t * Mathf.Pi * 2f;
                float px = cx + Mathf.Cos(angle) * animatedRadius;
                float py = cy + Mathf.Sin(angle) * animatedRadius;
                points.Add(LaserPoint.Colored(px, py, c.R, c.G, c.B));
            }

            return points;
        }
    }
}
