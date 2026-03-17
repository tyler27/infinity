using System;
using Godot;

/// <summary>
/// A generalized draggable, resizable floating panel that stays within the application window.
/// Drag the title bar to move. Drag edges/corners to resize.
/// Set PanelTitle and InitialSize before adding to the scene tree.
/// </summary>
public partial class FloatingPanel : Control
{
    public event Action OnCloseRequested;
    public VBoxContainer ContentContainer { get; private set; }

    public string PanelTitle { get; set; } = "Panel";
    public Vector2 InitialSize { get; set; } = new Vector2(500, 600);

    private static readonly Color TitleBarColor = new Color(0.15f, 0.15f, 0.18f, 0.98f);
    private static readonly Color PanelBgColor = new Color(0.08f, 0.08f, 0.1f, 0.98f);
    private static readonly Color BorderColor = new Color(0.3f, 0.6f, 0.4f, 0.8f);

    private const float TitleBarHeight = 30f;
    private const float ResizeMargin = 10f;
    private static readonly Vector2 MinPanelSize = new Vector2(300, 200);

    private bool _dragging;
    private bool _resizing;
    private Vector2 _dragStart;
    private Vector2 _posAtDragStart;
    private Vector2 _sizeAtDragStart;
    private ResizeEdge _resizeEdge;
    private bool _maximized;
    private Vector2 _preMaxPos;
    private Vector2 _preMaxSize;

    [Flags]
    private enum ResizeEdge
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 4,
        Bottom = 8,
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        Size = InitialSize;

        // Content area below the title bar — no ScrollContainer wrapper
        // (subclasses that need scrolling can add their own)
        ContentContainer = new VBoxContainer();
        ContentContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        ContentContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(ContentContainer);

