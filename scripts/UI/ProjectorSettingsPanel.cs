using Godot;
using LazerSystem.Core;
using LazerSystem.ArtNet;

/// <summary>
/// Floating panel for configuring projector network settings, discovering ArtNet nodes,
/// and testing connections/patterns for each of the 4 FB4 projectors.
/// </summary>
public partial class ProjectorSettingsPanel : FloatingPanel
{
    private static readonly Color[] ProjectorColors = {
        new Color(0.9f, 0.2f, 0.2f, 1f),
        new Color(0.2f, 0.9f, 0.3f, 1f),
        new Color(0.2f, 0.4f, 0.9f, 1f),
        new Color(0.9f, 0.8f, 0.1f, 1f),
    };

    private static readonly Color FieldBg = new Color(0.12f, 0.12f, 0.14f, 1f);
    private static readonly Color SectionBg = new Color(0.1f, 0.1f, 0.12f, 1f);
    private static readonly Color DimText = new Color(0.55f, 0.55f, 0.6f, 1f);

    // Network controls
    private LineEdit _broadcastEdit;
    private Label _discoveredLabel;
    private ItemList _nodeList;

    // Per-projector controls
    private LineEdit[] _nameEdits = new LineEdit[4];
    private LineEdit[] _ipEdits = new LineEdit[4];
    private CheckButton[] _enabledChecks = new CheckButton[4];
    private SpinBox[] _universeSpins = new SpinBox[4];
    private ColorRect[] _statusRects = new ColorRect[4];
    private Label[] _zoneLabels = new Label[4];

    // Timers
    private Timer _scanTimer;
    private Timer[] _testTimers = new Timer[4];

    public override void _Ready()
    {
        PanelTitle = "Projector Settings";
        InitialSize = new Vector2(520, 700);
        base._Ready();
        BuildUI();
        SyncFromManagers();
    }

    // We need a reference to the actual content VBox (inside a scroll)
    private VBoxContainer _contentVBox;

    private void BuildUI()
    {
        // Wrap everything in a ScrollContainer since projector settings can be tall
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        ContentContainer.AddChild(scroll);

        _contentVBox = new VBoxContainer();
        _contentVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _contentVBox.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_contentVBox);

        // ── Network Section ──
        AddSectionLabel("Network");

        // Broadcast row
        var bcastRow = new HBoxContainer();
        bcastRow.AddThemeConstantOverride("separation", 6);
        _contentVBox.AddChild(bcastRow);

        bcastRow.AddChild(MakeLabel("Broadcast", 80));

        _broadcastEdit = MakeLineEdit("2.255.255.255", 160);
        _broadcastEdit.TextSubmitted += (text) =>
        {
            if (ArtNetManager.Instance != null)
                ArtNetManager.Instance.BroadcastAddress = text;
        };
        bcastRow.AddChild(_broadcastEdit);

        var scanBtn = MakeButton("Scan Network", new Vector2(120, 28));
        scanBtn.Pressed += OnScanNetwork;
        bcastRow.AddChild(scanBtn);

        // Discovered label
        _discoveredLabel = MakeLabel("Discovered: 0 devices", 0);
        _discoveredLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _contentVBox.AddChild(_discoveredLabel);

        // Node list
        _nodeList = new ItemList();
        _nodeList.CustomMinimumSize = new Vector2(0, 80);
        _nodeList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _nodeList.AddThemeColorOverride("font_color", new Color(0.8f, 0.9f, 0.85f, 1f));
        var nodeListStyle = new StyleBoxFlat();
        nodeListStyle.BgColor = FieldBg;
        nodeListStyle.SetCornerRadiusAll(3);
        _nodeList.AddThemeStyleboxOverride("panel", nodeListStyle);
        _nodeList.ItemSelected += OnNodeSelected;
        _contentVBox.AddChild(_nodeList);

        // Separator
        _contentVBox.AddChild(MakeSeparator());

        // ── Projector Sections ──
        for (int i = 0; i < 4; i++)
        {
            BuildProjectorSection(i);
        }

        // Scan timer (not added to tree yet, created on demand)
        _scanTimer = new Timer();
        _scanTimer.OneShot = true;
        _scanTimer.WaitTime = 3.0;
        _scanTimer.Timeout += OnScanTimerTimeout;
        AddChild(_scanTimer);

