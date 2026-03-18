using System.Collections.Generic;
using System.Linq;
using Godot;
using LazerSystem.Core;
using LazerSystem.Timeline;
using LazerSystem.Timeline.Commands;
using LazerSystem.Sync;
using LazerSystem.Patterns;

namespace LazerSystem.UI
{
    public partial class TimelineUI : Control
    {
        [ExportGroup("References")]
        [Export] public PlaybackManager playbackManager;
        [Export] public SyncManager syncManager;
        [Export] public BlockInspectorPanel inspectorPanel;

        [ExportGroup("Layout")]
        [Export] private Control timelineViewport;
        [Export] private float trackHeaderWidth = 120f;

        [ExportGroup("Zoom & Scroll")]
        [Export] private float pixelsPerSecond = 100f;
        [Export] private float scrollOffset;
        [Export] private float minPixelsPerSecond = 20f;
        [Export] private float maxPixelsPerSecond = 500f;
        [Export] private float zoomSpeed = 20f;

        [ExportGroup("Snap")]
        [Export] private bool snapToGrid = true;
        [Export] private float snapBeatDivision = 1f;

        [ExportGroup("Visual Settings")]
        [Export] private Color playheadColor = Colors.Red;
        [Export] private Color beatLineColor = new Color(1f, 1f, 1f, 0.15f);
        [Export] private Color barLineColor = new Color(1f, 1f, 1f, 0.4f);
        [Export] private float trackHeight = 40f;
        [Export] private Color trackColor = new Color(0.2f, 0.2f, 0.25f, 1f);
        [Export] private Color trackMutedColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        [Export] private Color trackHeaderBg = new Color(0.12f, 0.12f, 0.15f, 1f);
        [Export] private Color trackSoloColor = new Color(0.9f, 0.8f, 0.2f, 1f);

        public event System.Action<LaserCueBlock> OnBlockSelected;

        // Ruler
        private const float RulerHeight = 20f;

        // Selection / drag
        private LaserCueBlock selectedBlock;
        private HashSet<LaserCueBlock> selectedBlocks = new();
        private LaserCueBlock draggingBlock;
        private bool isDragging;
        private float dragStartTime;
        private float dragOffsetX;
        private Dictionary<LaserCueBlock, float> multiDragOriginalStarts = new();

        // Rubber-band selection
        private bool isRubberBanding;
        private Vector2 rubberBandStart;
        private Vector2 rubberBandEnd;

        // Resize state
        private enum ResizeEdge { None, Left, Right }
        private ResizeEdge resizeEdge = ResizeEdge.None;
        private bool isResizing;
        private LaserCueBlock resizingBlock;
        private float resizeOriginalStart;
        private float resizeOriginalDuration;
        private const float EdgeThresholdPx = 6f;
        private Dictionary<LaserCueBlock, (float start, float duration)> multiResizeOriginals = new();

        // Context menu (pattern placement)
        private PopupMenu contextMenu;
        private float contextClickTime;
        private int contextClickTrack;

        // Block context menu
        private PopupMenu blockContextMenu;
        private LaserCueBlock contextClickBlock;

        // Track header context menu
        private PopupMenu trackHeaderMenu;
        private int trackHeaderMenuIndex;

        // Ruler context menu
        private PopupMenu rulerContextMenu;
        private float rulerContextClickTime;

        // Font
        private Font _font;

        // Draw mode
        private bool drawModeEnabled;
        private bool isDrawing;
        private float drawStartTime;
        private int drawTrackIndex;
        private float drawCurrentTime;
        private LaserPatternType lastUsedPatternType = LaserPatternType.Beam;

        // Waveform
        private float[] waveformPeaks;

        // Loop region dragging
        private enum LoopDragHandle { None, Start, End }
        private LoopDragHandle loopDragHandle = LoopDragHandle.None;
        private bool isDraggingLoop;
        private float loopDragAnchorTime; // the initial click time when Ctrl+click-dragging a new loop

        // Automation mode
        private bool automationEditMode;
        private string activeAutomationParam = "Intensity";
        private int selectedKeyframeIndex = -1;
        private AutomationLane selectedKeyframeLane;
        private bool isDraggingKeyframe;
        private float dragKfOriginalTime;
        private float dragKfOriginalValue;

        // Keyframe context menu
        private PopupMenu keyframeContextMenu;
        private AutomationKeyframe contextKeyframe;
        private AutomationLane contextKeyframeLane;
        private int contextKeyframeIndex;

        // Color automation picker popup
        private PopupPanel colorAutoPopup;
        private ColorPicker colorAutoPicker;
        private float colorAutoNormalizedTime;

        public override void _Ready()
        {
            _font = ThemeDB.FallbackFont;
            FocusMode = FocusModeEnum.All;

            // Pattern placement context menu
            contextMenu = new PopupMenu();
            AddChild(contextMenu);
            contextMenu.IdPressed += OnContextMenuItemSelected;

            // Block context menu (right-click on block)
            blockContextMenu = new PopupMenu();
            AddChild(blockContextMenu);
            blockContextMenu.IdPressed += OnBlockContextMenuItemSelected;

            // Track header context menu
            trackHeaderMenu = new PopupMenu();
            AddChild(trackHeaderMenu);
            trackHeaderMenu.IdPressed += OnTrackHeaderMenuItemSelected;

            // Ruler context menu
            rulerContextMenu = new PopupMenu();
            AddChild(rulerContextMenu);
            rulerContextMenu.IdPressed += OnRulerContextMenuItemSelected;

            // Keyframe context menu
            keyframeContextMenu = new PopupMenu();
            AddChild(keyframeContextMenu);
            keyframeContextMenu.IdPressed += OnKeyframeContextMenuItemSelected;

            // Color automation picker
            colorAutoPopup = new PopupPanel();
            colorAutoPopup.Size = new Vector2I(250, 280);
            var colorVBox = new VBoxContainer();
            colorAutoPicker = new ColorPicker();
            colorAutoPicker.CustomMinimumSize = new Vector2(240, 220);
            colorVBox.AddChild(colorAutoPicker);
            var confirmBtn = new Button { Text = "Add Color Keyframe" };
            confirmBtn.Pressed += OnColorAutoConfirmed;
            colorVBox.AddChild(confirmBtn);
            colorAutoPopup.AddChild(colorVBox);
            AddChild(colorAutoPopup);
        }

        private bool _dirty = true;
        private float _lastPlayheadTime = -1f;

        private void MarkDirty() { _dirty = true; }

