using System;
using Godot;
using LazerSystem.Core;

namespace LazerSystem.Patterns
{
    [Serializable]
    public class PatternParameters
    {
        public Color color = Colors.White;
        public float intensity = 1f;
        public float size = 0.5f;
        public float rotation;
        public float speed = 1f;
        public float spread;
        public int count = 1;
        public float frequency = 1f;
        public float amplitude = 0.5f;
        public Vector2 position = Vector2.Zero;
        public string text = "";

        public PatternParameters() { }

        public PatternParameters(Color color, float intensity, float size, float rotation,
            float speed, float spread, int count, float frequency, float amplitude, Vector2 position)
        {
            this.color = color;
            this.intensity = intensity;
            this.size = size;
            this.rotation = rotation;
            this.speed = speed;
            this.spread = spread;
            this.count = count;
            this.frequency = frequency;
            this.amplitude = amplitude;
            this.position = position;
        }

        /// <summary>
        /// Copies values from a LaserCue into this instance (no allocation).
        /// </summary>
        public void CopyFromCue(LaserCue cue)
        {
            color = cue.Color;
            intensity = cue.Intensity;
            size = cue.Size;
            rotation = cue.Rotation;
            speed = cue.Speed;
            spread = cue.Spread;
            count = cue.Count;
            frequency = cue.Frequency;
            amplitude = cue.Amplitude;
            position = cue.Position;
            text = cue.CueName ?? "";
        }

        /// <summary>
        /// Creates PatternParameters from a LaserCue Resource.
        /// </summary>
        public static PatternParameters FromCue(LaserCue cue)
        {
            return new PatternParameters
            {
                color = cue.Color,
                intensity = cue.Intensity,
                size = cue.Size,
                rotation = cue.Rotation,
                speed = cue.Speed,
                spread = cue.Spread,
                count = cue.Count,
                frequency = cue.Frequency,
                amplitude = cue.Amplitude,
                position = cue.Position,
                text = cue.CueName ?? ""
            };
        }

        /// <summary>
        /// Returns the color scaled by intensity.
        /// </summary>
        public Color EffectiveColor()
        {
            return color * intensity;
        }
    }
}
