using System;
using System.Collections.Generic;
using LazerSystem.Core;
using LazerSystem.Patterns;
using Godot;

namespace LazerSystem.ILDA
{
    /// <summary>
    /// Adapts an ILDAFile for playback through the ILaserPattern interface.
    /// Automatically steps through frames based on time and playback rate.
    /// Multi-frame files loop continuously.
    /// </summary>
    public class ILDAPatternAdapter : ILaserPattern
    {
        private readonly ILDAFile _file;
        private readonly List<List<LaserPoint>> _cachedFrames;

        /// <summary>
        /// Playback rate in frames per second. Default is 30 fps (standard ILDA playback).
        /// </summary>
        public float framesPerSecond = 30f;

        public string PatternName { get; }

        /// <summary>
        /// Creates a new adapter for the given ILDA file.
        /// </summary>
        /// <param name="file">The parsed ILDA file to play back.</param>
        /// <param name="patternName">Optional display name. Defaults to the file's embedded name.</param>
        /// <param name="fps">Playback rate in frames per second.</param>
        public ILDAPatternAdapter(ILDAFile file, string patternName = null, float fps = 30f)
        {
            _file = file ?? throw new ArgumentNullException(nameof(file));
            framesPerSecond = fps;
            PatternName = !string.IsNullOrEmpty(patternName) ? patternName : file.name;

            // Pre-convert all frames to LaserPoint lists for fast playback
            _cachedFrames = new List<List<LaserPoint>>(_file.TotalFrames);
            for (int i = 0; i < _file.TotalFrames; i++)
            {
                _cachedFrames.Add(ILDAParser.FrameToPoints(_file.frames[i]));
            }
        }

        /// <summary>
        /// Returns the laser points for the current frame based on elapsed time and playback speed.
        /// The speed parameter from PatternParameters scales the playback rate.
        /// </summary>
        public List<LaserPoint> Generate(float time, PatternParameters parameters)
        {
            if (_file.TotalFrames == 0)
                return new List<LaserPoint>();

            // Single-frame files always return the same frame
            if (_file.TotalFrames == 1)
                return ApplyParameters(_cachedFrames[0], parameters);

            float effectiveFps = framesPerSecond * parameters.speed;
            if (effectiveFps <= 0f)
                return ApplyParameters(_cachedFrames[0], parameters);

            // Calculate which frame to show based on time, looping for multi-frame files
            float frameFloat = time * effectiveFps;
            int frameIndex = ((int)frameFloat) % _file.TotalFrames;
            if (frameIndex < 0) frameIndex += _file.TotalFrames;

            return ApplyParameters(_cachedFrames[frameIndex], parameters);
        }

        /// <summary>
        /// Applies PatternParameters (size, rotation, position, color tint) to the frame points.
        /// </summary>
        private static List<LaserPoint> ApplyParameters(List<LaserPoint> sourcePoints, PatternParameters parameters)
        {
            if (parameters == null)
                return new List<LaserPoint>(sourcePoints);

            var result = new List<LaserPoint>(sourcePoints.Count);
            float cos = Mathf.Cos(Mathf.DegToRad(parameters.rotation));
            float sin = Mathf.Sin(Mathf.DegToRad(parameters.rotation));
            Color tint = parameters.EffectiveColor();

            for (int i = 0; i < sourcePoints.Count; i++)
            {
                LaserPoint src = sourcePoints[i];

                // Scale
                float sx = src.x * parameters.size;
                float sy = src.y * parameters.size;

                // Rotate
                float rx = sx * cos - sy * sin;
                float ry = sx * sin + sy * cos;

                // Translate
                float fx = rx + parameters.position.X;
                float fy = ry + parameters.position.Y;

                // Tint color
                float cr = src.blanking ? 0f : src.r * tint.R;
                float cg = src.blanking ? 0f : src.g * tint.G;
                float cb = src.blanking ? 0f : src.b * tint.B;

                result.Add(new LaserPoint(fx, fy, cr, cg, cb, src.blanking));
            }

            return result;
        }
    }
}
