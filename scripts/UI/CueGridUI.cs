using System.Collections.Generic;
using Godot;
using LazerSystem.Core;
using LazerSystem.Timeline;

namespace LazerSystem.UI
{
    /// <summary>
    /// UI controller that renders the cue grid as an interactive grid of buttons.
    /// Supports page tabs, live triggering, cue selection, and drag-to-timeline.
    /// </summary>
    public partial class CueGridUI : Control
    {
        [ExportGroup("References")]
        [Export] private CueGridManager cueGridManager;

        [ExportGroup("Grid Container")]
        [Export] private Control gridContainer;
        [Export] private PackedScene cueCellScene;

        [ExportGroup("Page Tabs")]
        [Export] private Control pageTabContainer;
        [Export] private PackedScene pageTabScene;

        [ExportGroup("Mode")]
        [Export] private bool liveMode = true;

        [ExportGroup("Visual Settings")]
        [Export] private Color emptyCellColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        [Export] private Color selectedCellColor = new Color(0.3f, 0.6f, 1f, 1f);
        [Export] private Color activeCellColor = new Color(0f, 1f, 0.3f, 1f);

        /// <summary>Event raised when a cue cell is clicked for editing.</summary>
        public event System.Action<int, int, LaserCue> OnCueSelected;

        /// <summary>Event raised when a cue drag begins (for dragging to timeline).</summary>
        public event System.Action<LaserCue> OnCueDragStarted;

        private List<List<CueCellUI>> cellGrid = new List<List<CueCellUI>>();
        private List<Button> pageButtons = new List<Button>();
        private int selectedRow = -1;
        private int selectedCol = -1;

        public override void _Ready()
        {
            BuildPageTabs();
            BuildGrid();
            RefreshGrid();
        }

        /// <summary>Creates page tab buttons at the top of the grid.</summary>
        private void BuildPageTabs()
        {
            if (cueGridManager == null || pageTabContainer == null || pageTabScene == null)
                return;

            pageButtons.Clear();

            for (int i = 0; i < cueGridManager.NumPages; i++)
            {
                int pageIndex = i;
                var tabNode = pageTabScene.Instantiate<Control>();
                tabNode.Name = $"PageTab_{i + 1}";
                pageTabContainer.AddChild(tabNode);

                var button = tabNode as Button ?? tabNode.GetNode<Button>(".");
                if (button != null)
                {
                    button.Pressed += () => SetPage(pageIndex);
                    pageButtons.Add(button);
                }

                var label = tabNode.GetNodeOrNull<Label>("Label");
                if (label != null)
                {
                    label.Text = $"Page {i + 1}";
                }
                else if (button != null)
                {
                    button.Text = $"Page {i + 1}";
                }
            }
        }

        /// <summary>Builds the grid of cue cell UI elements.</summary>
        private void BuildGrid()
        {
            if (cueGridManager == null || gridContainer == null || cueCellScene == null)
                return;

            // Clear existing cells
            foreach (var child in gridContainer.GetChildren())
            {
                child.QueueFree();
            }
            cellGrid.Clear();

            for (int row = 0; row < cueGridManager.Rows; row++)
            {
                var rowList = new List<CueCellUI>();

                for (int col = 0; col < cueGridManager.Columns; col++)
                {
                    int r = row;
                    int c = col;

                    var cellNode = cueCellScene.Instantiate<Control>();
                    cellNode.Name = $"Cell_{row}_{col}";
                    gridContainer.AddChild(cellNode);

                    var cellUI = cellNode as CueCellUI;
                    if (cellUI == null)
                    {
                        GD.Print($"[CueGridUI] Cell scene root must be a CueCellUI node.");
                        continue;
                    }

                    cellUI.Row = r;
                    cellUI.Column = c;

                    // Click handler
                    var button = cellNode.GetNodeOrNull<Button>("Button") ?? cellNode as Button;
                    if (button != null)
                    {
                        button.Pressed += () => OnCellClicked(r, c);

                        // Right-click handling
                        button.GuiInput += (InputEvent @event) =>
                        {
                            if (@event is InputEventMouseButton mouseEvent
                                && mouseEvent.Pressed
                                && mouseEvent.ButtonIndex == MouseButton.Right)
                            {
                                OnCellRightClicked(r, c);
                            }
                        };
                    }

                    rowList.Add(cellUI);
                }

                cellGrid.Add(rowList);
            }
        }

