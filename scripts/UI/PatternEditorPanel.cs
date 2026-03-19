using System;
using Godot;
using Godot.Collections;
using LazerSystem.Core;

/// <summary>
/// Freehand pattern editor panel. Users click on a canvas to place points,
/// which are connected by laser lines. The pattern can be saved as a CustomPointPattern
/// resource and placed into the cue grid.
/// </summary>
public partial class PatternEditorPanel : FloatingPanel
{
    private static readonly Color CanvasBg = new Color(0.06f, 0.06f, 0.08f, 1f);
    private static readonly Color GridLineColor = new Color(0.15f, 0.15f, 0.2f, 1f);
    private static readonly Color GridCenterColor = new Color(0.2f, 0.2f, 0.28f, 1f);
    private static readonly Color LineColor = new Color(0.2f, 0.8f, 0.4f, 0.9f);
    private static readonly Color PointColor = new Color(0.9f, 0.9f, 0.2f, 1f);
    private static readonly Color SelectedPointColor = new Color(1f, 0.4f, 0.2f, 1f);
    private static readonly Color TextCol = new Color(0.85f, 0.85f, 0.85f, 1f);
    private static readonly Color DimText = new Color(0.5f, 0.5f, 0.55f, 1f);
    private static readonly Color BtnColor = new Color(0.18f, 0.18f, 0.22f, 1f);
    private static readonly Color ActiveGreen = new Color(0.2f, 0.8f, 0.4f, 1f);

    // Canvas
    private Control _canvas;
    private const float CanvasSize = 300f;
    private const float PointRadius = 6f;
    private const float PointHitRadius = 12f;

    // Pattern data
    private Array<Vector2> _points = new();
    private Array<Color> _pointColors = new();
    private bool _closed;
    private int _selectedIndex = -1;

    // Drag state
    private bool _draggingPoint;
    private Vector2 _dragOffset;

    // Controls
    private LineEdit _nameEdit;
    private CheckButton _closedToggle;
    private ColorPickerButton _pointColorPicker;
    private SpinBox _pointXSpin;
    private SpinBox _pointYSpin;
    private Label _pointInfoLabel;

    // Save-to-grid controls
    private SpinBox _pageSpin;
    private SpinBox _rowSpin;
    private SpinBox _colSpin;

    public PatternEditorPanel()
    {
        PanelTitle = "Pattern Editor";
        InitialSize = new Vector2(340, 700);
    }

    public override void _Ready()
    {
        base._Ready();
        BuildUI();
    }

