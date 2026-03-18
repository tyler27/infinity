using Godot;
using LazerSystem.Sync;
using LazerSystem.Timeline;

namespace LazerSystem.UI
{
    /// <summary>
    /// UI controller for the transport bar providing playback controls,
    /// time display, BPM settings, and sync source selection.
    /// </summary>
    public partial class TransportBarUI : Control
    {
        [ExportGroup("References")]
        [Export] private SyncManager syncManager;
        [Export] private PlaybackManager playbackManager;

        [ExportGroup("Playback Buttons")]
        [Export] private Button playButton;
        [Export] private Button pauseButton;
        [Export] private Button stopButton;

        [ExportGroup("Time Display")]
        [Export] private Label timeDisplayText;
        [Export] private Label beatDisplayText;

        [ExportGroup("BPM")]
        [Export] private LineEdit bpmInputField;
        [Export] private Label bpmDisplayText;

        [ExportGroup("Sync Source")]
        [Export] private OptionButton syncSourceDropdown;

        [ExportGroup("Timeline Position")]
        [Export] private HSlider timelinePositionSlider;

        [ExportGroup("Loop")]
        [Export] private Button loopToggleButton;
        [Export] private Label loopButtonText;

        [ExportGroup("File")]
        [Export] private Button saveButton;
        [Export] private Button loadButton;

        private bool isLooping;
        private bool isDraggingSlider;
        private FileDialog saveDialog;
        private FileDialog loadDialog;

        public override void _Ready()
        {
            SetupButtonListeners();
            SetupSyncDropdown();
            SetupBpmField();
            SetupSlider();
            SetupFileDialogs();
        }

        public override void _ExitTree()
        {
            RemoveButtonListeners();
        }

        private void SetupButtonListeners()
        {
            if (playButton != null)
                playButton.Pressed += OnPlayClicked;

            if (pauseButton != null)
                pauseButton.Pressed += OnPauseClicked;

            if (stopButton != null)
                stopButton.Pressed += OnStopClicked;

            if (loopToggleButton != null)
                loopToggleButton.Pressed += OnLoopToggleClicked;

            if (saveButton != null)
                saveButton.Pressed += OnSaveClicked;

            if (loadButton != null)
                loadButton.Pressed += OnLoadClicked;
        }

        private void RemoveButtonListeners()
        {
            if (playButton != null)
                playButton.Pressed -= OnPlayClicked;

            if (pauseButton != null)
                pauseButton.Pressed -= OnPauseClicked;

            if (stopButton != null)
                stopButton.Pressed -= OnStopClicked;

            if (loopToggleButton != null)
                loopToggleButton.Pressed -= OnLoopToggleClicked;

            if (saveButton != null)
                saveButton.Pressed -= OnSaveClicked;

            if (loadButton != null)
                loadButton.Pressed -= OnLoadClicked;
        }

        private void SetupSyncDropdown()
        {
            if (syncSourceDropdown == null)
                return;

            syncSourceDropdown.Clear();
            syncSourceDropdown.AddItem("Internal");
            syncSourceDropdown.AddItem("MTC");
            syncSourceDropdown.AddItem("ArtNet TC");

            syncSourceDropdown.ItemSelected += OnSyncSourceChanged;
        }

        private void SetupBpmField()
        {
            if (bpmInputField == null)
                return;

            bpmInputField.TextSubmitted += OnBpmChanged;

            if (playbackManager != null)
            {
                bpmInputField.Text = playbackManager.BPM.ToString("F1");
            }
        }

        private void SetupSlider()
        {
            if (timelinePositionSlider == null)
                return;

            timelinePositionSlider.MinValue = 0f;
            timelinePositionSlider.MaxValue = 1f;

            timelinePositionSlider.DragStarted += () => isDraggingSlider = true;
            timelinePositionSlider.DragEnded += (valueChanged) =>
            {
                isDraggingSlider = false;
                OnSliderValueChanged((float)timelinePositionSlider.Value);
            };
        }

        public override void _Process(double delta)
        {
            UpdateTimeDisplay();
            UpdateSliderPosition();
            UpdateBeatDisplay();
        }

        /// <summary>Updates the time display text in MM:SS:FF format.</summary>
        private void UpdateTimeDisplay()
        {
            if (timeDisplayText == null || syncManager == null)
                return;

            float currentTime = syncManager.CurrentTime;
            int totalFrames = Mathf.FloorToInt(currentTime * 30f); // 30 fps
            int minutes = Mathf.FloorToInt(currentTime / 60f);
            int seconds = Mathf.FloorToInt(currentTime % 60f);
            int frames = totalFrames % 30;

            timeDisplayText.Text = $"{minutes:D2}:{seconds:D2}:{frames:D2}";
        }