        // Test connection timers
        for (int i = 0; i < 4; i++)
        {
            _testTimers[i] = new Timer();
            _testTimers[i].OneShot = true;
            _testTimers[i].WaitTime = 2.0;
            int idx = i;
            _testTimers[i].Timeout += () => OnTestConnectionTimeout(idx);
            AddChild(_testTimers[i]);
        }
    }

    private void BuildProjectorSection(int i)
    {
        var panel = new PanelContainer();
        panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = SectionBg;
        panelStyle.SetCornerRadiusAll(4);
        panelStyle.BorderWidthLeft = 3;
        panelStyle.BorderColor = ProjectorColors[i];
        panelStyle.ContentMarginLeft = 10;
        panelStyle.ContentMarginRight = 6;
        panelStyle.ContentMarginTop = 6;
        panelStyle.ContentMarginBottom = 6;
        panel.AddThemeStyleboxOverride("panel", panelStyle);
        _contentVBox.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        panel.AddChild(vbox);

        // Row 1: P# label, name, enabled
        var row1 = new HBoxContainer();
        row1.AddThemeConstantOverride("separation", 6);
        vbox.AddChild(row1);

        var pLabel = MakeLabel($"P{i + 1}", 30);
        pLabel.AddThemeColorOverride("font_color", ProjectorColors[i]);
        pLabel.AddThemeFontSizeOverride("font_size", 15);
        row1.AddChild(pLabel);

        _nameEdits[i] = MakeLineEdit($"Projector {i + 1}", 140);
        int nameIdx = i;
        _nameEdits[i].TextSubmitted += (text) => OnNameChanged(nameIdx, text);
        _nameEdits[i].FocusExited += () => OnNameChanged(nameIdx, _nameEdits[nameIdx].Text);
        row1.AddChild(_nameEdits[i]);

        _enabledChecks[i] = new CheckButton();
        _enabledChecks[i].Text = "Enabled";
        _enabledChecks[i].ButtonPressed = true;
        _enabledChecks[i].AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f, 1f));
        int enIdx = i;
        _enabledChecks[i].Toggled += (on) => OnEnabledToggled(enIdx, on);
        row1.AddChild(_enabledChecks[i]);

        // Row 2: IP, Universe
        var row2 = new HBoxContainer();
        row2.AddThemeConstantOverride("separation", 6);
        vbox.AddChild(row2);

        row2.AddChild(MakeLabel("IP", 22));

        _ipEdits[i] = MakeLineEdit($"192.168.0.{81 + i}", 130);
        int ipIdx = i;
        _ipEdits[i].TextSubmitted += (text) => OnIpChanged(ipIdx, text);
        _ipEdits[i].FocusExited += () => OnIpChanged(ipIdx, _ipEdits[ipIdx].Text);
        row2.AddChild(_ipEdits[i]);

        row2.AddChild(MakeLabel("Univ", 36));

        _universeSpins[i] = new SpinBox();
        _universeSpins[i].MinValue = 0;
        _universeSpins[i].MaxValue = 15;
        _universeSpins[i].Value = i;
        _universeSpins[i].Step = 1;
        _universeSpins[i].CustomMinimumSize = new Vector2(70, 28);
        int uniIdx = i;
        _universeSpins[i].ValueChanged += (val) => OnUniverseChanged(uniIdx, (int)val);
        row2.AddChild(_universeSpins[i]);

        // Row 3: Test Connection, Test Pattern, Status
        var row3 = new HBoxContainer();
        row3.AddThemeConstantOverride("separation", 6);
        vbox.AddChild(row3);

        var testConnBtn = MakeButton("Test Connection", new Vector2(130, 26));
        int connIdx = i;
        testConnBtn.Pressed += () => OnTestConnection(connIdx);
        row3.AddChild(testConnBtn);

        var testPatBtn = MakeButton("Test Pattern", new Vector2(110, 26));
        int patIdx = i;
        testPatBtn.Pressed += () => OnTestPattern(patIdx);
        row3.AddChild(testPatBtn);

        _statusRects[i] = new ColorRect();
        _statusRects[i].CustomMinimumSize = new Vector2(20, 20);
        _statusRects[i].Color = new Color(0.4f, 0.4f, 0.4f, 1f); // gray = unknown
        row3.AddChild(_statusRects[i]);

        // Row 4: Zone assignment label
        _zoneLabels[i] = MakeLabel("Zones: --", 0);
        _zoneLabels[i].SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _zoneLabels[i].AddThemeColorOverride("font_color", DimText);
        _zoneLabels[i].AddThemeFontSizeOverride("font_size", 11);
        vbox.AddChild(_zoneLabels[i]);
    }

    // ── Event Handlers ──

    private void OnScanNetwork()
    {
        if (ArtNetManager.Instance == null) return;

        ArtNetManager.Instance.DiscoveredNodes.Clear();
        ArtNetManager.Instance.SendArtPoll();

        _discoveredLabel.Text = "Scanning...";
        _nodeList.Clear();
        _scanTimer.Start();
    }

    private void OnScanTimerTimeout()
    {
        RefreshDiscoveredList();
    }

    private void RefreshDiscoveredList()
    {
        _nodeList.Clear();

        if (ArtNetManager.Instance == null)
        {
            _discoveredLabel.Text = "Discovered: 0 devices";
            return;
        }

        var nodes = ArtNetManager.Instance.DiscoveredNodes;
        _discoveredLabel.Text = $"Discovered: {nodes.Count} device{(nodes.Count != 1 ? "s" : "")}";

        foreach (var node in nodes)
        {
            _nodeList.AddItem(node.ToString());
        }
    }

    private void OnNodeSelected(long index)
    {
        if (ArtNetManager.Instance == null) return;
        if (index < 0 || index >= ArtNetManager.Instance.DiscoveredNodes.Count) return;

        var node = ArtNetManager.Instance.DiscoveredNodes[(int)index];

        // Find the first projector with an empty or default IP and fill it
        for (int i = 0; i < 4; i++)
        {
            string currentIp = _ipEdits[i].Text.Trim();
            if (string.IsNullOrEmpty(currentIp) || currentIp == $"192.168.0.{81 + i}")
            {
                _ipEdits[i].Text = node.ip;
                OnIpChanged(i, node.ip);
                break;
            }
        }
    }

    private void OnNameChanged(int idx, string text)
    {
        var mgr = LaserSystemManager.Instance;
        if (mgr == null || idx >= mgr.Projectors.Count) return;
        if (mgr.Projectors[idx] == null) return;

        mgr.Projectors[idx].ProjectorName = text;
    }

    private void OnIpChanged(int idx, string text)
    {
        var mgr = LaserSystemManager.Instance;
        if (mgr == null || idx >= mgr.Projectors.Count) return;
        if (mgr.Projectors[idx] == null) return;

        mgr.Projectors[idx].IpAddress = text;
    }

    private void OnEnabledToggled(int idx, bool on)
    {
        var mgr = LaserSystemManager.Instance;
        if (mgr != null && idx < mgr.Projectors.Count && mgr.Projectors[idx] != null)
            mgr.Projectors[idx].Enabled = on;

        if (LiveEngine.Instance != null && idx < LiveEngine.Instance.ProjectorEnabled.Length)
            LiveEngine.Instance.ProjectorEnabled[idx] = on;
    }

    private void OnUniverseChanged(int idx, int universe)
    {
        var mgr = LaserSystemManager.Instance;
        if (mgr == null || idx >= mgr.Projectors.Count) return;
        if (mgr.Projectors[idx] == null) return;

        mgr.Projectors[idx].ArtNetUniverse = universe;
    }

    private void OnTestConnection(int idx)
    {
        if (ArtNetManager.Instance == null) return;

        // Set status to yellow (testing)
        _statusRects[idx].Color = new Color(0.9f, 0.8f, 0.1f, 1f);

        ArtNetManager.Instance.SendArtPoll();
        _testTimers[idx].Start();
    }

    private void OnTestConnectionTimeout(int idx)
    {
        if (ArtNetManager.Instance == null)
        {
            _statusRects[idx].Color = new Color(0.8f, 0.15f, 0.15f, 1f); // red
            return;
        }

        string targetIp = _ipEdits[idx].Text.Trim();
        bool found = false;

        foreach (var node in ArtNetManager.Instance.DiscoveredNodes)
        {
            if (node.ip == targetIp)
            {
                found = true;
                break;
            }
        }

        _statusRects[idx].Color = found
            ? new Color(0.2f, 0.8f, 0.3f, 1f)  // green = ok
            : new Color(0.8f, 0.15f, 0.15f, 1f); // red = error
    }

    private void OnTestPattern(int idx)
    {
        if (ArtNetManager.Instance == null) return;

        int universe = (int)_universeSpins[idx].Value;

        byte[] frame = FB4ChannelMap.BuildDmxFrame(
            enabled: true,
            pattern: 0,
            x: 0,
            y: 0,
            sizeX: 0.5f,
            sizeY: 0.5f,
            rotation: 0,
            color: Colors.White,
            scanSpeed: 0.5f,
            effect: 0,
            effectSpeed: 0,
            effectSize: 0,
            zoom: 0.5f
        );

        ArtNetManager.Instance.SendDmx(universe, frame);
    }

    // ── Sync ──

    private void SyncFromManagers()
    {
        // Sync from ArtNetManager
        if (ArtNetManager.Instance != null)
        {
            _broadcastEdit.Text = ArtNetManager.Instance.BroadcastAddress;
            RefreshDiscoveredList();
        }

        // Sync from LaserSystemManager
        var mgr = LaserSystemManager.Instance;
        if (mgr == null) return;

        for (int i = 0; i < 4; i++)
        {
            if (i >= mgr.Projectors.Count || mgr.Projectors[i] == null)
                continue;

            var proj = mgr.Projectors[i];

            if (!string.IsNullOrEmpty(proj.ProjectorName))
                _nameEdits[i].Text = proj.ProjectorName;

            if (!string.IsNullOrEmpty(proj.IpAddress))
                _ipEdits[i].Text = proj.IpAddress;

            _enabledChecks[i].ButtonPressed = proj.Enabled;
            _universeSpins[i].Value = proj.ArtNetUniverse;

            // Sync enabled state to LiveEngine
            if (LiveEngine.Instance != null && i < LiveEngine.Instance.ProjectorEnabled.Length)
                _enabledChecks[i].ButtonPressed = LiveEngine.Instance.ProjectorEnabled[i];

            // Show assigned zones
            UpdateZoneLabel(i);
        }
    }

    private void UpdateZoneLabel(int projectorIdx)
    {
        var mgr = LaserSystemManager.Instance;
        if (mgr == null || mgr.Zones == null || mgr.Zones.Count == 0)
        {
            _zoneLabels[projectorIdx].Text = "Zones: none";
            return;
        }

        var zoneNames = new System.Collections.Generic.List<string>();
        for (int z = 0; z < mgr.Zones.Count; z++)
        {
            var zone = mgr.Zones[z];
            if (zone == null) continue;

            // ProjectionZone should reference a projector index; check if it matches
            // For now show all zones since we don't know the zone-to-projector mapping details
            zoneNames.Add($"Zone {z}");
        }

        _zoneLabels[projectorIdx].Text = zoneNames.Count > 0
            ? $"Zones: {string.Join(", ", zoneNames)}"
            : "Zones: none";
    }

    // ── UI Helpers ──

    private Label MakeLabel(string text, float minWidth)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f, 1f));
        label.AddThemeFontSizeOverride("font_size", 12);
        if (minWidth > 0)
            label.CustomMinimumSize = new Vector2(minWidth, 0);
        return label;
    }

    private LineEdit MakeLineEdit(string defaultText, float minWidth)
    {
        var edit = new LineEdit();
        edit.Text = defaultText;
        edit.CustomMinimumSize = new Vector2(minWidth, 28);
        edit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        edit.AddThemeColorOverride("font_color", new Color(0.85f, 0.9f, 0.85f, 1f));
        edit.AddThemeFontSizeOverride("font_size", 12);
        var editStyle = new StyleBoxFlat();
        editStyle.BgColor = FieldBg;
        editStyle.SetCornerRadiusAll(3);
        editStyle.ContentMarginLeft = 6;
        editStyle.ContentMarginRight = 6;
        edit.AddThemeStyleboxOverride("normal", editStyle);
        return edit;
    }

    private Button MakeButton(string text, Vector2 minSize)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = minSize;
        btn.AddThemeFontSizeOverride("font_size", 11);
        var btnStyle = new StyleBoxFlat();
        btnStyle.BgColor = new Color(0.2f, 0.2f, 0.25f, 1f);
        btnStyle.SetCornerRadiusAll(3);
        btnStyle.ContentMarginLeft = 8;
        btnStyle.ContentMarginRight = 8;
        btnStyle.ContentMarginTop = 2;
        btnStyle.ContentMarginBottom = 2;
        btn.AddThemeStyleboxOverride("normal", btnStyle);
        var btnHover = new StyleBoxFlat();
        btnHover.BgColor = new Color(0.28f, 0.28f, 0.33f, 1f);
        btnHover.SetCornerRadiusAll(3);
        btnHover.ContentMarginLeft = 8;
        btnHover.ContentMarginRight = 8;
        btnHover.ContentMarginTop = 2;
        btnHover.ContentMarginBottom = 2;
        btn.AddThemeStyleboxOverride("hover", btnHover);
        var btnPressed = new StyleBoxFlat();
        btnPressed.BgColor = new Color(0.15f, 0.15f, 0.2f, 1f);
        btnPressed.SetCornerRadiusAll(3);
        btnPressed.ContentMarginLeft = 8;
        btnPressed.ContentMarginRight = 8;
        btnPressed.ContentMarginTop = 2;
        btnPressed.ContentMarginBottom = 2;
        btn.AddThemeStyleboxOverride("pressed", btnPressed);
        return btn;
    }

    private void AddSectionLabel(string text)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeColorOverride("font_color", new Color(0.6f, 0.8f, 0.65f, 1f));
        label.AddThemeFontSizeOverride("font_size", 14);
        _contentVBox.AddChild(label);
    }

    private HSeparator MakeSeparator()
    {
        var sep = new HSeparator();
        sep.AddThemeConstantOverride("separation", 8);
        return sep;
    }
}
