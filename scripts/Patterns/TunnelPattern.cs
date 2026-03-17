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
        private const int PointsPerEdge = 4;
        private const int MaxSides = 32;

        // Pre-allocated vertex buffer to avoid per-frame allocations
        private readonly Vector2[] _vertexBuffer = new Vector2[MaxSides];

        public void Generate(float time, PatternParameters parameters, List<LaserPoint> output)
        {
            Color c = parameters.EffectiveColor();
            int sides = Mathf.Clamp(parameters.count, 3, MaxSides);
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

                // Compute vertices for this ring into pre-allocated buffer
                for (int i = 0; i < sides; i++)
                {
                    float angle = ringRotation + (float)i / sides * Mathf.Pi * 2f;
                    _vertexBuffer[i] = new Vector2(
                        cx + Mathf.Cos(angle) * radius,
                        cy + Mathf.Sin(angle) * radius);
                }

                // Blank move to first vertex of ring
                output.Add(LaserPoint.Blanked(_vertexBuffer[0].X, _vertexBuffer[0].Y));

                // Draw ring
                for (int i = 0; i < sides; i++)
                {
                    Vector2 from = _vertexBuffer[i];
                    Vector2 to = _vertexBuffer[(i + 1) % sides];

                    for (int p = 0; p <= PointsPerEdge; p++)
                    {
                        float t = (float)p / PointsPerEdge;
                        float px = Mathf.Lerp(from.X, to.X, t);
                        float py = Mathf.Lerp(from.Y, to.Y, t);
                        output.Add(LaserPoint.Colored(px, py, rc.R, rc.G, rc.B));
                    }
                }
            }
        }
    }
}