        public override void _Process(double delta)
        {
            // Only redraw when something changed
            if (syncManager != null)
            {
                float t = syncManager.CurrentTime;
                if (t != _lastPlayheadTime)
                {
                    _lastPlayheadTime = t;
                    _dirty = true;
                }
            }

            if (_dirty)
            {
                _dirty = false;
                QueueRedraw();
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!HasFocus())
                return;

            if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
            {
                // Undo/Redo
                if (keyEvent.CtrlPressed && keyEvent.Keycode == Key.Z)
                {
                    UndoManager.Instance.Undo();
                    GetViewport().SetInputAsHandled();
                    return;
                }
                if (keyEvent.CtrlPressed && keyEvent.Keycode == Key.Y)
                {
                    UndoManager.Instance.Redo();
                    GetViewport().SetInputAsHandled();
                    return;
                }

                // Deselect all
                if (keyEvent.CtrlPressed && keyEvent.Keycode == Key.D)
                {
                    SelectBlock(null);
                    if (syncManager != null && syncManager.HasLoopRegion)
                        syncManager.ClearLoopRegion();
                    GetViewport().SetInputAsHandled();
                    return;
                }

                // Copy/Paste
                if (keyEvent.CtrlPressed && keyEvent.Keycode == Key.C && selectedBlock != null)
                {
                    if (selectedBlocks.Count > 1)
                        ClipboardManager.CopyMultiple(selectedBlocks);
                    else
                        ClipboardManager.Copy(selectedBlock);
                    GetViewport().SetInputAsHandled();
                    return;
                }
                if (keyEvent.CtrlPressed && keyEvent.Keycode == Key.V && ClipboardManager.HasContent)
                {
                    PasteAtPlayhead();
                    GetViewport().SetInputAsHandled();
                    return;
                }

                // Loop region
                if (keyEvent.CtrlPressed && keyEvent.Keycode == Key.B && syncManager != null)
                {
                    syncManager.LoopStartTime = syncManager.CurrentTime;
                    GetViewport().SetInputAsHandled();
                    return;
                }
                if (keyEvent.CtrlPressed && keyEvent.Keycode == Key.E && syncManager != null)
                {
                    syncManager.LoopEndTime = syncManager.CurrentTime;
                    GetViewport().SetInputAsHandled();
                    return;
                }
                if (keyEvent.CtrlPressed && keyEvent.Keycode == Key.L && syncManager != null)
                {
                    syncManager.ClearLoopRegion();
                    GetViewport().SetInputAsHandled();
                    return;
                }

                // Add marker at playhead
                if (keyEvent.Keycode == Key.Enter && syncManager != null && playbackManager?.LaserShow != null)
                {
                    AddMarkerAtPlayhead();
                    GetViewport().SetInputAsHandled();
                    return;
                }

                // Toggle automation edit mode
                if (keyEvent.Keycode == Key.A && !keyEvent.CtrlPressed)
                {
                    automationEditMode = !automationEditMode;
                    selectedKeyframeIndex = -1;
                    selectedKeyframeLane = null;
                    GetViewport().SetInputAsHandled();
                    return;
                }

                // Delete keyframe or block
                if (keyEvent.Keycode == Key.Delete)
                {
                    if (automationEditMode && selectedKeyframeIndex >= 0 && selectedKeyframeLane != null)
                    {
                        var cmd = new RemoveKeyframeCommand(selectedKeyframeLane, selectedKeyframeIndex);
                        UndoManager.Instance.ExecuteCommand(cmd);
                        selectedKeyframeIndex = -1;
                        selectedKeyframeLane = null;
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                    if (selectedBlocks.Count > 1)
                    {
                        DeleteSelectedBlocks();
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                    if (selectedBlock != null)
                    {
                        DeleteSelectedBlock();
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                }
            }
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButton)
            {
                HandleMouseButton(mouseButton);
                _dirty = true;
            }
            else if (@event is InputEventMouseMotion mouseMotion)
            {
                HandleMouseMotion(mouseMotion);
                _dirty = true;
            }
        }

        // --- Draw Mode ---

        public void SetDrawMode(bool enabled)
        {
            drawModeEnabled = enabled;
            if (!enabled)
                isDrawing = false;
        }

        public bool IsDrawModeEnabled => drawModeEnabled;

        // --- Waveform ---

        public void SetWaveformData(float[] peaks)
        {
            waveformPeaks = peaks;
        }

        // --- Mouse Handling ---

        private void HandleMouseButton(InputEventMouseButton mouseButton)
        {
            if (mouseButton.Pressed)
            {
                if (mouseButton.ButtonIndex == MouseButton.WheelUp)
                {
                    if (mouseButton.CtrlPressed)
                    {
                        pixelsPerSecond = Mathf.Clamp(
                            pixelsPerSecond + zoomSpeed * pixelsPerSecond * 0.01f,
                            minPixelsPerSecond, maxPixelsPerSecond);
                    }
                    else
                    {
                        scrollOffset -= 200f / pixelsPerSecond;
                        scrollOffset = Mathf.Max(0f, scrollOffset);
                    }
                    AcceptEvent();
                }
                else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
                {
                    if (mouseButton.CtrlPressed)
                    {
                        pixelsPerSecond = Mathf.Clamp(
                            pixelsPerSecond - zoomSpeed * pixelsPerSecond * 0.01f,
                            minPixelsPerSecond, maxPixelsPerSecond);
                    }
                    else
                    {
                        scrollOffset += 200f / pixelsPerSecond;
                    }
                    AcceptEvent();
                }
                else if (mouseButton.ButtonIndex == MouseButton.Left)
                {
                    GrabFocus();
                    HandleLeftClick(mouseButton.Position, mouseButton.CtrlPressed, mouseButton.ShiftPressed);
                    AcceptEvent();
                }
                else if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
                {
                    GrabFocus();
                    HandleRightClick(mouseButton.Position, mouseButton.GlobalPosition);
                    AcceptEvent();
                }
            }
            else if (!mouseButton.Pressed)
            {
                if (isDraggingKeyframe && selectedKeyframeLane != null && selectedKeyframeIndex >= 0)
                {
                    var kf = selectedKeyframeLane.Keyframes[selectedKeyframeIndex];
                    float newTime = kf.Time;
                    float newValue = kf.Value;
                    if (newTime != dragKfOriginalTime || newValue != dragKfOriginalValue)
                    {
                        // Revert so command.Execute sets new values
                        kf.Time = dragKfOriginalTime;
                        kf.Value = dragKfOriginalValue;
                        var cmd = new MoveKeyframeCommand(selectedKeyframeLane, selectedKeyframeIndex,
                            dragKfOriginalTime, dragKfOriginalValue, newTime, newValue);
                        UndoManager.Instance.ExecuteCommand(cmd);
                    }
                    isDraggingKeyframe = false;
                }
                if (isResizing)
                {
                    // Commit resize command(s)
                    if (multiResizeOriginals.Count > 1)
                    {
                        // Multi-block resize: compound command
                        var commands = new List<ITimelineCommand>();
                        bool anyChanged = false;
                        foreach (var kvp in multiResizeOriginals)
                        {
                            var blk = kvp.Key;
                            float origStart = kvp.Value.start;
                            float origDur = kvp.Value.duration;
                            float curStart = blk.StartTime;
                            float curDur = blk.Duration;
                            if (curStart != origStart || curDur != origDur)
                            {
                                blk.StartTime = origStart;
                                blk.Duration = origDur;
                                commands.Add(new ResizeBlockCommand(blk, origStart, origDur, curStart, curDur));
                                anyChanged = true;
                            }
                        }
                        if (anyChanged)
                        {
                            var compound = new CompoundCommand("Resize Blocks", commands);
                            UndoManager.Instance.ExecuteCommand(compound);
                        }
                    }
                    else if (resizingBlock != null)
                    {
                        float newStart = resizingBlock.StartTime;
                        float newDuration = resizingBlock.Duration;
                        if (newStart != resizeOriginalStart || newDuration != resizeOriginalDuration)
                        {
                            resizingBlock.StartTime = resizeOriginalStart;
                            resizingBlock.Duration = resizeOriginalDuration;
                            var cmd = new ResizeBlockCommand(resizingBlock, resizeOriginalStart, resizeOriginalDuration, newStart, newDuration);
                            UndoManager.Instance.ExecuteCommand(cmd);
                        }
                    }
                    isResizing = false;
                    resizingBlock = null;
                    resizeEdge = ResizeEdge.None;
                    multiResizeOriginals.Clear();
                }
                if (isDragging)
                {
                    // Commit move command(s)
                    if (draggingBlock != null && selectedBlocks.Count > 1 && multiDragOriginalStarts.Count > 0)
                    {
                        // Multi-block move: create compound command
                        var commands = new List<ITimelineCommand>();
                        bool anyMoved = false;
                        foreach (var blk in selectedBlocks)
                        {
                            if (multiDragOriginalStarts.TryGetValue(blk, out float origStart))
                            {
                                float curStart = blk.StartTime;
                                if (curStart != origStart)
                                {
                                    blk.StartTime = origStart;
                                    commands.Add(new MoveBlockCommand(blk, origStart, curStart));
                                    anyMoved = true;
                                }
                            }
                        }
                        if (anyMoved)
                        {
                            var compound = new CompoundCommand("Move Blocks", commands);
                            UndoManager.Instance.ExecuteCommand(compound);
                        }
                        multiDragOriginalStarts.Clear();
                    }
                    else if (draggingBlock != null)
                    {
                        float newStart = draggingBlock.StartTime;
                        if (newStart != dragStartTime)
                        {
                            draggingBlock.StartTime = dragStartTime;
                            var cmd = new MoveBlockCommand(draggingBlock, dragStartTime, newStart);
                            UndoManager.Instance.ExecuteCommand(cmd);
                        }
                    }
                    isDragging = false;
                    draggingBlock = null;
                }
                if (isDrawing)
                {
                    FinishDrawing();
                }
                if (isDraggingLoop)
                {
                    isDraggingLoop = false;
                    loopDragHandle = LoopDragHandle.None;
                    loopDragAnchorTime = -1f;

                    // Clear if region is too tiny (< 0.05s)
                    if (syncManager != null && syncManager.LoopEndTime - syncManager.LoopStartTime < 0.05f)
                    {
                        syncManager.ClearLoopRegion();
                    }
                }
                if (isRubberBanding)
                {
                    isRubberBanding = false;
                }
            }
        }

        private void HandleMouseMotion(InputEventMouseMotion mouseMotion)
        {
            var pos = mouseMotion.Position;

            if (isDraggingKeyframe && selectedKeyframeLane != null && selectedKeyframeIndex >= 0 && selectedBlock != null)
            {
                float contentX = pos.X - trackHeaderWidth;
                float blockXPos = (selectedBlock.StartTime - scrollOffset) * pixelsPerSecond;
                float blockWidth = selectedBlock.Duration * pixelsPerSecond;
                float normalizedTime = Mathf.Clamp((contentX - blockXPos) / blockWidth, 0f, 1f);

                float trackY = selectedBlock.TrackIndex * trackHeight + RulerHeight + 2;
                float blockH = trackHeight - 4;
                float yInBlock = pos.Y - trackY;
                float valueFraction = 1f - Mathf.Clamp(yInBlock / blockH, 0f, 1f);

                var paramDef = AutomatableParameter.Find(activeAutomationParam);
                float value = paramDef != null
                    ? paramDef.Value.Min + valueFraction * (paramDef.Value.Max - paramDef.Value.Min)
                    : valueFraction;

                selectedKeyframeLane.Keyframes[selectedKeyframeIndex].Time = normalizedTime;
                selectedKeyframeLane.Keyframes[selectedKeyframeIndex].Value = value;
                AcceptEvent();
                return;
            }

            if (isDraggingLoop && syncManager != null)
            {
                float contentX = pos.X - trackHeaderWidth;
                float time = Mathf.Max(0f, contentX / pixelsPerSecond + scrollOffset);
                if (snapToGrid) time = SnapTimeToGrid(time);

                // When dragging from a Ctrl+click anchor, always keep start < end
                if (loopDragHandle == LoopDragHandle.End && loopDragAnchorTime >= 0f)
                {
                    syncManager.LoopStartTime = Mathf.Min(loopDragAnchorTime, time);
                    syncManager.LoopEndTime = Mathf.Max(loopDragAnchorTime, time);
                }
                else if (loopDragHandle == LoopDragHandle.Start)
                    syncManager.LoopStartTime = time;
                else if (loopDragHandle == LoopDragHandle.End)
                    syncManager.LoopEndTime = time;

                AcceptEvent();
                return;
            }

            if (isResizing && resizingBlock != null)
            {
                HandleResizeDrag(pos);
                AcceptEvent();
                return;
            }

            if (isDragging && draggingBlock != null)
            {
                float contentX = pos.X - trackHeaderWidth;
                float newTime = contentX / pixelsPerSecond + scrollOffset;
                newTime = Mathf.Max(0f, newTime);
                if (snapToGrid)
                    newTime = SnapTimeToGrid(newTime);

                // Multi-select drag: move all selected blocks by the same delta
                if (selectedBlocks.Count > 1 && selectedBlocks.Contains(draggingBlock))
                {
                    float delta = newTime - draggingBlock.StartTime;
                    foreach (var blk in selectedBlocks)
                    {
                        float proposed = blk.StartTime + delta;
                        blk.StartTime = Mathf.Max(0f, proposed);
                    }
                }
                else
                {
                    newTime = ClampMoveToAvoidOverlap(draggingBlock, draggingBlock.TrackIndex, newTime);
                    draggingBlock.StartTime = newTime;
                }
                AcceptEvent();
                return;
            }

            if (isDrawing)
            {
                float contentX = pos.X - trackHeaderWidth;
                drawCurrentTime = Mathf.Max(0f, contentX / pixelsPerSecond + scrollOffset);
                if (snapToGrid) drawCurrentTime = SnapTimeToGrid(drawCurrentTime);
                AcceptEvent();
                return;
            }

            if (isRubberBanding)
            {
                rubberBandEnd = pos;
                UpdateRubberBandSelection();
                AcceptEvent();
                return;
            }

            UpdateCursorForPosition(pos);
        }

        private void HandleLeftClick(Vector2 localPos, bool ctrlPressed, bool shiftPressed)
        {
            if (playbackManager == null)
                return;

            // Check ruler area
            if (localPos.Y < RulerHeight)
            {
                HandleRulerClick(localPos, ctrlPressed);
                return;
            }

            // Check track header area
            if (localPos.X < trackHeaderWidth)
            {
                HandleTrackHeaderClick(localPos);
                return;
            }

            var tracks = playbackManager.Tracks;
            float contentX = localPos.X - trackHeaderWidth;

            // Automation mode: handle keyframe clicks within the selected block
            if (automationEditMode && selectedBlock != null)
            {
                int blockTrack = selectedBlock.TrackIndex;
                if (blockTrack >= 0 && blockTrack < tracks.Count)
                {
                    float trackY = blockTrack * trackHeight + RulerHeight;
                    if (localPos.Y >= trackY && localPos.Y <= trackY + trackHeight)
                    {
                        float blockXPos = (selectedBlock.StartTime - scrollOffset) * pixelsPerSecond;
                        float blockWidth = selectedBlock.Duration * pixelsPerSecond;

                        if (contentX >= blockXPos && contentX <= blockXPos + blockWidth)
                        {
                            float normalizedTime = (contentX - blockXPos) / blockWidth;
                            normalizedTime = Mathf.Clamp(normalizedTime, 0f, 1f);

                            // Color composite mode: open color picker popup
                            if (activeAutomationParam == "Color")
                            {
                                colorAutoNormalizedTime = normalizedTime;

                                // Pre-fill picker with current color at this time
                                if (selectedBlock.Automation != null)
                                {
                                    var rLane = selectedBlock.Automation.GetLane("ColorR");
                                    var gLane = selectedBlock.Automation.GetLane("ColorG");
                                    var bLane = selectedBlock.Automation.GetLane("ColorB");
                                    float r = rLane != null ? rLane.Evaluate(normalizedTime) : (selectedBlock.Cue?.Color.R ?? 1f);
                                    float g = gLane != null ? gLane.Evaluate(normalizedTime) : (selectedBlock.Cue?.Color.G ?? 1f);
                                    float b = bLane != null ? bLane.Evaluate(normalizedTime) : (selectedBlock.Cue?.Color.B ?? 1f);
                                    colorAutoPicker.Color = new Color(r, g, b);
                                }
                                else
                                {
                                    colorAutoPicker.Color = selectedBlock.Cue?.Color ?? Colors.White;
                                }

                                var globalPos = GetGlobalTransformWithCanvas() * localPos;
                                colorAutoPopup.Position = new Vector2I((int)globalPos.X, (int)globalPos.Y);
                                colorAutoPopup.Popup();
                                return;
                            }

                            var paramDef = AutomatableParameter.Find(activeAutomationParam);
                            if (paramDef != null)
                            {
                                // Ensure automation data exists
                                if (selectedBlock.Automation == null)
                                    selectedBlock.Automation = new AutomationData();

                                var lane = selectedBlock.Automation.GetOrCreateLane(
                                    activeAutomationParam, paramDef.Value.Default, paramDef.Value.Min, paramDef.Value.Max);

                                // Check if clicking near an existing keyframe
                                float timeTolerance = 6f / blockWidth; // 6px tolerance
                                int nearIdx = lane.FindKeyframeNear(normalizedTime, timeTolerance);

                                if (nearIdx >= 0)
                                {
                                    // Select and start dragging the keyframe
                                    selectedKeyframeIndex = nearIdx;
                                    selectedKeyframeLane = lane;
                                    isDraggingKeyframe = true;
                                    dragKfOriginalTime = lane.Keyframes[nearIdx].Time;
                                    dragKfOriginalValue = lane.Keyframes[nearIdx].Value;
                                }
                                else
                                {
                                    // Create a new keyframe at click position
                                    float blockY = trackY + 2;
                                    float blockH = trackHeight - 4;
                                    float yInBlock = localPos.Y - blockY;
                                    float valueFraction = 1f - Mathf.Clamp(yInBlock / blockH, 0f, 1f);
                                    float value = paramDef.Value.Min + valueFraction * (paramDef.Value.Max - paramDef.Value.Min);

                                    var kf = new AutomationKeyframe(normalizedTime, value);
                                    var cmd = new AddKeyframeCommand(lane, kf);
                                    UndoManager.Instance.ExecuteCommand(cmd);

                                    selectedKeyframeIndex = lane.FindKeyframeNear(normalizedTime, 0.0001f);
                                    selectedKeyframeLane = lane;
                                }
                                return;
                            }
                        }
                    }
                }
            }

            // Check for edge resize first (skip locked blocks)
            for (int trackIdx = 0; trackIdx < tracks.Count; trackIdx++)
            {
                float trackY = trackIdx * trackHeight + RulerHeight;
                if (localPos.Y < trackY || localPos.Y > trackY + trackHeight)
                    continue;
                if (tracks[trackIdx].blocks == null)
                    continue;

                foreach (var block in tracks[trackIdx].blocks)
                {
                    if (block == null || block.Locked) continue;
                    float xPos = (block.StartTime - scrollOffset) * pixelsPerSecond;
                    float width = block.Duration * pixelsPerSecond;

                    if (Mathf.Abs(contentX - xPos) <= EdgeThresholdPx)
                    {
                        StartResize(block, ResizeEdge.Left);
                        return;
                    }
                    if (Mathf.Abs(contentX - (xPos + width)) <= EdgeThresholdPx)
                    {
                        StartResize(block, ResizeEdge.Right);
                        return;
                    }
                }
            }

            // Check for block body click (select + drag / ctrl+drag duplicate / shift+click multi)
            for (int trackIdx = 0; trackIdx < tracks.Count; trackIdx++)
            {
                float trackY = trackIdx * trackHeight + RulerHeight;
                if (localPos.Y < trackY || localPos.Y > trackY + trackHeight)
                    continue;
                if (tracks[trackIdx].blocks == null)
                    continue;

                foreach (var block in tracks[trackIdx].blocks)
                {
                    if (block == null) continue;
                    float xPos = (block.StartTime - scrollOffset) * pixelsPerSecond;
                    float width = block.Duration * pixelsPerSecond;

                    if (contentX >= xPos && contentX <= xPos + width)
                    {
                        // Ctrl+drag = duplicate
                        if (ctrlPressed && !block.Locked)
                        {
                            var clone = block.DeepClone();
                            clone.TrackIndex = trackIdx;
                            var track = tracks[trackIdx];
                            var cmd = new AddBlockCommand(clone, track, playbackManager.LaserShow);
                            UndoManager.Instance.ExecuteCommand(cmd);
                            SelectBlock(clone);
                            isDragging = true;
                            draggingBlock = clone;
                            dragStartTime = clone.StartTime;
                            dragOffsetX = contentX - xPos;
                            return;
                        }

                        // Shift+click = toggle multi-select
                        if (shiftPressed)
                        {
                            ToggleBlockSelection(block);
                            return;
                        }

                        // If clicking on an already-selected block in a multi-selection, start multi-drag
                        if (selectedBlocks.Contains(block) && selectedBlocks.Count > 1)
                        {
                            selectedBlock = block;
                            if (!block.Locked)
                            {
                                isDragging = true;
                                draggingBlock = block;
                                dragStartTime = block.StartTime;
                                dragOffsetX = contentX - xPos;
                                // Store original positions for all selected blocks
                                multiDragOriginalStarts.Clear();
                                foreach (var blk in selectedBlocks)
                                    multiDragOriginalStarts[blk] = blk.StartTime;
                            }
                            return;
                        }

                        // Normal click = single select + drag
                        SelectBlock(block);
                        if (!block.Locked)
                        {
                            isDragging = true;
                            draggingBlock = block;
                            dragStartTime = block.StartTime;
                            dragOffsetX = contentX - xPos;
                            multiDragOriginalStarts.Clear();
                            multiDragOriginalStarts[block] = block.StartTime;
                        }
                        return;
                    }
                }
            }

            // Draw mode: start drawing on empty space
            if (drawModeEnabled)
            {
                int trackIdx = TrackIndexFromY(localPos.Y);
                if (trackIdx >= 0)
                {
                    isDrawing = true;
                    drawTrackIndex = trackIdx;
                    float time = contentX / pixelsPerSecond + scrollOffset;
                    drawStartTime = snapToGrid ? SnapTimeToGrid(time) : time;
                    drawCurrentTime = drawStartTime;
                    return;
                }
            }

            // Start rubber-band selection on empty space
            if (!drawModeEnabled && localPos.X > trackHeaderWidth)
            {
                isRubberBanding = true;
                rubberBandStart = localPos;
                rubberBandEnd = localPos;
                if (!shiftPressed)
                {
                    SelectBlock(null);
                }
                return;
            }

            // Clicked empty space
            SelectBlock(null);
        }

        private void HandleRulerClick(Vector2 localPos, bool ctrlPressed)
        {
            if (syncManager == null) return;
            float contentX = localPos.X - trackHeaderWidth;
            if (contentX < 0) return;

            float time = contentX / pixelsPerSecond + scrollOffset;
            if (snapToGrid) time = SnapTimeToGrid(time);
            time = Mathf.Max(0f, time);

            // Ctrl+click: start defining a loop region by dragging
            if (ctrlPressed)
            {
                loopDragAnchorTime = time;
                syncManager.LoopStartTime = time;
                syncManager.LoopEndTime = time;
                isDraggingLoop = true;
                loopDragHandle = LoopDragHandle.End;
                return;
            }

            // Check for loop handle dragging on existing region
            if (syncManager.HasLoopRegion)
            {
                float loopStartX = (syncManager.LoopStartTime - scrollOffset) * pixelsPerSecond;
                float loopEndX = (syncManager.LoopEndTime - scrollOffset) * pixelsPerSecond;

                if (Mathf.Abs(contentX - loopStartX) <= EdgeThresholdPx)
                {
                    isDraggingLoop = true;
                    loopDragHandle = LoopDragHandle.Start;
                    loopDragAnchorTime = -1f;
                    return;
                }
                if (Mathf.Abs(contentX - loopEndX) <= EdgeThresholdPx)
                {
                    isDraggingLoop = true;
                    loopDragHandle = LoopDragHandle.End;
                    loopDragAnchorTime = -1f;
                    return;
                }
            }

            // Normal click: seek
            syncManager.Seek(time);
        }

        private void HandleRightClick(Vector2 localPos, Vector2 globalPos)
        {
            if (playbackManager == null)
                return;

            // Ruler right-click
            if (localPos.Y < RulerHeight)
            {
                float contentX = localPos.X - trackHeaderWidth;
                if (contentX >= 0)
                {
                    rulerContextClickTime = snapToGrid
                        ? SnapTimeToGrid(contentX / pixelsPerSecond + scrollOffset)
                        : contentX / pixelsPerSecond + scrollOffset;
                    ShowRulerContextMenu(globalPos);
                }
                return;
            }

            // Track header right-click
            if (localPos.X < trackHeaderWidth)
            {
                int trackIdx = TrackIndexFromY(localPos.Y);
                if (trackIdx >= 0)
                    ShowTrackHeaderContextMenu(trackIdx, globalPos);
                return;
            }

            // Automation mode: right-click keyframe for curve type menu
            if (automationEditMode && selectedBlock != null)
            {
                float contentXA = localPos.X - trackHeaderWidth;
                int blockTrack = selectedBlock.TrackIndex;
                if (blockTrack >= 0 && blockTrack < playbackManager.Tracks.Count)
                {
                    float trackYA = blockTrack * trackHeight + RulerHeight;
                    if (localPos.Y >= trackYA && localPos.Y <= trackYA + trackHeight)
                    {
                        float blockXPos = (selectedBlock.StartTime - scrollOffset) * pixelsPerSecond;
                        float blockWidth = selectedBlock.Duration * pixelsPerSecond;
                        if (contentXA >= blockXPos && contentXA <= blockXPos + blockWidth)
                        {
                            float normTime = Mathf.Clamp((contentXA - blockXPos) / blockWidth, 0f, 1f);
                            var lane = selectedBlock.Automation?.GetLane(activeAutomationParam);
                            if (lane != null)
                            {
                                float timeTol = 6f / blockWidth;
                                int nearIdx = lane.FindKeyframeNear(normTime, timeTol);
                                if (nearIdx >= 0)
                                {
                                    contextKeyframe = lane.Keyframes[nearIdx];
                                    contextKeyframeLane = lane;
                                    contextKeyframeIndex = nearIdx;
                                    ShowKeyframeContextMenu(globalPos);
                                    return;
                                }
                            }
                        }
                    }
                }
            }

            float contentXR = localPos.X - trackHeaderWidth;
            float time = contentXR / pixelsPerSecond + scrollOffset;
            int clickTrack = TrackIndexFromY(localPos.Y);

            if (clickTrack < 0)
                return;

            // Check if right-clicking a block
            var tracks = playbackManager.Tracks;
            if (tracks[clickTrack].blocks != null)
            {
                foreach (var block in tracks[clickTrack].blocks)
                {
                    if (block == null) continue;
                    float xPos = (block.StartTime - scrollOffset) * pixelsPerSecond;
                    float width = block.Duration * pixelsPerSecond;
                    if (contentXR >= xPos && contentXR <= xPos + width)
                    {
                        contextClickBlock = block;
                        ShowBlockContextMenu(globalPos);
                        return;
                    }
                }
            }

            // Right-click empty space — pattern placement menu
            contextClickTime = snapToGrid ? SnapTimeToGrid(time) : time;
            contextClickTrack = clickTrack;

            contextMenu.Clear();
            var patternTypes = PatternFactory.RegisteredTypes;
            int id = 0;
            foreach (var pType in patternTypes)
            {
                contextMenu.AddItem(pType.ToString(), id);
                id++;
            }

            contextMenu.Position = new Vector2I((int)globalPos.X, (int)globalPos.Y);
            contextMenu.Popup();
        }

        // --- Context Menus ---

        private void OnContextMenuItemSelected(long id)
        {
            if (playbackManager == null)
                return;

            var patternTypes = PatternFactory.RegisteredTypes;
            var typeList = new List<LaserPatternType>(patternTypes);
            if (id < 0 || id >= typeList.Count)
                return;

            var patternType = typeList[(int)id];
            lastUsedPatternType = patternType;

            var cue = new LaserCue
            {
                PatternType = patternType,
                CueName = patternType.ToString(),
                Color = Colors.White,
                Intensity = 1f,
                Size = 0.5f,
                Speed = 1f
            };

            float beatDuration = 60f / playbackManager.BPM;
            float duration = beatDuration * 2f;

            var tracks = playbackManager.Tracks;
            if (contextClickTrack < 0 || contextClickTrack >= tracks.Count)
                return;

            var block = new LaserCueBlock
            {
                Cue = cue,
                TrackIndex = contextClickTrack,
                StartTime = contextClickTime,
                Duration = duration,
                ZoneIndex = tracks[contextClickTrack].zoneIndex
            };

            var cmd = new AddBlockCommand(block, tracks[contextClickTrack], playbackManager.LaserShow);
            UndoManager.Instance.ExecuteCommand(cmd);

            SelectBlock(block);
        }

        private void ShowBlockContextMenu(Vector2 globalPos)
        {
            blockContextMenu.Clear();
            if (contextClickBlock == null) return;

            blockContextMenu.AddItem(contextClickBlock.Muted ? "Unmute" : "Mute", 0);
            blockContextMenu.AddItem(contextClickBlock.Locked ? "Unlock" : "Lock", 1);
            blockContextMenu.AddSeparator();
            blockContextMenu.AddItem("Delete", 2);

            blockContextMenu.Position = new Vector2I((int)globalPos.X, (int)globalPos.Y);
            blockContextMenu.Popup();
        }

        private void OnBlockContextMenuItemSelected(long id)
        {
            if (contextClickBlock == null || playbackManager == null) return;

            switch ((int)id)
            {
                case 0: // Mute/Unmute
                {
                    bool oldVal = contextClickBlock.Muted;
                    bool newVal = !oldVal;
                    var blk = contextClickBlock;
                    var cmd = new ModifyBlockPropertyCommand(
                        newVal ? "Mute Block" : "Unmute Block",
                        () => blk.Muted = newVal,
                        () => blk.Muted = oldVal);
                    UndoManager.Instance.ExecuteCommand(cmd);
                    break;
                }
                case 1: // Lock/Unlock
                {
                    bool oldVal = contextClickBlock.Locked;
                    bool newVal = !oldVal;
                    var blk = contextClickBlock;
                    var cmd = new ModifyBlockPropertyCommand(
                        newVal ? "Lock Block" : "Unlock Block",
                        () => blk.Locked = newVal,
                        () => blk.Locked = oldVal);
                    UndoManager.Instance.ExecuteCommand(cmd);
                    break;
                }
                case 2: // Delete
                {
                    if (contextClickBlock.Locked) break;
                    SelectBlock(contextClickBlock);
                    DeleteSelectedBlock();
                    break;
                }
            }
            contextClickBlock = null;
        }

        private void ShowRulerContextMenu(Vector2 globalPos)
        {
            rulerContextMenu.Clear();
            rulerContextMenu.AddItem("Add Marker", 0);

            // Check if there's a marker near the click
            var marker = FindMarkerNear(rulerContextClickTime, 0.5f);
            if (marker != null)
            {
                rulerContextMenu.AddItem("Delete Marker", 1);
            }

            rulerContextMenu.Position = new Vector2I((int)globalPos.X, (int)globalPos.Y);
            rulerContextMenu.Popup();
        }

        private void OnRulerContextMenuItemSelected(long id)
        {
            if (playbackManager?.LaserShow == null) return;

            switch ((int)id)
            {
                case 0: // Add Marker
                {
                    var marker = new TimeMarker
                    {
                        Name = $"M{playbackManager.LaserShow.Markers.Count + 1}",
                        Time = rulerContextClickTime,
                        MarkerColor = Colors.Yellow
                    };
                    playbackManager.LaserShow.Markers.Add(marker);
                    break;
                }
                case 1: // Delete Marker
                {
                    var marker = FindMarkerNear(rulerContextClickTime, 0.5f);
                    if (marker != null)
                        playbackManager.LaserShow.Markers.Remove(marker);
                    break;
                }
            }
        }

        // --- Paste ---

        private void PasteAtPlayhead()
        {
            if (playbackManager == null || syncManager == null) return;
            var tracks = playbackManager.Tracks;
            if (tracks.Count == 0) return;

            float pasteTime = syncManager.CurrentTime;
            if (snapToGrid) pasteTime = SnapTimeToGrid(pasteTime);

            var clones = ClipboardManager.PasteMultipleClones();
            if (clones.Count == 0) return;

            // Find the earliest start time in the copied blocks to calculate relative offsets
            float earliestStart = float.MaxValue;
            foreach (var c in clones)
                if (c.StartTime < earliestStart) earliestStart = c.StartTime;

            var commands = new List<ITimelineCommand>();
            foreach (var clone in clones)
            {
                float offset = clone.StartTime - earliestStart;
                clone.StartTime = pasteTime + offset;

                int trackIdx = clone.TrackIndex;
                if (trackIdx < 0 || trackIdx >= tracks.Count) trackIdx = 0;
                clone.TrackIndex = trackIdx;

                commands.Add(new AddBlockCommand(clone, tracks[trackIdx], playbackManager.LaserShow));
            }

            if (commands.Count == 1)
            {
                UndoManager.Instance.ExecuteCommand(commands[0]);
            }
            else
            {
                var compound = new CompoundCommand("Paste Blocks", commands);
                UndoManager.Instance.ExecuteCommand(compound);
            }

            // Select the pasted blocks
            selectedBlocks.Clear();
            foreach (var clone in clones)
                selectedBlocks.Add(clone);
            selectedBlock = clones[0];
            OnBlockSelected?.Invoke(selectedBlock);
            MarkDirty();
        }

        // --- Draw Mode Finish ---

        private void FinishDrawing()
        {
            isDrawing = false;
            if (playbackManager == null) return;

            var tracks = playbackManager.Tracks;
            if (drawTrackIndex < 0 || drawTrackIndex >= tracks.Count) return;

            float start = Mathf.Min(drawStartTime, drawCurrentTime);
            float end = Mathf.Max(drawStartTime, drawCurrentTime);
            float duration = end - start;
            float minDur = GetMinBlockDuration();
            if (duration < minDur) duration = minDur;

            var cue = new LaserCue
            {
                PatternType = lastUsedPatternType,
                CueName = lastUsedPatternType.ToString(),
                Color = Colors.White,
                Intensity = 1f,
                Size = 0.5f,
                Speed = 1f
            };

            var block = new LaserCueBlock
            {
                Cue = cue,
                TrackIndex = drawTrackIndex,
                StartTime = start,
                Duration = duration,
                ZoneIndex = tracks[drawTrackIndex].zoneIndex
            };

            var cmd = new AddBlockCommand(block, tracks[drawTrackIndex], playbackManager.LaserShow);
            UndoManager.Instance.ExecuteCommand(cmd);
            SelectBlock(block);
        }

        // --- Markers ---

        private void AddMarkerAtPlayhead()
        {
            if (playbackManager?.LaserShow == null || syncManager == null) return;
            var marker = new TimeMarker
            {
                Name = $"M{playbackManager.LaserShow.Markers.Count + 1}",
                Time = syncManager.CurrentTime,
                MarkerColor = Colors.Yellow
            };
            playbackManager.LaserShow.Markers.Add(marker);
        }

        private TimeMarker FindMarkerNear(float time, float threshold)
        {
            if (playbackManager?.LaserShow?.Markers == null) return null;
            foreach (var m in playbackManager.LaserShow.Markers)
            {
                if (Mathf.Abs(m.Time - time) <= threshold)
                    return m;
            }
            return null;
        }

        // --- Block Resize ---

        private void StartResize(LaserCueBlock block, ResizeEdge edge)
        {
            isResizing = true;
            resizingBlock = block;
            resizeEdge = edge;
            resizeOriginalStart = block.StartTime;
            resizeOriginalDuration = block.Duration;

            // Store originals for all selected blocks (for multi-resize)
            multiResizeOriginals.Clear();
            if (selectedBlocks.Contains(block) && selectedBlocks.Count > 1)
            {
                foreach (var blk in selectedBlocks)
                    multiResizeOriginals[blk] = (blk.StartTime, blk.Duration);
            }
            else
            {
                // Single block resize: make sure it's selected
                SelectBlock(block);
            }
        }

        private void HandleResizeDrag(Vector2 pos)
        {
            float contentX = pos.X - trackHeaderWidth;
            float time = contentX / pixelsPerSecond + scrollOffset;
            if (snapToGrid)
                time = SnapTimeToGrid(time);

            float minDuration = GetMinBlockDuration();

            if (resizeEdge == ResizeEdge.Right)
            {
                float newDuration = time - resizingBlock.StartTime;
                newDuration = Mathf.Max(minDuration, newDuration);
                newDuration = ClampResizeRightToAvoidOverlap(resizingBlock, resizingBlock.TrackIndex, newDuration);
                newDuration = Mathf.Max(minDuration, newDuration);

                // Multi-select: apply proportional resize to all selected blocks
                if (multiResizeOriginals.Count > 1 && resizeOriginalDuration > 0f)
                {
                    float ratio = newDuration / resizeOriginalDuration;
                    foreach (var kvp in multiResizeOriginals)
                    {
                        float scaledDur = Mathf.Max(minDuration, kvp.Value.duration * ratio);
                        kvp.Key.Duration = scaledDur;
                    }
                }
                else
                {
                    resizingBlock.Duration = newDuration;
                }
            }
            else if (resizeEdge == ResizeEdge.Left)
            {
                float originalEnd = resizingBlock.StartTime + resizingBlock.Duration;
                float newStart = Mathf.Min(time, originalEnd - minDuration);
                newStart = Mathf.Max(0f, newStart);
                newStart = ClampResizeLeftToAvoidOverlap(resizingBlock, resizingBlock.TrackIndex, newStart, originalEnd);
                float newDuration = originalEnd - newStart;

                // Multi-select: apply proportional resize to all selected blocks
                if (multiResizeOriginals.Count > 1 && resizeOriginalDuration > 0f)
                {
                    float ratio = newDuration / resizeOriginalDuration;
                    foreach (var kvp in multiResizeOriginals)
                    {
                        float origEnd = kvp.Value.start + kvp.Value.duration;
                        float scaledDur = Mathf.Max(minDuration, kvp.Value.duration * ratio);
                        kvp.Key.Duration = scaledDur;
                        kvp.Key.StartTime = origEnd - scaledDur;
                    }
                }
                else
                {
                    resizingBlock.Duration = newDuration;
                    resizingBlock.StartTime = newStart;
                }
            }
        }

        private float GetMinBlockDuration()
        {
            if (playbackManager == null || playbackManager.BPM <= 0f)
                return 0.1f;
            return 60f / playbackManager.BPM * snapBeatDivision;
        }

        // --- Overlap Prevention ---

        private TimelineTrack FindTrackContaining(LaserCueBlock block)
        {
            if (playbackManager == null) return null;
            foreach (var track in playbackManager.Tracks)
            {
                if (track.blocks != null && track.blocks.Contains(block))
                    return track;
            }
            return null;
        }

        private float ClampMoveToAvoidOverlap(LaserCueBlock block, int trackIndex, float desiredStart)
        {
            var track = FindTrackContaining(block);
            if (track == null) return desiredStart;

            float duration = block.Duration;
            float desiredEnd = desiredStart + duration;

            foreach (var other in track.blocks)
            {
                if (other == block || other == null) continue;
                if (selectedBlocks.Contains(other)) continue;
                float otherEnd = other.StartTime + other.Duration;

                if (desiredStart < otherEnd && desiredEnd > other.StartTime)
                {
                    float snapLeft = other.StartTime - duration;
                    float snapRight = otherEnd;

                    float distLeft = Mathf.Abs(desiredStart - snapLeft);
                    float distRight = Mathf.Abs(desiredStart - snapRight);

                    desiredStart = distLeft <= distRight ? snapLeft : snapRight;
                }
            }

            return Mathf.Max(0f, desiredStart);
        }

        private float ClampResizeRightToAvoidOverlap(LaserCueBlock block, int trackIndex, float desiredDuration)
        {
            var track = FindTrackContaining(block);
            if (track == null) return desiredDuration;

            float blockStart = block.StartTime;
            float maxEnd = blockStart + desiredDuration;

            foreach (var other in track.blocks)
            {
                if (other == block || other == null) continue;
                if (selectedBlocks.Contains(other)) continue;

                // Any block whose start falls within our new extent is a collision
                if (other.StartTime >= blockStart && other.StartTime < maxEnd)
                {
                    maxEnd = Mathf.Min(maxEnd, other.StartTime);
                }
            }

            return maxEnd - blockStart;
        }

        private float ClampResizeLeftToAvoidOverlap(LaserCueBlock block, int trackIndex, float desiredStart, float fixedEnd)
        {
            var track = FindTrackContaining(block);
            if (track == null) return desiredStart;

            foreach (var other in track.blocks)
            {
                if (other == block || other == null) continue;
                if (selectedBlocks.Contains(other)) continue;
                float otherEnd = other.StartTime + other.Duration;

                // Other block ends within our new range — push our start past it
                if (otherEnd > desiredStart && other.StartTime < fixedEnd)
                {
                    desiredStart = Mathf.Max(desiredStart, otherEnd);
                }
            }

            return desiredStart;
        }

        // --- Block Delete ---

        private void DeleteSelectedBlock()
        {
            if (selectedBlock == null || playbackManager == null)
                return;
            if (selectedBlock.Locked)
                return;

            var tracks = playbackManager.Tracks;
            // Find the track containing this block
            TimelineTrack owningTrack = null;
            foreach (var track in tracks)
            {
                if (track.blocks != null && track.blocks.Contains(selectedBlock))
                {
                    owningTrack = track;
                    break;
                }
            }

            if (owningTrack != null)
            {
                var cmd = new RemoveBlockCommand(selectedBlock, owningTrack, playbackManager.LaserShow);
                UndoManager.Instance.ExecuteCommand(cmd);
            }

            SelectBlock(null);
        }

        private void DeleteSelectedBlocks()
        {
            if (playbackManager == null || selectedBlocks.Count == 0)
                return;

            var tracks = playbackManager.Tracks;
            var commands = new List<ITimelineCommand>();

            foreach (var block in selectedBlocks)
            {
                if (block.Locked) continue;
                TimelineTrack owningTrack = null;
                foreach (var track in tracks)
                {
                    if (track.blocks != null && track.blocks.Contains(block))
                    {
                        owningTrack = track;
                        break;
                    }
                }
                if (owningTrack != null)
                    commands.Add(new RemoveBlockCommand(block, owningTrack, playbackManager.LaserShow));
            }

            if (commands.Count > 0)
            {
                var compound = new CompoundCommand("Delete Blocks", commands);
                UndoManager.Instance.ExecuteCommand(compound);
            }

            SelectBlock(null);
        }

        // --- Block Selection ---

        private void SelectBlock(LaserCueBlock block)
        {
            selectedBlock = block;
            selectedBlocks.Clear();
            if (block != null)
                selectedBlocks.Add(block);

            OnBlockSelected?.Invoke(block);

            if (inspectorPanel != null)
            {
                inspectorPanel.timelineUI = this;
                inspectorPanel.playbackManager = playbackManager;
                if (block != null)
                    inspectorPanel.ShowBlock(block);
                else
                    inspectorPanel.HidePanel();
            }

            MarkDirty();
        }

        private void ToggleBlockSelection(LaserCueBlock block)
        {
            if (block == null) return;

            if (selectedBlocks.Contains(block))
            {
                selectedBlocks.Remove(block);
                if (selectedBlock == block)
                    selectedBlock = selectedBlocks.Count > 0 ? System.Linq.Enumerable.First(selectedBlocks) : null;
            }
            else
            {
                selectedBlocks.Add(block);
                selectedBlock = block;
            }

            OnBlockSelected?.Invoke(selectedBlock);

            if (inspectorPanel != null)
            {
                inspectorPanel.timelineUI = this;
                inspectorPanel.playbackManager = playbackManager;
                if (selectedBlock != null)
                    inspectorPanel.ShowBlock(selectedBlock);
                else
                    inspectorPanel.HidePanel();
            }

            MarkDirty();
        }

        private bool IsBlockSelected(LaserCueBlock block)
        {
            return selectedBlocks.Contains(block);
        }

        private void UpdateRubberBandSelection()
        {
            if (playbackManager == null) return;
            var tracks = playbackManager.Tracks;

            // Convert rubber band rect to time/track space
            float x1 = Mathf.Min(rubberBandStart.X, rubberBandEnd.X);
            float x2 = Mathf.Max(rubberBandStart.X, rubberBandEnd.X);
            float y1 = Mathf.Min(rubberBandStart.Y, rubberBandEnd.Y);
            float y2 = Mathf.Max(rubberBandStart.Y, rubberBandEnd.Y);

            float contentX1 = x1 - trackHeaderWidth;
            float contentX2 = x2 - trackHeaderWidth;
            float timeStart = contentX1 / pixelsPerSecond + scrollOffset;
            float timeEnd = contentX2 / pixelsPerSecond + scrollOffset;

            selectedBlocks.Clear();
            selectedBlock = null;

            for (int trackIdx = 0; trackIdx < tracks.Count; trackIdx++)
            {
                float trackY = trackIdx * trackHeight + RulerHeight;
                float trackBottom = trackY + trackHeight;

                // Check if rubber band overlaps this track row
                if (y2 < trackY || y1 > trackBottom)
                    continue;

                if (tracks[trackIdx].blocks == null)
                    continue;

                foreach (var block in tracks[trackIdx].blocks)
                {
                    if (block == null) continue;
                    float blockEnd = block.StartTime + block.Duration;

                    // Check time overlap
                    if (block.StartTime < timeEnd && blockEnd > timeStart)
                    {
                        selectedBlocks.Add(block);
                        if (selectedBlock == null)
                            selectedBlock = block;
                    }
                }
            }

            // Update inspector with first selected block
            OnBlockSelected?.Invoke(selectedBlock);
            if (inspectorPanel != null)
            {
                inspectorPanel.timelineUI = this;
                inspectorPanel.playbackManager = playbackManager;
                if (selectedBlock != null)
                    inspectorPanel.ShowBlock(selectedBlock);
                else
                    inspectorPanel.HidePanel();
            }
        }

        // --- Track Header Interaction ---

        private void HandleTrackHeaderClick(Vector2 localPos)
        {
            if (playbackManager == null)
                return;

            var tracks = playbackManager.Tracks;
            int trackIdx = TrackIndexFromY(localPos.Y);

            // "+" button below all tracks
            float addButtonY = tracks.Count * trackHeight + RulerHeight;
            if (localPos.Y >= addButtonY && localPos.Y <= addButtonY + trackHeight)
            {
                AddNewTrack();
                return;
            }

            if (trackIdx < 0)
                return;

            var track = tracks[trackIdx];
            float trackY = trackIdx * trackHeight + RulerHeight;

            // Mute button: 4..34
            float btnY = trackY + trackHeight / 2 - 8;
            if (localPos.X >= 4 && localPos.X <= 34 && localPos.Y >= btnY && localPos.Y <= btnY + 16)
            {
                track.muted = !track.muted;
                return;
            }

            // Solo button: 38..68
            if (localPos.X >= 38 && localPos.X <= 68 && localPos.Y >= btnY && localPos.Y <= btnY + 16)
            {
                track.solo = !track.solo;
                return;
            }
        }

        private void ShowTrackHeaderContextMenu(int trackIdx, Vector2 globalPos)
        {
            trackHeaderMenuIndex = trackIdx;
            trackHeaderMenu.Clear();
            trackHeaderMenu.AddItem("Rename Track", 0);
            trackHeaderMenu.AddItem("Delete Track", 1);
            trackHeaderMenu.Position = new Vector2I((int)globalPos.X, (int)globalPos.Y);
            trackHeaderMenu.Popup();
        }

        private void OnTrackHeaderMenuItemSelected(long id)
        {
            if (playbackManager == null)
                return;

            var tracks = playbackManager.Tracks;
            if (trackHeaderMenuIndex < 0 || trackHeaderMenuIndex >= tracks.Count)
                return;

            switch ((int)id)
            {
                case 0:
                    ShowRenameDialog(trackHeaderMenuIndex);
                    break;
                case 1:
                    DeleteTrack(trackHeaderMenuIndex);
                    break;
            }
        }

        private void ShowRenameDialog(int trackIdx)
        {
            var tracks = playbackManager.Tracks;
            if (trackIdx < 0 || trackIdx >= tracks.Count)
                return;

            var dialog = new AcceptDialog();
            dialog.Title = "Rename Track";
            var lineEdit = new LineEdit();
            lineEdit.Text = tracks[trackIdx].trackName;
            dialog.AddChild(lineEdit);

            int capturedIdx = trackIdx;
            dialog.Confirmed += () =>
            {
                if (capturedIdx < tracks.Count)
                    tracks[capturedIdx].trackName = lineEdit.Text;
                dialog.QueueFree();
            };
            dialog.Canceled += () => dialog.QueueFree();

            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(300, 80));
        }

        private void DeleteTrack(int trackIdx)
        {
            var tracks = playbackManager.Tracks;
            if (trackIdx < 0 || trackIdx >= tracks.Count)
                return;

            var track = tracks[trackIdx];

            if (playbackManager.LaserShow != null && track.blocks != null)
            {
                foreach (var block in track.blocks)
                    playbackManager.LaserShow.TimelineBlocks.Remove(block);
            }

            tracks.RemoveAt(trackIdx);

            if (selectedBlock != null)
            {
                bool found = false;
                foreach (var t in tracks)
                {
                    if (t.blocks != null && t.blocks.Contains(selectedBlock))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    SelectBlock(null);
            }
        }

        private void AddNewTrack()
        {
            if (playbackManager == null)
                return;

            var tracks = playbackManager.Tracks;
            int idx = tracks.Count;
            var track = new TimelineTrack
            {
                trackName = $"Track {idx + 1}",
                zoneIndex = 0
            };
            tracks.Add(track);
        }

        // --- Cursor Management ---

        private void UpdateCursorForPosition(Vector2 pos)
        {
            if (playbackManager == null || pos.X < trackHeaderWidth || pos.Y < RulerHeight)
            {
                MouseDefaultCursorShape = CursorShape.Arrow;
                return;
            }

            float contentX = pos.X - trackHeaderWidth;
            var tracks = playbackManager.Tracks;

            for (int trackIdx = 0; trackIdx < tracks.Count; trackIdx++)
            {
                float trackY = trackIdx * trackHeight + RulerHeight;
                if (pos.Y < trackY || pos.Y > trackY + trackHeight)
                    continue;

                if (tracks[trackIdx].blocks == null)
                    continue;

                foreach (var block in tracks[trackIdx].blocks)
                {
                    if (block == null) continue;
                    float xPos = (block.StartTime - scrollOffset) * pixelsPerSecond;
                    float width = block.Duration * pixelsPerSecond;

                    if (Mathf.Abs(contentX - xPos) <= EdgeThresholdPx ||
                        Mathf.Abs(contentX - (xPos + width)) <= EdgeThresholdPx)
                    {
                        MouseDefaultCursorShape = CursorShape.Hsize;
                        return;
                    }
                }
            }

            MouseDefaultCursorShape = drawModeEnabled ? CursorShape.Cross : CursorShape.Arrow;
        }

        // --- Helpers ---

        private int TrackIndexFromY(float y)
        {
            if (playbackManager == null)
                return -1;
            int idx = Mathf.FloorToInt((y - RulerHeight) / trackHeight);
            if (idx < 0 || idx >= playbackManager.Tracks.Count)
                return -1;
            return idx;
        }

        private float SnapTimeToGrid(float time)
        {
            if (playbackManager == null || playbackManager.BPM <= 0f)
                return time;

            float beatDuration = 60f / playbackManager.BPM * snapBeatDivision;
            if (beatDuration <= 0f)
                return time;

            float snapped = Mathf.Round(time / beatDuration) * beatDuration;

            // Also snap to markers if close enough
            if (playbackManager.LaserShow?.Markers != null)
            {
                float markerSnapThreshold = beatDuration * 0.4f;
                foreach (var marker in playbackManager.LaserShow.Markers)
                {
                    if (Mathf.Abs(time - marker.Time) < markerSnapThreshold)
                    {
                        if (Mathf.Abs(time - marker.Time) < Mathf.Abs(time - snapped))
                            snapped = marker.Time;
                    }
                }
            }

            return snapped;
        }

        // --- Drawing ---

        public override void _Draw()
        {
            if (playbackManager == null)
                return;

            var tracks = playbackManager.Tracks;
            float viewWidth = Size.X;
            float viewHeight = Size.Y;
            float contentLeft = trackHeaderWidth;
            float contentWidth = viewWidth - trackHeaderWidth;

            // Draw ruler
            DrawRuler(contentLeft, contentWidth);

            // Draw track headers
            DrawTrackHeaders(tracks, viewHeight);

            // Draw track backgrounds
            for (int i = 0; i < tracks.Count; i++)
            {
                float y = i * trackHeight + RulerHeight;
                Color bgColor = tracks[i].muted ? trackMutedColor : trackColor;
                DrawRect(new Rect2(contentLeft, y, contentWidth, trackHeight), bgColor);
                DrawLine(new Vector2(contentLeft, y + trackHeight), new Vector2(viewWidth, y + trackHeight),
                    new Color(1f, 1f, 1f, 0.1f));
            }

            // "+" add track area
            float addY = tracks.Count * trackHeight + RulerHeight;
            if (addY < viewHeight && _font != null)
            {
                DrawRect(new Rect2(0, addY, trackHeaderWidth, trackHeight),
                    new Color(0.1f, 0.1f, 0.12f, 1f));
                DrawString(_font, new Vector2(trackHeaderWidth / 2 - 6, addY + trackHeight / 2 + 4),
                    "+", HorizontalAlignment.Center, -1, 16, new Color(1f, 1f, 1f, 0.4f));
            }

            // Waveform (behind everything else in content area)
            DrawWaveform(contentLeft, contentWidth, tracks.Count);

            // Beat grid
            DrawBeatGrid(contentLeft, contentWidth, viewHeight, tracks.Count);

            // Loop region overlay
            DrawLoopRegion(contentLeft, contentWidth, tracks.Count);

            // Markers
            DrawMarkers(contentLeft, contentWidth, tracks.Count);

            // Cue blocks
            for (int trackIdx = 0; trackIdx < tracks.Count; trackIdx++)
            {
                var track = tracks[trackIdx];
                if (track.blocks == null)
                    continue;

                foreach (var block in track.blocks)
                {
                    if (block == null || block.Cue == null)
                        continue;

                    float rawXPos = contentLeft + (block.StartTime - scrollOffset) * pixelsPerSecond;
                    float rawWidth = block.Duration * pixelsPerSecond;
                    float y = trackIdx * trackHeight + RulerHeight + 2;
                    float height = trackHeight - 4;

                    if (rawXPos + rawWidth < contentLeft || rawXPos > viewWidth)
                        continue;

                    // Clip block to content area so it doesn't draw over track headers
                    float xPos = Mathf.Max(rawXPos, contentLeft);
                    float xEnd = Mathf.Min(rawXPos + rawWidth, viewWidth);
                    float width = xEnd - xPos;
                    if (width <= 0) continue;

                    // Block fill
                    Color blockColor = block.Cue.Color;
                    float alpha = IsBlockSelected(block) ? 1f : 0.7f;
                    if (block.Muted) alpha = 0.25f;
                    blockColor.A = alpha;
                    DrawRect(new Rect2(xPos, y, width, height), blockColor);

                    // Fade regions (clipped)
                    if (block.FadeInDuration > 0f)
                    {
                        float fadeW = block.FadeInDuration * pixelsPerSecond;
                        float fadeX1 = Mathf.Max(rawXPos, contentLeft);
                        float fadeX2 = Mathf.Min(rawXPos + Mathf.Min(fadeW, rawWidth), viewWidth);
                        if (fadeX2 > fadeX1)
                            DrawRect(new Rect2(fadeX1, y, fadeX2 - fadeX1, height),
                                new Color(0f, 0f, 0f, 0.25f));
                    }
                    if (block.FadeOutDuration > 0f)
                    {
                        float fadeW = block.FadeOutDuration * pixelsPerSecond;
                        float fadeStart = rawXPos + rawWidth - Mathf.Min(fadeW, rawWidth);
                        float fadeX1 = Mathf.Max(fadeStart, contentLeft);
                        float fadeX2 = Mathf.Min(rawXPos + rawWidth, viewWidth);
                        if (fadeX2 > fadeX1)
                            DrawRect(new Rect2(fadeX1, y, fadeX2 - fadeX1, height),
                                new Color(0f, 0f, 0f, 0.25f));
                    }

                    // Border
                    Color borderColor = IsBlockSelected(block)
                        ? Colors.White
                        : new Color(1f, 1f, 1f, 0.3f);
                    float bw = IsBlockSelected(block) ? 2f : 1f;
                    DrawRect(new Rect2(xPos, y, width, height), borderColor, false, bw);

                    // Block label (only if left edge is visible enough)
                    if (_font != null && width > 20)
                    {
                        string label = block.Cue.CueName;
                        if (block.Locked) label = "L " + label;
                        float labelX = Mathf.Max(xPos + 4, contentLeft + 4);
                        float labelMaxW = xEnd - labelX - 4;
                        if (labelMaxW > 10)
                            DrawString(_font, new Vector2(labelX, y + height / 2 + 4),
                                label, HorizontalAlignment.Left, (int)labelMaxW, 11, Colors.White);
                    }

                    // Automation envelope overlay (use raw coords since it clips internally)
                    if (automationEditMode && block.Automation != null)
                    {
                        DrawAutomationEnvelope(block, rawXPos, y, rawWidth, height);
                    }
                }
            }


            // Draw mode preview
            if (isDrawing)
            {
                float start = Mathf.Min(drawStartTime, drawCurrentTime);
                float end = Mathf.Max(drawStartTime, drawCurrentTime);
                float drawX = contentLeft + (start - scrollOffset) * pixelsPerSecond;
                float drawW = (end - start) * pixelsPerSecond;
                float drawY = drawTrackIndex * trackHeight + RulerHeight + 2;
                DrawRect(new Rect2(drawX, drawY, drawW, trackHeight - 4),
                    new Color(1f, 1f, 1f, 0.2f));
                DrawRect(new Rect2(drawX, drawY, drawW, trackHeight - 4),
                    new Color(1f, 1f, 1f, 0.5f), false, 1f);
            }

            // Rubber-band selection rect
            if (isRubberBanding)
            {
                float rx = Mathf.Min(rubberBandStart.X, rubberBandEnd.X);
                float ry = Mathf.Min(rubberBandStart.Y, rubberBandEnd.Y);
                float rw = Mathf.Abs(rubberBandEnd.X - rubberBandStart.X);
                float rh = Mathf.Abs(rubberBandEnd.Y - rubberBandStart.Y);
                DrawRect(new Rect2(rx, ry, rw, rh), new Color(0.3f, 0.6f, 1f, 0.15f));
                DrawRect(new Rect2(rx, ry, rw, rh), new Color(0.3f, 0.6f, 1f, 0.6f), false, 1f);
            }

            // Multi-selection count indicator
            if (selectedBlocks.Count > 1 && _font != null)
            {
                DrawString(_font, new Vector2(contentLeft + contentWidth - 80, RulerHeight - 4),
                    $"{selectedBlocks.Count} selected", HorizontalAlignment.Right, -1, 10, new Color(1f, 1f, 1f, 0.6f));
            }

            // Playhead
            if (syncManager != null)
            {
                float currentTime = syncManager.CurrentTime;
                float playheadX = contentLeft + (currentTime - scrollOffset) * pixelsPerSecond;

                if (playheadX >= contentLeft && playheadX <= viewWidth)
                {
                    float totalHeight = Mathf.Max(tracks.Count * trackHeight + RulerHeight, viewHeight);
                    DrawLine(new Vector2(playheadX, 0), new Vector2(playheadX, totalHeight),
                        playheadColor, 2f);
                }
            }

            // Automation mode indicator
            if (automationEditMode && _font != null)
            {
                string modeLabel = $"AUTO: {activeAutomationParam}";
                Color autoColor = AutomationParamColors.ContainsKey(activeAutomationParam)
                    ? AutomationParamColors[activeAutomationParam]
                    : Colors.Yellow;
                DrawString(_font, new Vector2(contentLeft + 4, RulerHeight - 4),
                    modeLabel, HorizontalAlignment.Left, -1, 10, autoColor);
            }
        }

        private void DrawRuler(float contentLeft, float contentWidth)
        {
            // Ruler background
            DrawRect(new Rect2(0, 0, contentLeft + contentWidth, RulerHeight),
                new Color(0.08f, 0.08f, 0.1f, 1f));

            if (playbackManager == null || playbackManager.BPM <= 0f || _font == null)
                return;

            float bpm = playbackManager.BPM;
            float beatDuration = 60f / bpm;

            float startTime = scrollOffset;
            float endTime = scrollOffset + contentWidth / pixelsPerSecond;

            int startBar = Mathf.FloorToInt(startTime / (beatDuration * 4));
            int endBar = Mathf.CeilToInt(endTime / (beatDuration * 4));

            for (int bar = startBar; bar <= endBar; bar++)
            {
                if (bar < 0) continue;
                float time = bar * beatDuration * 4;
                float x = contentLeft + (time - scrollOffset) * pixelsPerSecond;

                if (x < contentLeft || x > contentLeft + contentWidth)
                    continue;

                DrawLine(new Vector2(x, 0), new Vector2(x, RulerHeight), barLineColor, 1f);
                DrawString(_font, new Vector2(x + 2, RulerHeight - 4),
                    (bar + 1).ToString(), HorizontalAlignment.Left, -1, 10, new Color(1f, 1f, 1f, 0.6f));
            }
        }

        private void DrawTrackHeaders(List<TimelineTrack> tracks, float viewHeight)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                float y = i * trackHeight + RulerHeight;
                var track = tracks[i];

                DrawRect(new Rect2(0, y, trackHeaderWidth, trackHeight), trackHeaderBg);
                DrawLine(new Vector2(trackHeaderWidth, y), new Vector2(trackHeaderWidth, y + trackHeight),
                    new Color(1f, 1f, 1f, 0.2f));

                float btnY = y + trackHeight / 2 - 8;

                // Mute button
                Color muteColor = track.muted ? Colors.Red : new Color(0.4f, 0.4f, 0.4f, 1f);
                DrawRect(new Rect2(4, btnY, 30, 16), muteColor);
                if (_font != null)
                    DrawString(_font, new Vector2(8, btnY + 12), "M", HorizontalAlignment.Left, -1, 10, Colors.White);

                // Solo button
                Color soloColor = track.solo ? trackSoloColor : new Color(0.4f, 0.4f, 0.4f, 1f);
                DrawRect(new Rect2(38, btnY, 30, 16), soloColor);
                if (_font != null)
                    DrawString(_font, new Vector2(44, btnY + 12), "S", HorizontalAlignment.Left, -1, 10, Colors.White);

                // Track name
                if (_font != null)
                {
                    DrawString(_font, new Vector2(72, y + trackHeight / 2 + 4), track.trackName,
                        HorizontalAlignment.Left, (int)trackHeaderWidth - 76, 10, new Color(1f, 1f, 1f, 0.8f));
                }
            }
        }

