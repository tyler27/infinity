using Godot;
using LazerSystem.Core;
using LazerSystem.Patterns;

public partial class MainUI : CanvasLayer
{
    // Theme colors
    private static readonly Color PanelBg = new Color(0.12f, 0.12f, 0.15f, 0.92f);
    private static readonly Color SidebarBg = new Color(0.09f, 0.09f, 0.11f, 0.95f);
    private static readonly Color ActiveGreen = new Color(0.2f, 0.8f, 0.4f, 1f);
    private static readonly Color DangerRed = new Color(0.9f, 0.15f, 0.15f, 1f);
    private static readonly Color WarningYellow = new Color(0.9f, 0.7f, 0.1f, 1f);
    private static readonly Color TextColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    private static readonly Color DimText = new Color(0.5f, 0.5f, 0.55f, 1f);
    private static readonly Color CueDefault = new Color(0.18f, 0.18f, 0.22f, 1f);
    private static readonly Color CueActive = new Color(0.15f, 0.6f, 0.3f, 1f);
    private static readonly Color SidebarItem = new Color(0.14f, 0.14f, 0.17f, 1f);
    private static readonly Color SidebarItemSelected = new Color(0.18f, 0.25f, 0.35f, 1f);

    private static readonly Color[] ProjectorColors = {
        new Color(0.9f, 0.2f, 0.2f, 1f),
        new Color(0.2f, 0.9f, 0.3f, 1f),
        new Color(0.2f, 0.4f, 0.9f, 1f),
        new Color(0.9f, 0.85f, 0.2f, 1f)
    };

    // Grid
    private Button[,] _gridButtons = new Button[6, 10];

    // Sidebar page list
    private VBoxContainer _pageListContainer;
    private Button[] _pageListButtons;
    private LineEdit _searchBox;

    // Favorite bar
    private HBoxContainer _favBar;

    // Toolbar
    private Button _laserOutputBtn;
    private Button _blackoutBtn;
    private Button[] _projectorBtns = new Button[4];
    private Label _fpsLabel;

    // Zone routing buttons
    private Button[] _zoneBtns = new Button[4];
    private Button _zoneAllBtn;

    // Settings panels
    private FloatingPanel _projectorSettingsPanel;
    private FloatingPanel _zoneEditorPanel;

    // Live control
    private HSlider _masterIntensitySlider;
    private HSlider _masterSizeSlider;
    private HSlider _posXSlider;
    private HSlider _posYSlider;
    private HSlider _rotationSlider;
    private HSlider _speedSlider;
    private ColorPickerButton _colorPicker;
    private Label _masterIntensityValue;
    private Label _masterSizeValue;
    private Label _posXValue;
    private Label _posYValue;
    private Label _rotationValue;
    private Label _speedValue;

    // Status
    private Label _statusLabel;

    // Keyboard mapping
    private static readonly Key[][] KeyMap = {
        new Key[] { Key.Key1, Key.Key2, Key.Key3, Key.Key4, Key.Key5, Key.Key6, Key.Key7, Key.Key8, Key.Key9, Key.Key0 },
        new Key[] { Key.Q, Key.W, Key.E, Key.R, Key.T, Key.Y, Key.U, Key.I, Key.O, Key.P },
        new Key[] { Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J, Key.K, Key.L, Key.Semicolon },
        new Key[] { Key.Z, Key.X, Key.C, Key.V, Key.B, Key.N, Key.M, Key.Comma, Key.Period, Key.Slash }
    };

    private static readonly string[][] KeyLabels = {
        new string[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" },
        new string[] { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" },
        new string[] { "A", "S", "D", "F", "G", "H", "J", "K", "L", ";" },
        new string[] { "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/" }
    };

    // Track previous page to avoid redundant refreshes
    private int _lastRenderedPage = -1;

    public override void _Ready()
    {
        var root = new VBoxContainer();
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 0);
        AddChild(root);

        BuildSafetyToolbar(root);
        BuildMainContent(root);
        BuildStatusBar(root);

        // Disable the main scene camera so nothing renders behind the UI
        CallDeferred(nameof(DisableMainCamera));
    }

    // ═══════════════════════════════════════════════
    //  SAFETY TOOLBAR (top)
    // ═══════════════════════════════════════════════
    private void BuildSafetyToolbar(VBoxContainer root)
    {
        var toolbarPanel = new PanelContainer();
        toolbarPanel.CustomMinimumSize = new Vector2(0, 40);
        toolbarPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        ApplyPanelStyle(toolbarPanel, PanelBg);
        root.AddChild(toolbarPanel);

        var toolbar = new HBoxContainer();
        toolbar.AddThemeConstantOverride("separation", 6);
        toolbarPanel.AddChild(toolbar);

        // Laser Output
        _laserOutputBtn = CreateStyledButton("LASER OUTPUT", new Vector2(140, 34), DangerRed);
        _laserOutputBtn.Pressed += () => { LiveEngine.Instance.ToggleLaserEnable(); };
        toolbar.AddChild(_laserOutputBtn);

        // Blackout
        _blackoutBtn = CreateStyledButton("BLACKOUT", new Vector2(100, 34), DangerRed);
        _blackoutBtn.Pressed += () => { LiveEngine.Instance.ToggleBlackout(); };
        toolbar.AddChild(_blackoutBtn);

        toolbar.AddChild(CreateToolbarSeparator());

        // P1-P4
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            var pBtn = CreateStyledButton($"P{i + 1}", new Vector2(40, 34), ProjectorColors[i]);
            pBtn.Pressed += () =>
            {
                LiveEngine.Instance.ProjectorEnabled[idx] = !LiveEngine.Instance.ProjectorEnabled[idx];
            };
            _projectorBtns[i] = pBtn;
            toolbar.AddChild(pBtn);
        }

        toolbar.AddChild(CreateToolbarSeparator());

        // Zone routing buttons
        var zoneLabel = new Label();
        zoneLabel.Text = "ZONE:";
        zoneLabel.AddThemeColorOverride("font_color", TextColor);
        zoneLabel.AddThemeFontSizeOverride("font_size", 11);
        toolbar.AddChild(zoneLabel);

        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            var zBtn = CreateStyledButton($"Z{i + 1}", new Vector2(36, 34), ActiveGreen);
            zBtn.Pressed += () => ToggleZone(idx);
            _zoneBtns[i] = zBtn;
            toolbar.AddChild(zBtn);
        }

