using System.Collections.Generic;
using Godot;
using LazerSystem.Core;

namespace LazerSystem.Patterns
{
    /// <summary>
    /// Draws the iconic Mario "?" block — beveled outer square, inner border,
    /// corner rivets, and a "?" drawn as line strokes with beveled corners.
    /// </summary>
    public class QuestionBlockPattern : ILaserPattern
    {
        public string PatternName => "QuestionBlock";

        private const int PointsPerSegment = 4;
        private const int QuestionRepeats = 2;
        private const float Bevel = 0.05f;

        // Outer block — square with beveled corners
        private static readonly Vector2[] Block =
        {
            new(-0.40f, -0.45f), new(0.40f, -0.45f),  // bottom
            new(0.45f, -0.40f),                         // bevel
            new(0.45f, 0.40f),                          // right
            new(0.40f, 0.45f),                          // bevel
            new(-0.40f, 0.45f),                         // top
            new(-0.45f, 0.40f),                         // bevel
            new(-0.45f, -0.40f),                        // left
        };

        // Inner border — same shape, pulled in
        private static readonly Vector2[] InnerBorder =
        {
            new(-0.33f, -0.38f), new(0.33f, -0.38f),
            new(0.38f, -0.33f),
            new(0.38f, 0.33f),
            new(0.33f, 0.38f),
            new(-0.33f, 0.38f),
            new(-0.38f, 0.33f),
            new(-0.38f, -0.33f),
        };

        // Corner rivets — four small squares
        private static readonly Vector2[][] Rivets =
        {
            MakeRivet(-0.32f, 0.32f),
            MakeRivet(0.32f, 0.32f),
            MakeRivet(-0.32f, -0.32f),
            MakeRivet(0.32f, -0.32f),
        };

        // "?" as a single closed polygon tracing the thick outline.
        // Outer contour clockwise, then inner contour back — one continuous loop.
        // Uniform stroke width of 0.08 throughout.
        //
        //     ╔══════════╗       <- thick top bar
        //     ║          ║
        //     ╚╗         ║       <- left pillar ends (gap)
        //      ╚═══╗     ║
        //          ╚═════╝       <- hook bevels into right side
        //          ║
        //          ║             <- thick stem
        //
        //          ■             <- dot (separate)

        private static readonly Vector2[] QuestionMark =
        {
            // -- Outer contour (clockwise) --
            new(-0.16f, 0.12f),    // A: bottom-left of left pillar
            new(-0.16f, 0.22f),    // B: up outer left
            new(-0.10f, 0.28f),    // C: bevel to top
            new(0.10f, 0.28f),     // D: across outer top
            new(0.16f, 0.22f),     // E: bevel to right side
            new(0.16f, 0.06f),     // F: down outer right
            new(0.10f, 0.00f),     // G: bevel to hook bottom
            new(0.00f, 0.00f),     // H: hook bottom ends at stem right edge
            new(0.00f, -0.08f),    // I: down stem right side
            new(-0.08f, -0.08f),   // J: across stem bottom
            new(-0.08f, 0.04f),    // K: up stem left + hook left, stop before corner
            new(-0.04f, 0.08f),    // K2: bevel into hook ceiling
            // -- Inner contour (counter-clockwise back) --
            new(0.06f, 0.08f),     // L: hook inner ceiling goes right
            new(0.08f, 0.10f),     // M: bevel to right pillar inner
            new(0.08f, 0.18f),     // N: up inner right
            new(0.06f, 0.20f),     // O: bevel to inner top
            new(-0.06f, 0.20f),    // P: across inner top left
            new(-0.08f, 0.18f),    // Q: bevel to left pillar inner
            new(-0.08f, 0.12f),    // R: down inner left to bottom
            // -- Left pillar bottom cap closes back to A --
        };

        // Dot — small square centered under the stem
        private static readonly Vector2[] QuestionDot =
        {
            new(-0.08f, -0.16f), new(0.00f, -0.16f),
            new(0.00f, -0.22f), new(-0.08f, -0.22f),
        };

        private const int RivetSegments = 8;

        private static Vector2[] MakeRivet(float cx, float cy)
        {
            const float r = 0.03f;
            var pts = new Vector2[RivetSegments];
            for (int i = 0; i < RivetSegments; i++)
            {
                float angle = Mathf.Tau * i / RivetSegments;
                pts[i] = new Vector2(cx + r * Mathf.Cos(angle), cy + r * Mathf.Sin(angle));
            }
            return pts;
        }

        public void Generate(float time, PatternParameters parameters, List<LaserPoint> output)
        {
            Color c = parameters.EffectiveColor();
            float size = parameters.size;
            float rot = Mathf.DegToRad(parameters.rotation) + time * parameters.speed;
            float cx = parameters.position.X;
            float cy = parameters.position.Y;

            // Block frame
            DrawClosedPath(output, Block, size, rot, cx, cy, c);
            DrawClosedPath(output, InnerBorder, size, rot, cx, cy, c);
            foreach (var rivet in Rivets)
                DrawClosedPath(output, rivet, size, rot, cx, cy, c);

            // "?" — single closed outline loop + dot
            for (int i = 0; i < QuestionRepeats; i++)
            {
                DrawClosedPath(output, QuestionMark, size, rot, cx, cy, c);
                DrawClosedPath(output, QuestionDot, size, rot, cx, cy, c);
            }
        }

        private Vector2 Transform(Vector2 point, float size, float rot, float cx, float cy)
        {
            float cosR = Mathf.Cos(rot);
            float sinR = Mathf.Sin(rot);
            float px = point.X * size;
            float py = point.Y * size;
            return new Vector2(
                cx + px * cosR - py * sinR,
                cy + px * sinR + py * cosR);
        }

        private void DrawClosedPath(List<LaserPoint> output, Vector2[] verts,
            float size, float rot, float cx, float cy, Color c)
        {
            Vector2 first = Transform(verts[0], size, rot, cx, cy);
            output.Add(LaserPoint.Blanked(first.X, first.Y));

            for (int i = 0; i < verts.Length; i++)
            {
                Vector2 from = Transform(verts[i], size, rot, cx, cy);
                Vector2 to = Transform(verts[(i + 1) % verts.Length], size, rot, cx, cy);
                LerpSegment(output, from, to, c);
            }
        }

        private void LerpSegment(List<LaserPoint> output, Vector2 from, Vector2 to, Color c)
        {
            for (int p = 0; p <= PointsPerSegment; p++)
            {
                float t = (float)p / PointsPerSegment;
                float px = Mathf.Lerp(from.X, to.X, t);
                float py = Mathf.Lerp(from.Y, to.Y, t);
                output.Add(LaserPoint.Colored(px, py, c.R, c.G, c.B));
            }
        }
    }
}
