using Godot;

namespace LazerSystem.Core
{
    [System.Serializable]
    public struct LaserPoint
    {
        /// <summary>Normalized X position (-1 to 1).</summary>
        public float x;

        /// <summary>Normalized Y position (-1 to 1).</summary>
        public float y;

        /// <summary>Red channel (0-1).</summary>
        public float r;

        /// <summary>Green channel (0-1).</summary>
        public float g;

        /// <summary>Blue channel (0-1).</summary>
        public float b;

        /// <summary>When true the laser is blanked (off) while travelling to this point.</summary>
        public bool blanking;

        public LaserPoint(float x, float y, float r, float g, float b, bool blanking = false)
        {
            this.x = Mathf.Clamp(x, -1f, 1f);
            this.y = Mathf.Clamp(y, -1f, 1f);
            this.r = Mathf.Clamp(r, 0f, 1f);
            this.g = Mathf.Clamp(g, 0f, 1f);
            this.b = Mathf.Clamp(b, 0f, 1f);
            this.blanking = blanking;
        }

        /// <summary>Creates a blanked (invisible) point used for repositioning the beam.</summary>
        public static LaserPoint Blanked(float x, float y)
        {
            return new LaserPoint(x, y, 0f, 0f, 0f, blanking: true);
        }

        /// <summary>Creates a visible point with the specified color.</summary>
        public static LaserPoint Colored(float x, float y, Color color)
        {
            return new LaserPoint(x, y, color.R, color.G, color.B, blanking: false);
        }

        /// <summary>Creates a visible point with explicit RGB values.</summary>
        public static LaserPoint Colored(float x, float y, float r, float g, float b)
        {
            return new LaserPoint(x, y, r, g, b, blanking: false);
        }

        /// <summary>Returns the color of this point as a Godot Color.</summary>
        public Color ToColor()
        {
            return new Color(r, g, b, blanking ? 0f : 1f);
        }

        /// <summary>Returns the position as a Vector2.</summary>
        public Vector2 ToVector2()
        {
            return new Vector2(x, y);
        }

        public override string ToString()
        {
            return $"LaserPoint({x:F2}, {y:F2}) RGB({r:F2}, {g:F2}, {b:F2}) {(blanking ? "[blanked]" : "")}";
        }
    }
}
