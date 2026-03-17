using System;
using Godot;

/// <summary>
/// A visual 2D control for dragging keystone correction corners.
/// Displays a small preview (about 250x200) showing the warped quad
/// defined by four keystone corners, with draggable handles.
/// </summary>
public partial class ZoneKeystoneCanvas : Control
{
    private static readonly Color BgColor = new Color(0.06f, 0.06f, 0.08f, 1f);
    private static readonly Color GridColor = new Color(0.3f, 0.3f, 0.35f, 0.6f);
    private static readonly Color QuadLineColor = new Color(0.2f, 0.8f, 0.4f, 1f);
    private static readonly Color QuadFillColor = new Color(0.2f, 0.8f, 0.4f, 0.08f);
    private static readonly Color HandleColor = new Color(0.2f, 0.8f, 0.4f, 1f);
    private static readonly Color HandleActiveColor = new Color(0.4f, 1.0f, 0.6f, 1f);
    private static readonly Color LabelColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    private const float HandleRadius = 6f;
    private const float HitRadius = 12f;
    private const float DashLength = 6f;
    private const float GapLength = 4f;

    /// <summary>
    /// Four corners for keystone correction:
    /// [0] = bottom-left, [1] = bottom-right, [2] = top-right, [3] = top-left.
    /// Values in normalized space (-1..1), clamped to -1.5..1.5 during dragging.
    /// </summary>
    public Vector2[] Corners { get; set; } = new Vector2[]
    {
        new Vector2(-1f, -1f), // BL
        new Vector2( 1f, -1f), // BR
        new Vector2( 1f,  1f), // TR
        new Vector2(-1f,  1f), // TL
    };

    /// <summary>Fired whenever a corner is moved by dragging.</summary>
    public event Action<Vector2[]> CornersChanged;

    private int _dragIndex = -1;
    private int _hoverIndex = -1;

