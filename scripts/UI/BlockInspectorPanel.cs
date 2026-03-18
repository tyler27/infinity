using Godot;
using LazerSystem.Core;
using LazerSystem.Patterns;
using LazerSystem.Timeline;
using LazerSystem.Timeline.Commands;

namespace LazerSystem.UI
{
    public partial class BlockInspectorPanel : VBoxContainer
    {
        private LaserCueBlock currentBlock;

        // Controls
        private LineEdit nameInput;
        private OptionButton patternDropdown;
        private ColorPickerButton colorPicker;
        private HSlider intensitySlider;
        private HSlider sizeSlider;
        private HSlider speedSlider;
        private HSlider rotationSlider;
        private HSlider spreadSlider;
        private HSlider fadeInSlider;
        private HSlider fadeOutSlider;
        private SpinBox zoneSpinBox;
        private SpinBox countSpinBox;

        // Automation controls
        private CheckButton autoKeyToggle;
        private OptionButton automationParamDropdown;
        [Export] public TimelineUI timelineUI;
        [Export] public PlaybackManager playbackManager;

        private bool isUpdating;

        // Idle-commit timer for slider changes
        private float _idleTimer;
        private bool _pendingCommit;
        private string _pendingDescription;
        private System.Action _pendingApplyNew;
        private System.Action _pendingApplyOld;
        private const float IdleCommitDelay = 0.3f;

        public override void _Ready()
        {
            BuildUI();
            HidePanel();
        }

        public override void _Process(double delta)
        {
            if (_pendingCommit)
            {
                _idleTimer += (float)delta;
                if (_idleTimer >= IdleCommitDelay)
                {
                    CommitPending();
                }
            }

            // Update slider readout to show automated value at playhead
            if (IsAutoKeyActive && currentBlock != null && Visible)
            {
                RefreshAutomatedValues();
            }
        }

        private void CommitPending()
        {
            if (!_pendingCommit) return;
            _pendingCommit = false;
            _idleTimer = 0f;

            // Value is already applied live, so we create the command and push it
            // onto the undo stack. ExecuteCommand will call Execute() which re-applies
            // the same current value (no-op), which is fine.
            var cmd = new ModifyBlockPropertyCommand(_pendingDescription, _pendingApplyNew, _pendingApplyOld);
            UndoManager.Instance.ExecuteCommand(cmd);
        }

        private void StartOrUpdatePending(string description, System.Action applyNew, System.Action applyOld)
        {
            if (_pendingCommit)
            {
                // Update the pending new-value action but keep the original old-value
                _pendingApplyNew = applyNew;
                _idleTimer = 0f;
            }
            else
            {
                _pendingCommit = true;
                _pendingDescription = description;
                _pendingApplyNew = applyNew;
                _pendingApplyOld = applyOld;
                _idleTimer = 0f;
            }
        }

        private void BuildUI()
        {
            var header = new Label { Text = "Block Inspector" };
            header.AddThemeColorOverride("font_color", Colors.White);
            header.AddThemeFontSizeOverride("font_size", 14);
            AddChild(header);

            AddChild(new HSeparator());

            AddChild(new Label { Text = "Name" });
            nameInput = new LineEdit();
            nameInput.PlaceholderText = "Block name...";
            nameInput.CustomMinimumSize = new Vector2(0, 28);
            nameInput.AddThemeFontSizeOverride("font_size", 12);
            nameInput.TextSubmitted += OnNameSubmitted;
            nameInput.FocusExited += OnNameFocusLost;
            AddChild(nameInput);

            AddChild(new Label { Text = "Pattern" });
            patternDropdown = new OptionButton();
            var types = PatternFactory.RegisteredTypes;
            foreach (var t in types)
                patternDropdown.AddItem(t.ToString());
            patternDropdown.ItemSelected += OnPatternChanged;
            AddChild(patternDropdown);

            AddChild(new Label { Text = "Color" });
            colorPicker = new ColorPickerButton { CustomMinimumSize = new Vector2(0, 30) };
            colorPicker.ColorChanged += OnColorChanged;
            AddChild(colorPicker);

            intensitySlider = CreateSlider("Intensity", 0f, 1f, 0.01f, OnIntensityChanged);
            sizeSlider = CreateSlider("Size", 0f, 1f, 0.01f, OnSizeChanged);
            speedSlider = CreateSlider("Speed", 0f, 10f, 0.1f, OnSpeedChanged);
            rotationSlider = CreateSlider("Rotation", -180f, 180f, 1f, OnRotationChanged);
            spreadSlider = CreateSlider("Spread", 0f, 1f, 0.01f, OnSpreadChanged);

            AddChild(new Label { Text = "Count" });
            countSpinBox = new SpinBox { MinValue = 1, MaxValue = 64, Step = 1 };
            countSpinBox.ValueChanged += OnCountChanged;
            AddChild(countSpinBox);

            AddChild(new HSeparator());

            fadeInSlider = CreateSlider("Fade In (s)", 0f, 5f, 0.05f, OnFadeInChanged);
            fadeOutSlider = CreateSlider("Fade Out (s)", 0f, 5f, 0.05f, OnFadeOutChanged);

            AddChild(new HSeparator());

            AddChild(new Label { Text = "Zone" });
            zoneSpinBox = new SpinBox { MinValue = 0, MaxValue = 15, Step = 1 };
            zoneSpinBox.ValueChanged += OnZoneChanged;
            AddChild(zoneSpinBox);

            // Automation section
            AddChild(new HSeparator());
            AddChild(new Label { Text = "Automation" });

            autoKeyToggle = new CheckButton { Text = "Auto-Key" };
            AddChild(autoKeyToggle);

            automationParamDropdown = new OptionButton();
            automationParamDropdown.AddItem("Color");
            for (int i = 0; i < AutomatableParameter.All.Length; i++)
                automationParamDropdown.AddItem(AutomatableParameter.All[i].Name);
            automationParamDropdown.ItemSelected += OnAutomationParamSelected;
            AddChild(automationParamDropdown);
        }