    private void BuildUI()
    {
        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        ContentContainer.AddChild(scroll);

        var root = new VBoxContainer();
        root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(root);

        // --- Canvas ---
        _canvas = new Control();
        _canvas.CustomMinimumSize = new Vector2(CanvasSize, CanvasSize);
        _canvas.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _canvas.Draw += OnCanvasDraw;
        _canvas.GuiInput += OnCanvasInput;
        _canvas.MouseFilter = MouseFilterEnum.Stop;
        root.AddChild(_canvas);

        // Hint label
        var hint = new Label();
        hint.Text = "Click to add points. Right-click to delete. Drag to move.";
        hint.AddThemeColorOverride("font_color", DimText);
        hint.AddThemeFontSizeOverride("font_size", 11);
        root.AddChild(hint);

        // --- Name ---
        var nameRow = new HBoxContainer();
        nameRow.AddThemeConstantOverride("separation", 6);
        root.AddChild(nameRow);

        var nameLabel = new Label();
        nameLabel.Text = "Name:";
        nameLabel.AddThemeColorOverride("font_color", TextCol);
        nameLabel.CustomMinimumSize = new Vector2(80, 0);
        nameRow.AddChild(nameLabel);

        _nameEdit = new LineEdit();
        _nameEdit.Text = "MyPattern";
        _nameEdit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        nameRow.AddChild(_nameEdit);

        // --- Closed Loop ---
        _closedToggle = new CheckButton();
        _closedToggle.Text = "Closed Loop";
        _closedToggle.AddThemeColorOverride("font_color", TextCol);
        _closedToggle.Toggled += (on) => { _closed = on; _canvas.QueueRedraw(); };
        root.AddChild(_closedToggle);

        // --- Point Color ---
        var colorRow = new HBoxContainer();
        colorRow.AddThemeConstantOverride("separation", 6);
        root.AddChild(colorRow);

        var colorLabel = new Label();
        colorLabel.Text = "Point Color:";
        colorLabel.AddThemeColorOverride("font_color", TextCol);
        colorLabel.CustomMinimumSize = new Vector2(80, 0);
        colorRow.AddChild(colorLabel);

        _pointColorPicker = new ColorPickerButton();
        _pointColorPicker.Color = Colors.White;
        _pointColorPicker.CustomMinimumSize = new Vector2(60, 28);
        _pointColorPicker.ColorChanged += OnPointColorChanged;
        colorRow.AddChild(_pointColorPicker);

        // --- Selected Point Info ---
        _pointInfoLabel = new Label();
        _pointInfoLabel.Text = "No point selected";
        _pointInfoLabel.AddThemeColorOverride("font_color", DimText);
        _pointInfoLabel.AddThemeFontSizeOverride("font_size", 12);
        root.AddChild(_pointInfoLabel);

        // --- Point X/Y spinboxes ---
        var posRow = new HBoxContainer();
        posRow.AddThemeConstantOverride("separation", 6);
        root.AddChild(posRow);

        var xLabel = new Label();
        xLabel.Text = "X:";
        xLabel.AddThemeColorOverride("font_color", TextCol);
        posRow.AddChild(xLabel);

        _pointXSpin = new SpinBox();
        _pointXSpin.MinValue = -1.0;
        _pointXSpin.MaxValue = 1.0;
        _pointXSpin.Step = 0.01;
        _pointXSpin.CustomMinimumSize = new Vector2(90, 0);
        _pointXSpin.ValueChanged += OnPointXChanged;
        posRow.AddChild(_pointXSpin);

        var yLabel = new Label();
        yLabel.Text = "Y:";
        yLabel.AddThemeColorOverride("font_color", TextCol);
        posRow.AddChild(yLabel);

        _pointYSpin = new SpinBox();
        _pointYSpin.MinValue = -1.0;
        _pointYSpin.MaxValue = 1.0;
        _pointYSpin.Step = 0.01;
        _pointYSpin.CustomMinimumSize = new Vector2(90, 0);
        _pointYSpin.ValueChanged += OnPointYChanged;
        posRow.AddChild(_pointYSpin);

        // --- Action buttons ---
        var actionRow = new HBoxContainer();
        actionRow.AddThemeConstantOverride("separation", 6);
        root.AddChild(actionRow);

        var deleteBtn = CreateButton("Delete Point", BtnColor);
        deleteBtn.Pressed += OnDeletePoint;
        actionRow.AddChild(deleteBtn);

        var clearBtn = CreateButton("Clear All", new Color(0.5f, 0.15f, 0.15f, 1f));
        clearBtn.Pressed += OnClearAll;
        actionRow.AddChild(clearBtn);

        // --- Separator ---
        var sep = new HSeparator();
        root.AddChild(sep);

        // --- Save to Grid ---
        var saveHeader = new Label();
        saveHeader.Text = "Save to Grid";
        saveHeader.AddThemeColorOverride("font_color", ActiveGreen);
        saveHeader.AddThemeFontSizeOverride("font_size", 14);
        root.AddChild(saveHeader);

        var gridRow = new HBoxContainer();
        gridRow.AddThemeConstantOverride("separation", 6);
        root.AddChild(gridRow);

        var pageLabel = new Label();
        pageLabel.Text = "Page:";
        pageLabel.AddThemeColorOverride("font_color", TextCol);
        gridRow.AddChild(pageLabel);

        _pageSpin = new SpinBox();
        _pageSpin.MinValue = 1;
        _pageSpin.MaxValue = 256;
        _pageSpin.Value = 1;
        _pageSpin.CustomMinimumSize = new Vector2(70, 0);
        gridRow.AddChild(_pageSpin);

        var rowLabel = new Label();
        rowLabel.Text = "Row:";
        rowLabel.AddThemeColorOverride("font_color", TextCol);
        gridRow.AddChild(rowLabel);

        _rowSpin = new SpinBox();
        _rowSpin.MinValue = 1;
        _rowSpin.MaxValue = 6;
        _rowSpin.Value = 1;
        _rowSpin.CustomMinimumSize = new Vector2(60, 0);
        gridRow.AddChild(_rowSpin);

        var colLabel = new Label();
        colLabel.Text = "Col:";
        colLabel.AddThemeColorOverride("font_color", TextCol);
        gridRow.AddChild(colLabel);

        _colSpin = new SpinBox();
        _colSpin.MinValue = 1;
        _colSpin.MaxValue = 10;
        _colSpin.Value = 1;
        _colSpin.CustomMinimumSize = new Vector2(60, 0);
        gridRow.AddChild(_colSpin);

        // --- Bottom bar ---
        var bottomRow = new HBoxContainer();
        bottomRow.AddThemeConstantOverride("separation", 10);
        root.AddChild(bottomRow);

        var saveBtn = CreateButton("Save", ActiveGreen);
        saveBtn.Pressed += OnSave;
        bottomRow.AddChild(saveBtn);

        var cancelBtn = CreateButton("Cancel", BtnColor);
        cancelBtn.Pressed += () => QueueFree();
        bottomRow.AddChild(cancelBtn);

        UpdatePointInfo();
    }

