using System.Collections.Generic;
using Godot;
using LazerSystem.Core;

/// <summary>
/// 2D top-down laser output view — shows the exact scan output like an oscilloscope/ILDA viewer.
/// Draws the normalized -1..1 scan area with zone boundary, safety zone, and live laser points.
/// This is what Beyond shows in its preview window — the raw laser output.
/// </summary>
public partial class LaserOutputView : Control
{
    private static readonly Color BgColor = new Color(0.04f, 0.04f, 0.06f, 1f);
    private static readonly Color GridColor = new Color(0.15f, 0.15f, 0.2f, 0.4f);
    private static readonly Color BoundaryColor = new Color(0.3f, 0.35f, 0.45f, 0.6f);
    private static readonly Color SafetyColor = new Color(0.8f, 0.6f, 0.1f, 0.4f);
    private static readonly Color OverflowBoundaryColor = new Color(1f, 0.2f, 0.1f, 0.8f);
    private static readonly Color CenterCrossColor = new Color(0.2f, 0.2f, 0.25f, 0.3f);

    // Per-projector colors matching the toolbar
    private static readonly Color[] ProjectorColors = {
        new Color(1f, 0.3f, 0.3f, 1f),
        new Color(0.3f, 1f, 0.4f, 1f),
        new Color(0.3f, 0.5f, 1f, 1f),
        new Color(1f, 0.9f, 0.3f, 1f)
    };

    /// <summary>Which projector index to display. -1 = all projectors overlaid.</summary>
    public int DisplayProjector { get; set; } = -1;

    // Point data set each frame by LiveEngine
    private List<LaserPoint>[] _projectorPoints = new List<LaserPoint>[4];
    private bool[] _projectorActive = new bool[4];
    private bool _overflow;

    public override void _Ready()
    {
        for (int i = 0; i < 4; i++)
            _projectorPoints[i] = new List<LaserPoint>();
    }

    /// <summary>Set the points for a projector this frame.</summary>
    public void SetProjectorPoints(int projector, List<LaserPoint> points, bool active)
    {
        if (projector < 0 || projector >= 4) return;
        _projectorPoints[projector].Clear();
        if (points != null)
            _projectorPoints[projector].AddRange(points);
        _projectorActive[projector] = active;
    }

    /// <summary>Set whether any projector has overflow.</summary>
    public void SetOverflow(bool overflow)
    {
        _overflow = overflow;
    }

    /// <summary>Clear all points (call at start of frame before setting new points).</summary>
    public void ClearAll()
    {
        for (int i = 0; i < 4; i++)
        {
            _projectorPoints[i].Clear();
            _projectorActive[i] = false;
        }
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        // Background
        DrawRect(new Rect2(Vector2.Zero, Size), BgColor);

        // Calculate drawing area (maintain square aspect ratio, centered)
        float margin = 10f;
        float available = Mathf.Min(Size.X - margin * 2, Size.Y - margin * 2);
        float drawSize = Mathf.Max(available, 50f);
        Vector2 center = Size / 2f;
        Vector2 origin = center - new Vector2(drawSize, drawSize) / 2f;

        // Draw grid lines
        DrawGridLines(origin, drawSize);

        // Draw zone boundary (-1..1)
        Color boundColor = _overflow ? OverflowBoundaryColor : BoundaryColor;
        float bThick = _overflow ? 2f : 1f;
        Vector2 bl = NormToPixel(-1f, -1f, origin, drawSize);
        Vector2 br = NormToPixel(1f, -1f, origin, drawSize);
        Vector2 tr = NormToPixel(1f, 1f, origin, drawSize);
        Vector2 tl = NormToPixel(-1f, 1f, origin, drawSize);
        DrawLine(tl, tr, boundColor, bThick);
        DrawLine(tr, br, boundColor, bThick);
        DrawLine(br, bl, boundColor, bThick);
        DrawLine(bl, tl, boundColor, bThick);

        // Corner brackets
        float bracketPx = drawSize * 0.06f;
        DrawCornerBracket(tl, bracketPx, true, true, boundColor, bThick);
        DrawCornerBracket(tr, bracketPx, false, true, boundColor, bThick);
        DrawCornerBracket(br, bracketPx, false, false, boundColor, bThick);
        DrawCornerBracket(bl, bracketPx, true, false, boundColor, bThick);

        // Draw safety zone if configured (from zone editor)
        DrawSafetyZone(origin, drawSize);

        // Draw center crosshair
        Vector2 c = NormToPixel(0, 0, origin, drawSize);
        float crossLen = drawSize * 0.03f;
        DrawLine(c - new Vector2(crossLen, 0), c + new Vector2(crossLen, 0), CenterCrossColor, 1f);
        DrawLine(c - new Vector2(0, crossLen), c + new Vector2(0, crossLen), CenterCrossColor, 1f);

        // Draw laser points for each projector
        for (int p = 0; p < 4; p++)
        {
            if (DisplayProjector >= 0 && p != DisplayProjector) continue;
            if (!_projectorActive[p]) continue;

            var points = _projectorPoints[p];
            if (points.Count < 2) continue;

            Color projColor = ProjectorColors[p];
            DrawLaserPoints(points, origin, drawSize, projColor, p);
        }

        // Projector indicator labels in bottom-left
        float labelY = Size.Y - 18f;
        var font = ThemeDB.FallbackFont;
        for (int p = 0; p < 4; p++)
        {
            if (DisplayProjector >= 0 && p != DisplayProjector) continue;
            Color lc = _projectorActive[p] ? ProjectorColors[p] : ProjectorColors[p].Darkened(0.6f);
            DrawString(font, new Vector2(margin + p * 40f, labelY), $"P{p + 1}", HorizontalAlignment.Left, -1, 10, lc);
        }
    }