        private HSlider CreateSlider(string label, float min, float max, float step,
            Godot.Range.ValueChangedEventHandler handler)
        {
            AddChild(new Label { Text = label });
            var slider = new HSlider
            {
                MinValue = min,
                MaxValue = max,
                Step = step,
                CustomMinimumSize = new Vector2(0, 20)
            };
            slider.ValueChanged += handler;
            AddChild(slider);
            return slider;
        }

        public void ShowBlock(LaserCueBlock block)
        {
            // Commit any pending changes from previous block
            if (_pendingCommit) CommitPending();

            currentBlock = block;
            Visible = true;
            RefreshFromBlock();
        }

        public void HidePanel()
        {
            if (_pendingCommit) CommitPending();
            currentBlock = null;
            Visible = false;
        }

        private void RefreshFromBlock()
        {
            if (currentBlock == null || currentBlock.Cue == null)
                return;

            isUpdating = true;

            var cue = currentBlock.Cue;

            nameInput.Text = cue.CueName ?? "";

            var types = new System.Collections.Generic.List<LaserPatternType>(PatternFactory.RegisteredTypes);
            int idx = types.IndexOf(cue.PatternType);
            if (idx >= 0)
                patternDropdown.Selected = idx;

            colorPicker.Color = cue.Color;
            intensitySlider.Value = cue.Intensity;
            sizeSlider.Value = cue.Size;
            speedSlider.Value = cue.Speed;
            rotationSlider.Value = cue.Rotation;
            spreadSlider.Value = cue.Spread;
            countSpinBox.Value = cue.Count;
            fadeInSlider.Value = currentBlock.FadeInDuration;
            fadeOutSlider.Value = currentBlock.FadeOutDuration;
            zoneSpinBox.Value = currentBlock.ZoneIndex;

            isUpdating = false;
        }

        // --- Change Handlers with Undo Support ---

        private void CommitNameChange()
        {
            if (isUpdating || currentBlock?.Cue == null) return;
            var newName = nameInput.Text;
            if (newName == currentBlock.Cue.CueName) return;

            var oldName = currentBlock.Cue.CueName;
            var blk = currentBlock;
            blk.Cue.CueName = newName;

            var cmd = new ModifyBlockPropertyCommand("Rename Block",
                () => blk.Cue.CueName = newName,
                () => blk.Cue.CueName = oldName);
            UndoManager.Instance.ExecuteCommand(cmd);
        }

        private void OnNameSubmitted(string text)
        {
            CommitNameChange();
        }

        private void OnNameFocusLost()
        {
            CommitNameChange();
        }

        private void OnPatternChanged(long index)
        {
            if (isUpdating || currentBlock?.Cue == null) return;
            var types = new System.Collections.Generic.List<LaserPatternType>(PatternFactory.RegisteredTypes);
            if (index < 0 || index >= types.Count) return;

            var oldType = currentBlock.Cue.PatternType;
            var oldName = currentBlock.Cue.CueName;
            var newType = types[(int)index];
            var newName = newType.ToString();
            var blk = currentBlock;

            currentBlock.Cue.PatternType = newType;
            currentBlock.Cue.CueName = newName;
            nameInput.Text = newName;

            var cmd = new ModifyBlockPropertyCommand("Change Pattern",
                () => { blk.Cue.PatternType = newType; blk.Cue.CueName = newName; },
                () => { blk.Cue.PatternType = oldType; blk.Cue.CueName = oldName; });
            UndoManager.Instance.ExecuteCommand(cmd);
        }

