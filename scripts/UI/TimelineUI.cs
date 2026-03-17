using System.Collections.Generic;
using Godot;
using LazerSystem.Core;
using LazerSystem.Timeline;
using LazerSystem.Sync;

namespace LazerSystem.UI
{
    /// <summary>
    /// UI controller for the timeline view, rendering tracks, cue blocks,
    /// playhead, beat grid, and supporting zoom/scroll/drag interactions.
    /// Uses Godot's Control._Draw() for custom rendering.
    /// </summary>
    public partial class TimelineUI : Control
    {
        [ExportGroup("References")]
        [Export] private PlaybackManager playbackManager;
        [Export] private SyncManager syncManager;

        [ExportGroup("Layout")]
        [Export] private Control timelineViewport;

        [ExportGroup("Zoom & Scroll")]
        [Export] private float pixelsPerSecond = 100f;
        [Export] private float scrollOffset;
        [Export] private float minPixelsPerSecond = 20f;
        [Export] private float maxPixelsPerSecond = 500f;
        [Export] private float zoomSpeed = 20f;

        [ExportGroup("Snap")]
        [Export] private bool snapToGrid = true;
        [Export] private float snapBeatDivision = 1f; // 1 = quarter note, 0.5 = eighth, etc.

        [ExportGroup("Visual Settings")]
        [Export] private Color playheadColor = Colors.Red;
        [Export] private Color beatLineColor = new Color(1f, 1f, 1f, 0.15f);
        [Export] private Color barLineColor = new Color(1f, 1f, 1f, 0.4f);
        [Export] private float trackHeight = 40f;
        [Export] private Color trackColor = new Color(0.2f, 0.2f, 0.25f, 1f);
        [Export] private Color trackMutedColor = new Color(0.15f, 0.15f, 0.15f, 1f);

        /// <summary>Event raised when a cue block is clicked/selected.</summary>
        public event System.Action<LaserCueBlock> OnBlockSelected;

        private LaserCueBlock selectedBlock;
        private LaserCueBlock draggingBlock;
        private bool isDragging;
        private float dragStartTime;
        private float dragOffsetX;

        // Font for drawing text
        private Font _font;

        public override void _Ready()
        {
            _font = ThemeDB.FallbackFont;
        }

        public override void _Process(double delta)
        {
            // Continuously redraw to keep playhead updated
            QueueRedraw();
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButton)
            {
                if (mouseButton.Pressed)
                {
                    if (mouseButton.ButtonIndex == MouseButton.WheelUp)
                    {
                        if (mouseButton.CtrlPressed)
                        {
                            // Ctrl + scroll up = zoom in
                            pixelsPerSecond = Mathf.Clamp(
                                pixelsPerSecond + zoomSpeed * pixelsPerSecond * 0.01f,
                                minPixelsPerSecond,
                                maxPixelsPerSecond
                            );
                        }
                        else
                        {
                            // Scroll up = pan left
                            scrollOffset -= 200f / pixelsPerSecond;
                            scrollOffset = Mathf.Max(0f, scrollOffset);
                        }
                        AcceptEvent();
                    }
                    else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
                    {
                        if (mouseButton.CtrlPressed)
                        {
                            // Ctrl + scroll down = zoom out
                            pixelsPerSecond = Mathf.Clamp(
                                pixelsPerSecond - zoomSpeed * pixelsPerSecond * 0.01f,
                                minPixelsPerSecond,
                                maxPixelsPerSecond
                            );
                        }
                        else
                        {
                            // Scroll down = pan right
                            scrollOffset += 200f / pixelsPerSecond;
                        }
                        AcceptEvent();
                    }
                    else if (mouseButton.ButtonIndex == MouseButton.Left)
                    {
                        // Check if clicking on a block
                        HandleBlockClick(mouseButton.Position);
                        AcceptEvent();
                    }
                }
                else if (!mouseButton.Pressed && isDragging)
                {
                    isDragging = false;
                    draggingBlock = null;
                }
            }
            else if (@event is InputEventMouseMotion mouseMotion && isDragging && draggingBlock != null)
            {
                // Handle block dragging
                float newTime = (mouseMotion.Position.X - dragOffsetX) / pixelsPerSecond + scrollOffset;
                newTime = Mathf.Max(0f, newTime);

                if (snapToGrid)
                {
                    newTime = SnapTimeToGrid(newTime);
                }

                draggingBlock.StartTime = newTime;
                AcceptEvent();
            }
        }