        private void DrawBeatGrid(float contentLeft, float contentWidth, float viewHeight, int trackCount)
        {
            if (playbackManager == null || playbackManager.BPM <= 0f)
                return;

            float bpm = playbackManager.BPM;
            float beatDuration = 60f / bpm;
            float totalHeight = Mathf.Max(trackCount * trackHeight + RulerHeight, viewHeight);

            float startTime = scrollOffset;
            float endTime = scrollOffset + contentWidth / pixelsPerSecond;

            int startBeat = Mathf.FloorToInt(startTime / beatDuration);
            int endBeat = Mathf.CeilToInt(endTime / beatDuration);

            for (int beat = startBeat; beat <= endBeat; beat++)
            {
                if (beat < 0) continue;

                float time = beat * beatDuration;
                float x = contentLeft + (time - scrollOffset) * pixelsPerSecond;

                if (x < contentLeft || x > contentLeft + contentWidth)
                    continue;

                bool isBar = (beat % 4) == 0;
                Color lineColor = isBar ? barLineColor : beatLineColor;
                float lineWidth = isBar ? 1.5f : 1f;

                DrawLine(new Vector2(x, RulerHeight), new Vector2(x, totalHeight), lineColor, lineWidth);
            }
        }

        private void DrawWaveform(float contentLeft, float contentWidth, int trackCount)
        {
            if (waveformPeaks == null || waveformPeaks.Length == 0)
                return;

            float totalHeight = trackCount * trackHeight;
            float waveformY = RulerHeight;
            var waveColor = new Color(0.2f, 0.4f, 0.8f, 0.25f);

            int startPx = Mathf.Max(0, Mathf.FloorToInt(scrollOffset * pixelsPerSecond));
            int endPx = Mathf.Min(waveformPeaks.Length, startPx + Mathf.CeilToInt(contentWidth));

            for (int px = startPx; px < endPx; px++)
            {
                float x = contentLeft + (px - scrollOffset * pixelsPerSecond);
                if (x < contentLeft || x > contentLeft + contentWidth)
                    continue;

                float peak = waveformPeaks[px];
                float barH = peak * totalHeight;
                float centerY = waveformY + totalHeight / 2f;

                DrawLine(new Vector2(x, centerY - barH / 2), new Vector2(x, centerY + barH / 2), waveColor, 1f);
            }
        }

