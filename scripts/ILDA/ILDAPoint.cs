namespace LazerSystem.ILDA
{
    /// <summary>
    /// Represents a single point from an ILDA laser frame.
    /// Coordinates are normalized to the -1..1 range. Colors are normalized to 0..1.
    /// </summary>
    public struct ILDAPoint
    {
        /// <summary>Normalized X coordinate (-1 to 1).</summary>
        public float x;

        /// <summary>Normalized Y coordinate (-1 to 1).</summary>
        public float y;

        /// <summary>Normalized Z coordinate (-1 to 1). Zero for 2D formats.</summary>
        public float z;

        /// <summary>Red channel (0-1).</summary>
        public float r;

        /// <summary>Green channel (0-1).</summary>
        public float g;

        /// <summary>Blue channel (0-1).</summary>
        public float b;

        /// <summary>When true the laser beam is blanked (off) while moving to this point.</summary>
        public bool blanking;

        /// <summary>When true this is the last point in the frame.</summary>
        public bool isLastPoint;

        public override string ToString()
        {
            return $"ILDAPoint({x:F3}, {y:F3}, {z:F3}) RGB({r:F2}, {g:F2}, {b:F2}){(blanking ? " [blanked]" : "")}{(isLastPoint ? " [last]" : "")}";
        }
    }
}