    // Corner label names in index order: BL, BR, TR, TL
    private static readonly string[] CornerLabels = { "BL", "BR", "TR", "TL" };

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(250, 200);
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                // Check if mouse is near any corner handle
                Vector2 mousePixel = mb.Position;
                for (int i = 0; i < 4; i++)
                {
                    Vector2 cornerPixel = NormalizedToPixel(Corners[i]);
                    if (mousePixel.DistanceTo(cornerPixel) <= HitRadius)
                    {
                        _dragIndex = i;
                        AcceptEvent();
                        return;
                    }
                }
            }
            else
            {
                if (_dragIndex >= 0)
                {
                    _dragIndex = -1;
                    AcceptEvent();
                }
            }
        }
        else if (@event is InputEventMouseMotion mm)
        {
            if (_dragIndex >= 0)
            {
                // Update the dragged corner position
                Vector2 normalized = PixelToNormalized(mm.Position);
                normalized.X = Mathf.Clamp(normalized.X, -1.5f, 1.5f);
                normalized.Y = Mathf.Clamp(normalized.Y, -1.5f, 1.5f);
                Corners[_dragIndex] = normalized;
                QueueRedraw();
                CornersChanged?.Invoke(Corners);
                AcceptEvent();
            }
            else
            {
                // Update hover state for visual feedback
                int newHover = -1;
                Vector2 mousePixel = mm.Position;
                for (int i = 0; i < 4; i++)
                {
                    Vector2 cornerPixel = NormalizedToPixel(Corners[i]);
                    if (mousePixel.DistanceTo(cornerPixel) <= HitRadius)
                    {
                        newHover = i;
                        break;
                    }
                }
                if (newHover != _hoverIndex)
                {
                    _hoverIndex = newHover;
                    QueueRedraw();
                }
            }
        }
    }

    public override void _Draw()
    {
        // Background fill
        DrawRect(new Rect2(Vector2.Zero, Size), BgColor);

        // Dashed rectangle for the default zone bounds (-1..1)
        Vector2 topLeft = NormalizedToPixel(new Vector2(-1f, 1f));
        Vector2 topRight = NormalizedToPixel(new Vector2(1f, 1f));
        Vector2 bottomLeft = NormalizedToPixel(new Vector2(-1f, -1f));
        Vector2 bottomRight = NormalizedToPixel(new Vector2(1f, -1f));

        DrawDashedLine(topLeft, topRight, GridColor);
        DrawDashedLine(topRight, bottomRight, GridColor);
        DrawDashedLine(bottomRight, bottomLeft, GridColor);
        DrawDashedLine(bottomLeft, topLeft, GridColor);

        // Filled quad (very dim semi-transparent)
        Vector2 pBL = NormalizedToPixel(Corners[0]);
        Vector2 pBR = NormalizedToPixel(Corners[1]);
        Vector2 pTR = NormalizedToPixel(Corners[2]);
        Vector2 pTL = NormalizedToPixel(Corners[3]);

        // Draw filled quad using two triangles
        Vector2[] fillPoints = { pTL, pTR, pBR, pBL };
        Color[] fillColors = { QuadFillColor, QuadFillColor, QuadFillColor, QuadFillColor };
        DrawPolygon(fillPoints, fillColors);

        // Warped quad outline (green lines)
        DrawLine(pBL, pBR, QuadLineColor, 2f);
        DrawLine(pBR, pTR, QuadLineColor, 2f);
        DrawLine(pTR, pTL, QuadLineColor, 2f);
        DrawLine(pTL, pBL, QuadLineColor, 2f);

        // Corner handles and labels
        var font = ThemeDB.FallbackFont;
        int fontSize = 10;

        for (int i = 0; i < 4; i++)
        {
            Vector2 pixelPos = NormalizedToPixel(Corners[i]);
            bool active = (i == _dragIndex || i == _hoverIndex);
            Color handleCol = active ? HandleActiveColor : HandleColor;
            float radius = active ? HandleRadius + 2f : HandleRadius;

            DrawCircle(pixelPos, radius, handleCol);

            // Label offset based on corner position to avoid overlap
            Vector2 labelOffset = i switch
            {
                0 => new Vector2(-18f, 14f),  // BL - below left
                1 => new Vector2(8f, 14f),     // BR - below right
                2 => new Vector2(8f, -6f),     // TR - above right
                3 => new Vector2(-18f, -6f),   // TL - above left
                _ => Vector2.Zero
            };

            DrawString(font, pixelPos + labelOffset, CornerLabels[i],
                HorizontalAlignment.Left, -1, fontSize, LabelColor);
        }
    }

    /// <summary>
    /// Converts normalized coordinates (-1..1) to pixel coordinates within this control.
    /// Y is flipped so that +Y is up in normalized space but down in pixel space.
    /// </summary>
    private Vector2 NormalizedToPixel(Vector2 normalized)
    {
        float px = (normalized.X + 1f) * 0.5f * Size.X;
        float py = (1f - (normalized.Y + 1f) * 0.5f) * Size.Y;
        return new Vector2(px, py);
    }

    /// <summary>
    /// Converts pixel coordinates to normalized coordinates (-1..1).
    /// Y is flipped so that moving down in pixels means -Y in normalized space.
    /// </summary>
    private Vector2 PixelToNormalized(Vector2 pixel)
    {
        float nx = (pixel.X / Size.X) * 2f - 1f;
        float ny = -((pixel.Y / Size.Y) * 2f - 1f);
        return new Vector2(nx, ny);
    }

    /// <summary>
    /// Draws a dashed line between two points.
    /// </summary>
    private void DrawDashedLine(Vector2 from, Vector2 to, Color color)
    {
        float totalLength = from.DistanceTo(to);
        if (totalLength < 1f) return;

        Vector2 dir = (to - from).Normalized();
        float drawn = 0f;
        bool drawing = true;

        while (drawn < totalLength)
        {
            float segLen = drawing ? DashLength : GapLength;
            float end = Mathf.Min(drawn + segLen, totalLength);

            if (drawing)
            {
                Vector2 segStart = from + dir * drawn;
                Vector2 segEnd = from + dir * end;
                DrawLine(segStart, segEnd, color, 1f);
            }

            drawn = end;
            drawing = !drawing;
        }
    }
}
