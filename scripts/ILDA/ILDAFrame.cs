using System.Collections.Generic;

namespace LazerSystem.ILDA
{
    /// <summary>
    /// Represents a single frame (section) from an ILDA file.
    /// </summary>
    public class ILDAFrame
    {
        /// <summary>All points in this frame.</summary>
        public List<ILDAPoint> points = new List<ILDAPoint>();

        /// <summary>The frame/section name from the ILDA header (up to 8 characters).</summary>
        public string frameName = string.Empty;

        /// <summary>The frame number as stored in the ILDA header.</summary>
        public int frameNumber;
    }
}
