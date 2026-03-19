using System.Collections.Generic;
using Godot;
using LazerSystem.Core;

namespace LazerSystem.Patterns
{
    /// <summary>
    /// Generates laser points from a CustomPointPattern resource.
    /// Interpolates between user-defined points with configurable sub-point density.
    /// </summary>
    public class CustomPointPatternGen : ILaserPattern
    {
        public string PatternName => "Custom";

        private const int SubPointsPerSegment = 12;

        public void Generate(float time, PatternParameters parameters, List<LaserPoint> output)
        {
            var pattern = parameters.customPattern;
            if (pattern == null || pattern.Points == null || pattern.Points.Count < 2)
                return;

            Color tint = parameters.EffectiveColor();
            float size = parameters.size;
            float rot = Mathf.DegToRad(parameters.rotation) + time * parameters.speed;
            float cx = parameters.position.X;
            float cy = parameters.position.Y;

            float cosR = Mathf.Cos(rot);
            float sinR = Mathf.Sin(rot);

            int pointCount = pattern.Points.Count;
            int segmentCount = pattern.Closed ? pointCount : pointCount - 1;

            // Blank-move to first point
            Vector2 first = TransformPoint(pattern.Points[0], size, cosR, sinR, cx, cy);
            output.Add(LaserPoint.Blanked(first.X, first.Y));

            for (int seg = 0; seg < segmentCount; seg++)
            {
                int idxA = seg;
                int idxB = (seg + 1) % pointCount;
                Vector2 a = pattern.Points[idxA];
                Vector2 b = pattern.Points[idxB];
                Color colorA = ApplyTint(pattern.GetPointColor(idxA), tint);
                Color colorB = ApplyTint(pattern.GetPointColor(idxB), tint);

                for (int s = 1; s <= SubPointsPerSegment; s++)
                {
                    float t = (float)s / SubPointsPerSegment;
                    float px = Mathf.Lerp(a.X, b.X, t);
                    float py = Mathf.Lerp(a.Y, b.Y, t);
                    Vector2 transformed = TransformPoint(new Vector2(px, py), size, cosR, sinR, cx, cy);
                    Color c = colorA.Lerp(colorB, t);
                    output.Add(LaserPoint.Colored(transformed.X, transformed.Y, c.R, c.G, c.B));
                }
            }
        }

        private static Vector2 TransformPoint(Vector2 p, float size, float cosR, float sinR, float cx, float cy)
        {
            float sx = p.X * size;
            float sy = p.Y * size;
            float rx = sx * cosR - sy * sinR;
            float ry = sx * sinR + sy * cosR;
            return new Vector2(rx + cx, ry + cy);
        }

        private static Color ApplyTint(Color pointColor, Color tint)
        {
            return new Color(pointColor.R * tint.R, pointColor.G * tint.G, pointColor.B * tint.B);
        }
    }
}
