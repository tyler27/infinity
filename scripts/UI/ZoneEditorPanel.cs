using System;
using Godot;
using LazerSystem.Core;
using LazerSystem.ArtNet;

/// <summary>
/// Complete zone editor panel for configuring projection zones.
/// Provides zone list management, position/scale/rotation controls,
/// keystone correction canvas, safety zone settings, and test pattern output.
/// </summary>
public partial class ZoneEditorPanel : FloatingPanel
{
    // Theme colors (matching MainUI)
    private static readonly Color PanelBg = new Color(0.12f, 0.12f, 0.15f, 0.92f);
    private static readonly Color ActiveGreen = new Color(0.2f, 0.8f, 0.4f, 1f);
    private static readonly Color TextColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    private static readonly Color DimText = new Color(0.5f, 0.5f, 0.55f, 1f);
    private static readonly Color CueDefault = new Color(0.18f, 0.18f, 0.22f, 1f);
    private static readonly Color SectionHeaderColor = new Color(0.2f, 0.8f, 0.4f, 1f);

    // Zone list
    private VBoxContainer _zoneListContainer;
    private int _selectedZoneIndex = -1;

    // Right panel container (zone properties)
    private VBoxContainer _rightPanelContent;
    private ScrollContainer _rightScroll;

    // Identity controls
    private LineEdit _nameEdit;
    private OptionButton _projectorOption;
    private CheckButton _enabledCheck;

    // Position & Scale sliders
    private HSlider _offsetXSlider;
    private Label _offsetXValue;
    private HSlider _offsetYSlider;
    private Label _offsetYValue;
    private HSlider _scaleXSlider;
    private Label _scaleXValue;
    private HSlider _scaleYSlider;
    private Label _scaleYValue;
    private HSlider _rotationSlider;
    private Label _rotationValue;

    // Keystone
    private ZoneKeystoneCanvas _keystoneCanvas;

    // Safety Zone sliders
    private HSlider _safetyLeftSlider;
    private Label _safetyLeftValue;
    private HSlider _safetyRightSlider;
    private Label _safetyRightValue;
    private HSlider _safetyTopSlider;
    private Label _safetyTopValue;
    private HSlider _safetyBottomSlider;
    private Label _safetyBottomValue;

    // Actions
    private Button _testPatternBtn;
    private Button _showBoundaryBtn;
    private Button _showAllBoundariesBtn;
    private bool _showingBoundary;
    private bool _showingAllBoundaries;

    public override void _Ready()
    {
        PanelTitle = "Zone Editor";
        InitialSize = new Vector2(650, 700);
        base._Ready();
        BuildUI();
    }

    private void BuildUI()
    {
        var split = new HSplitContainer();
        split.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        split.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        split.SplitOffset = 180;
        ContentContainer.AddChild(split);

        // Left panel: zone list
        BuildLeftPanel(split);

        // Right panel: zone properties
        BuildRightPanel(split);
    }

