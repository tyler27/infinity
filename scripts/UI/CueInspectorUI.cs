using Godot;
using LazerSystem.Core;
using LazerSystem.Patterns;
using LazerSystem.Timeline;

namespace LazerSystem.UI
{
    /// <summary>
    /// UI inspector panel that displays and edits the properties of a selected LaserCue.
    /// Updates the cue in real time as values change.
    /// </summary>
    public partial class CueInspectorUI : Control
    {
        [ExportGroup("References")]
        [Export] private PlaybackManager playbackManager;

        [ExportGroup("Pattern")]
        [Export] private OptionButton patternTypeDropdown;

        [ExportGroup("Color")]
        [Export] private HSlider colorRedSlider;
        [Export] private HSlider colorGreenSlider;
        [Export] private HSlider colorBlueSlider;
        [Export] private ColorRect colorPreviewRect;

        [ExportGroup("Intensity")]
        [Export] private HSlider intensitySlider;
        [Export] private Label intensityValueText;

        [ExportGroup("Size")]
        [Export] private HSlider sizeSlider;
        [Export] private Label sizeValueText;

        [ExportGroup("Rotation")]
        [Export] private HSlider rotationSlider;
        [Export] private Label rotationValueText;

        [ExportGroup("Speed")]
        [Export] private HSlider speedSlider;
        [Export] private Label speedValueText;

        [ExportGroup("Count")]
        [Export] private LineEdit countField;

        [ExportGroup("Spread")]
        [Export] private HSlider spreadSlider;
        [Export] private Label spreadValueText;

        [ExportGroup("Frequency")]
        [Export] private HSlider frequencySlider;
        [Export] private Label frequencyValueText;

        [ExportGroup("Amplitude")]
        [Export] private HSlider amplitudeSlider;
        [Export] private Label amplitudeValueText;

        [ExportGroup("Position")]
        [Export] private HSlider positionXSlider;
        [Export] private HSlider positionYSlider;
        [Export] private Label positionValueText;

        [ExportGroup("Preview")]
        [Export] private Button previewButton;

        [ExportGroup("Info")]
        [Export] private Label cueNameText;

        private LaserCue selectedCue;
        private bool isUpdating; // Guard against recursive updates

        public override void _Ready()
        {
            SetupPatternDropdown();
            SetupSliders();
            SetupListeners();
            SetInteractable(false);
        }

        public override void _ExitTree()
        {
            RemoveListeners();
        }

        /// <summary>Sets the cue to inspect and updates all UI fields.</summary>
        public void SetCue(LaserCue cue)
        {
            selectedCue = cue;

            if (cue == null)
            {
                SetInteractable(false);
                if (cueNameText != null) cueNameText.Text = "No cue selected";
                return;
            }

            SetInteractable(true);
            RefreshFromCue();
        }

        /// <summary>Returns the currently selected cue.</summary>
        public LaserCue SelectedCue => selectedCue;

        /// <summary>Populates all UI fields from the selected cue's values.</summary>
        private void RefreshFromCue()
        {
            if (selectedCue == null)
                return;

            isUpdating = true;

            if (cueNameText != null)
                cueNameText.Text = selectedCue.CueName;

            if (patternTypeDropdown != null)
                patternTypeDropdown.Selected = (int)selectedCue.PatternType;

            if (colorRedSlider != null) colorRedSlider.Value = selectedCue.Color.R;
            if (colorGreenSlider != null) colorGreenSlider.Value = selectedCue.Color.G;
            if (colorBlueSlider != null) colorBlueSlider.Value = selectedCue.Color.B;
            UpdateColorPreview();

            if (intensitySlider != null) intensitySlider.Value = selectedCue.Intensity;
            if (sizeSlider != null) sizeSlider.Value = selectedCue.Size;
            if (rotationSlider != null) rotationSlider.Value = selectedCue.Rotation;
            if (speedSlider != null) speedSlider.Value = selectedCue.Speed;
            if (spreadSlider != null) spreadSlider.Value = selectedCue.Spread;
            if (frequencySlider != null) frequencySlider.Value = selectedCue.Frequency;
            if (amplitudeSlider != null) amplitudeSlider.Value = selectedCue.Amplitude;
            if (positionXSlider != null) positionXSlider.Value = selectedCue.Position.X;
            if (positionYSlider != null) positionYSlider.Value = selectedCue.Position.Y;

            if (countField != null)
                countField.Text = selectedCue.Count.ToString();

            UpdateValueLabels();

            isUpdating = false;
        }

