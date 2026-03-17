using System.Collections.Generic;
using Godot;
using LazerSystem.Core;

namespace LazerSystem.Patterns
{
    /// <summary>
    /// Tunnel pattern: concentric shapes expanding outward from center.
    /// Multiple copies of a polygon at different scales, animated to expand over time.
    /// </summary>
    public class TunnelPattern : ILaserPattern
    {
        public string PatternName => "Tunnel";

        private const int RingCount = 6;
        private const int PointsPerEdge = 6;

        public List<LaserPoint> Generate(float time, PatternParameters parameters)
        {
            var points = new List<LaserPoint>();
            Color c = parameters.EffectiveColor();
            int sides = Mathf.Max(3, parameters.count);
            float maxRadius = parameters.size;
            float rotationBase = Mathf.DegToRad(parameters.rotation);
            float speed = parameters.speed;
            float cx = parameters.position.X;
            float cy = parameters.position.Y;

            for (int ring = 0; ring < RingCount; ring++)
            {
                // Animated expanding scale - each ring phase-offset so they cycle outward
                float phase = (float)ring / RingCount;
                float animT = (float)Mathf.PosMod(phase + time * speed * 0.3f, 1f);
                float radius = animT * maxRadius;

                // Fade alpha for inner rings (smaller = dimmer)
                float fade = animT;
                Color rc = c * fade;

                // Rotation varies per ring for twist effect
                float ringRotation = rotationBase + ring * 0.15f + time * speed * 0.5f;

                // Compute vertices for this ring
                var vertices = new Vector2[sides];
                for (int i = 0; i < sides; i++)
                {
                    float angle = ringRotation + (float)i / sides * Mathf.Pi * 2f;
                    vertices[i] = new Vector2(
                        cx + Mathf.Cos(angle) * radius,
                        cy + Mathf.Sin(angle) * radius);
                }

                // Blank move to first vertex of ring
                points.Add(LaserPoint.Blanked(vertices[0].X, vertices[0].Y));

                // Draw ring
                for (int i = 0; i < sides; i++)
                {
                    Vector2 from = vertices[i];
                    Vector2 to = vertices[(i + 1) % sides];

                    for (int p = 0; p <= PointsPerEdge; p++)
                    {
                        float t = (float)p / PointsPerEdge;
                        float px = Mathf.Lerp(from.X, to.X, t);
                        float py = Mathf.Lerp(from.Y, to.Y, t);
                        points.Add(LaserPoint.Colored(px, py, rc.R, rc.G, rc.B));
                    }
                }
            }

            return points;
        }
    }
}