    // -----------------------------------------------
    //  LEFT PANEL - Zone List
    // -----------------------------------------------
    private void BuildLeftPanel(HSplitContainer parent)
    {
        var leftPanel = new PanelContainer();
        leftPanel.CustomMinimumSize = new Vector2(180, 0);
        leftPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        ApplyPanelStyle(leftPanel, PanelBg);
        parent.AddChild(leftPanel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        vbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        leftPanel.AddChild(vbox);

        // Header
        var header = new Label();
        header.Text = "ZONES";
        header.HorizontalAlignment = HorizontalAlignment.Center;
        header.AddThemeColorOverride("font_color", ActiveGreen);
        header.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(header);

        // Scrollable zone list
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        vbox.AddChild(scroll);

        _zoneListContainer = new VBoxContainer();
        _zoneListContainer.AddThemeConstantOverride("separation", 2);
        _zoneListContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(_zoneListContainer);

        // Add/Remove buttons
        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(btnRow);

        var addBtn = CreateStyledButton("+ Add", new Vector2(0, 30), ActiveGreen);
        addBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        addBtn.AddThemeColorOverride("font_color", Colors.Black);
        addBtn.Pressed += OnAddZone;
        btnRow.AddChild(addBtn);

        var removeBtn = CreateStyledButton("- Remove", new Vector2(0, 30), new Color(0.7f, 0.15f, 0.15f, 1f));
        removeBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        removeBtn.Pressed += OnRemoveZone;
        btnRow.AddChild(removeBtn);

        RefreshZoneList();
    }

    // -----------------------------------------------
    //  RIGHT PANEL - Zone Properties
    // -----------------------------------------------
    private void BuildRightPanel(HSplitContainer parent)
    {
        _rightScroll = new ScrollContainer();
        _rightScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _rightScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _rightScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        parent.AddChild(_rightScroll);

        _rightPanelContent = new VBoxContainer();
        _rightPanelContent.AddThemeConstantOverride("separation", 8);
        _rightPanelContent.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _rightScroll.AddChild(_rightPanelContent);

        // -- IDENTITY section --
        AddSectionHeader(_rightPanelContent, "IDENTITY");

        // Name
        var nameRow = new HBoxContainer();
        nameRow.AddThemeConstantOverride("separation", 6);
        _rightPanelContent.AddChild(nameRow);
        AddRowLabel(nameRow, "Name");
        _nameEdit = new LineEdit();
        _nameEdit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _nameEdit.CustomMinimumSize = new Vector2(0, 28);
        _nameEdit.AddThemeFontSizeOverride("font_size", 12);
        _nameEdit.TextChanged += (text) =>
        {
            var zone = GetSelectedZone();
            if (zone != null) zone.ZoneName = text;
            RefreshZoneList();
        };
        nameRow.AddChild(_nameEdit);

        // Projector
        var projRow = new HBoxContainer();
        projRow.AddThemeConstantOverride("separation", 6);
        _rightPanelContent.AddChild(projRow);
        AddRowLabel(projRow, "Projector");
        _projectorOption = new OptionButton();
        _projectorOption.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _projectorOption.CustomMinimumSize = new Vector2(0, 28);
        _projectorOption.AddThemeFontSizeOverride("font_size", 12);
        for (int i = 0; i < 4; i++)
        {
            string projName = GetProjectorDisplayName(i);
            _projectorOption.AddItem($"P{i + 1} ({projName})", i);
        }
        _projectorOption.ItemSelected += (idx) =>
        {
            var zone = GetSelectedZone();
            if (zone != null) zone.ProjectorIndex = (int)idx;
        };
        projRow.AddChild(_projectorOption);

        // Enabled
        var enabledRow = new HBoxContainer();
        enabledRow.AddThemeConstantOverride("separation", 6);
        _rightPanelContent.AddChild(enabledRow);
        AddRowLabel(enabledRow, "Enabled");
        _enabledCheck = new CheckButton();
        _enabledCheck.ButtonPressed = true;
        _enabledCheck.Toggled += (on) =>
        {
            var zone = GetSelectedZone();
            if (zone != null) zone.Enabled = on;
        };
        enabledRow.AddChild(_enabledCheck);

        // -- POSITION & SCALE section --
        AddSectionHeader(_rightPanelContent, "POSITION & SCALE");

        (_offsetXSlider, _offsetXValue) = CreateSliderRow(_rightPanelContent, "Offset X", -100, 100, 0);
        _offsetXSlider.ValueChanged += (val) =>
        {
            var zone = GetSelectedZone();
            if (zone != null)
                zone.PositionOffset = new Vector2((float)(val / 100.0), zone.PositionOffset.Y);
            _offsetXValue.Text = $"{(int)val}";
        };

        (_offsetYSlider, _offsetYValue) = CreateSliderRow(_rightPanelContent, "Offset Y", -100, 100, 0);
        _offsetYSlider.ValueChanged += (val) =>
        {
            var zone = GetSelectedZone();
            if (zone != null)
                zone.PositionOffset = new Vector2(zone.PositionOffset.X, (float)(val / 100.0));
            _offsetYValue.Text = $"{(int)val}";
        };

        (_scaleXSlider, _scaleXValue) = CreateSliderRow(_rightPanelContent, "Scale X", 10, 200, 100);
        _scaleXSlider.ValueChanged += (val) =>
        {
            var zone = GetSelectedZone();
            if (zone != null)
                zone.Scale = new Vector2((float)(val / 100.0), zone.Scale.Y);
            _scaleXValue.Text = $"{(int)val}%";
        };

        (_scaleYSlider, _scaleYValue) = CreateSliderRow(_rightPanelContent, "Scale Y", 10, 200, 100);
        _scaleYSlider.ValueChanged += (val) =>
        {
            var zone = GetSelectedZone();
            if (zone != null)
                zone.Scale = new Vector2(zone.Scale.X, (float)(val / 100.0));
            _scaleYValue.Text = $"{(int)val}%";
        };

        (_rotationSlider, _rotationValue) = CreateSliderRow(_rightPanelContent, "Rotation", -180, 180, 0);
        _rotationSlider.ValueChanged += (val) =>
        {
            var zone = GetSelectedZone();
            if (zone != null)
                zone.Rotation = (float)val;
            _rotationValue.Text = $"{(int)val}\u00b0";
        };

        // -- KEYSTONE section --
        AddSectionHeader(_rightPanelContent, "KEYSTONE");

        _keystoneCanvas = new ZoneKeystoneCanvas();
        _keystoneCanvas.CustomMinimumSize = new Vector2(250, 200);
        _keystoneCanvas.CornersChanged += (corners) =>
        {
            var zone = GetSelectedZone();
            if (zone != null)
            {
                zone.KeystoneCorners = new Vector2[]
                {
                    corners[0], corners[1], corners[2], corners[3]
                };
            }
        };
        _rightPanelContent.AddChild(_keystoneCanvas);

        var resetKeystoneBtn = CreateStyledButton("Reset Keystone", new Vector2(0, 28), CueDefault);
        resetKeystoneBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        resetKeystoneBtn.Pressed += () =>
        {
            var defaultCorners = new Vector2[]
            {
                new Vector2(-1f, -1f),
                new Vector2( 1f, -1f),
                new Vector2( 1f,  1f),
                new Vector2(-1f,  1f),
            };
            _keystoneCanvas.Corners = defaultCorners;
            _keystoneCanvas.QueueRedraw();

            var zone = GetSelectedZone();
            if (zone != null)
            {
                zone.KeystoneCorners = new Vector2[]
                {
                    defaultCorners[0], defaultCorners[1],
                    defaultCorners[2], defaultCorners[3]
                };
            }
        };
        _rightPanelContent.AddChild(resetKeystoneBtn);

        // -- SAFETY ZONE section --
        AddSectionHeader(_rightPanelContent, "SAFETY ZONE");

        (_safetyLeftSlider, _safetyLeftValue) = CreateSliderRow(_rightPanelContent, "Left", 0, 100, 0);
        _safetyLeftSlider.ValueChanged += (val) =>
        {
            var zone = GetSelectedZone();
            if (zone != null)
            {
                float left = (float)(val / 100.0);
                zone.SafetyZone = new Rect2(left, zone.SafetyZone.Position.Y,
                    zone.SafetyZone.Size.X, zone.SafetyZone.Size.Y);
            }
            _safetyLeftValue.Text = $"{(int)val}";
        };

        (_safetyRightSlider, _safetyRightValue) = CreateSliderRow(_rightPanelContent, "Right", 0, 100, 100);
        _safetyRightSlider.ValueChanged += (val) =>
        {
            var zone = GetSelectedZone();
            if (zone != null)
            {
                float right = (float)(val / 100.0);
                float width = right - zone.SafetyZone.Position.X;
                zone.SafetyZone = new Rect2(zone.SafetyZone.Position.X, zone.SafetyZone.Position.Y,
                    width, zone.SafetyZone.Size.Y);
            }
            _safetyRightValue.Text = $"{(int)val}";
        };

        (_safetyTopSlider, _safetyTopValue) = CreateSliderRow(_rightPanelContent, "Top", 0, 100, 0);
        _safetyTopSlider.ValueChanged += (val) =>
        {
            var zone = GetSelectedZone();
            if (zone != null)
            {
                float top = (float)(val / 100.0);
                zone.SafetyZone = new Rect2(zone.SafetyZone.Position.X, top,
                    zone.SafetyZone.Size.X, zone.SafetyZone.Size.Y);
            }
            _safetyTopValue.Text = $"{(int)val}";
        };

        (_safetyBottomSlider, _safetyBottomValue) = CreateSliderRow(_rightPanelContent, "Bottom", 0, 100, 100);
        _safetyBottomSlider.ValueChanged += (val) =>
        {
            var zone = GetSelectedZone();
            if (zone != null)
            {
                float bottom = (float)(val / 100.0);
                float height = bottom - zone.SafetyZone.Position.Y;
                zone.SafetyZone = new Rect2(zone.SafetyZone.Position.X, zone.SafetyZone.Position.Y,
                    zone.SafetyZone.Size.X, height);
            }
            _safetyBottomValue.Text = $"{(int)val}";
        };

        // -- ACTIONS section --
        AddSectionHeader(_rightPanelContent, "ACTIONS");

        _testPatternBtn = CreateStyledButton("Test Pattern", new Vector2(0, 32), CueDefault);
        _testPatternBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _testPatternBtn.Pressed += OnTestPattern;
        _rightPanelContent.AddChild(_testPatternBtn);

        _showBoundaryBtn = CreateStyledButton("Show Zone Boundary", new Vector2(0, 32), CueDefault);
        _showBoundaryBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _showBoundaryBtn.Pressed += OnToggleBoundary;
        _rightPanelContent.AddChild(_showBoundaryBtn);

        _showAllBoundariesBtn = CreateStyledButton("Show All Boundaries", new Vector2(0, 32), CueDefault);
        _showAllBoundariesBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _showAllBoundariesBtn.Pressed += OnToggleAllBoundaries;
        _rightPanelContent.AddChild(_showAllBoundariesBtn);

        // Initially hide the right panel until a zone is selected
        _rightScroll.Visible = false;
    }

    // -----------------------------------------------
    //  Zone List Management
    // -----------------------------------------------
    private void RefreshZoneList()
    {
        // Clear existing buttons
        foreach (var child in _zoneListContainer.GetChildren())
            child.QueueFree();

        var mgr = LaserSystemManager.Instance;
        if (mgr == null) return;

        for (int i = 0; i < mgr.Zones.Count; i++)
        {
            int idx = i;
            var zone = mgr.Zones[i];
            if (zone == null) continue;

            string displayName = string.IsNullOrEmpty(zone.ZoneName) ? $"Zone {i + 1}" : zone.ZoneName;
            bool selected = (i == _selectedZoneIndex);

            var btn = new Button();
            btn.Text = $"{displayName} [P{zone.ProjectorIndex + 1}]";
            btn.CustomMinimumSize = new Vector2(0, 28);
            btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            btn.Alignment = HorizontalAlignment.Left;
            btn.ClipText = true;
            btn.AddThemeFontSizeOverride("font_size", 11);
            btn.AddThemeColorOverride("font_color", selected ? ActiveGreen : TextColor);

            var style = new StyleBoxFlat();
            style.BgColor = selected ? new Color(0.18f, 0.25f, 0.35f, 1f) : CueDefault;
            style.SetCornerRadiusAll(3);
            style.SetContentMarginAll(4);
            btn.AddThemeStyleboxOverride("normal", style);

            var hoverStyle = new StyleBoxFlat();
            hoverStyle.BgColor = selected ? new Color(0.22f, 0.3f, 0.4f, 1f) : CueDefault.Lightened(0.15f);
            hoverStyle.SetCornerRadiusAll(3);
            hoverStyle.SetContentMarginAll(4);
            btn.AddThemeStyleboxOverride("hover", hoverStyle);

            btn.Pressed += () => SelectZone(idx);
            _zoneListContainer.AddChild(btn);
        }
    }

    private void SelectZone(int index)
    {
        var mgr = LaserSystemManager.Instance;
        if (mgr == null || index < 0 || index >= mgr.Zones.Count)
            return;

        _selectedZoneIndex = index;
        var zone = mgr.Zones[index];
        if (zone == null) return;

        _rightScroll.Visible = true;

        // Populate identity
        _nameEdit.Text = zone.ZoneName ?? "";
        _projectorOption.Selected = Mathf.Clamp(zone.ProjectorIndex, 0, 3);
        _enabledCheck.ButtonPressed = zone.Enabled;

        // Populate position & scale
        _offsetXSlider.SetValueNoSignal(zone.PositionOffset.X * 100.0);
        _offsetXValue.Text = $"{(int)(zone.PositionOffset.X * 100.0)}";
        _offsetYSlider.SetValueNoSignal(zone.PositionOffset.Y * 100.0);
        _offsetYValue.Text = $"{(int)(zone.PositionOffset.Y * 100.0)}";
        _scaleXSlider.SetValueNoSignal(zone.Scale.X * 100.0);
        _scaleXValue.Text = $"{(int)(zone.Scale.X * 100.0)}%";
        _scaleYSlider.SetValueNoSignal(zone.Scale.Y * 100.0);
        _scaleYValue.Text = $"{(int)(zone.Scale.Y * 100.0)}%";
        _rotationSlider.SetValueNoSignal(zone.Rotation);
        _rotationValue.Text = $"{(int)zone.Rotation}\u00b0";

        // Populate keystone
        if (zone.KeystoneCorners != null && zone.KeystoneCorners.Length == 4)
        {
            _keystoneCanvas.Corners = new Vector2[]
            {
                zone.KeystoneCorners[0],
                zone.KeystoneCorners[1],
                zone.KeystoneCorners[2],
                zone.KeystoneCorners[3],
            };
        }
        else
        {
            _keystoneCanvas.Corners = new Vector2[]
            {
                new Vector2(-1f, -1f),
                new Vector2( 1f, -1f),
                new Vector2( 1f,  1f),
                new Vector2(-1f,  1f),
            };
        }
        _keystoneCanvas.QueueRedraw();

        // Populate safety zone
        Rect2 sz = zone.SafetyZone;
        _safetyLeftSlider.SetValueNoSignal(sz.Position.X * 100.0);
        _safetyLeftValue.Text = $"{(int)(sz.Position.X * 100.0)}";
        _safetyRightSlider.SetValueNoSignal(sz.End.X * 100.0);
        _safetyRightValue.Text = $"{(int)(sz.End.X * 100.0)}";
        _safetyTopSlider.SetValueNoSignal(sz.Position.Y * 100.0);
        _safetyTopValue.Text = $"{(int)(sz.Position.Y * 100.0)}";
        _safetyBottomSlider.SetValueNoSignal(sz.End.Y * 100.0);
        _safetyBottomValue.Text = $"{(int)(sz.End.Y * 100.0)}";

        // Sync boundary button state with this zone's projector renderer
        var renderer = GetRendererForZone(zone.ProjectorIndex);
        _showingBoundary = renderer != null && renderer.ShowZoneBoundary;
        StyleButton(_showBoundaryBtn, _showingBoundary ? ActiveGreen : CueDefault);
        _showBoundaryBtn.Text = _showingBoundary ? "Hide Zone Boundary" : "Show Zone Boundary";
        _showBoundaryBtn.AddThemeColorOverride("font_color",
            _showingBoundary ? Colors.Black : TextColor);

        RefreshZoneList();
    }

    private void OnAddZone()
    {
        var mgr = LaserSystemManager.Instance;
        if (mgr == null) return;

        var zone = new ProjectionZone();
        zone.ZoneName = $"Zone {mgr.Zones.Count + 1}";
        zone.ProjectorIndex = 0;
        zone.PositionOffset = Vector2.Zero;
        zone.Scale = Vector2.One;
        zone.Rotation = 0f;
        zone.KeystoneCorners = new Vector2[]
        {
            new Vector2(-1f, -1f),
            new Vector2( 1f, -1f),
            new Vector2( 1f,  1f),
            new Vector2(-1f,  1f),
        };
        zone.SafetyZone = new Rect2(0f, 0f, 1f, 1f);
        zone.Enabled = true;

        mgr.Zones.Add(zone);
        RefreshZoneList();

        // Auto-select the new zone
        SelectZone(mgr.Zones.Count - 1);
    }

    private void OnRemoveZone()
    {
        var mgr = LaserSystemManager.Instance;
        if (mgr == null || _selectedZoneIndex < 0 || _selectedZoneIndex >= mgr.Zones.Count)
            return;

        mgr.Zones.RemoveAt(_selectedZoneIndex);

        if (_selectedZoneIndex >= mgr.Zones.Count)
            _selectedZoneIndex = mgr.Zones.Count - 1;

        if (_selectedZoneIndex >= 0)
            SelectZone(_selectedZoneIndex);
        else
        {
            _selectedZoneIndex = -1;
            _rightScroll.Visible = false;
        }

        RefreshZoneList();
    }

    // -----------------------------------------------
    //  Actions
    // -----------------------------------------------
    private void OnTestPattern()
    {
        var zone = GetSelectedZone();
        if (zone == null) return;

        if (ArtNetManager.Instance == null)
        {
            GD.PushWarning("[ZoneEditorPanel] ArtNetManager not available for test pattern.");
            return;
        }

        // Send a test grid pattern: white, centered, medium size, enabled
        byte[] frame = FB4ChannelMap.BuildDmxFrame(
            enabled: true,
            pattern: 1,         // Pattern 1 (typically a grid/test pattern on FB4)
            x: 0f,
            y: 0f,
            sizeX: 0.8f,
            sizeY: 0.8f,
            rotation: 0f,
            color: Colors.White,
            scanSpeed: 0.5f,
            effect: 0,
            effectSpeed: 0f,
            effectSize: 0f,
            zoom: 1f
        );

        int universe = zone.ProjectorIndex;
        ArtNetManager.Instance.SendDmx(universe, frame);
        GD.Print($"[ZoneEditorPanel] Test pattern sent to projector {universe + 1}.");
    }

    private void OnToggleBoundary()
    {
        _showingBoundary = !_showingBoundary;
        StyleButton(_showBoundaryBtn, _showingBoundary ? ActiveGreen : CueDefault);
        _showBoundaryBtn.Text = _showingBoundary ? "Hide Zone Boundary" : "Show Zone Boundary";
        _showBoundaryBtn.AddThemeColorOverride("font_color",
            _showingBoundary ? Colors.Black : TextColor);

        // Toggle boundary on this zone's projector renderer
        var zone = GetSelectedZone();
        if (zone == null) return;

        var renderer = GetRendererForZone(zone.ProjectorIndex);
        if (renderer != null)
        {
            renderer.SetShowZoneBoundary(_showingBoundary);
            renderer.SetZoneColor(GetProjectorColor(zone.ProjectorIndex));
        }
    }

    private void OnToggleAllBoundaries()
    {
        _showingAllBoundaries = !_showingAllBoundaries;
        StyleButton(_showAllBoundariesBtn, _showingAllBoundaries ? ActiveGreen : CueDefault);
        _showAllBoundariesBtn.Text = _showingAllBoundaries ? "Hide All Boundaries" : "Show All Boundaries";
        _showAllBoundariesBtn.AddThemeColorOverride("font_color",
            _showingAllBoundaries ? Colors.Black : TextColor);

        // Toggle boundary on all projector renderers
        var preview3D = GetTree().Root.FindChild("Preview3D", true, false);
        if (preview3D == null) return;

        for (int i = 0; i < 4; i++)
        {
            var renderer = preview3D.GetNodeOrNull<LazerSystem.Preview.LaserPreviewRenderer>($"Projector{i + 1}");
            if (renderer != null)
            {
                renderer.SetShowZoneBoundary(_showingAllBoundaries);
                renderer.SetZoneColor(GetProjectorColor(i));
            }
        }

        // Sync per-zone button to match
        var selectedZone = GetSelectedZone();
        if (selectedZone != null)
        {
            var selRenderer = GetRendererForZone(selectedZone.ProjectorIndex);
            _showingBoundary = selRenderer != null && selRenderer.ShowZoneBoundary;
            StyleButton(_showBoundaryBtn, _showingBoundary ? ActiveGreen : CueDefault);
            _showBoundaryBtn.Text = _showingBoundary ? "Hide Zone Boundary" : "Show Zone Boundary";
            _showBoundaryBtn.AddThemeColorOverride("font_color",
                _showingBoundary ? Colors.Black : TextColor);
        }
    }

    private LazerSystem.Preview.LaserPreviewRenderer GetRendererForZone(int projectorIndex)
    {
        var preview3D = GetTree().Root.FindChild("Preview3D", true, false);
        if (preview3D == null) return null;
        return preview3D.GetNodeOrNull<LazerSystem.Preview.LaserPreviewRenderer>($"Projector{projectorIndex + 1}");
    }

    private Color GetProjectorColor(int index)
    {
        return index switch
        {
            0 => new Color(0.9f, 0.3f, 0.3f, 0.5f),
            1 => new Color(0.3f, 0.9f, 0.4f, 0.5f),
            2 => new Color(0.3f, 0.5f, 0.9f, 0.5f),
            3 => new Color(0.9f, 0.85f, 0.3f, 0.5f),
            _ => new Color(0.5f, 0.5f, 0.5f, 0.5f),
        };
    }

    // -----------------------------------------------
    //  Helpers
    // -----------------------------------------------
    private ProjectionZone GetSelectedZone()
    {
        var mgr = LaserSystemManager.Instance;
        if (mgr == null || _selectedZoneIndex < 0 || _selectedZoneIndex >= mgr.Zones.Count)
            return null;
        return mgr.Zones[_selectedZoneIndex];
    }

    private string GetProjectorDisplayName(int index)
    {
        var mgr = LaserSystemManager.Instance;
        if (mgr != null && index >= 0 && index < mgr.Projectors.Count)
        {
            var proj = mgr.Projectors[index];
            if (proj != null && !string.IsNullOrEmpty(proj.ProjectorName))
                return proj.ProjectorName;
        }
        return $"Projector {index + 1}";
    }

    private void AddSectionHeader(VBoxContainer parent, string text)
    {
        var sep = new HSeparator();
        sep.AddThemeConstantOverride("separation", 4);
        parent.AddChild(sep);

        var label = new Label();
        label.Text = text;
        label.HorizontalAlignment = HorizontalAlignment.Left;
        label.AddThemeColorOverride("font_color", SectionHeaderColor);
        label.AddThemeFontSizeOverride("font_size", 12);
        parent.AddChild(label);
    }

    private void AddRowLabel(HBoxContainer row, string text)
    {
        var label = new Label();
        label.Text = text;
        label.CustomMinimumSize = new Vector2(70, 0);
        label.HorizontalAlignment = HorizontalAlignment.Right;
        label.AddThemeColorOverride("font_color", TextColor);
        label.AddThemeFontSizeOverride("font_size", 12);
        row.AddChild(label);
    }

    private (HSlider slider, Label valueLabel) CreateSliderRow(VBoxContainer parent, string labelText, double min, double max, double defaultVal)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        parent.AddChild(row);

        var label = new Label();
        label.Text = labelText;
        label.CustomMinimumSize = new Vector2(70, 0);
        label.HorizontalAlignment = HorizontalAlignment.Right;
        label.AddThemeColorOverride("font_color", TextColor);
        label.AddThemeFontSizeOverride("font_size", 12);
        row.AddChild(label);

        var slider = new HSlider();
        slider.MinValue = min;
        slider.MaxValue = max;
        slider.Value = defaultVal;
        slider.Step = 1;
        slider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        slider.CustomMinimumSize = new Vector2(0, 22);
        row.AddChild(slider);

        string initText;
        if (labelText == "Rotation")
            initText = $"{(int)defaultVal}\u00b0";
        else if (min < 0)
            initText = $"{(int)defaultVal}";
        else if (max <= 100 && min >= 0 && (labelText.Contains("Left") || labelText.Contains("Right") || labelText.Contains("Top") || labelText.Contains("Bottom")))
            initText = $"{(int)defaultVal}";
        else
            initText = $"{(int)defaultVal}%";

        var valueLabel = new Label();
        valueLabel.Text = initText;
        valueLabel.CustomMinimumSize = new Vector2(45, 0);
        valueLabel.HorizontalAlignment = HorizontalAlignment.Left;
        valueLabel.AddThemeColorOverride("font_color", TextColor);
        valueLabel.AddThemeFontSizeOverride("font_size", 12);
        row.AddChild(valueLabel);

        return (slider, valueLabel);
    }