        /// <summary>Refreshes all cell visuals to reflect current cue grid state.</summary>
        public void RefreshGrid()
        {
            if (cueGridManager == null)
                return;

            int page = cueGridManager.CurrentPage;

            for (int row = 0; row < cueGridManager.Rows && row < cellGrid.Count; row++)
            {
                for (int col = 0; col < cueGridManager.Columns && col < cellGrid[row].Count; col++)
                {
                    var cell = cellGrid[row][col];
                    LaserCue cue = cueGridManager.GetCue(page, row, col);

                    if (cue != null)
                    {
                        cell.SetCue(cue.CueName, cue.Color);
                    }
                    else
                    {
                        cell.SetEmpty(emptyCellColor);
                    }

                    // Highlight selected cell
                    if (row == selectedRow && col == selectedCol)
                    {
                        cell.SetHighlight(selectedCellColor);
                    }
                }
            }

            // Update page tab highlights
            for (int i = 0; i < pageButtons.Count; i++)
            {
                if (i == page)
                {
                    pageButtons[i].Modulate = selectedCellColor;
                }
                else
                {
                    pageButtons[i].Modulate = Colors.White;
                }
            }
        }

        /// <summary>Handles cell click - triggers cue in live mode or selects for editing.</summary>
        public void OnCellClicked(int row, int col)
        {
            if (liveMode)
            {
                // Live mode: trigger the cue
                if (cueGridManager != null)
                {
                    cueGridManager.TriggerCue(row, col);

                    // Flash the cell
                    if (row < cellGrid.Count && col < cellGrid[row].Count)
                    {
                        cellGrid[row][col].SetHighlight(activeCellColor);
                    }
                }
            }
            else
            {
                // Edit mode: select the cue
                selectedRow = row;
                selectedCol = col;

                LaserCue cue = cueGridManager != null
                    ? cueGridManager.GetCue(cueGridManager.CurrentPage, row, col)
                    : null;

                OnCueSelected?.Invoke(row, col, cue);
                RefreshGrid();
            }
        }

        /// <summary>Handles right-click on a cell - opens cue editor.</summary>
        private void OnCellRightClicked(int row, int col)
        {
            selectedRow = row;
            selectedCol = col;

            LaserCue cue = cueGridManager != null
                ? cueGridManager.GetCue(cueGridManager.CurrentPage, row, col)
                : null;

            OnCueSelected?.Invoke(row, col, cue);
            RefreshGrid();
        }

        /// <summary>Switches to the specified page and refreshes the grid.</summary>
        public void SetPage(int page)
        {
            if (cueGridManager != null)
            {
                cueGridManager.CurrentPage = page;
            }

            selectedRow = -1;
            selectedCol = -1;
            RefreshGrid();
        }

        /// <summary>Toggles between live and edit mode.</summary>
        public void SetLiveMode(bool live)
        {
            liveMode = live;
        }
    }

    /// <summary>
    /// Simple component attached to each cue cell in the grid for visual state management.
    /// </summary>
    public partial class CueCellUI : Control
    {
        public int Row { get; set; }
        public int Column { get; set; }

        [Export] private ColorRect backgroundRect;
        [Export] private ColorRect colorIndicator;
        [Export] private Label nameLabel;

        public override void _Ready()
        {
            if (backgroundRect == null)
                backgroundRect = GetNodeOrNull<ColorRect>("Background");

            if (nameLabel == null)
                nameLabel = GetNodeOrNull<Label>("Label");

            if (colorIndicator == null)
                colorIndicator = GetNodeOrNull<ColorRect>("ColorIndicator");
        }

        public void SetCue(string cueName, Color cueColor)
        {
            if (nameLabel != null)
                nameLabel.Text = cueName;

            if (colorIndicator != null)
                colorIndicator.Color = cueColor;

            if (backgroundRect != null)
            {
                Color bg = cueColor;
                bg.A = 0.3f;
                backgroundRect.Color = bg;
            }
        }

        public void SetEmpty(Color emptyColor)
        {
            if (nameLabel != null)
                nameLabel.Text = "";

            if (colorIndicator != null)
                colorIndicator.Color = Colors.Transparent;

            if (backgroundRect != null)
                backgroundRect.Color = emptyColor;
        }

        public void SetHighlight(Color highlightColor)
        {
            if (backgroundRect != null)
                backgroundRect.Color = highlightColor;
        }
    }
}
