using System.Collections.Generic;
using Godot;
using LazerSystem.Core;

namespace LazerSystem.Patterns
{
    /// <summary>
    /// Single beam/point pattern. Generates a single laser point at the specified position.
    /// </summary>
    public class BeamPattern : ILaserPattern
    {
        public string PatternName => "Beam";

        public void Generate(float time, PatternParameters parameters, List<LaserPoint> output)
        {
            Color c = parameters.EffectiveColor();

            // Blanking move to position
            output.Add(LaserPoint.Blanked(parameters.position.X, parameters.position.Y));

            // Single visible point at position
            output.Add(LaserPoint.Colored(
                parameters.position.X,
                parameters.position.Y,
                c.R, c.G, c.B));
        }
    }
}