        _zoneAllBtn = CreateStyledButton("ALL", new Vector2(44, 34), ActiveGreen);
        _zoneAllBtn.Pressed += () => { LiveEngine.Instance.ActiveZones = new int[] { 0, 1, 2, 3 }; };
        toolbar.AddChild(_zoneAllBtn);

        toolbar.AddChild(CreateToolbarSeparator());

        // Settings panel buttons
        var projSettingsBtn = CreateStyledButton("Projectors", new Vector2(85, 34), new Color(0.2f, 0.3f, 0.5f, 1f));
        projSettingsBtn.Pressed += ToggleProjectorSettings;
        toolbar.AddChild(projSettingsBtn);

        var zoneEditorBtn = CreateStyledButton("Zones", new Vector2(55, 34), new Color(0.2f, 0.3f, 0.5f, 1f));
        zoneEditorBtn.Pressed += ToggleZoneEditor;
        toolbar.AddChild(zoneEditorBtn);

        var spacer = new Control();
        spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        toolbar.AddChild(spacer);

        // Clear All
        var clearBtn = CreateStyledButton("CLEAR ALL", new Vector2(100, 34), new Color(0.5f, 0.15f, 0.15f, 1f));
        clearBtn.Pressed += () => LiveEngine.Instance.ClearAll();
        toolbar.AddChild(clearBtn);

        var spacer2 = new Control();
        spacer2.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        toolbar.AddChild(spacer2);