        private void DrawLoopRegion(float contentLeft, float contentWidth, int trackCount)
        {
            if (syncManager == null || !syncManager.HasLoopRegion)
                return;

            float totalHeight = trackCount * trackHeight + RulerHeight;
            float loopStartX = contentLeft + (syncManager.LoopStartTime - scrollOffset) * pixelsPerSecond;
            float loopEndX = contentLeft + (syncManager.LoopEndTime - scrollOffset) * pixelsPerSecond;

            // Tinted overlay
            var loopColor = new Color(0f, 0.8f, 0.8f, 0.08f);
            DrawRect(new Rect2(loopStartX, 0, loopEndX - loopStartX, totalHeight), loopColor);

            // Vertical lines
            var lineColor = new Color(0f, 0.8f, 0.8f, 0.7f);
            DrawLine(new Vector2(loopStartX, 0), new Vector2(loopStartX, totalHeight), lineColor, 2f);
            DrawLine(new Vector2(loopEndX, 0), new Vector2(loopEndX, totalHeight), lineColor, 2f);

            // Ruler bar
            DrawRect(new Rect2(loopStartX, 0, loopEndX - loopStartX, RulerHeight),
                new Color(0f, 0.8f, 0.8f, 0.2f));
        }

        private void DrawMarkers(float contentLeft, float contentWidth, int trackCount)
        {
            if (playbackManager?.LaserShow?.Markers == null || _font == null)
                return;

            float totalHeight = Mathf.Max(trackCount * trackHeight + RulerHeight, Size.Y);

            foreach (var marker in playbackManager.LaserShow.Markers)
            {
                float x = contentLeft + (marker.Time - scrollOffset) * pixelsPerSecond;
                if (x < contentLeft || x > contentLeft + contentWidth)
                    continue;

                // Vertical line
                DrawLine(new Vector2(x, 0), new Vector2(x, totalHeight), marker.MarkerColor, 1f);

                // Triangle in ruler
                var points = new Vector2[]
                {
                    new Vector2(x - 4, 0),
                    new Vector2(x + 4, 0),
                    new Vector2(x, 8)
                };
                DrawPolygon(points, new Color[] { marker.MarkerColor });

                // Label
                DrawString(_font, new Vector2(x + 2, RulerHeight - 3),
                    marker.Name ?? "", HorizontalAlignment.Left, -1, 9, marker.MarkerColor);
            }
        }

