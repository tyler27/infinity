using System.Collections.Generic;
using Godot;
using LazerSystem.Core;
using LazerSystem.Sync;

namespace LazerSystem.Timeline
{
    /// <summary>
    /// Manages a paged 2D grid of laser cues for live triggering and organization.
    /// Cues are organized by [page][row][column].
    /// </summary>
    public partial class CueGridManager : Node
    {
        [ExportGroup("Grid Dimensions")]
        [Export] private int numPages = 4;
        [Export] private int rows = 8;
        [Export] private int columns = 16;

        [ExportGroup("References")]
        [Export] private SyncManager syncManager;

        [ExportGroup("State")]
        [Export] private int currentPage;

        // 3D array flattened: [page][row][column]
        private LaserCue[,,] cueGrid;

        [ExportGroup("Live Cues")]
        private List<ActiveLiveCue> activeLiveCues = new List<ActiveLiveCue>();

        public int CurrentPage
        {
            get => currentPage;
            set => currentPage = Mathf.Clamp(value, 0, numPages - 1);
        }

        public int NumPages => numPages;
        public int Rows => rows;
        public int Columns => columns;
        public List<ActiveLiveCue> ActiveLiveCues => activeLiveCues;

        public override void _Ready()
        {
            InitializeGrid();
        }

        /// <summary>Initializes or reinitializes the cue grid array.</summary>
        private void InitializeGrid()
        {
            cueGrid = new LaserCue[numPages, rows, columns];
        }

        /// <summary>
        /// Gets the cue at the specified grid position.
        /// Returns null if out of bounds or no cue is assigned.
        /// </summary>
        public LaserCue GetCue(int page, int row, int col)
        {
            if (!IsValidPosition(page, row, col))
                return null;

            return cueGrid[page, row, col];
        }

        /// <summary>
        /// Sets a cue at the specified grid position.
        /// Pass null to clear the slot.
        /// </summary>
        public void SetCue(int page, int row, int col, LaserCue cue)
        {
            if (!IsValidPosition(page, row, col))
            {
                GD.Print($"[CueGridManager] Invalid grid position: page={page}, row={row}, col={col}");
                return;
            }

            cueGrid[page, row, col] = cue;
        }

        /// <summary>
        /// Triggers a cue at the specified row and column on the current page.
        /// The cue is immediately added to the active live cues list for playback
        /// on its assigned zone.
        /// </summary>
        public void TriggerCue(int row, int col)
        {
            TriggerCue(currentPage, row, col);
        }

        /// <summary>
        /// Triggers a cue at the specified page, row, and column.
        /// </summary>
        public void TriggerCue(int page, int row, int col)
        {
            LaserCue cue = GetCue(page, row, col);
            if (cue == null)
            {
                GD.Print($"[CueGridManager] No cue at page={page}, row={row}, col={col}");
                return;
            }

            float triggerTime = syncManager != null ? syncManager.CurrentTime : (float)(Time.GetTicksMsec() / 1000.0);

            // Determine zone index from the cue's grid row (one zone per row by default)
            int zoneIndex = row;

            var liveCue = new ActiveLiveCue
            {
                cue = cue,
                triggerTime = triggerTime,
                zoneIndex = zoneIndex,
                isActive = true
            };

            activeLiveCues.Add(liveCue);

            GD.Print($"[CueGridManager] Triggered cue '{cue.CueName}' on zone {zoneIndex}");
        }

        /// <summary>
        /// Stops a live cue by marking it inactive.
        /// </summary>
        public void StopLiveCue(int index)
        {
            if (index >= 0 && index < activeLiveCues.Count)
            {
                var cue = activeLiveCues[index];
                cue.isActive = false;
                activeLiveCues[index] = cue;
            }
        }

        /// <summary>
        /// Removes all inactive live cues from the list.
        /// </summary>
        public void CleanupInactiveCues()
        {
            activeLiveCues.RemoveAll(c => !c.isActive);
        }

        /// <summary>
        /// Stops all active live cues.
        /// </summary>
        public void StopAllLiveCues()
        {
            for (int i = 0; i < activeLiveCues.Count; i++)
            {
                var cue = activeLiveCues[i];
                cue.isActive = false;
                activeLiveCues[i] = cue;
            }
            activeLiveCues.Clear();
        }

        private bool IsValidPosition(int page, int row, int col)
        {
            return page >= 0 && page < numPages
                && row >= 0 && row < rows
                && col >= 0 && col < columns;
        }
    }

    /// <summary>
    /// Represents a cue that has been triggered live (outside the timeline).
    /// </summary>
    public class ActiveLiveCue
    {
        public LaserCue cue;
        public float triggerTime;
        public int zoneIndex;
        public bool isActive;
    }
}