        private void OnColorChanged(Color color)
        {
            if (isUpdating || currentBlock?.Cue == null) return;

            // Auto-key: insert keyframes on all 3 color channels
            if (IsAutoKeyActive && TryAutoKeyColor(color))
                return;

            var oldColor = currentBlock.Cue.Color;
            var blk = currentBlock;
            currentBlock.Cue.Color = color;

            StartOrUpdatePending("Change Color",
                () => blk.Cue.Color = color,
                () => blk.Cue.Color = oldColor);
        }

        private bool TryAutoKeyColor(Color color)
        {
            bool r = TryAutoKey("ColorR", color.R);
            bool g = TryAutoKey("ColorG", color.G);
            bool b = TryAutoKey("ColorB", color.B);
            return r || g || b;
        }

        private void OnIntensityChanged(double value)
        {
            if (isUpdating || currentBlock?.Cue == null) return;
            float newVal = (float)value;
            if (TryAutoKey("Intensity", newVal)) return;

            float oldVal = currentBlock.Cue.Intensity;
            var blk = currentBlock;
            blk.Cue.Intensity = newVal;

            StartOrUpdatePending("Change Intensity",
                () => blk.Cue.Intensity = newVal,
                () => blk.Cue.Intensity = oldVal);
        }

        private void OnSizeChanged(double value)
        {
            if (isUpdating || currentBlock?.Cue == null) return;
            float newVal = (float)value;
            if (TryAutoKey("Size", newVal)) return;

            float oldVal = currentBlock.Cue.Size;
            var blk = currentBlock;
            blk.Cue.Size = newVal;

            StartOrUpdatePending("Change Size",
                () => blk.Cue.Size = newVal,
                () => blk.Cue.Size = oldVal);
        }

        private void OnSpeedChanged(double value)
        {
            if (isUpdating || currentBlock?.Cue == null) return;
            float newVal = (float)value;
            if (TryAutoKey("Speed", newVal)) return;

            float oldVal = currentBlock.Cue.Speed;
            var blk = currentBlock;
            blk.Cue.Speed = newVal;

            StartOrUpdatePending("Change Speed",
                () => blk.Cue.Speed = newVal,
                () => blk.Cue.Speed = oldVal);
        }

        private void OnRotationChanged(double value)
        {
            if (isUpdating || currentBlock?.Cue == null) return;
            float newVal = (float)value;
            if (TryAutoKey("Rotation", newVal)) return;

            float oldVal = currentBlock.Cue.Rotation;
            var blk = currentBlock;
            blk.Cue.Rotation = newVal;

            StartOrUpdatePending("Change Rotation",
                () => blk.Cue.Rotation = newVal,
                () => blk.Cue.Rotation = oldVal);
        }

        private void OnSpreadChanged(double value)
        {
            if (isUpdating || currentBlock?.Cue == null) return;
            float newVal = (float)value;
            if (TryAutoKey("Spread", newVal)) return;

            float oldVal = currentBlock.Cue.Spread;
            var blk = currentBlock;
            blk.Cue.Spread = newVal;

            StartOrUpdatePending("Change Spread",
                () => blk.Cue.Spread = newVal,
                () => blk.Cue.Spread = oldVal);
        }

        private void OnCountChanged(double value)
        {
            if (isUpdating || currentBlock?.Cue == null) return;
            int oldVal = currentBlock.Cue.Count;
            int newVal = (int)value;
            var blk = currentBlock;
            blk.Cue.Count = newVal;

            var cmd = new ModifyBlockPropertyCommand("Change Count",
                () => blk.Cue.Count = newVal,
                () => blk.Cue.Count = oldVal);
            UndoManager.Instance.ExecuteCommand(cmd);
        }

        private void OnFadeInChanged(double value)
        {
            if (isUpdating || currentBlock == null) return;
            float oldVal = currentBlock.FadeInDuration;
            float newVal = (float)value;
            var blk = currentBlock;
            blk.FadeInDuration = newVal;

            StartOrUpdatePending("Change Fade In",
                () => blk.FadeInDuration = newVal,
                () => blk.FadeInDuration = oldVal);
        }

        private void OnFadeOutChanged(double value)
        {
            if (isUpdating || currentBlock == null) return;
            float oldVal = currentBlock.FadeOutDuration;
            float newVal = (float)value;
            var blk = currentBlock;
            blk.FadeOutDuration = newVal;

            StartOrUpdatePending("Change Fade Out",
                () => blk.FadeOutDuration = newVal,
                () => blk.FadeOutDuration = oldVal);
        }

