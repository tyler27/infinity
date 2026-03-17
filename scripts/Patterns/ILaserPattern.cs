using System.Collections.Generic;
using LazerSystem.Core;

namespace LazerSystem.Patterns
{
    public interface ILaserPattern
    {
        string PatternName { get; }

        /// <summary>
        /// Generates pattern points into the provided list (cleared by caller).
        /// </summary>
        void Generate(float time, PatternParameters parameters, List<LaserPoint> output);
    }
}
