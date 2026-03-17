using System.Collections.Generic;
using LazerSystem.Core;

namespace LazerSystem.Patterns
{
    public interface ILaserPattern
    {
        string PatternName { get; }
        List<LaserPoint> Generate(float time, PatternParameters parameters);
    }
}