        // --- Keyframe Context Menu ---

        private void ShowKeyframeContextMenu(Vector2 globalPos)
        {
            keyframeContextMenu.Clear();
            keyframeContextMenu.AddItem("Linear", 0);
            keyframeContextMenu.AddItem("Ease In", 1);
            keyframeContextMenu.AddItem("Ease Out", 2);
            keyframeContextMenu.AddItem("Ease In/Out", 3);
            keyframeContextMenu.AddItem("Step", 4);
            keyframeContextMenu.AddSeparator();
            keyframeContextMenu.AddItem("Delete", 10);
            keyframeContextMenu.Position = new Vector2I((int)globalPos.X, (int)globalPos.Y);
            keyframeContextMenu.Popup();
        }

        private void OnKeyframeContextMenuItemSelected(long id)
        {
            if (contextKeyframe == null || contextKeyframeLane == null) return;

            if (id == 10)
            {
                var cmd = new RemoveKeyframeCommand(contextKeyframeLane, contextKeyframeIndex);
                UndoManager.Instance.ExecuteCommand(cmd);
                selectedKeyframeIndex = -1;
                selectedKeyframeLane = null;
            }
            else
            {
                var newType = (AutomationCurveType)(int)id;
                var oldType = contextKeyframe.CurveType;
                if (newType != oldType)
                {
                    var cmd = new ChangeCurveTypeCommand(contextKeyframe, oldType, newType);
                    UndoManager.Instance.ExecuteCommand(cmd);
                }
            }
            contextKeyframe = null;
            contextKeyframeLane = null;
        }