        private void OnZoneChanged(double value)
        {
            if (isUpdating || currentBlock == null) return;
            int oldVal = currentBlock.ZoneIndex;
            int newVal = (int)value;
            var blk = currentBlock;
            blk.ZoneIndex = newVal;

            var cmd = new ModifyBlockPropertyCommand("Change Zone",
                () => blk.ZoneIndex = newVal,
                () => blk.ZoneIndex = oldVal);
            UndoManager.Instance.ExecuteCommand(cmd);
        }

        private TimelineUI GetTimelineUI()
        {
            if (timelineUI != null) return timelineUI;
            // Walk up the tree to find TimelineUI if export wasn't wired
            var parent = GetParent();
            while (parent != null)
            {
                if (parent is TimelineUI tui) return tui;
                // Check siblings
                foreach (var child in parent.GetChildren())
                {
                    if (child is TimelineUI found)
                    {
                        timelineUI = found;
                        return found;
                    }
                }
                parent = parent.GetParent();
            }
            return null;
        }

        private void OnAutomationParamSelected(long index)
        {
            // Index 0 = "Color" composite, then AutomatableParameter.All entries
            string paramName;
            if (index == 0)
                paramName = "Color";
            else if (index - 1 < AutomatableParameter.All.Length)
                paramName = AutomatableParameter.All[(int)index - 1].Name;
            else
                return;

            var tui = GetTimelineUI();
            if (tui != null)
                tui.SetActiveAutomationParam(paramName);
        }

        // --- Auto-Key Support ---

        private bool IsAutoKeyActive => autoKeyToggle != null && autoKeyToggle.ButtonPressed;

        private bool TryAutoKey(string parameterName, float value)
        {
            if (!IsAutoKeyActive || currentBlock == null || playbackManager == null)
                return false;

            float currentTime = playbackManager.CurrentTime;
            if (currentTime < currentBlock.StartTime || currentTime > currentBlock.StartTime + currentBlock.Duration)
                return false;

            float normalizedTime = (currentTime - currentBlock.StartTime) / currentBlock.Duration;
            normalizedTime = Godot.Mathf.Clamp(normalizedTime, 0f, 1f);

            var paramDef = AutomatableParameter.Find(parameterName);
            if (paramDef == null) return false;

            if (currentBlock.Automation == null)
                currentBlock.Automation = new AutomationData();

            var lane = currentBlock.Automation.GetOrCreateLane(
                parameterName, paramDef.Value.Default, paramDef.Value.Min, paramDef.Value.Max);

            // Check if there's already a keyframe near this time
            int existing = lane.FindKeyframeNear(normalizedTime, 0.01f);
            if (existing >= 0)
            {
                float oldVal = lane.Keyframes[existing].Value;
                var cmd = new MoveKeyframeCommand(lane, existing,
                    lane.Keyframes[existing].Time, oldVal,
                    normalizedTime, value);
                UndoManager.Instance.ExecuteCommand(cmd);
            }
            else
            {
                var kf = new AutomationKeyframe(normalizedTime, value);
                var cmd = new AddKeyframeCommand(lane, kf);
                UndoManager.Instance.ExecuteCommand(cmd);
            }

            return true;
        }

        public void RefreshAutomatedValues()
        {
            if (!IsAutoKeyActive || currentBlock == null || playbackManager == null)
                return;

            float currentTime = playbackManager.CurrentTime;
            if (currentTime < currentBlock.StartTime || currentTime > currentBlock.StartTime + currentBlock.Duration)
                return;

            if (currentBlock.Automation == null || !currentBlock.Automation.HasAnyAutomation)
                return;

            float normalizedTime = (currentTime - currentBlock.StartTime) / currentBlock.Duration;
            isUpdating = true;

            var intensityLane = currentBlock.Automation.GetLane("Intensity");
            if (intensityLane != null && intensityLane.Keyframes.Count > 0)
                intensitySlider.Value = intensityLane.Evaluate(normalizedTime);

            var sizeLane = currentBlock.Automation.GetLane("Size");
            if (sizeLane != null && sizeLane.Keyframes.Count > 0)
                sizeSlider.Value = sizeLane.Evaluate(normalizedTime);

            var speedLane = currentBlock.Automation.GetLane("Speed");
            if (speedLane != null && speedLane.Keyframes.Count > 0)
                speedSlider.Value = speedLane.Evaluate(normalizedTime);

            var rotationLane = currentBlock.Automation.GetLane("Rotation");
            if (rotationLane != null && rotationLane.Keyframes.Count > 0)
                rotationSlider.Value = rotationLane.Evaluate(normalizedTime);

            var spreadLane = currentBlock.Automation.GetLane("Spread");
            if (spreadLane != null && spreadLane.Keyframes.Count > 0)
                spreadSlider.Value = spreadLane.Evaluate(normalizedTime);

            isUpdating = false;
        }
    }
}