    // ═══════════════════════════════════════
    //  Canvas drawing
    // ═══════════════════════════════════════

    private void OnCanvasDraw()
    {
        float w = _canvas.Size.X;
        float h = Mathf.Min(_canvas.Size.Y, CanvasSize);

        // Background
        _canvas.DrawRect(new Rect2(0, 0, w, h), CanvasBg);

        // Grid lines at -1, -0.5, 0, 0.5, 1 on each axis
        for (int i = 0; i <= 4; i++)
        {
            float t = i / 4f;
            float x = t * w;
            float y = t * h;
            var lineCol = (i == 2) ? GridCenterColor : GridLineColor;
            _canvas.DrawLine(new Vector2(x, 0), new Vector2(x, h), lineCol, 1f);
            _canvas.DrawLine(new Vector2(0, y), new Vector2(w, y), lineCol, 1f);
        }

        if (_points.Count == 0)
            return;

        // Draw lines between consecutive points
        for (int i = 0; i < _points.Count - 1; i++)
        {
            Vector2 a = NormalizedToCanvas(_points[i], w, h);
            Vector2 b = NormalizedToCanvas(_points[i + 1], w, h);
            Color ca = (i < _pointColors.Count) ? _pointColors[i] : Colors.White;
            Color cb = (i + 1 < _pointColors.Count) ? _pointColors[i + 1] : Colors.White;
            // Average color for segment
            Color segCol = ca.Lerp(cb, 0.5f);
            segCol.A = 0.9f;
            _canvas.DrawLine(a, b, segCol, 2f);
        }

        // Closing line
        if (_closed && _points.Count > 2)
        {
            Vector2 a = NormalizedToCanvas(_points[_points.Count - 1], w, h);
            Vector2 b = NormalizedToCanvas(_points[0], w, h);
            _canvas.DrawLine(a, b, LineColor, 2f, true);
        }

        // Draw points
        var font = ThemeDB.FallbackFont;
        for (int i = 0; i < _points.Count; i++)
        {
            Vector2 pos = NormalizedToCanvas(_points[i], w, h);
            Color col = (i < _pointColors.Count) ? _pointColors[i] : Colors.White;
            Color drawCol = (i == _selectedIndex) ? SelectedPointColor : col;
            _canvas.DrawCircle(pos, PointRadius, drawCol);
            _canvas.DrawArc(pos, PointRadius + 1, 0, Mathf.Tau, 16,
                (i == _selectedIndex) ? Colors.White : DimText, 1f);

            // Index number
            _canvas.DrawString(font, pos + new Vector2(8, -4), i.ToString(),
                HorizontalAlignment.Left, -1, 10, TextCol);
        }
    }

    // ═══════════════════════════════════════
    //  Canvas input
    // ═══════════════════════════════════════