        // --- Automation Envelope Drawing ---

        private static readonly System.Collections.Generic.Dictionary<string, Color> AutomationParamColors = new()
        {
            { "Intensity", Colors.Yellow },
            { "Size", Colors.Cyan },
            { "Rotation", Colors.Magenta },
            { "Speed", Colors.Green },
            { "Spread", new Color(0.5f, 1f, 0.5f) },
            { "Frequency", new Color(0.4f, 0.8f, 1f) },
            { "Amplitude", new Color(1f, 0.6f, 0.2f) },
            { "PositionX", Colors.Orange },
            { "PositionY", new Color(1f, 0.5f, 0f) },
            { "ColorR", Colors.Red },
            { "ColorG", Colors.Green },
            { "ColorB", new Color(0.3f, 0.5f, 1f) },
            { "Color", Colors.White },
        };

        private void DrawAutomationEnvelope(LaserCueBlock block, float blockX, float blockY, float blockWidth, float blockHeight)
        {
            if (block.Automation == null) return;

            // Composite Color mode: draw R, G, B lanes overlaid
            if (activeAutomationParam == "Color")
            {
                DrawSingleLaneEnvelope(block.Automation.GetLane("ColorR"), block, blockX, blockY, blockWidth, blockHeight, Colors.Red);
                DrawSingleLaneEnvelope(block.Automation.GetLane("ColorG"), block, blockX, blockY, blockWidth, blockHeight, Colors.Green);
                DrawSingleLaneEnvelope(block.Automation.GetLane("ColorB"), block, blockX, blockY, blockWidth, blockHeight, new Color(0.3f, 0.5f, 1f));
                return;
            }

            var lane = block.Automation.GetLane(activeAutomationParam);
            if (lane == null || lane.Keyframes == null || lane.Keyframes.Count == 0) return;

            Color lineColor = AutomationParamColors.ContainsKey(activeAutomationParam)
                ? AutomationParamColors[activeAutomationParam]
                : Colors.White;

            float range = lane.MaxValue - lane.MinValue;
            if (range <= 0f) range = 1f;

            // Draw filled area + polyline by sampling at ~2px intervals
            int steps = Mathf.Max(2, (int)(blockWidth / 4f));
            var polylinePoints = new Vector2[steps + 1];

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float value = lane.Evaluate(t);
                float valueFraction = (value - lane.MinValue) / range;
                float px = blockX + t * blockWidth;
                float py = blockY + blockHeight * (1f - valueFraction);
                polylinePoints[i] = new Vector2(px, py);
            }

