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

        private const int PointsPerEdge = 6;
        private const int MaxSides = 32;

        private readonly bool _isStar;

        // Pre-allocated vertex buffer to avoid per-frame allocations
        private readonly Vector2[] _vertexBuffer = new Vector2[MaxSides * 2];

        public ShapePattern(bool isStar = false)
        {
            _isStar = isStar;
        }

        public void Generate(float time, PatternParameters parameters, List<LaserPoint> output)
        {
            Color c = parameters.EffectiveColor();
            int sides = Mathf.Clamp(parameters.count, 3, MaxSides);
            float radius = parameters.size;
            float rotationOffset = Mathf.DegToRad(parameters.rotation) + time * parameters.speed;
            float cx = parameters.position.X;
            float cy = parameters.position.Y;

            if (_isStar)
            {
                GenerateStar(output, sides, radius, rotationOffset, cx, cy, c);
            }
            else
            {
                GeneratePolygon(output, sides, radius, rotationOffset, cx, cy, c);
            }
        }

        private void GeneratePolygon(List<LaserPoint> output, int sides, float radius,
            float rotationOffset, float cx, float cy, Color c)
        {
            // Compute vertices into pre-allocated buffer
            for (int i = 0; i < sides; i++)
            {
                float angle = rotationOffset + (float)i / sides * Mathf.Pi * 2f;
                _vertexBuffer[i] = new Vector2(
                    cx + Mathf.Cos(angle) * radius,
                    cy + Mathf.Sin(angle) * radius);
            }

            // Blank move to first vertex
            output.Add(LaserPoint.Blanked(_vertexBuffer[0].X, _vertexBuffer[0].Y));

            // Draw edges
            for (int i = 0; i < sides; i++)
            {
                Vector2 from = _vertexBuffer[i];
                Vector2 to = _vertexBuffer[(i + 1) % sides];

                for (int p = 0; p <= PointsPerEdge; p++)
                {
                    float t = (float)p / PointsPerEdge;
                    float px = Mathf.Lerp(from.X, to.X, t);
                    float py = Mathf.Lerp(from.Y, to.Y, t);
                    output.Add(LaserPoint.Colored(px, py, c.R, c.G, c.B));
                }
            }
        }

        private void GenerateStar(List<LaserPoint> output, int starPoints, float outerRadius,
            float rotationOffset, float cx, float cy, Color c)
        {
            float innerRadius = outerRadius * 0.4f;
            int totalVertices = starPoints * 2;

            for (int i = 0; i < totalVertices; i++)
            {
                float angle = rotationOffset + (float)i / totalVertices * Mathf.Pi * 2f;
                float r = (i % 2 == 0) ? outerRadius : innerRadius;
                _vertexBuffer[i] = new Vector2(
                    cx + Mathf.Cos(angle) * r,
                    cy + Mathf.Sin(angle) * r);
            }

            // Blank move to first vertex
            output.Add(LaserPoint.Blanked(_vertexBuffer[0].X, _vertexBuffer[0].Y));

            // Draw star outline
            for (int i = 0; i < totalVertices; i++)
            {
                Vector2 from = _vertexBuffer[i];
                Vector2 to = _vertexBuffer[(i + 1) % totalVertices];

                for (int p = 0; p <= PointsPerEdge; p++)
                {
                    float t = (float)p / PointsPerEdge;
                    float px = Mathf.Lerp(from.X, to.X, t);
                    float py = Mathf.Lerp(from.Y, to.Y, t);
                    output.Add(LaserPoint.Colored(px, py, c.R, c.G, c.B));
                }
            }
        }
    }
}