        /// <summary>Updates the beat display text.</summary>
        private void UpdateBeatDisplay()
        {
            if (beatDisplayText == null || playbackManager == null)
                return;

            int beat = playbackManager.CurrentBeat;
            int bar = beat / 4 + 1;
            int beatInBar = beat % 4 + 1;
            beatDisplayText.Text = $"{bar}.{beatInBar}";
        }

        /// <summary>Updates the timeline slider position to reflect current time.</summary>
        private void UpdateSliderPosition()
        {
            if (timelinePositionSlider == null || syncManager == null || isDraggingSlider)
                return;

            float duration = syncManager.Duration;
            if (duration > 0f)
            {
                timelinePositionSlider.Value = syncManager.CurrentTime / duration;
            }
        }

        private void OnPlayClicked()
        {
            if (playbackManager != null)
            {
                playbackManager.PlayShow();
            }
        }

        private void OnPauseClicked()
        {
            if (playbackManager != null)
            {
                playbackManager.PauseShow();
            }
        }

        private void OnStopClicked()
        {
            if (playbackManager != null)
            {
                playbackManager.StopShow();
            }
        }

        private void OnLoopToggleClicked()
        {
            isLooping = !isLooping;

            if (syncManager != null)
            {
                syncManager.SetLooping(isLooping);
            }

            if (loopButtonText != null)
            {
                loopButtonText.Text = isLooping ? "Loop: ON" : "Loop: OFF";
            }
        }

        private void OnSyncSourceChanged(long index)
        {
            if (syncManager == null)
                return;

            switch ((int)index)
            {
                case 0:
                    syncManager.SetSyncSource(SyncManager.SyncSource.Internal);
                    break;
                case 1:
                    syncManager.SetSyncSource(SyncManager.SyncSource.MidiTimeCode);
                    break;
                case 2:
                    syncManager.SetSyncSource(SyncManager.SyncSource.ArtNetTimeCode);
                    break;
            }
        }

        private void OnBpmChanged(string value)
        {
            if (float.TryParse(value, out float bpm))
            {
                bpm = Mathf.Clamp(bpm, 20f, 999f);

                if (playbackManager != null && playbackManager.LaserShow != null)
                {
                    playbackManager.LaserShow.Bpm = bpm;
                }

                if (bpmDisplayText != null)
                {
                    bpmDisplayText.Text = $"{bpm:F1} BPM";
                }
            }
        }

        private void OnSliderValueChanged(float normalizedPosition)
        {
            if (syncManager == null)
                return;

            float targetTime = normalizedPosition * syncManager.Duration;
            syncManager.Seek(targetTime);
        }

        private void SetupFileDialogs()
        {
            saveDialog = new FileDialog
            {
                FileMode = FileDialog.FileModeEnum.SaveFile,
                Title = "Save Show",
                Access = FileDialog.AccessEnum.Userdata,
                CurrentDir = "user://shows",
            };
            saveDialog.AddFilter("*.tres ; Godot Resource");
            saveDialog.FileSelected += OnSaveFileSelected;
            AddChild(saveDialog);

            loadDialog = new FileDialog
            {
                FileMode = FileDialog.FileModeEnum.OpenFile,
                Title = "Load Show",
                Access = FileDialog.AccessEnum.Userdata,
                CurrentDir = "user://shows",
            };
            loadDialog.AddFilter("*.tres ; Godot Resource");
            loadDialog.FileSelected += OnLoadFileSelected;
            AddChild(loadDialog);
        }

        private void OnSaveClicked()
        {
            // Ensure shows directory exists
            DirAccess.MakeDirAbsolute("user://shows");
            saveDialog.PopupCentered(new Vector2I(600, 400));
        }

        private void OnLoadClicked()
        {
            loadDialog.PopupCentered(new Vector2I(600, 400));
        }

        private void OnSaveFileSelected(string path)
        {
            if (playbackManager != null)
                playbackManager.SaveShow(path);
        }

        private void OnLoadFileSelected(string path)
        {
            if (playbackManager != null)
            {
                playbackManager.LoadShow(path);
                // Update BPM display
                if (bpmInputField != null)
                    bpmInputField.Text = playbackManager.BPM.ToString("F1");
                if (bpmDisplayText != null)
                    bpmDisplayText.Text = $"{playbackManager.BPM:F1} BPM";
            }
        }
    }
}
