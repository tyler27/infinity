using Godot;
using LazerSystem.Core;
using LazerSystem.ArtNet;

public partial class AppSettingsPanel : FloatingPanel
{
    private static readonly Color SectionColor = new Color(0.6f, 0.8f, 0.65f, 1f);

    // Performance controls
    private SpinBox _fpsCapSpin;
    private CheckButton _vsyncCheck;
    private HSlider _artNetRateSlider;
    private Label _artNetRateLabel;

    // Safety controls
    private CheckButton _blackoutOnLaunchCheck;

    private VBoxContainer _contentVBox;

    public override void _Ready()
    {
        PanelTitle = "Application Settings";
        InitialSize = new Vector2(460, 340);
        base._Ready();
        BuildUI();
        SyncFromSettings();
    }

    private void BuildUI()
    {
        var outerVBox = new VBoxContainer();
        outerVBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        outerVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        outerVBox.AddThemeConstantOverride("separation", 0);
        ContentContainer.AddChild(outerVBox);

        // Scrollable content area
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        outerVBox.AddChild(scroll);

        _contentVBox = new VBoxContainer();
        _contentVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _contentVBox.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_contentVBox);

        // ── Performance Section ──
        AddSectionLabel("Performance");

        // FPS Cap
        var fpsRow = MakeRow();
        fpsRow.AddChild(MakeLabel("FPS Cap", 110));
        _fpsCapSpin = new SpinBox();
        _fpsCapSpin.MinValue = 30;
        _fpsCapSpin.MaxValue = 300;
        _fpsCapSpin.Value = 165;
        _fpsCapSpin.Step = 1;
        _fpsCapSpin.CustomMinimumSize = new Vector2(100, 28);
        fpsRow.AddChild(_fpsCapSpin);

        // VSync
        var vsyncRow = MakeRow();
        vsyncRow.AddChild(MakeLabel("VSync", 110));
        _vsyncCheck = new CheckButton();
        _vsyncCheck.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f, 1f));
        vsyncRow.AddChild(_vsyncCheck);

        // ArtNet Send Rate
        var rateRow = MakeRow();
        rateRow.AddChild(MakeLabel("ArtNet Rate", 110));
        _artNetRateSlider = new HSlider();
        _artNetRateSlider.MinValue = 1;
        _artNetRateSlider.MaxValue = 44;
        _artNetRateSlider.Value = 44;
        _artNetRateSlider.Step = 1;
        _artNetRateSlider.CustomMinimumSize = new Vector2(180, 20);
        _artNetRateSlider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _artNetRateSlider.ValueChanged += (val) =>
        {
            _artNetRateLabel.Text = $"{(int)val} Hz";
        };
        rateRow.AddChild(_artNetRateSlider);
        _artNetRateLabel = MakeLabel("44 Hz", 50);
        rateRow.AddChild(_artNetRateLabel);

        _contentVBox.AddChild(MakeSeparator());

        // ── Safety Section ──
        AddSectionLabel("Safety");

        // Blackout on Launch
        var blackoutRow = MakeRow();
        blackoutRow.AddChild(MakeLabel("Blackout on Launch", 110));
        _blackoutOnLaunchCheck = new CheckButton();
        _blackoutOnLaunchCheck.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f, 1f));
        blackoutRow.AddChild(_blackoutOnLaunchCheck);

        // ── Bottom button bar ──
        var buttonBar = new HBoxContainer();
        buttonBar.AddThemeConstantOverride("separation", 8);
        buttonBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        outerVBox.AddChild(buttonBar);

        // Reset to Defaults (left side)
        var resetBtn = MakeButton("Reset to Defaults", new Vector2(130, 32));
        resetBtn.Pressed += OnResetDefaults;
        buttonBar.AddChild(resetBtn);

        // Spacer pushes Apply/Save to the right
        var spacer = new Control();
        spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        buttonBar.AddChild(spacer);

        var applyBtn = MakeButton("Apply", new Vector2(80, 32));
        applyBtn.Pressed += OnApply;
        buttonBar.AddChild(applyBtn);

        var saveBtn = MakeButton("Save", new Vector2(80, 32));
        saveBtn.Pressed += OnSave;
        buttonBar.AddChild(saveBtn);
    }

    private void SyncFromSettings()
    {
        var s = GetSettings();
        if (s == null) return;

        _fpsCapSpin.Value = s.FpsCap;
        _vsyncCheck.ButtonPressed = s.VSync;
        _artNetRateSlider.Value = s.ArtNetSendRate;
        _artNetRateLabel.Text = $"{s.ArtNetSendRate} Hz";
        _blackoutOnLaunchCheck.ButtonPressed = s.BlackoutOnLaunch;
    }

    private void WriteControlsToSettings()
    {
        var s = GetSettings();
        if (s == null) return;

        s.FpsCap = (int)_fpsCapSpin.Value;
        s.VSync = _vsyncCheck.ButtonPressed;
        s.ArtNetSendRate = (int)_artNetRateSlider.Value;
        s.BlackoutOnLaunch = _blackoutOnLaunchCheck.ButtonPressed;
    }

    private void OnApply()
    {
        WriteControlsToSettings();
        LaserSystemManager.Instance?.ApplySettings();
    }

    private void OnSave()
    {
        WriteControlsToSettings();
        LaserSystemManager.Instance?.ApplySettings();
        LaserSystemManager.Instance?.SaveSettings();
    }

    private void OnResetDefaults()
    {
        var s = GetSettings();
        if (s == null) return;

        s.ResetToDefaults();
        LaserSystemManager.Instance?.ApplySettings();
        SyncFromSettings();
    }

    private AppSettings GetSettings()
    {
        return LaserSystemManager.Instance?.Settings;
    }

    // ── UI Helpers ──

    private HBoxContainer MakeRow()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        _contentVBox.AddChild(row);
        return row;
    }

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

    private void AddSectionLabel(string text)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeColorOverride("font_color", SectionColor);
        label.AddThemeFontSizeOverride("font_size", 14);
        _contentVBox.AddChild(label);
    }

    private HSeparator MakeSeparator()
    {
        var sep = new HSeparator();
        sep.AddThemeConstantOverride("separation", 8);
        return sep;
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
        return btn;
    }
}