            // Semi-transparent fill as single polygon (curve top + bottom edge)
            Color fillColor = lineColor;
            fillColor.A = 0.15f;
            float bottomY = blockY + blockHeight;
            var fillVerts = new Vector2[(steps + 1) * 2];
            var fillColors = new Color[(steps + 1) * 2];
            for (int i = 0; i <= steps; i++)
            {
                fillVerts[i] = polylinePoints[i];
                fillVerts[fillVerts.Length - 1 - i] = new Vector2(polylinePoints[i].X, bottomY);
                fillColors[i] = fillColor;
                fillColors[fillColors.Length - 1 - i] = fillColor;
            }
            DrawPolygon(fillVerts, fillColors);

            // Polyline
            lineColor.A = 0.9f;
            for (int i = 0; i < steps; i++)
            {
                bool isStep = false;
                // Check if this segment is a step curve
                if (lane.Keyframes.Count > 1)
                {
                    float segT = (float)i / steps;
                    for (int k = 0; k < lane.Keyframes.Count - 1; k++)
                    {
                        if (segT >= lane.Keyframes[k].Time && segT < lane.Keyframes[k + 1].Time
                            && lane.Keyframes[k].CurveType == AutomationCurveType.Step)
                        {
                            isStep = true;
                            break;
                        }
                    }
                }

                if (isStep)
                {
                    // Dashed line for step segments
                    float dx = polylinePoints[i + 1].X - polylinePoints[i].X;
                    if (((int)(polylinePoints[i].X) / 4) % 2 == 0)
                        DrawLine(polylinePoints[i], polylinePoints[i + 1], lineColor, 1.5f);
                }
                else
                {
                    DrawLine(polylinePoints[i], polylinePoints[i + 1], lineColor, 1.5f);
                }
            }

