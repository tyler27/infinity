using System.Collections.Generic;

namespace LazerSystem.ILDA
{
    /// <summary>
    /// Represents a parsed ILDA standard laser file containing one or more frames.
    /// </summary>
    public class ILDAFile
    {
        /// <summary>All frames parsed from the file.</summary>
        public List<ILDAFrame> frames = new List<ILDAFrame>();

        /// <summary>Name read from the first section header.</summary>
        public string name = string.Empty;

        /// <summary>Company name read from the first section header.</summary>
        public string companyName = string.Empty;

        /// <summary>Total number of frames in this file.</summary>
        public int TotalFrames => frames.Count;
    }
}
