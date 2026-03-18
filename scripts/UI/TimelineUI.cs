using System.Collections.Generic;
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
        private LaserCueBlock draggingBlock;
        private bool isDragging;
        private float dragStartTime;
        private float dragOffsetX;

        // Resize state
        private enum ResizeEdge { None, Left, Right }
        private ResizeEdge resizeEdge = ResizeEdge.None;
        private bool isResizing;
        private LaserCueBlock resizingBlock;
        private float resizeOriginalStart;
        private float resizeOriginalDuration;
        private const float EdgeThresholdPx = 6f;

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
        }

        public override void _Process(double delta)
        {
            QueueRedraw();
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

                // Copy/Paste
                if (keyEvent.CtrlPressed && keyEvent.Keycode == Key.C && selectedBlock != null)
                {
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

                // Delete
                if (keyEvent.Keycode == Key.Delete && selectedBlock != null)
                {
                    DeleteSelectedBlock();
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButton)
            {
                HandleMouseButton(mouseButton);
            }
            else if (@event is InputEventMouseMotion mouseMotion)
            {
                HandleMouseMotion(mouseMotion);
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
                    HandleLeftClick(mouseButton.Position, mouseButton.CtrlPressed);
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
                if (isResizing)
                {
                    // Commit resize command
                    if (resizingBlock != null)
                    {
                        float newStart = resizingBlock.StartTime;
                        float newDuration = resizingBlock.Duration;
                        if (newStart != resizeOriginalStart || newDuration != resizeOriginalDuration)
                        {
                            // Revert to original so command.Execute sets new values
                            resizingBlock.StartTime = resizeOriginalStart;
                            resizingBlock.Duration = resizeOriginalDuration;
                            var cmd = new ResizeBlockCommand(resizingBlock, resizeOriginalStart, resizeOriginalDuration, newStart, newDuration);
                            UndoManager.Instance.ExecuteCommand(cmd);
                        }
                    }
                    isResizing = false;
                    resizingBlock = null;
                    resizeEdge = ResizeEdge.None;
                }
                if (isDragging)
                {
                    // Commit move command
                    if (draggingBlock != null)
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
                }
            }
        }

        private void HandleMouseMotion(InputEventMouseMotion mouseMotion)
        {
            var pos = mouseMotion.Position;

            if (isDraggingLoop && syncManager != null)
            {
                float contentX = pos.X - trackHeaderWidth;
                float time = Mathf.Max(0f, contentX / pixelsPerSecond + scrollOffset);
                if (snapToGrid) time = SnapTimeToGrid(time);
                if (loopDragHandle == LoopDragHandle.Start)
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
                draggingBlock.StartTime = newTime;
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

            UpdateCursorForPosition(pos);
        }

        private void HandleLeftClick(Vector2 localPos, bool ctrlPressed)
        {
            if (playbackManager == null)
                return;

            // Check ruler area
            if (localPos.Y < RulerHeight)
            {
                HandleRulerClick(localPos);
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

            // Check for block body click (select + drag / ctrl+drag duplicate)
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

                        SelectBlock(block);
                        if (!block.Locked)
                        {
                            isDragging = true;
                            draggingBlock = block;
                            dragStartTime = block.StartTime;
                            dragOffsetX = contentX - xPos;
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

            // Clicked empty space
            SelectBlock(null);
        }

        private void HandleRulerClick(Vector2 localPos)
        {
            if (syncManager == null) return;
            float contentX = localPos.X - trackHeaderWidth;
            if (contentX < 0) return;

            // Check for loop handle dragging
            if (syncManager.HasLoopRegion)
            {
                float loopStartX = (syncManager.LoopStartTime - scrollOffset) * pixelsPerSecond;
                float loopEndX = (syncManager.LoopEndTime - scrollOffset) * pixelsPerSecond;

                if (Mathf.Abs(contentX - loopStartX) <= EdgeThresholdPx)
                {
                    isDraggingLoop = true;
                    loopDragHandle = LoopDragHandle.Start;
                    return;
                }
                if (Mathf.Abs(contentX - loopEndX) <= EdgeThresholdPx)
                {
                    isDraggingLoop = true;
                    loopDragHandle = LoopDragHandle.End;
                    return;
                }
            }

            // Click ruler to seek
            float time = contentX / pixelsPerSecond + scrollOffset;
            syncManager.Seek(Mathf.Max(0f, time));
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

            var clone = ClipboardManager.PasteClone();
            if (clone == null) return;

            float pasteTime = syncManager.CurrentTime;
            if (snapToGrid) pasteTime = SnapTimeToGrid(pasteTime);
            clone.StartTime = pasteTime;

            // Find track for paste
            int trackIdx = selectedBlock?.TrackIndex ?? 0;
            var tracks = playbackManager.Tracks;
            if (trackIdx < 0 || trackIdx >= tracks.Count) trackIdx = 0;
            if (tracks.Count == 0) return;

            clone.TrackIndex = trackIdx;
            var cmd = new AddBlockCommand(clone, tracks[trackIdx], playbackManager.LaserShow);
            UndoManager.Instance.ExecuteCommand(cmd);
            SelectBlock(clone);
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
            SelectBlock(block);
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
                resizingBlock.Duration = Mathf.Max(minDuration, newDuration);
            }
            else if (resizeEdge == ResizeEdge.Left)
            {
                float originalEnd = resizingBlock.StartTime + resizingBlock.Duration;
                float newStart = Mathf.Min(time, originalEnd - minDuration);
                newStart = Mathf.Max(0f, newStart);
                resizingBlock.Duration = originalEnd - newStart;
                resizingBlock.StartTime = newStart;
            }
        }

        private float GetMinBlockDuration()
        {
            if (playbackManager == null || playbackManager.BPM <= 0f)
                return 0.1f;
            return 60f / playbackManager.BPM * snapBeatDivision;
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

        // --- Block Selection ---

        private void SelectBlock(LaserCueBlock block)
        {
            selectedBlock = block;
            OnBlockSelected?.Invoke(block);

            if (inspectorPanel != null)
            {
                if (block != null)
                    inspectorPanel.ShowBlock(block);
                else
                    inspectorPanel.HidePanel();
            }

            QueueRedraw();
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

                    float xPos = contentLeft + (block.StartTime - scrollOffset) * pixelsPerSecond;
                    float width = block.Duration * pixelsPerSecond;
                    float y = trackIdx * trackHeight + RulerHeight + 2;
                    float height = trackHeight - 4;

                    if (xPos + width < contentLeft || xPos > viewWidth)
                        continue;

                    // Block fill
                    Color blockColor = block.Cue.Color;
                    float alpha = (block == selectedBlock) ? 1f : 0.7f;
                    if (block.Muted) alpha = 0.25f;
                    blockColor.A = alpha;
                    DrawRect(new Rect2(xPos, y, width, height), blockColor);

                    // Fade regions
                    if (block.FadeInDuration > 0f)
                    {
                        float fadeW = block.FadeInDuration * pixelsPerSecond;
                        DrawRect(new Rect2(xPos, y, Mathf.Min(fadeW, width), height),
                            new Color(0f, 0f, 0f, 0.25f));
                    }
                    if (block.FadeOutDuration > 0f)
                    {
                        float fadeW = block.FadeOutDuration * pixelsPerSecond;
                        float fadeX = xPos + width - Mathf.Min(fadeW, width);
                        DrawRect(new Rect2(fadeX, y, Mathf.Min(fadeW, width), height),
                            new Color(0f, 0f, 0f, 0.25f));
                    }

                    // Border
                    Color borderColor = (block == selectedBlock)
                        ? Colors.White
                        : new Color(1f, 1f, 1f, 0.3f);
                    float borderWidth = (block == selectedBlock) ? 2f : 1f;
                    DrawRect(new Rect2(xPos, y, width, height), borderColor, false, borderWidth);

                    // Block label
                    if (_font != null && width > 20)
                    {
                        string label = block.Cue.CueName;
                        if (block.Locked) label = "L " + label;
                        DrawString(_font, new Vector2(xPos + 4, y + height / 2 + 4),
                            label, HorizontalAlignment.Left, (int)width - 8, 11, Colors.White);
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
            QueueRedraw();
        }

        public void ZoomOut()
        {
            pixelsPerSecond = Mathf.Max(pixelsPerSecond * 0.8f, minPixelsPerSecond);
            QueueRedraw();
        }

        public void ScrollHorizontal(float deltaSeconds)
        {
            scrollOffset = Mathf.Max(0f, scrollOffset + deltaSeconds);
            QueueRedraw();
        }
    }
}