    private Button CreateStyledButton(string text, Vector2 minSize, Color bgColor)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = minSize;
        StyleButton(btn, bgColor);
        btn.AddThemeColorOverride("font_color", TextColor);
        btn.AddThemeColorOverride("font_hover_color", Colors.White);
        btn.AddThemeColorOverride("font_pressed_color", Colors.White);
        return btn;
    }

    private void StyleButton(Button btn, Color bgColor)
    {
        var style = new StyleBoxFlat();
        style.BgColor = bgColor;
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(4);
        btn.AddThemeStyleboxOverride("normal", style);

        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = bgColor.Lightened(0.15f);
        hoverStyle.SetCornerRadiusAll(4);
        hoverStyle.SetContentMarginAll(4);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);

        var pressedStyle = new StyleBoxFlat();
        pressedStyle.BgColor = bgColor.Lightened(0.3f);
        pressedStyle.SetCornerRadiusAll(4);
        pressedStyle.SetContentMarginAll(4);
        btn.AddThemeStyleboxOverride("pressed", pressedStyle);
    }

    private void ApplyPanelStyle(PanelContainer panel, Color bgColor)
    {
        var style = new StyleBoxFlat();
        style.BgColor = bgColor;
        style.SetContentMarginAll(6);
        panel.AddThemeStyleboxOverride("panel", style);
    }
}