    private void OnCanvasInput(InputEvent @event)
    {
        float w = _canvas.Size.X;
        float h = Mathf.Min(_canvas.Size.Y, CanvasSize);

        if (@event is InputEventMouseButton mb)
        {
            Vector2 localPos = mb.Position;

            if (mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                // Check if clicking an existing point
                int hitIdx = HitTestPoint(localPos, w, h);
                if (hitIdx >= 0)
                {
                    _selectedIndex = hitIdx;
                    _draggingPoint = true;
                    Vector2 pointCanvas = NormalizedToCanvas(_points[hitIdx], w, h);
                    _dragOffset = pointCanvas - localPos;
                    UpdatePointInfo();
                    _canvas.QueueRedraw();
                }
                else
                {
                    // Add new point
                    Vector2 norm = CanvasToNormalized(localPos, w, h);
                    norm.X = Mathf.Clamp(norm.X, -1f, 1f);
                    norm.Y = Mathf.Clamp(norm.Y, -1f, 1f);
                    _points.Add(norm);
                    _pointColors.Add(_pointColorPicker.Color);
                    _selectedIndex = _points.Count - 1;
                    UpdatePointInfo();
                    _canvas.QueueRedraw();
                }
                _canvas.AcceptEvent();
            }
            else if (mb.Pressed && mb.ButtonIndex == MouseButton.Right)
            {
                // Right-click to delete
                int hitIdx = HitTestPoint(localPos, w, h);
                if (hitIdx >= 0)
                {
                    _points.RemoveAt(hitIdx);
                    if (hitIdx < _pointColors.Count)
                        _pointColors.RemoveAt(hitIdx);
                    if (_selectedIndex >= _points.Count)
                        _selectedIndex = _points.Count - 1;
                    UpdatePointInfo();
                    _canvas.QueueRedraw();
                }
                _canvas.AcceptEvent();
            }
            else if (!mb.Pressed)
            {
                _draggingPoint = false;
            }
        }
        else if (@event is InputEventMouseMotion mm && _draggingPoint && _selectedIndex >= 0)
        {
            Vector2 canvasPos = mm.Position + _dragOffset;
            Vector2 norm = CanvasToNormalized(canvasPos, w, h);
            norm.X = Mathf.Clamp(norm.X, -1f, 1f);
            norm.Y = Mathf.Clamp(norm.Y, -1f, 1f);
            _points[_selectedIndex] = norm;
            UpdatePointInfo();
            _canvas.QueueRedraw();
            _canvas.AcceptEvent();
        }
    }

    // ═══════════════════════════════════════
    //  Coordinate helpers
    // ═══════════════════════════════════════

    /// <summary>Converts normalized (-1..1) to canvas pixel coords.</summary>
    private static Vector2 NormalizedToCanvas(Vector2 norm, float w, float h)
    {
        float x = (norm.X + 1f) * 0.5f * w;
        float y = (1f - (norm.Y + 1f) * 0.5f) * h; // Y-up → Y-down
        return new Vector2(x, y);
    }

    /// <summary>Converts canvas pixel coords to normalized (-1..1).</summary>
    private static Vector2 CanvasToNormalized(Vector2 canvas, float w, float h)
    {
        float x = (canvas.X / w) * 2f - 1f;
        float y = 1f - (canvas.Y / h) * 2f; // Y-down → Y-up
        return new Vector2(x, y);
    }

    private int HitTestPoint(Vector2 localPos, float w, float h)
    {
        for (int i = _points.Count - 1; i >= 0; i--)
        {
            Vector2 ptCanvas = NormalizedToCanvas(_points[i], w, h);
            if (ptCanvas.DistanceTo(localPos) < PointHitRadius)
                return i;
        }
        return -1;
    }

    // ═══════════════════════════════════════
    //  Control callbacks
    // ═══════════════════════════════════════

    private void OnPointColorChanged(Color color)
    {
        if (_selectedIndex >= 0 && _selectedIndex < _pointColors.Count)
        {
            _pointColors[_selectedIndex] = color;
            _canvas.QueueRedraw();
        }
    }

    private void OnPointXChanged(double val)
    {
        if (_selectedIndex >= 0 && _selectedIndex < _points.Count)
        {
            var p = _points[_selectedIndex];
            p.X = (float)val;
            _points[_selectedIndex] = p;
            _canvas.QueueRedraw();
        }
    }

    private void OnPointYChanged(double val)
    {
        if (_selectedIndex >= 0 && _selectedIndex < _points.Count)
        {
            var p = _points[_selectedIndex];
            p.Y = (float)val;
            _points[_selectedIndex] = p;
            _canvas.QueueRedraw();
        }
    }