        private void SetupPatternDropdown()
        {
            if (patternTypeDropdown == null)
                return;

            patternTypeDropdown.Clear();
            var names = System.Enum.GetNames(typeof(LaserPatternType));
            foreach (var name in names)
            {
                patternTypeDropdown.AddItem(name);
            }
        }

        private void SetupSliders()
        {
            // Color sliders: 0-1
            SetSliderRange(colorRedSlider, 0f, 1f);
            SetSliderRange(colorGreenSlider, 0f, 1f);
            SetSliderRange(colorBlueSlider, 0f, 1f);

            // Intensity: 0-1
            SetSliderRange(intensitySlider, 0f, 1f);

            // Size: 0-1
            SetSliderRange(sizeSlider, 0f, 1f);

            // Rotation: -360 to 360
            SetSliderRange(rotationSlider, -360f, 360f);

            // Speed: 0-10
            SetSliderRange(speedSlider, 0f, 10f);

            // Spread: 0-1
            SetSliderRange(spreadSlider, 0f, 1f);

            // Frequency: 0-20
            SetSliderRange(frequencySlider, 0f, 20f);

            // Amplitude: 0-1
            SetSliderRange(amplitudeSlider, 0f, 1f);

            // Position: -1 to 1
            SetSliderRange(positionXSlider, -1f, 1f);
            SetSliderRange(positionYSlider, -1f, 1f);
        }

        private void SetSliderRange(HSlider slider, float min, float max)
        {
            if (slider == null) return;
            slider.MinValue = min;
            slider.MaxValue = max;
        }

        private void SetupListeners()
        {
            if (patternTypeDropdown != null)
                patternTypeDropdown.ItemSelected += OnPatternTypeChanged;

            if (colorRedSlider != null) colorRedSlider.ValueChanged += _ => OnColorChanged();
            if (colorGreenSlider != null) colorGreenSlider.ValueChanged += _ => OnColorChanged();
            if (colorBlueSlider != null) colorBlueSlider.ValueChanged += _ => OnColorChanged();

            if (intensitySlider != null) intensitySlider.ValueChanged += OnIntensityChanged;
            if (sizeSlider != null) sizeSlider.ValueChanged += OnSizeChanged;
            if (rotationSlider != null) rotationSlider.ValueChanged += OnRotationChanged;
            if (speedSlider != null) speedSlider.ValueChanged += OnSpeedChanged;
            if (spreadSlider != null) spreadSlider.ValueChanged += OnSpreadChanged;
            if (frequencySlider != null) frequencySlider.ValueChanged += OnFrequencyChanged;
            if (amplitudeSlider != null) amplitudeSlider.ValueChanged += OnAmplitudeChanged;
            if (positionXSlider != null) positionXSlider.ValueChanged += _ => OnPositionChanged();
            if (positionYSlider != null) positionYSlider.ValueChanged += _ => OnPositionChanged();

            if (countField != null) countField.TextSubmitted += OnCountChanged;
            if (previewButton != null) previewButton.Pressed += OnPreviewClicked;
        }

        private void RemoveListeners()
        {
            if (patternTypeDropdown != null)
                patternTypeDropdown.ItemSelected -= OnPatternTypeChanged;

            // Slider signals are disconnected automatically when nodes exit tree,
            // but we can explicitly disconnect for safety
            if (countField != null) countField.TextSubmitted -= OnCountChanged;
            if (previewButton != null) previewButton.Pressed -= OnPreviewClicked;
        }

        private void OnPatternTypeChanged(long index)
        {
            if (isUpdating || selectedCue == null) return;
            selectedCue.PatternType = (LaserPatternType)(int)index;
        }

        private void OnColorChanged()
        {
            if (isUpdating || selectedCue == null) return;

            selectedCue.Color = new Color(
                colorRedSlider != null ? (float)colorRedSlider.Value : 1f,
                colorGreenSlider != null ? (float)colorGreenSlider.Value : 1f,
                colorBlueSlider != null ? (float)colorBlueSlider.Value : 1f
            );

            UpdateColorPreview();
            UpdateValueLabels();
        }

        private void OnIntensityChanged(double value)
        {
            if (isUpdating || selectedCue == null) return;
            selectedCue.Intensity = (float)value;
            UpdateValueLabels();
        }

        private void OnSizeChanged(double value)
        {
            if (isUpdating || selectedCue == null) return;
            selectedCue.Size = (float)value;
            UpdateValueLabels();
        }