            // Draw keyframe dots
            bool isSelected = (block == selectedBlock);
            for (int i = 0; i < lane.Keyframes.Count; i++)
            {
                var kf = lane.Keyframes[i];
                float valueFrac = (kf.Value - lane.MinValue) / range;
                float kfX = blockX + kf.Time * blockWidth;
                float kfY = blockY + blockHeight * (1f - valueFrac);

                Color dotColor = (isSelected && selectedKeyframeLane == lane && selectedKeyframeIndex == i)
                    ? Colors.White
                    : lineColor;
                float radius = (isSelected && selectedKeyframeLane == lane && selectedKeyframeIndex == i) ? 4f : 3f;
                DrawCircle(new Vector2(kfX, kfY), radius, dotColor);
            }
        }

        private void OnColorAutoConfirmed()
        {
            if (selectedBlock == null) return;
            colorAutoPopup.Hide();

            var color = colorAutoPicker.Color;
            float t = colorAutoNormalizedTime;

            if (selectedBlock.Automation == null)
                selectedBlock.Automation = new AutomationData();

            InsertOrUpdateColorKeyframe(selectedBlock.Automation, "ColorR", t, color.R);
            InsertOrUpdateColorKeyframe(selectedBlock.Automation, "ColorG", t, color.G);
            InsertOrUpdateColorKeyframe(selectedBlock.Automation, "ColorB", t, color.B);
        }

        private void InsertOrUpdateColorKeyframe(AutomationData data, string paramName, float time, float value)
        {
            var paramDef = AutomatableParameter.Find(paramName);
            if (paramDef == null) return;

            var lane = data.GetOrCreateLane(paramName, paramDef.Value.Default, paramDef.Value.Min, paramDef.Value.Max);

            int existing = lane.FindKeyframeNear(time, 0.01f);
            if (existing >= 0)
            {
                float oldVal = lane.Keyframes[existing].Value;
                var cmd = new MoveKeyframeCommand(lane, existing,
                    lane.Keyframes[existing].Time, oldVal, time, value);
                UndoManager.Instance.ExecuteCommand(cmd);
            }
            else
            {
                var kf = new AutomationKeyframe(time, value);
                var cmd = new AddKeyframeCommand(lane, kf);
                UndoManager.Instance.ExecuteCommand(cmd);
            }
        }

        private void DrawSingleLaneEnvelope(AutomationLane lane, LaserCueBlock block, float blockX, float blockY, float blockWidth, float blockHeight, Color lineColor)
        {
            if (lane == null || lane.Keyframes == null || lane.Keyframes.Count == 0) return;

            float range = lane.MaxValue - lane.MinValue;
            if (range <= 0f) range = 1f;

            int steps = Mathf.Max(2, (int)(blockWidth / 4f));
            var polylinePoints = new Vector2[steps + 1];

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float value = lane.Evaluate(t);
                float valueFraction = (value - lane.MinValue) / range;
                float px = blockX + t * blockWidth;
                float py = blockY + blockHeight * (1f - valueFraction);
                polylinePoints[i] = new Vector2(px, py);
            }

            // Semi-transparent fill as single polygon
            Color fillColor = lineColor;
            fillColor.A = 0.08f;
            float bottomY = blockY + blockHeight;
            var fillVerts = new Vector2[(steps + 1) * 2];
            var fillColors = new Color[(steps + 1) * 2];
            for (int i = 0; i <= steps; i++)
            {
                fillVerts[i] = polylinePoints[i];
                fillVerts[fillVerts.Length - 1 - i] = new Vector2(polylinePoints[i].X, bottomY);
                fillColors[i] = fillColor;
                fillColors[fillColors.Length - 1 - i] = fillColor;
            }
            DrawPolygon(fillVerts, fillColors);

            // Polyline
            Color drawColor = lineColor;
            drawColor.A = 0.9f;
            for (int i = 0; i < steps; i++)
                DrawLine(polylinePoints[i], polylinePoints[i + 1], drawColor, 1.5f);

            // Keyframe dots
            bool isSelected = (block == selectedBlock);
            for (int i = 0; i < lane.Keyframes.Count; i++)
            {
                var kf = lane.Keyframes[i];
                float valueFrac = (kf.Value - lane.MinValue) / range;
                float kfX = blockX + kf.Time * blockWidth;
                float kfY = blockY + blockHeight * (1f - valueFrac);

                Color dotColor = (isSelected && selectedKeyframeLane == lane && selectedKeyframeIndex == i)
                    ? Colors.White : lineColor;
                float radius = (isSelected && selectedKeyframeLane == lane && selectedKeyframeIndex == i) ? 4f : 3f;
                DrawCircle(new Vector2(kfX, kfY), radius, dotColor);
            }
        }

        // --- Automation Public API ---

        public bool IsAutomationEditMode => automationEditMode;

        public string ActiveAutomationParam
        {
            get => activeAutomationParam;
            set => activeAutomationParam = value;
        }

        public void SetActiveAutomationParam(string param)
        {
            activeAutomationParam = param;
        }

        // --- Public API ---

        public void OnBlockClicked(LaserCueBlock block)
        {
            SelectBlock(block);
        }

        public void AddBlockAtPosition(LaserCue cue, int trackIndex, float time)
        {
            if (playbackManager == null || cue == null)
                return;

            var tracks = playbackManager.Tracks;
            if (trackIndex < 0 || trackIndex >= tracks.Count)
                return;

            if (snapToGrid)
                time = SnapTimeToGrid(time);

            var block = new LaserCueBlock
            {
                Cue = cue,
                TrackIndex = trackIndex,
                StartTime = time,
                Duration = 60f / playbackManager.BPM * 2f,
                ZoneIndex = tracks[trackIndex].zoneIndex
            };

            var cmd = new AddBlockCommand(block, tracks[trackIndex], playbackManager.LaserShow);
            UndoManager.Instance.ExecuteCommand(cmd);

            SelectBlock(block);
        }

        public void ZoomIn()
        {
            pixelsPerSecond = Mathf.Min(pixelsPerSecond * 1.25f, maxPixelsPerSecond);
            MarkDirty();
        }

        public void ZoomOut()
        {
            pixelsPerSecond = Mathf.Max(pixelsPerSecond * 0.8f, minPixelsPerSecond);
            MarkDirty();
        }

        public void ScrollHorizontal(float deltaSeconds)
        {
            scrollOffset = Mathf.Max(0f, scrollOffset + deltaSeconds);
            MarkDirty();
        }
    }
}
