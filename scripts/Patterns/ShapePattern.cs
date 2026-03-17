using System.Collections.Generic;
using Godot;
using LazerSystem.Core;

namespace LazerSystem.Patterns
{
    /// <summary>
    /// Geometric shape pattern. Generates regular polygons (triangle, square, pentagon, etc.)
    /// or star shapes using alternating inner/outer radius.
    /// Count determines the number of vertices. For stars, count is the number of points.
    /// </summary>
    public class ShapePattern : ILaserPattern
    {
        public string PatternName => "Shape";

        private const int PointsPerEdge = 8;

        private readonly bool _isStar;

        public ShapePattern(bool isStar = false)
        {
            _isStar = isStar;
        }

        public List<LaserPoint> Generate(float time, PatternParameters parameters)
        {
            var points = new List<LaserPoint>();
            Color c = parameters.EffectiveColor();
            int sides = Mathf.Max(3, parameters.count);
            float radius = parameters.size;
            float rotationOffset = Mathf.DegToRad(parameters.rotation) + time * parameters.speed;
            float cx = parameters.position.X;
            float cy = parameters.position.Y;

            if (_isStar)
            {
                return GenerateStar(parameters, time, sides, radius, rotationOffset, cx, cy, c);
            }

            return GeneratePolygon(sides, radius, rotationOffset, cx, cy, c);
        }

        private List<LaserPoint> GeneratePolygon(int sides, float radius, float rotationOffset,
            float cx, float cy, Color c)
        {
            var points = new List<LaserPoint>();

            // Compute vertices
            var vertices = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = rotationOffset + (float)i / sides * Mathf.Pi * 2f;
                vertices[i] = new Vector2(
                    cx + Mathf.Cos(angle) * radius,
                    cy + Mathf.Sin(angle) * radius);
            }

            // Blank move to first vertex
            points.Add(LaserPoint.Blanked(vertices[0].X, vertices[0].Y));

            // Draw edges
            for (int i = 0; i < sides; i++)
            {
                Vector2 from = vertices[i];
                Vector2 to = vertices[(i + 1) % sides];

                for (int p = 0; p <= PointsPerEdge; p++)
                {
                    float t = (float)p / PointsPerEdge;
                    float px = Mathf.Lerp(from.X, to.X, t);
                    float py = Mathf.Lerp(from.Y, to.Y, t);
                    points.Add(LaserPoint.Colored(px, py, c.R, c.G, c.B));
                }
            }

            return points;
        }

        private List<LaserPoint> GenerateStar(PatternParameters parameters, float time,
            int starPoints, float outerRadius, float rotationOffset,
            float cx, float cy, Color c)
        {
            var points = new List<LaserPoint>();
            float innerRadius = outerRadius * 0.4f;
            int totalVertices = starPoints * 2;

            var vertices = new Vector2[totalVertices];
            for (int i = 0; i < totalVertices; i++)
            {
                float angle = rotationOffset + (float)i / totalVertices * Mathf.Pi * 2f;
                float r = (i % 2 == 0) ? outerRadius : innerRadius;
                vertices[i] = new Vector2(
                    cx + Mathf.Cos(angle) * r,
                    cy + Mathf.Sin(angle) * r);
            }

            // Blank move to first vertex
            points.Add(LaserPoint.Blanked(vertices[0].X, vertices[0].Y));

            // Draw star outline
            for (int i = 0; i < totalVertices; i++)
            {
                Vector2 from = vertices[i];
                Vector2 to = vertices[(i + 1) % totalVertices];

                for (int p = 0; p <= PointsPerEdge; p++)
                {
                    float t = (float)p / PointsPerEdge;
                    float px = Mathf.Lerp(from.X, to.X, t);
                    float py = Mathf.Lerp(from.Y, to.Y, t);
                    points.Add(LaserPoint.Colored(px, py, c.R, c.G, c.B));
                }
            }

            return points;
        }
    }
}