        private void OnRotationChanged(double value)
        {
            if (isUpdating || selectedCue == null) return;
            selectedCue.Rotation = (float)value;
            UpdateValueLabels();
        }

        private void OnSpeedChanged(double value)
        {
            if (isUpdating || selectedCue == null) return;
            selectedCue.Speed = (float)value;
            UpdateValueLabels();
        }

        private void OnSpreadChanged(double value)
        {
            if (isUpdating || selectedCue == null) return;
            selectedCue.Spread = (float)value;
            UpdateValueLabels();
        }

        private void OnFrequencyChanged(double value)
        {
            if (isUpdating || selectedCue == null) return;
            selectedCue.Frequency = (float)value;
            UpdateValueLabels();
        }

        private void OnAmplitudeChanged(double value)
        {
            if (isUpdating || selectedCue == null) return;
            selectedCue.Amplitude = (float)value;
            UpdateValueLabels();
        }

        private void OnPositionChanged()
        {
            if (isUpdating || selectedCue == null) return;

            selectedCue.Position = new Vector2(
                positionXSlider != null ? (float)positionXSlider.Value : 0f,
                positionYSlider != null ? (float)positionYSlider.Value : 0f
            );
            UpdateValueLabels();
        }

        private void OnCountChanged(string value)
        {
            if (isUpdating || selectedCue == null) return;

            if (int.TryParse(value, out int count))
            {
                selectedCue.Count = Mathf.Max(1, count);
            }
        }

        private void OnPreviewClicked()
        {
            if (selectedCue == null || playbackManager == null)
                return;

            // Generate preview pattern and send to preview manager
            var parameters = PatternParameters.FromCue(selectedCue);
            GD.Print($"[CueInspectorUI] Preview cue: {selectedCue.CueName} ({selectedCue.PatternType})");
        }

        private void UpdateColorPreview()
        {
            if (colorPreviewRect == null)
                return;

            colorPreviewRect.Color = new Color(
                colorRedSlider != null ? (float)colorRedSlider.Value : 1f,
                colorGreenSlider != null ? (float)colorGreenSlider.Value : 1f,
                colorBlueSlider != null ? (float)colorBlueSlider.Value : 1f
            );
        }

        private void UpdateValueLabels()
        {
            if (intensityValueText != null && intensitySlider != null)
                intensityValueText.Text = $"{intensitySlider.Value:P0}";

            if (sizeValueText != null && sizeSlider != null)
                sizeValueText.Text = $"{sizeSlider.Value:F2}";

            if (rotationValueText != null && rotationSlider != null)
                rotationValueText.Text = $"{rotationSlider.Value:F1}\u00B0";

            if (speedValueText != null && speedSlider != null)
                speedValueText.Text = $"{speedSlider.Value:F2}";

            if (spreadValueText != null && spreadSlider != null)
                spreadValueText.Text = $"{spreadSlider.Value:F2}";

            if (frequencyValueText != null && frequencySlider != null)
                frequencyValueText.Text = $"{frequencySlider.Value:F2}";

            if (amplitudeValueText != null && amplitudeSlider != null)
                amplitudeValueText.Text = $"{amplitudeSlider.Value:F2}";

            if (positionValueText != null)
            {
                float px = positionXSlider != null ? (float)positionXSlider.Value : 0f;
                float py = positionYSlider != null ? (float)positionYSlider.Value : 0f;
                positionValueText.Text = $"({px:F2}, {py:F2})";
            }
        }

        private void SetInteractable(bool interactable)
        {
            if (patternTypeDropdown != null) patternTypeDropdown.Disabled = !interactable;
            if (colorRedSlider != null) colorRedSlider.Editable = interactable;
            if (colorGreenSlider != null) colorGreenSlider.Editable = interactable;
            if (colorBlueSlider != null) colorBlueSlider.Editable = interactable;
            if (intensitySlider != null) intensitySlider.Editable = interactable;
            if (sizeSlider != null) sizeSlider.Editable = interactable;
            if (rotationSlider != null) rotationSlider.Editable = interactable;
            if (speedSlider != null) speedSlider.Editable = interactable;
            if (spreadSlider != null) spreadSlider.Editable = interactable;
            if (frequencySlider != null) frequencySlider.Editable = interactable;
            if (amplitudeSlider != null) amplitudeSlider.Editable = interactable;
            if (positionXSlider != null) positionXSlider.Editable = interactable;
            if (positionYSlider != null) positionYSlider.Editable = interactable;
            if (countField != null) countField.Editable = interactable;
            if (previewButton != null) previewButton.Disabled = !interactable;
        }
    }
}