        /// <summary>Handles clicking on cue blocks in the timeline.</summary>
        private void HandleBlockClick(Vector2 localPos)
        {
            if (playbackManager == null)
                return;

            var tracks = playbackManager.Tracks;

            for (int trackIdx = 0; trackIdx < tracks.Count; trackIdx++)
            {
                float trackY = trackIdx * trackHeight;
                if (localPos.Y < trackY || localPos.Y > trackY + trackHeight)
                    continue;

                foreach (var block in tracks[trackIdx].GetActiveBlocks(0f))
                {
                    // Check all blocks, not just active
                }

                // Check all blocks on this track
                var allBlocks = tracks[trackIdx].GetActiveBlocks(float.MaxValue);
                // Since GetActiveBlocks filters by time, iterate the blocks list directly
            }

            // Iterate all tracks and blocks for hit testing
            for (int trackIdx = 0; trackIdx < tracks.Count; trackIdx++)
            {
                float trackY = trackIdx * trackHeight;
                if (localPos.Y < trackY || localPos.Y > trackY + trackHeight)
                    continue;

                // Access blocks via the track
                foreach (var block in GetAllBlocksForTrack(tracks[trackIdx]))
                {
                    float xPos = (block.StartTime - scrollOffset) * pixelsPerSecond;
                    float width = block.Duration * pixelsPerSecond;

                    if (localPos.X >= xPos && localPos.X <= xPos + width)
                    {
                        // Found clicked block
                        selectedBlock = block;
                        OnBlockSelected?.Invoke(block);

                        // Start dragging
                        isDragging = true;
                        draggingBlock = block;
                        dragStartTime = block.StartTime;
                        dragOffsetX = localPos.X - xPos;
                        return;
                    }
                }
            }
        }

        /// <summary>Gets all blocks from a track (helper to avoid time filtering).</summary>
        private List<LaserCueBlock> GetAllBlocksForTrack(TimelineTrack track)
        {
            // Return all blocks by checking a very wide time range
            var result = new List<LaserCueBlock>();
            // Access through the public blocks property if available
            // Since TimelineTrack stores blocks in a Godot Array, we iterate it
            if (track.blocks != null)
            {
                foreach (var block in track.blocks)
                    result.Add(block);
            }
            return result;
        }

        public override void _Draw()
        {
            if (playbackManager == null)
                return;

            var tracks = playbackManager.Tracks;
            float viewWidth = Size.X;
            float viewHeight = Size.Y;

            // Draw track backgrounds
            for (int i = 0; i < tracks.Count; i++)
            {
                float y = i * trackHeight;
                Color bgColor = tracks[i].muted ? trackMutedColor : trackColor;
                DrawRect(new Rect2(0, y, viewWidth, trackHeight), bgColor);

                // Track separator line
                DrawLine(new Vector2(0, y + trackHeight), new Vector2(viewWidth, y + trackHeight),
                    new Color(1f, 1f, 1f, 0.1f));

                // Track name
                if (_font != null)
                {
                    DrawString(_font, new Vector2(4, y + 14), tracks[i].trackName,
                        HorizontalAlignment.Left, -1, 12, new Color(1f, 1f, 1f, 0.6f));
                }
            }

            // Draw beat grid lines
            DrawBeatGrid(viewWidth, viewHeight, tracks.Count);

            // Draw cue blocks
            for (int trackIdx = 0; trackIdx < tracks.Count; trackIdx++)
            {
                var track = tracks[trackIdx];
                if (track.blocks == null)
                    continue;

                foreach (var block in track.blocks)
                {
                    if (block == null || block.Cue == null)
                        continue;

                    float xPos = (block.StartTime - scrollOffset) * pixelsPerSecond;
                    float width = block.Duration * pixelsPerSecond;
                    float y = trackIdx * trackHeight + 2;
                    float height = trackHeight - 4;

                    // Skip blocks outside view
                    if (xPos + width < 0 || xPos > viewWidth)
                        continue;

                    // Block rectangle
                    Color blockColor = block.Cue.Color;
                    blockColor.A = (block == selectedBlock) ? 1f : 0.7f;
                    DrawRect(new Rect2(xPos, y, width, height), blockColor);

                    // Block border
                    DrawRect(new Rect2(xPos, y, width, height), new Color(1f, 1f, 1f, 0.3f), false, 1f);

                    // Block label
                    if (_font != null && width > 20)
                    {
                        DrawString(_font, new Vector2(xPos + 4, y + height / 2 + 4),
                            block.Cue.CueName, HorizontalAlignment.Left, (int)width - 8, 11, Colors.White);
                    }
                }
            }

            // Draw playhead
            if (syncManager != null)
            {
                float currentTime = syncManager.CurrentTime;
                float playheadX = (currentTime - scrollOffset) * pixelsPerSecond;

                if (playheadX >= 0 && playheadX <= viewWidth)
                {
                    float totalHeight = Mathf.Max(tracks.Count * trackHeight, viewHeight);
                    DrawLine(new Vector2(playheadX, 0), new Vector2(playheadX, totalHeight),
                        playheadColor, 2f);
                }
            }
        }

