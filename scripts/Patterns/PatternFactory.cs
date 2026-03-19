using System.Collections.Generic;
using LazerSystem.Core;

namespace LazerSystem.Patterns
{
    /// <summary>
    /// Factory for creating laser pattern instances by type.
    /// </summary>
    public static class PatternFactory
    {
        private static readonly Dictionary<LaserPatternType, ILaserPattern> Patterns =
            new Dictionary<LaserPatternType, ILaserPattern>
            {
                { LaserPatternType.Beam, new BeamPattern() },
                { LaserPatternType.Fan, new FanPattern() },
                { LaserPatternType.Cone, new ConePattern() },
                { LaserPatternType.Circle, new CirclePattern() },
                { LaserPatternType.Line, new LinePattern() },
                { LaserPatternType.Wave, new WavePattern() },
                { LaserPatternType.Triangle, new ShapePattern(isStar: false) },
                { LaserPatternType.Square, new ShapePattern(isStar: false) },
                { LaserPatternType.Star, new ShapePattern(isStar: true) },
                { LaserPatternType.Text, new TextPattern() },
                { LaserPatternType.Tunnel, new TunnelPattern() },
                { LaserPatternType.QuestionBlock, new QuestionBlockPattern() },
                { LaserPatternType.CustomILDA, new CustomPointPatternGen() },
            };

        /// <summary>
        /// Creates (returns) a laser pattern instance for the given type.
        /// Returns null if the type is not registered (e.g. CustomILDA).
        /// </summary>
        public static ILaserPattern Create(LaserPatternType type)
        {
            return Patterns.TryGetValue(type, out ILaserPattern pattern) ? pattern : null;
        }

        /// <summary>
        /// Returns all registered pattern types.
        /// </summary>
        public static IEnumerable<LaserPatternType> RegisteredTypes => Patterns.Keys;
    }
}