        _fpsLabel = new Label();
        _fpsLabel.Text = "FPS: 60";
        _fpsLabel.CustomMinimumSize = new Vector2(80, 0);
        _fpsLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _fpsLabel.AddThemeColorOverride("font_color", TextColor);
        toolbar.AddChild(_fpsLabel);
    }

    // ═══════════════════════════════════════════════
    //  MAIN CONTENT: Sidebar | Grid ||| Preview+Controls
    // ═══════════════════════════════════════════════
    private HSplitContainer _mainSplit;

    private void BuildMainContent(VBoxContainer root)
    {
        var content = new HBoxContainer();
        content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        content.AddThemeConstantOverride("separation", 0);
        root.AddChild(content);

        BuildPageSidebar(content);

        // Resizable split between grid and preview+controls
        _mainSplit = new HSplitContainer();
        _mainSplit.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _mainSplit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _mainSplit.SplitOffset = -320;
        content.AddChild(_mainSplit);

        BuildCenterArea(_mainSplit);
        BuildRightPanel(_mainSplit);
    }

    // ═══════════════════════════════════════════════
    //  PAGE SIDEBAR (left, file-explorer style)
    // ═══════════════════════════════════════════════
    private void BuildPageSidebar(HBoxContainer parent)
    {
        var sidebarPanel = new PanelContainer();
        sidebarPanel.CustomMinimumSize = new Vector2(180, 0);
        ApplyPanelStyle(sidebarPanel, SidebarBg);
        parent.AddChild(sidebarPanel);

        var sidebarVBox = new VBoxContainer();
        sidebarVBox.AddThemeConstantOverride("separation", 2);
        sidebarPanel.AddChild(sidebarVBox);

        // Header
        var headerLabel = new Label();
        headerLabel.Text = "PAGES";
        headerLabel.HorizontalAlignment = HorizontalAlignment.Center;
        headerLabel.AddThemeColorOverride("font_color", ActiveGreen);
        headerLabel.AddThemeFontSizeOverride("font_size", 13);
        sidebarVBox.AddChild(headerLabel);

        // Search / filter
        _searchBox = new LineEdit();
        _searchBox.PlaceholderText = "Search pages...";
        _searchBox.CustomMinimumSize = new Vector2(0, 28);
        _searchBox.AddThemeFontSizeOverride("font_size", 11);
        _searchBox.TextChanged += (_) => RefreshPageList();
        sidebarVBox.AddChild(_searchBox);

        var sep = new HSeparator();
        sep.AddThemeConstantOverride("separation", 4);
        sidebarVBox.AddChild(sep);

        // Scrollable page list
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        sidebarVBox.AddChild(scroll);

        _pageListContainer = new VBoxContainer();
        _pageListContainer.AddThemeConstantOverride("separation", 1);
        _pageListContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(_pageListContainer);

        _pageListButtons = new Button[LiveEngine.PageCount];
        for (int i = 0; i < LiveEngine.PageCount; i++)
        {
            int pageIdx = i;
            var btn = new Button();
            btn.CustomMinimumSize = new Vector2(0, 26);
            btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            btn.Alignment = HorizontalAlignment.Left;
            btn.ClipText = true;
            btn.AddThemeFontSizeOverride("font_size", 11);
            btn.AddThemeColorOverride("font_color", TextColor);

            // Right-click to toggle favorite
            btn.GuiInput += (inputEvent) =>
            {
                if (inputEvent is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right)
                {
                    ToggleFavorite(pageIdx);
                    btn.AcceptEvent();
                }
            };

            btn.Pressed += () =>
            {
                LiveEngine.Instance.CurrentPage = pageIdx;
                RefreshPageList();
                RefreshFavBar();
                RefreshGrid();
            };

            _pageListButtons[i] = btn;
            _pageListContainer.AddChild(btn);
        }

        RefreshPageList();
    }

    private void RefreshPageList()
    {
        string filter = _searchBox?.Text?.Trim().ToLower() ?? "";
        int currentPage = LiveEngine.Instance.CurrentPage;

        for (int i = 0; i < LiveEngine.PageCount; i++)
        {
            var btn = _pageListButtons[i];
            string displayName = LiveEngine.Instance.GetPageDisplayName(i);
            bool hasCues = LiveEngine.Instance.PageHasCues(i);
            bool isFav = LiveEngine.Instance.FavoritePages.Contains(i);

            // Filter: show if matches search, has cues, is favorite, or is current, or search is empty and index < 32
            bool visible;
            if (filter.Length > 0)
            {
                visible = displayName.ToLower().Contains(filter);
            }
            else
            {
                // Show pages that have cues, are favorites, or are in the first 32 slots
                visible = hasCues || isFav || i < 32;
            }
            btn.Visible = visible;

            // Build label
            string prefix = isFav ? "\u2605 " : ""; // star for favorites
            string suffix = hasCues ? "" : " (empty)";
            btn.Text = $"{prefix}{displayName}{suffix}";

            // Style
            bool selected = (i == currentPage);
            var style = new StyleBoxFlat();
            style.BgColor = selected ? SidebarItemSelected : SidebarItem;
            style.SetCornerRadiusAll(3);
            style.SetContentMarginAll(4);
            btn.AddThemeStyleboxOverride("normal", style);

            var hoverStyle = new StyleBoxFlat();
            hoverStyle.BgColor = selected ? SidebarItemSelected.Lightened(0.1f) : SidebarItem.Lightened(0.15f);
            hoverStyle.SetCornerRadiusAll(3);
            hoverStyle.SetContentMarginAll(4);
            btn.AddThemeStyleboxOverride("hover", hoverStyle);

            btn.AddThemeColorOverride("font_color", selected ? ActiveGreen : (hasCues ? TextColor : DimText));
        }
    }

    private void ToggleFavorite(int pageIdx)
    {
        var favs = LiveEngine.Instance.FavoritePages;
        if (favs.Contains(pageIdx))
            favs.Remove(pageIdx);
        else
            favs.Add(pageIdx);
        RefreshPageList();
        RefreshFavBar();
    }

    // ═══════════════════════════════════════════════
    //  CENTER: Fav bar + Cue Grid
    // ═══════════════════════════════════════════════
    private void BuildCenterArea(HSplitContainer parent)
    {
        var centerPanel = new PanelContainer();
        centerPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        centerPanel.SizeFlagsStretchRatio = 7f;
        ApplyPanelStyle(centerPanel, PanelBg);
        parent.AddChild(centerPanel);

        var centerVBox = new VBoxContainer();
        centerVBox.AddThemeConstantOverride("separation", 4);
        centerPanel.AddChild(centerVBox);

        // Favorites quick-access bar
        _favBar = new HBoxContainer();
        _favBar.AddThemeConstantOverride("separation", 2);
        centerVBox.AddChild(_favBar);
        RefreshFavBar();

        // Cue grid
        BuildCueGrid(centerVBox);
    }

    private void RefreshFavBar()
    {
        // Clear existing
        foreach (var child in _favBar.GetChildren())
            child.QueueFree();

        var favs = LiveEngine.Instance.FavoritePages;
        int currentPage = LiveEngine.Instance.CurrentPage;

        foreach (int pageIdx in favs)
        {
            int idx = pageIdx;
            string name = LiveEngine.Instance.GetPageDisplayName(idx);
            bool selected = (idx == currentPage);
            Color bg = selected ? ActiveGreen : CueDefault;

            var btn = CreateStyledButton(name, new Vector2(0, 26), bg);
            btn.AddThemeFontSizeOverride("font_size", 11);
            btn.CustomMinimumSize = new Vector2(90, 26);
            btn.ClipText = true;
            btn.Pressed += () =>
            {
                LiveEngine.Instance.CurrentPage = idx;
                RefreshPageList();
                RefreshFavBar();
                RefreshGrid();
            };
            _favBar.AddChild(btn);
        }

        // Add a label hint if empty
        if (favs.Count == 0)
        {
            var hint = new Label();
            hint.Text = "Right-click pages to add favorites";
            hint.AddThemeColorOverride("font_color", DimText);
            hint.AddThemeFontSizeOverride("font_size", 11);
            _favBar.AddChild(hint);
        }
    }

    private void BuildCueGrid(VBoxContainer parent)
    {
        var scrollContainer = new ScrollContainer();
        scrollContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scrollContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        parent.AddChild(scrollContainer);

        var grid = new GridContainer();
        grid.Columns = 10;
        grid.AddThemeConstantOverride("h_separation", 3);
        grid.AddThemeConstantOverride("v_separation", 3);
        grid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scrollContainer.AddChild(grid);

        for (int r = 0; r < 6; r++)
        {
            for (int c = 0; c < 10; c++)
            {
                int row = r;
                int col = c;
                var btn = CreateStyledButton("---", new Vector2(90, 56), CueDefault);
                btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                btn.ClipText = true;
                btn.AddThemeFontSizeOverride("font_size", 11);
                btn.Pressed += () => LiveEngine.Instance.TriggerCue(row, col);
                _gridButtons[r, c] = btn;
                grid.AddChild(btn);
            }
        }
    }

    // ═══════════════════════════════════════════════
    //  LIVE CONTROL PANEL (right) with 3D preview
    // ═══════════════════════════════════════════════
    //  RIGHT PANEL: Preview (resizable) + Live Controls
    // ═══════════════════════════════════════════════
    private SubViewportContainer _previewContainer;
    private SubViewport _previewViewport;
    private LaserOutputView _outputView;
    private FloatingPreviewPanel _floatingPanel;
    private bool _isPopped;
    private VBoxContainer _inlinePreviewParent;
    private bool _show3D; // false = 2D output view (default), true = 3D perspective
    private Button _viewToggleBtn;
    private LazerSystem.Preview.OrbitCamera _orbitCamera;
    private HBoxContainer _presetButtonsContainer;
    private VBoxContainer _previewSettingsPanel;
    private bool _showPreviewSettings;

    public LaserOutputView OutputView => _outputView;

    private void BuildRightPanel(HSplitContainer parent)
    {
        var rightPanel = new PanelContainer();
        rightPanel.CustomMinimumSize = new Vector2(300, 0);
        rightPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        ApplyPanelStyle(rightPanel, PanelBg);
        parent.AddChild(rightPanel);

        // VSplit: preview on top, controls on bottom, draggable divider
        var vSplit = new VSplitContainer();
        vSplit.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vSplit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rightPanel.AddChild(vSplit);

        // ── TOP: PREVIEW (2D + 3D togglable) ──
        var previewSection = new VBoxContainer();
        previewSection.CustomMinimumSize = new Vector2(0, 120);
        previewSection.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        previewSection.AddThemeConstantOverride("separation", 2);
        vSplit.AddChild(previewSection);

        // Preview header with view toggle and popout buttons
        var previewHeader = new HBoxContainer();
        previewHeader.AddThemeConstantOverride("separation", 4);
        previewSection.AddChild(previewHeader);

        var previewLabel = new Label();
        previewLabel.Text = "PREVIEW";
        previewLabel.AddThemeColorOverride("font_color", ActiveGreen);
        previewLabel.AddThemeFontSizeOverride("font_size", 12);
        previewHeader.AddChild(previewLabel);

        _viewToggleBtn = CreateStyledButton("2D", new Vector2(36, 22), ActiveGreen);
        _viewToggleBtn.AddThemeFontSizeOverride("font_size", 10);
        _viewToggleBtn.Pressed += TogglePreviewMode;
        previewHeader.AddChild(_viewToggleBtn);

        // 3D camera preset buttons (only visible in 3D mode)
        _presetButtonsContainer = new HBoxContainer();
        _presetButtonsContainer.AddThemeConstantOverride("separation", 2);
        _presetButtonsContainer.Visible = false;
        previewHeader.AddChild(_presetButtonsContainer);

        var presetColor = new Color(0.22f, 0.22f, 0.28f, 1f);
        var frontBtn = CreateStyledButton("Front", new Vector2(45, 22), presetColor);
        frontBtn.AddThemeFontSizeOverride("font_size", 9);
        frontBtn.Pressed += () => _orbitCamera?.SetFront();
        _presetButtonsContainer.AddChild(frontBtn);

        var topBtn = CreateStyledButton("Top", new Vector2(35, 22), presetColor);
        topBtn.AddThemeFontSizeOverride("font_size", 9);
        topBtn.Pressed += () => _orbitCamera?.SetTop();
        _presetButtonsContainer.AddChild(topBtn);

        var sideBtn = CreateStyledButton("Side", new Vector2(38, 22), presetColor);
        sideBtn.AddThemeFontSizeOverride("font_size", 9);
        sideBtn.Pressed += () => _orbitCamera?.SetSide();
        _presetButtonsContainer.AddChild(sideBtn);

        var projBtn = CreateStyledButton("Proj", new Vector2(38, 22), presetColor);
        projBtn.AddThemeFontSizeOverride("font_size", 9);
        projBtn.Pressed += () => _orbitCamera?.SetProjectorView();
        _presetButtonsContainer.AddChild(projBtn);

        var resetBtn2 = CreateStyledButton("Reset", new Vector2(42, 22), presetColor);
        resetBtn2.AddThemeFontSizeOverride("font_size", 9);
        resetBtn2.Pressed += () => _orbitCamera?.Reset();
        _presetButtonsContainer.AddChild(resetBtn2);

        // Spacer to push Pop Out to the right
        var previewSpacer = new Control();
        previewSpacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        previewHeader.AddChild(previewSpacer);

        var settingsBtn = CreateStyledButton("Settings", new Vector2(55, 22), new Color(0.25f, 0.25f, 0.3f, 1f));
        settingsBtn.AddThemeFontSizeOverride("font_size", 10);
        settingsBtn.Pressed += TogglePreviewSettings;
        previewHeader.AddChild(settingsBtn);

        var popoutBtn = CreateStyledButton("Pop Out", new Vector2(60, 22), new Color(0.25f, 0.25f, 0.3f, 1f));
        popoutBtn.AddThemeFontSizeOverride("font_size", 10);
        popoutBtn.Pressed += TogglePopout;
        previewHeader.AddChild(popoutBtn);

        // Preview settings panel (hidden by default)
        _previewSettingsPanel = new VBoxContainer();
        _previewSettingsPanel.Visible = false;
        _previewSettingsPanel.AddThemeConstantOverride("separation", 4);
        previewSection.AddChild(_previewSettingsPanel);
        BuildPreviewSettings(_previewSettingsPanel);

        _inlinePreviewParent = previewSection;

        // 2D Output View (default)
        _outputView = new LaserOutputView();
        _outputView.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _outputView.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        previewSection.AddChild(_outputView);

        // 3D SubViewport (hidden by default)
        _previewContainer = new SubViewportContainer();
        _previewContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _previewContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _previewContainer.Stretch = true;
        _previewContainer.Visible = false;
        previewSection.AddChild(_previewContainer);

        _previewViewport = new SubViewport();
        _previewViewport.Size = new Vector2I(640, 360);
        _previewViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        _previewViewport.TransparentBg = false;
        _previewViewport.World3D = GetViewport().World3D;
        _previewContainer.AddChild(_previewViewport);

        _orbitCamera = new LazerSystem.Preview.OrbitCamera();
        _previewViewport.AddChild(_orbitCamera);

        // ── BOTTOM: LIVE CONTROL SLIDERS ──
        var controlSection = new PanelContainer();
        controlSection.CustomMinimumSize = new Vector2(0, 200);
        controlSection.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        ApplyPanelStyle(controlSection, new Color(0.1f, 0.1f, 0.13f, 0.95f));
        vSplit.AddChild(controlSection);

        var scrollContainer = new ScrollContainer();
        scrollContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scrollContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        controlSection.AddChild(scrollContainer);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scrollContainer.AddChild(vbox);

        var header = new Label();
        header.Text = "LIVE CONTROL";
        header.HorizontalAlignment = HorizontalAlignment.Center;
        header.AddThemeColorOverride("font_color", ActiveGreen);
        header.AddThemeFontSizeOverride("font_size", 12);
        vbox.AddChild(header);

        // Sliders
        (_masterIntensitySlider, _masterIntensityValue) = CreateSliderRow(vbox, "Intensity", 0, 100, 100);
        _masterIntensitySlider.ValueChanged += (val) =>
        {
            LiveEngine.Instance.MasterIntensity = (float)(val / 100.0);
            _masterIntensityValue.Text = $"{(int)val}%";
        };

        (_masterSizeSlider, _masterSizeValue) = CreateSliderRow(vbox, "Size", 0, 200, 100);
        _masterSizeSlider.ValueChanged += (val) =>
        {
            LiveEngine.Instance.MasterSize = (float)(val / 100.0);
            _masterSizeValue.Text = $"{(int)val}%";
        };

        (_posXSlider, _posXValue) = CreateSliderRow(vbox, "Pos X", -100, 100, 0);
        _posXSlider.ValueChanged += (val) =>
        {
            var o = LiveEngine.Instance.LiveOverrides;
            o.position = new Vector2((float)(val / 100.0), o.position.Y);
            LiveEngine.Instance.LiveOverrides = o;
            _posXValue.Text = $"{(int)val}";
        };

        (_posYSlider, _posYValue) = CreateSliderRow(vbox, "Pos Y", -100, 100, 0);
        _posYSlider.ValueChanged += (val) =>
        {
            var o = LiveEngine.Instance.LiveOverrides;
            o.position = new Vector2(o.position.X, (float)(val / 100.0));
            LiveEngine.Instance.LiveOverrides = o;
            _posYValue.Text = $"{(int)val}";
        };

        (_rotationSlider, _rotationValue) = CreateSliderRow(vbox, "Rotation", 0, 360, 0);
        _rotationSlider.ValueChanged += (val) =>
        {
            var o = LiveEngine.Instance.LiveOverrides;
            o.rotation = (float)val;
            LiveEngine.Instance.LiveOverrides = o;
            _rotationValue.Text = $"{(int)val}\u00b0";
        };

        (_speedSlider, _speedValue) = CreateSliderRow(vbox, "Speed", 0, 300, 100);
        _speedSlider.ValueChanged += (val) =>
        {
            var o = LiveEngine.Instance.LiveOverrides;
            o.speed = (float)(val / 100.0);
            LiveEngine.Instance.LiveOverrides = o;
            _speedValue.Text = $"{(int)val}%";
        };

        // Color
        var colorRow = new HBoxContainer();
        colorRow.AddThemeConstantOverride("separation", 6);
        vbox.AddChild(colorRow);

        var colorLabel = new Label();
        colorLabel.Text = "Color";
        colorLabel.CustomMinimumSize = new Vector2(70, 0);
        colorLabel.HorizontalAlignment = HorizontalAlignment.Right;
        colorLabel.AddThemeColorOverride("font_color", TextColor);
        colorRow.AddChild(colorLabel);

        _colorPicker = new ColorPickerButton();
        _colorPicker.Color = Colors.White;
        _colorPicker.CustomMinimumSize = new Vector2(0, 28);
        _colorPicker.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _colorPicker.EditAlpha = false;
        _colorPicker.ColorChanged += (color) =>
        {
            var o = LiveEngine.Instance.LiveOverrides;
            o.color = color;
            LiveEngine.Instance.LiveOverrides = o;
        };
        colorRow.AddChild(_colorPicker);

        // Reset
        var resetBtn = CreateStyledButton("Reset All", new Vector2(0, 30), new Color(0.35f, 0.15f, 0.15f, 1f));
        resetBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        resetBtn.Pressed += ResetAllSliders;
        vbox.AddChild(resetBtn);
    }

    // ═══════════════════════════════════════════════
    //  PREVIEW POP-OUT (in-app floating panel)
    // ═══════════════════════════════════════════════
    private void TogglePreviewMode()
    {
        _show3D = !_show3D;
        _outputView.Visible = !_show3D;
        _previewContainer.Visible = _show3D;
        _presetButtonsContainer.Visible = _show3D;
        _viewToggleBtn.Text = _show3D ? "3D" : "2D";
        StyleButton(_viewToggleBtn, _show3D ? new Color(0.4f, 0.3f, 0.7f, 1f) : ActiveGreen);
    }

    private void TogglePreviewSettings()
    {
        _showPreviewSettings = !_showPreviewSettings;
        _previewSettingsPanel.Visible = _showPreviewSettings;
    }

    private void BuildPreviewSettings(VBoxContainer parent)
    {
        var bgPanel = new PanelContainer();
        var bgStyle = new StyleBoxFlat();
        bgStyle.BgColor = new Color(0.1f, 0.1f, 0.13f, 0.9f);
        bgStyle.SetCornerRadiusAll(4);
        bgStyle.SetContentMarginAll(6);
        bgPanel.AddThemeStyleboxOverride("panel", bgStyle);
        parent.AddChild(bgPanel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 5);
        bgPanel.AddChild(vbox);

        // ── Source Beams toggle ──
        var beamToggle = new CheckButton();
        beamToggle.Text = "Show Source Beams";
        beamToggle.ButtonPressed = false;
        beamToggle.AddThemeColorOverride("font_color", TextColor);
        beamToggle.AddThemeFontSizeOverride("font_size", 11);
        beamToggle.Toggled += (pressed) =>
        {
            var preview3D = GetTree().Root.FindChild("Preview3D", true, false);
            if (preview3D == null) return;
            for (int i = 0; i < 4; i++)
            {
                var r = preview3D.GetNodeOrNull<LazerSystem.Preview.LaserPreviewRenderer>($"Projector{i + 1}");
                r?.SetShowSourceBeams(pressed);
            }
        };
        vbox.AddChild(beamToggle);

        // ── Haze Density slider ──
        var hazeRow = new HBoxContainer();
        hazeRow.AddThemeConstantOverride("separation", 6);
        vbox.AddChild(hazeRow);

        var hazeLabel = new Label();
        hazeLabel.Text = "Haze Density";
        hazeLabel.CustomMinimumSize = new Vector2(80, 0);
        hazeLabel.HorizontalAlignment = HorizontalAlignment.Right;
        hazeLabel.AddThemeColorOverride("font_color", TextColor);
        hazeLabel.AddThemeFontSizeOverride("font_size", 11);
        hazeRow.AddChild(hazeLabel);

        var hazeSlider = new HSlider();
        hazeSlider.MinValue = 0;
        hazeSlider.MaxValue = 50;
        hazeSlider.Value = 10;
        hazeSlider.Step = 1;
        hazeSlider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hazeSlider.CustomMinimumSize = new Vector2(0, 20);
        hazeRow.AddChild(hazeSlider);

        var hazeValueLabel = new Label();
        hazeValueLabel.Text = "1.0%";
        hazeValueLabel.CustomMinimumSize = new Vector2(45, 0);
        hazeValueLabel.HorizontalAlignment = HorizontalAlignment.Left;
        hazeValueLabel.AddThemeColorOverride("font_color", TextColor);
        hazeValueLabel.AddThemeFontSizeOverride("font_size", 11);
        hazeRow.AddChild(hazeValueLabel);

        hazeSlider.ValueChanged += (val) =>
        {
            float pct = (float)(val / 10.0);
            hazeValueLabel.Text = $"{pct:F1}%";
            float density = (float)(val / 1000.0);
            var preview3D = GetTree().Root.FindChild("Preview3D", true, false);
            if (preview3D == null) return;
            for (int i = 0; i < 4; i++)
            {
                var r = preview3D.GetNodeOrNull<LazerSystem.Preview.LaserPreviewRenderer>($"Projector{i + 1}");
                if (r != null) r.HazeDensity = density;
            }
        };

        // ── Venue Bounds toggle ──
        _boundsToggle = new CheckButton();
        _boundsToggle.Text = "Show Venue Bounds (Tab)";
        _boundsToggle.ButtonPressed = false;
        _boundsToggle.AddThemeColorOverride("font_color", TextColor);
        _boundsToggle.AddThemeFontSizeOverride("font_size", 11);
        _boundsToggle.Toggled += (pressed) =>
        {
            _showBounds = pressed;
            var venueGrid = GetTree().Root.FindChild("VenueGrid", true, false) as LazerSystem.Preview.VenueGrid;
            venueGrid?.SetShowBounds(pressed);
        };
        vbox.AddChild(_boundsToggle);

        // ── Projector Positions & Rotation ──
        var posHeader = new Label();
        posHeader.Text = "Projector Transform";
        posHeader.AddThemeColorOverride("font_color", ActiveGreen);
        posHeader.AddThemeFontSizeOverride("font_size", 11);
        vbox.AddChild(posHeader);

        string[] projNames = { "P1", "P2", "P3", "P4" };
        Color[] projColors = {
            new Color(0.9f, 0.3f, 0.3f, 1f),
            new Color(0.3f, 0.9f, 0.4f, 1f),
            new Color(0.3f, 0.5f, 0.9f, 1f),
            new Color(0.9f, 0.85f, 0.3f, 1f)
        };
        float[] defaultX = { -3f, -1f, 1f, 3f };

        for (int i = 0; i < 4; i++)
        {
            int idx = i;

            // Projector label
            var pLabel = new Label();
            pLabel.Text = projNames[i];
            pLabel.AddThemeColorOverride("font_color", projColors[i]);
            pLabel.AddThemeFontSizeOverride("font_size", 11);
            vbox.AddChild(pLabel);

            // Position row: X Y Z
            var posRow = new HBoxContainer();
            posRow.AddThemeConstantOverride("separation", 3);
            vbox.AddChild(posRow);

            AddAxisSlider(posRow, "X", -20, 20, defaultX[i], 0.5, (val) => UpdateProjectorTransform(idx, posX: (float)val));
            AddAxisSlider(posRow, "Y", 0, 15, 4, 0.5, (val) => UpdateProjectorTransform(idx, posY: (float)val));
            AddAxisSlider(posRow, "Z", -20, 20, 0, 0.5, (val) => UpdateProjectorTransform(idx, posZ: (float)val));

            // Rotation row: RX RY RZ (degrees)
            var rotRow = new HBoxContainer();
            rotRow.AddThemeConstantOverride("separation", 3);
            vbox.AddChild(rotRow);

            AddAxisSlider(rotRow, "RX", -180, 180, 0, 1, (val) => UpdateProjectorTransform(idx, rotX: (float)val));
            AddAxisSlider(rotRow, "RY", -180, 180, 0, 1, (val) => UpdateProjectorTransform(idx, rotY: (float)val));
            AddAxisSlider(rotRow, "RZ", -180, 180, 0, 1, (val) => UpdateProjectorTransform(idx, rotZ: (float)val));
        }
    }

    private void AddAxisSlider(HBoxContainer parent, string label, double min, double max, double defaultVal, double step, HSlider.ValueChangedEventHandler handler)
    {
        bool isRot = label.StartsWith("R");
        Color labelColor = label.Contains("X") ? new Color(0.8f, 0.3f, 0.3f, 0.8f) :
                           label.Contains("Y") ? new Color(0.3f, 0.8f, 0.3f, 0.8f) :
                                                 new Color(0.3f, 0.3f, 0.8f, 0.8f);

        var lbl = new Label();
        lbl.Text = label;
        lbl.CustomMinimumSize = new Vector2(isRot ? 22 : 14, 0);
        lbl.AddThemeColorOverride("font_color", labelColor);
        lbl.AddThemeFontSizeOverride("font_size", 9);
        parent.AddChild(lbl);

        var slider = new HSlider();
        slider.MinValue = min;
        slider.MaxValue = max;
        slider.Value = defaultVal;
        slider.Step = step;
        slider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        slider.CustomMinimumSize = new Vector2(0, 16);
        slider.ValueChanged += handler;
        parent.AddChild(slider);
    }

    // Store intended rotation per projector (degrees) to avoid Euler decomposition issues
    private Vector3[] _projRotDeg = { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

    private void UpdateProjectorTransform(int projectorIndex,
        float? posX = null, float? posY = null, float? posZ = null,
        float? rotX = null, float? rotY = null, float? rotZ = null)
    {
        var preview3D = GetTree().Root.FindChild("Preview3D", true, false);
        if (preview3D == null) return;

        var projNode = preview3D.GetNodeOrNull<Node3D>($"Projector{projectorIndex + 1}");
        if (projNode == null) return;

        // Position
        Vector3 pos = projNode.Position;
        if (posX.HasValue) pos.X = posX.Value;
        if (posY.HasValue) pos.Y = posY.Value;
        if (posZ.HasValue) pos.Z = posZ.Value;
        projNode.Position = pos;

        // Rotation (accumulate into stored degrees, then apply as Euler)
        Vector3 rot = _projRotDeg[projectorIndex];
        if (rotX.HasValue) rot.X = rotX.Value;
        if (rotY.HasValue) rot.Y = rotY.Value;
        if (rotZ.HasValue) rot.Z = rotZ.Value;
        _projRotDeg[projectorIndex] = rot;
        projNode.Basis = Basis.FromEuler(new Vector3(
            Mathf.DegToRad(rot.X),
            Mathf.DegToRad(rot.Y),
            Mathf.DegToRad(rot.Z)
        ));

        // Rebuild venue grid
        var venueGrid = preview3D.GetNodeOrNull<LazerSystem.Preview.VenueGrid>("VenueGrid");
        venueGrid?.MarkDirty();
    }

    private void TogglePopout()
    {
        if (_isPopped)
        {
            // Return preview to inline
            if (_floatingPanel != null)
            {
                _floatingPanel.ContentContainer.RemoveChild(_previewContainer);
                _inlinePreviewParent.AddChild(_previewContainer);
                _inlinePreviewParent.MoveChild(_previewContainer, 1);
                _previewContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
                _previewContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                _floatingPanel.QueueFree();
                _floatingPanel = null;
            }
            _isPopped = false;
        }
        else
        {
            // Pop out into a floating in-app panel
            _inlinePreviewParent.RemoveChild(_previewContainer);

            _floatingPanel = new FloatingPreviewPanel();
            _floatingPanel.Position = new Vector2(200, 80);
            _floatingPanel.Size = new Vector2(800, 500);
            _floatingPanel.OnCloseRequested += TogglePopout;
            AddChild(_floatingPanel);

            _previewContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _previewContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _floatingPanel.ContentContainer.AddChild(_previewContainer);

            _isPopped = true;
        }
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

        string initText = (labelText == "Rotation") ? $"{(int)defaultVal}\u00b0" : (min < 0 ? $"{(int)defaultVal}" : $"{(int)defaultVal}%");
        var valueLabel = new Label();
        valueLabel.Text = initText;
        valueLabel.CustomMinimumSize = new Vector2(45, 0);
        valueLabel.HorizontalAlignment = HorizontalAlignment.Left;
        valueLabel.AddThemeColorOverride("font_color", TextColor);
        valueLabel.AddThemeFontSizeOverride("font_size", 12);
        row.AddChild(valueLabel);

        return (slider, valueLabel);
    }

    // ═══════════════════════════════════════════════
    //  STATUS BAR (bottom)
    // ═══════════════════════════════════════════════
    private void BuildStatusBar(VBoxContainer root)
    {
        var statusPanel = new PanelContainer();
        statusPanel.CustomMinimumSize = new Vector2(0, 24);
        statusPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        ApplyPanelStyle(statusPanel, new Color(0.08f, 0.08f, 0.1f, 0.95f));
        root.AddChild(statusPanel);

        _statusLabel = new Label();
        _statusLabel.AddThemeColorOverride("font_color", TextColor);
        _statusLabel.AddThemeFontSizeOverride("font_size", 11);
        _statusLabel.VerticalAlignment = VerticalAlignment.Center;
        statusPanel.AddChild(_statusLabel);
    }

    // ═══════════════════════════════════════════════
    //  KEYBOARD INPUT
    // ═══════════════════════════════════════════════
    private bool _showBounds;
    private CheckButton _boundsToggle; // kept in sync with hotkey

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            // Tab: toggle venue boundary surfaces
            if (keyEvent.Keycode == Key.Tab)
            {
                _showBounds = !_showBounds;
                if (_boundsToggle != null) _boundsToggle.SetPressedNoSignal(_showBounds);
                var venueGrid = GetTree().Root.FindChild("VenueGrid", true, false) as LazerSystem.Preview.VenueGrid;
                venueGrid?.SetShowBounds(_showBounds);
                GetViewport().SetInputAsHandled();
                return;
            }

            // F1-F12 quick page switch (first 12 fav pages or sequential)
            if (keyEvent.Keycode >= Key.F1 && keyEvent.Keycode <= Key.F12)
            {
                int fIdx = (int)(keyEvent.Keycode - Key.F1);
                var favs = LiveEngine.Instance.FavoritePages;
                int targetPage = fIdx < favs.Count ? favs[fIdx] : fIdx;
                if (targetPage >= 0 && targetPage < LiveEngine.PageCount)
                {
                    LiveEngine.Instance.CurrentPage = targetPage;
                    RefreshPageList();
                    RefreshFavBar();
                    RefreshGrid();
                }
                GetViewport().SetInputAsHandled();
                return;
            }

            // Cue trigger keys
            for (int r = 0; r < KeyMap.Length; r++)
            {
                for (int c = 0; c < KeyMap[r].Length; c++)
                {
                    if (keyEvent.Keycode == KeyMap[r][c])
                    {
                        LiveEngine.Instance.TriggerCue(r, c);
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                }
            }
        }
    }

    // ═══════════════════════════════════════════════
    //  PROCESS UPDATE
    // ═══════════════════════════════════════════════
    public override void _Process(double delta)
    {
        int currentPage = LiveEngine.Instance.CurrentPage;
        if (currentPage != _lastRenderedPage)
        {
            _lastRenderedPage = currentPage;
            RefreshPageList();
            RefreshFavBar();
        }
        RefreshGrid();
        UpdateToolbar();
        UpdateStatusBar();
    }

    // ═══════════════════════════════════════════════
    //  REFRESH METHODS
    // ═══════════════════════════════════════════════
    private void RefreshGrid()
    {
        int page = LiveEngine.Instance.CurrentPage;
        for (int r = 0; r < 6; r++)
        {
            for (int c = 0; c < 10; c++)
            {
                var cue = LiveEngine.Instance.GetCue(page, r, c);
                var btn = _gridButtons[r, c];
                bool active = LiveEngine.Instance.IsCueActive(r, c);

                if (cue != null)
                {
                    string icon = GetPatternIcon(cue.PatternType);
                    string key = GetKeyHint(r, c);
                    string keyTag = key.Length > 0 ? $"[{key}] " : "";
                    btn.Text = $"{keyTag}{icon} {cue.CueName}";
                    btn.TooltipText = $"{cue.PatternType} | Key: {key}";

                    var style = new StyleBoxFlat();
                    if (active)
                    {
                        style.BgColor = CueActive;
                        style.BorderColor = ActiveGreen;
                        style.SetBorderWidthAll(2);
                    }
                    else
                    {
                        Color dimmed = new Color(cue.Color.R * 0.3f, cue.Color.G * 0.3f, cue.Color.B * 0.3f, 1f);
                        style.BgColor = dimmed.Lerp(CueDefault, 0.5f);
                    }
                    style.SetCornerRadiusAll(4);
                    btn.AddThemeStyleboxOverride("normal", style);

                    var hoverStyle = new StyleBoxFlat();
                    hoverStyle.BgColor = active ? CueActive.Lightened(0.15f) : style.BgColor.Lightened(0.2f);
                    hoverStyle.SetCornerRadiusAll(4);
                    btn.AddThemeStyleboxOverride("hover", hoverStyle);

                    var pressedStyle = new StyleBoxFlat();
                    pressedStyle.BgColor = ActiveGreen;
                    pressedStyle.SetCornerRadiusAll(4);
                    btn.AddThemeStyleboxOverride("pressed", pressedStyle);
                }
                else
                {
                    string key = GetKeyHint(r, c);
                    btn.Text = key.Length > 0 ? $"[{key}]" : "";
                    btn.TooltipText = key.Length > 0 ? $"Empty | Key: {key}" : "Empty";

                    var style = new StyleBoxFlat();
                    style.BgColor = CueDefault;
                    style.SetCornerRadiusAll(4);
                    btn.AddThemeStyleboxOverride("normal", style);

                    var hoverStyle = new StyleBoxFlat();
                    hoverStyle.BgColor = CueDefault.Lightened(0.15f);
                    hoverStyle.SetCornerRadiusAll(4);
                    btn.AddThemeStyleboxOverride("hover", hoverStyle);

                    var pressedStyle = new StyleBoxFlat();
                    pressedStyle.BgColor = CueDefault.Lightened(0.3f);
                    pressedStyle.SetCornerRadiusAll(4);
                    btn.AddThemeStyleboxOverride("pressed", pressedStyle);
                }

                btn.AddThemeColorOverride("font_color", TextColor);
            }
        }
    }

    private void UpdateToolbar()
    {
        // FPS
        _fpsLabel.Text = $"FPS: {Engine.GetFramesPerSecond()}";

        // Laser output button
        bool laserOn = LiveEngine.Instance.LaserEnabled;
        StyleButton(_laserOutputBtn, laserOn ? ActiveGreen : DangerRed);

        // Blackout button
        bool blackout = LiveEngine.Instance.Blackout;
        StyleButton(_blackoutBtn, blackout ? WarningYellow : DangerRed);
        _blackoutBtn.AddThemeColorOverride("font_color", blackout ? Colors.Black : Colors.White);

        // Projector buttons
        for (int i = 0; i < 4; i++)
        {
            bool en = LiveEngine.Instance.ProjectorEnabled[i];
            StyleButton(_projectorBtns[i], en ? ProjectorColors[i] : ProjectorColors[i].Darkened(0.6f));
            _projectorBtns[i].AddThemeColorOverride("font_color", en ? Colors.White : DimText);
        }

        // Zone routing buttons
        var activeZones = LiveEngine.Instance.ActiveZones;
        for (int i = 0; i < 4; i++)
        {
            bool active = System.Array.IndexOf(activeZones, i) >= 0;
            StyleButton(_zoneBtns[i], active ? ActiveGreen : new Color(0.25f, 0.25f, 0.3f, 1f));
            _zoneBtns[i].AddThemeColorOverride("font_color", active ? Colors.White : DimText);
        }
    }

    private void UpdateStatusBar()
    {
        int page = LiveEngine.Instance.CurrentPage;
        string pageName = LiveEngine.Instance.GetPageDisplayName(page);
        int activeCues = CountActiveCues();
        bool laserOn = LiveEngine.Instance.LaserEnabled;
        var zones = LiveEngine.Instance.ActiveZones;
        string zoneStr = zones.Length >= 4 ? "ALL" : string.Join(", ", System.Array.ConvertAll(zones, z => $"Z{z + 1}"));
        _statusLabel.Text = $"{pageName} | Active: {activeCues} cues | Laser: {(laserOn ? "ON" : "OFF")} | Zones: {zoneStr}";
    }

    private int CountActiveCues()
    {
        int count = 0;
        var grid = LiveEngine.Instance.ActiveCueGrid;
        for (int r = 0; r < 6; r++)
            for (int c = 0; c < 10; c++)
                if (grid[r, c] >= 0) count++;
        return count;
    }

    private void ResetAllSliders()
    {
        _masterIntensitySlider.Value = 100;
        _masterSizeSlider.Value = 100;
        _posXSlider.Value = 0;
        _posYSlider.Value = 0;
        _rotationSlider.Value = 0;
        _speedSlider.Value = 100;
        _colorPicker.Color = Colors.White;

        LiveEngine.Instance.MasterIntensity = 1f;
        LiveEngine.Instance.MasterSize = 1f;
        LiveEngine.Instance.LiveOverrides = new PatternParameters
        {
            color = Colors.White,
            intensity = 1f,
            size = 1f,
            rotation = 0f,
            speed = 1f,
            position = Vector2.Zero,
        };
    }

    // ═══════════════════════════════════════════════
    //  ZONE / SETTINGS TOGGLES
    // ═══════════════════════════════════════════════
    private void ToggleZone(int zoneIndex)
    {
        var current = new System.Collections.Generic.List<int>(LiveEngine.Instance.ActiveZones);
        if (current.Contains(zoneIndex))
            current.Remove(zoneIndex);
        else
            current.Add(zoneIndex);
        if (current.Count == 0)
            current.Add(zoneIndex); // don't allow empty
        LiveEngine.Instance.ActiveZones = current.ToArray();
    }

    private void ToggleProjectorSettings()
    {
        if (_projectorSettingsPanel != null)
        {
            _projectorSettingsPanel.QueueFree();
            _projectorSettingsPanel = null;
            return;
        }
        _projectorSettingsPanel = new ProjectorSettingsPanel();
        _projectorSettingsPanel.Position = new Vector2(100, 60);
        _projectorSettingsPanel.Size = new Vector2(520, 620);
        _projectorSettingsPanel.OnCloseRequested += ToggleProjectorSettings;
        AddChild(_projectorSettingsPanel);
    }

    private void ToggleZoneEditor()
    {
        if (_zoneEditorPanel != null)
        {
            _zoneEditorPanel.QueueFree();
            _zoneEditorPanel = null;
            return;
        }
        _zoneEditorPanel = new ZoneEditorPanel();
        _zoneEditorPanel.Position = new Vector2(150, 50);
        _zoneEditorPanel.Size = new Vector2(650, 700);
        _zoneEditorPanel.OnCloseRequested += ToggleZoneEditor;
        AddChild(_zoneEditorPanel);
    }

    // ═══════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════
    private static string GetPatternIcon(LaserPatternType type)
    {
        return type switch
        {
            LaserPatternType.Beam     => "/",
            LaserPatternType.Fan      => "W",
            LaserPatternType.Cone     => "V",
            LaserPatternType.Circle   => "O",
            LaserPatternType.Line     => "-",
            LaserPatternType.Wave     => "~",
            LaserPatternType.Triangle => "^",
            LaserPatternType.Square   => "#",
            LaserPatternType.Star     => "*",
            LaserPatternType.Text     => "T",
            LaserPatternType.Tunnel   => "@",
            _ => "?"
        };
    }

    private string GetKeyHint(int row, int col)
    {
        if (row < KeyLabels.Length && col < KeyLabels[row].Length)
            return KeyLabels[row][col];
        return "";
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

    private void DisableMainCamera()
    {
        // Find the main scene camera and disable it so nothing renders behind the UI panels
        var mainCam = GetTree().Root.FindChild("Camera", true, false) as Camera3D;
        if (mainCam != null && mainCam.GetParent()?.Name == "Preview3D")
        {
            mainCam.Current = false;
            mainCam.Visible = false;
        }
    }

    private VSeparator CreateToolbarSeparator()
    {
        var sep = new VSeparator();
        sep.CustomMinimumSize = new Vector2(2, 0);
        return sep;
    }
}