    private void OnDeletePoint()
    {
        if (_selectedIndex >= 0 && _selectedIndex < _points.Count)
        {
            _points.RemoveAt(_selectedIndex);
            if (_selectedIndex < _pointColors.Count)
                _pointColors.RemoveAt(_selectedIndex);
            if (_selectedIndex >= _points.Count)
                _selectedIndex = _points.Count - 1;
            UpdatePointInfo();
            _canvas.QueueRedraw();
        }
    }

    private void OnClearAll()
    {
        _points.Clear();
        _pointColors.Clear();
        _selectedIndex = -1;
        UpdatePointInfo();
        _canvas.QueueRedraw();
    }

    private void UpdatePointInfo()
    {
        if (_selectedIndex >= 0 && _selectedIndex < _points.Count)
        {
            var p = _points[_selectedIndex];
            _pointInfoLabel.Text = $"Point {_selectedIndex}: ({p.X:F2}, {p.Y:F2})";
            // Update spinboxes without triggering callbacks
            _pointXSpin.SetValueNoSignal(p.X);
            _pointYSpin.SetValueNoSignal(p.Y);
            if (_selectedIndex < _pointColors.Count)
                _pointColorPicker.Color = _pointColors[_selectedIndex];
        }
        else
        {
            _pointInfoLabel.Text = $"{_points.Count} points — No selection";
        }
    }

    // ═══════════════════════════════════════
    //  Save
    // ═══════════════════════════════════════

    private void OnSave()
    {
        if (_points.Count < 2)
        {
            GD.PushWarning("[PatternEditor] Need at least 2 points to save.");
            return;
        }

        string patternName = _nameEdit.Text.Trim();
        if (string.IsNullOrEmpty(patternName))
            patternName = "Untitled";

        // Sanitize filename
        string safeName = patternName.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");

        // Ensure directory exists
        string dir = "user://patterns";
        if (!DirAccess.DirExistsAbsolute(dir))
            DirAccess.MakeDirRecursiveAbsolute(dir);

        // Create resource
        var pattern = new CustomPointPattern();
        pattern.PatternName = patternName;
        pattern.Points = new Array<Vector2>(_points);
        pattern.PointColors = new Array<Color>(_pointColors);
        pattern.Closed = _closed;

        string path = $"{dir}/{safeName}.tres";
        var err = ResourceSaver.Save(pattern, path);
        if (err != Error.Ok)
        {
            GD.PushError($"[PatternEditor] Failed to save pattern: {err}");
            return;
        }

        GD.Print($"[PatternEditor] Saved pattern '{patternName}' to {path}");

        // Place cue in grid
        int page = (int)_pageSpin.Value - 1;
        int row = (int)_rowSpin.Value - 1;
        int col = (int)_colSpin.Value - 1;

        var cue = new LaserCue();
        cue.CueName = patternName;
        cue.PatternType = LaserPatternType.CustomILDA;
        cue.IldaAssetPath = path;
        cue.Color = Colors.White;
        cue.Intensity = 1f;
        cue.Size = 0.5f;
        cue.Speed = 0f;
        cue.GridPage = page;
        cue.GridRow = row;
        cue.GridColumn = col;

        LiveEngine.Instance?.SetCue(page, row, col, cue);

        GD.Print($"[PatternEditor] Placed cue at page {page + 1}, row {row + 1}, col {col + 1}");

        // Close panel
        QueueFree();
    }

    // ═══════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════

    private static Button CreateButton(string text, Color bgColor)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(90, 30);

        var style = new StyleBoxFlat();
        style.BgColor = bgColor;
        style.CornerRadiusBottomLeft = 4;
        style.CornerRadiusBottomRight = 4;
        style.CornerRadiusTopLeft = 4;
        style.CornerRadiusTopRight = 4;
        style.ContentMarginLeft = 8;
        style.ContentMarginRight = 8;
        style.ContentMarginTop = 4;
        style.ContentMarginBottom = 4;
        btn.AddThemeStyleboxOverride("normal", style);

        var hoverStyle = (StyleBoxFlat)style.Duplicate();
        hoverStyle.BgColor = bgColor.Lightened(0.15f);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);

        var pressedStyle = (StyleBoxFlat)style.Duplicate();
        pressedStyle.BgColor = bgColor.Lightened(0.3f);
        btn.AddThemeStyleboxOverride("pressed", pressedStyle);

        return btn;
    }
}