    private void DrawGridLines(Vector2 origin, float drawSize)
    {
        // Draw grid at 0.5 intervals (-1, -0.5, 0, 0.5, 1)
        for (int i = -2; i <= 2; i++)
        {
            float norm = i * 0.5f;
            Vector2 left = NormToPixel(-1f, norm, origin, drawSize);
            Vector2 right = NormToPixel(1f, norm, origin, drawSize);
            Vector2 top = NormToPixel(norm, 1f, origin, drawSize);
            Vector2 bottom = NormToPixel(norm, -1f, origin, drawSize);

            if (i == 0)
            {
                // Center lines slightly brighter
                DrawLine(left, right, CenterCrossColor, 1f);
                DrawLine(top, bottom, CenterCrossColor, 1f);
            }
            else
            {
                DrawLine(left, right, GridColor, 1f);
                DrawLine(top, bottom, GridColor, 1f);
            }
        }
    }

    private void DrawSafetyZone(Vector2 origin, float drawSize)
    {
        var mgr = LaserSystemManager.Instance;
        if (mgr == null || mgr.Zones.Count == 0) return;

        // Draw safety zones for active zones
        var activeZones = LiveEngine.Instance?.ActiveZones;
        if (activeZones == null) return;

        foreach (int zi in activeZones)
        {
            if (zi < 0 || zi >= mgr.Zones.Count) continue;
            var zone = mgr.Zones[zi];
            if (zone == null) continue;

            var sz = zone.SafetyZone;
            // SafetyZone is Rect2 with position and size in 0..1 space
            // Map to -1..1: safeLeft = sz.Position.X * 2 - 1, etc.
            float safeLeft = sz.Position.X * 2f - 1f;
            float safeBottom = sz.Position.Y * 2f - 1f;
            float safeRight = (sz.Position.X + sz.Size.X) * 2f - 1f;
            float safeTop = (sz.Position.Y + sz.Size.Y) * 2f - 1f;

            // Only draw if it differs from full zone
            if (Mathf.Abs(safeLeft + 1f) > 0.02f || Mathf.Abs(safeRight - 1f) > 0.02f ||
                Mathf.Abs(safeBottom + 1f) > 0.02f || Mathf.Abs(safeTop - 1f) > 0.02f)
            {
                Vector2 sBL = NormToPixel(safeLeft, safeBottom, origin, drawSize);
                Vector2 sBR = NormToPixel(safeRight, safeBottom, origin, drawSize);
                Vector2 sTR = NormToPixel(safeRight, safeTop, origin, drawSize);
                Vector2 sTL = NormToPixel(safeLeft, safeTop, origin, drawSize);

                DrawLine(sTL, sTR, SafetyColor, 1f);
                DrawLine(sTR, sBR, SafetyColor, 1f);
                DrawLine(sBR, sBL, SafetyColor, 1f);
                DrawLine(sBL, sTL, SafetyColor, 1f);
            }
        }
    }

    private void DrawLaserPoints(List<LaserPoint> points, Vector2 origin, float drawSize, Color projColor, int projectorIndex)
    {
        // Draw visible segments as connected lines
        // Offset each projector slightly so overlapping beams are all visible
        float offsetPx = (projectorIndex - 1.5f) * 1.5f;
        Vector2 projOffset = new Vector2(offsetPx, -offsetPx * 0.5f);

        bool wasBlank = true;
        Vector2 prevPixel = Vector2.Zero;

        for (int i = 0; i < points.Count; i++)
        {
            var pt = points[i];
            Vector2 pixel = NormToPixel(pt.x, pt.y, origin, drawSize) + projOffset;

            if (pt.blanking)
            {
                wasBlank = true;
                continue;
            }

            if (!wasBlank)
            {
                Color lineColor = new Color(pt.r, pt.g, pt.b, 1f);
                // Blend toward projector color so each projector is distinguishable
                lineColor = lineColor.Lerp(projColor, 0.3f);
                DrawLine(prevPixel, pixel, lineColor, 1.5f);
            }

            prevPixel = pixel;
            wasBlank = false;
        }
    }

    private void DrawCornerBracket(Vector2 corner, float len, bool left, bool top, Color color, float thickness)
    {
        float dx = left ? len : -len;
        float dy = top ? len : -len;
        DrawLine(corner, corner + new Vector2(dx, 0), color, thickness + 1f);
        DrawLine(corner, corner + new Vector2(0, dy), color, thickness + 1f);
    }

    /// <summary>Map normalized (-1..1) laser coords to pixel position. Y is flipped (up = positive).</summary>
    private Vector2 NormToPixel(float normX, float normY, Vector2 origin, float drawSize)
    {
        float px = origin.X + (normX + 1f) * 0.5f * drawSize;
        float py = origin.Y + (1f - (normY + 1f) * 0.5f) * drawSize; // flip Y
        return new Vector2(px, py);
    }
}