        /// <summary>Draws beat and bar grid lines across the timeline.</summary>
        private void DrawBeatGrid(float viewWidth, float viewHeight, int trackCount)
        {
            if (playbackManager == null || playbackManager.BPM <= 0f)
                return;

            float bpm = playbackManager.BPM;
            float beatDuration = 60f / bpm;
            float totalHeight = Mathf.Max(trackCount * trackHeight, viewHeight);

            // Calculate visible time range
            float startTime = scrollOffset;
            float endTime = scrollOffset + viewWidth / pixelsPerSecond;

            int startBeat = Mathf.FloorToInt(startTime / beatDuration);
            int endBeat = Mathf.CeilToInt(endTime / beatDuration);

            for (int beat = startBeat; beat <= endBeat; beat++)
            {
                if (beat < 0) continue;

                float time = beat * beatDuration;
                float x = (time - scrollOffset) * pixelsPerSecond;

                if (x < 0 || x > viewWidth)
                    continue;

                bool isBar = (beat % 4) == 0;
                Color lineColor = isBar ? barLineColor : beatLineColor;
                float lineWidth = isBar ? 1.5f : 1f;

                DrawLine(new Vector2(x, 0), new Vector2(x, totalHeight), lineColor, lineWidth);

                // Draw bar numbers at bar lines
                if (isBar && _font != null)
                {
                    int barNumber = beat / 4 + 1;
                    DrawString(_font, new Vector2(x + 2, 10), barNumber.ToString(),
                        HorizontalAlignment.Left, -1, 10, new Color(1f, 1f, 1f, 0.5f));
                }
            }
        }

        /// <summary>Snaps a time value to the nearest beat grid position.</summary>
        private float SnapTimeToGrid(float time)
        {
            if (playbackManager == null || playbackManager.BPM <= 0f)
                return time;

            float beatDuration = 60f / playbackManager.BPM * snapBeatDivision;
            if (beatDuration <= 0f)
                return time;

            return Mathf.Round(time / beatDuration) * beatDuration;
        }

        /// <summary>Handles cue block selection.</summary>
        public void OnBlockClicked(LaserCueBlock block)
        {
            selectedBlock = block;
            OnBlockSelected?.Invoke(block);
            QueueRedraw();
        }

        /// <summary>
        /// Adds a new cue block at the specified position on the timeline.
        /// </summary>
        public void AddBlockAtPosition(LaserCue cue, int trackIndex, float time)
        {
            if (playbackManager == null || cue == null)
                return;

            var tracks = playbackManager.Tracks;
            if (trackIndex < 0 || trackIndex >= tracks.Count)
                return;

            if (snapToGrid)
            {
                time = SnapTimeToGrid(time);
            }

            var block = new LaserCueBlock
            {
                Cue = cue,
                TrackIndex = trackIndex,
                StartTime = time,
                Duration = 60f / playbackManager.BPM, // Default: one beat
                ZoneIndex = tracks[trackIndex].zoneIndex
            };

            tracks[trackIndex].AddBlock(block);

            // Also add to the show's master list
            if (playbackManager.LaserShow != null)
            {
                playbackManager.LaserShow.TimelineBlocks.Add(block);
            }

            QueueRedraw();
        }

        /// <summary>Zooms in on the timeline.</summary>
        public void ZoomIn()
        {
            pixelsPerSecond = Mathf.Min(pixelsPerSecond * 1.25f, maxPixelsPerSecond);
            QueueRedraw();
        }

        /// <summary>Zooms out on the timeline.</summary>
        public void ZoomOut()
        {
            pixelsPerSecond = Mathf.Max(pixelsPerSecond * 0.8f, minPixelsPerSecond);
            QueueRedraw();
        }

        /// <summary>Scrolls the timeline horizontally by the given amount in seconds.</summary>
        public void ScrollHorizontal(float deltaSeconds)
        {
            scrollOffset = Mathf.Max(0f, scrollOffset + deltaSeconds);
            QueueRedraw();
        }
    }
}