        UpdateLayout();
    }

    public override void _Draw()
    {
        // Background
        var bgRect = new Rect2(Vector2.Zero, Size);
        DrawRect(bgRect, PanelBgColor);

        // Border
        DrawRect(bgRect, BorderColor, false, 2f);

        // Title bar
        var titleRect = new Rect2(0, 0, Size.X, TitleBarHeight);
        DrawRect(titleRect, TitleBarColor);

        // Title bar bottom border
        DrawLine(new Vector2(0, TitleBarHeight), new Vector2(Size.X, TitleBarHeight), BorderColor, 1f);

        // Title text
        var font = ThemeDB.FallbackFont;
        int fontSize = 13;
        DrawString(font, new Vector2(12, 20), PanelTitle, HorizontalAlignment.Left,
            -1, fontSize, new Color(0.8f, 0.9f, 0.85f, 1f));

        // Close button [X]
        var closeBtnRect = new Rect2(Size.X - 34, 4, 26, 22);
        DrawRect(closeBtnRect, new Color(0.6f, 0.15f, 0.15f, 1f));
        DrawString(font, new Vector2(Size.X - 27, 20), "X", HorizontalAlignment.Left,
            -1, 12, Colors.White);

        // Maximize button [+/-]
        var maxBtnRect = new Rect2(Size.X - 64, 4, 26, 22);
        DrawRect(maxBtnRect, new Color(0.25f, 0.25f, 0.3f, 1f));
        DrawString(font, new Vector2(Size.X - 58, 20), _maximized ? "-" : "+", HorizontalAlignment.Left,
            -1, 14, Colors.White);

        // Resize handle indicator (bottom-right corner)
        var handleColor = new Color(0.4f, 0.7f, 0.5f, 0.5f);
        float hx = Size.X;
        float hy = Size.Y;
        DrawLine(new Vector2(hx - 14, hy - 2), new Vector2(hx - 2, hy - 14), handleColor, 1.5f);
        DrawLine(new Vector2(hx - 10, hy - 2), new Vector2(hx - 2, hy - 10), handleColor, 1.5f);
        DrawLine(new Vector2(hx - 6, hy - 2), new Vector2(hx - 2, hy - 6), handleColor, 1.5f);
    }

    private void UpdateLayout()
    {
        if (ContentContainer == null) return;

        ContentContainer.Position = new Vector2(4, TitleBarHeight + 2);
        ContentContainer.Size = new Vector2(Size.X - 8, Size.Y - TitleBarHeight - 6);
        ContentContainer.CustomMinimumSize = new Vector2(Size.X - 8, Size.Y - TitleBarHeight - 6);
        QueueRedraw();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            Vector2 localPos = mb.GlobalPosition - GlobalPosition;
            bool isInside = new Rect2(Vector2.Zero, Size).HasPoint(localPos);

            if (mb.Pressed && isInside)
            {
                // Close button hit test
                var closeBtnRect = new Rect2(Size.X - 34, 4, 26, 22);
                if (closeBtnRect.HasPoint(localPos))
                {
                    OnCloseRequested?.Invoke();
                    GetViewport().SetInputAsHandled();
                    return;
                }

                // Maximize button hit test
                var maxBtnRect = new Rect2(Size.X - 64, 4, 26, 22);
                if (maxBtnRect.HasPoint(localPos))
                {
                    ToggleMaximize();
                    GetViewport().SetInputAsHandled();
                    return;
                }

                // Check resize edges
                _resizeEdge = GetResizeEdge(localPos);
                if (_resizeEdge != ResizeEdge.None)
                {
                    _resizing = true;
                    _dragging = false;
                    _dragStart = mb.GlobalPosition;
                    _posAtDragStart = Position;
                    _sizeAtDragStart = Size;
                    GetViewport().SetInputAsHandled();
                    return;
                }

                // Title bar drag
                if (localPos.Y < TitleBarHeight)
                {
                    _dragging = true;
                    _resizing = false;
                    _dragStart = mb.GlobalPosition;
                    _posAtDragStart = Position;
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }
            else if (!mb.Pressed)
            {
                _dragging = false;
                _resizing = false;
            }
        }
        else if (@event is InputEventMouseMotion mm)
        {
            if (_dragging)
            {
                Vector2 delta = mm.GlobalPosition - _dragStart;
                var newPos = _posAtDragStart + delta;

                // Clamp to viewport
                var vpSize = GetViewportRect().Size;
                newPos.X = Mathf.Clamp(newPos.X, -Size.X + 100, vpSize.X - 100);
                newPos.Y = Mathf.Clamp(newPos.Y, 0, vpSize.Y - TitleBarHeight);
                Position = newPos;
                _maximized = false;
                GetViewport().SetInputAsHandled();
                UpdateLayout();
            }
            else if (_resizing)
            {
                Vector2 delta = mm.GlobalPosition - _dragStart;
                Vector2 newPos = _posAtDragStart;
                Vector2 newSize = _sizeAtDragStart;

                if (_resizeEdge.HasFlag(ResizeEdge.Right))
                    newSize.X = _sizeAtDragStart.X + delta.X;
                if (_resizeEdge.HasFlag(ResizeEdge.Bottom))
                    newSize.Y = _sizeAtDragStart.Y + delta.Y;
                if (_resizeEdge.HasFlag(ResizeEdge.Left))
                {
                    newPos.X = _posAtDragStart.X + delta.X;
                    newSize.X = _sizeAtDragStart.X - delta.X;
                }
                if (_resizeEdge.HasFlag(ResizeEdge.Top))
                {
                    newPos.Y = _posAtDragStart.Y + delta.Y;
                    newSize.Y = _sizeAtDragStart.Y - delta.Y;
                }

                // Enforce min size
                if (newSize.X < MinPanelSize.X)
                {
                    if (_resizeEdge.HasFlag(ResizeEdge.Left))
                        newPos.X = _posAtDragStart.X + _sizeAtDragStart.X - MinPanelSize.X;
                    newSize.X = MinPanelSize.X;
                }
                if (newSize.Y < MinPanelSize.Y)
                {
                    if (_resizeEdge.HasFlag(ResizeEdge.Top))
                        newPos.Y = _posAtDragStart.Y + _sizeAtDragStart.Y - MinPanelSize.Y;
                    newSize.Y = MinPanelSize.Y;
                }

                Position = newPos;
                Size = newSize;
                _maximized = false;
                GetViewport().SetInputAsHandled();
                UpdateLayout();
            }
            else
            {
                // Update cursor on hover
                Vector2 localPos = mm.GlobalPosition - GlobalPosition;
                bool isInside = new Rect2(Vector2.Zero, Size).HasPoint(localPos);
                if (isInside)
                {
                    var edge = GetResizeEdge(localPos);
                    MouseDefaultCursorShape = edge switch
                    {
                        ResizeEdge.Left or ResizeEdge.Right => CursorShape.Hsize,
                        ResizeEdge.Top or ResizeEdge.Bottom => CursorShape.Vsize,
                        ResizeEdge.Left | ResizeEdge.Top => CursorShape.Fdiagsize,
                        ResizeEdge.Right | ResizeEdge.Bottom => CursorShape.Fdiagsize,
                        ResizeEdge.Right | ResizeEdge.Top => CursorShape.Bdiagsize,
                        ResizeEdge.Left | ResizeEdge.Bottom => CursorShape.Bdiagsize,
                        _ => localPos.Y < TitleBarHeight ? CursorShape.Move : CursorShape.Arrow,
                    };
                }
            }
        }
    }

    private void ToggleMaximize()
    {
        if (_maximized)
        {
            Position = _preMaxPos;
            Size = _preMaxSize;
            _maximized = false;
        }
        else
        {
            _preMaxPos = Position;
            _preMaxSize = Size;
            Position = Vector2.Zero;
            Size = GetViewportRect().Size;
            _maximized = true;
        }
        UpdateLayout();
    }

    private ResizeEdge GetResizeEdge(Vector2 localPos)
    {
        ResizeEdge edge = ResizeEdge.None;
        if (localPos.X < ResizeMargin) edge |= ResizeEdge.Left;
        if (localPos.X > Size.X - ResizeMargin) edge |= ResizeEdge.Right;
        if (localPos.Y < ResizeMargin) edge |= ResizeEdge.Top;
        if (localPos.Y > Size.Y - ResizeMargin) edge |= ResizeEdge.Bottom;
        return edge;
    }
}
