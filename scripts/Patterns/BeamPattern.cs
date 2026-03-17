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

        public List<LaserPoint> Generate(float time, PatternParameters parameters)
        {
            var points = new List<LaserPoint>();
            Color c = parameters.EffectiveColor();

            // Blanking move to position
            points.Add(LaserPoint.Blanked(parameters.position.X, parameters.position.Y));

            // Single visible point at position
            points.Add(LaserPoint.Colored(
                parameters.position.X,
                parameters.position.Y,
                c.R, c.G, c.B));

            return points;
        }
    }
}
