using Godot;
using Godot.Collections;

namespace LazerSystem.Core
{
    /// <summary>
    /// Stores a freehand laser pattern as a list of normalized points.
    /// Saved as .tres files in user://patterns/.
    /// </summary>
    [GlobalClass]
    public partial class CustomPointPattern : Resource
    {
        [Export] public string PatternName = "Untitled";

        /// <summary>Normalized points in -1..1 range.</summary>
        [Export] public Array<Vector2> Points { get; set; } = new Array<Vector2>();

        /// <summary>Per-point colors. If empty or shorter than Points, uses white.</summary>
        [Export] public Array<Color> PointColors { get; set; } = new Array<Color>();

        /// <summary>Whether to connect the last point back to the first.</summary>
        [Export] public bool Closed { get; set; }

        /// <summary>Gets the color for a given point index, defaulting to white.</summary>
        public Color GetPointColor(int index)
        {
            if (PointColors != null && index >= 0 && index < PointColors.Count)
                return PointColors[index];
            return Colors.White;
        }
    }
}
