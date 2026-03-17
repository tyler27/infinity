using Godot;

namespace LazerSystem.Core
{
    [GlobalClass]
    public partial class ProjectionZone : Resource
    {
        [Export] public string ZoneName;
        [Export] public int ProjectorIndex;

        [Export] public Vector2 PositionOffset = Vector2.Zero;
        [Export] public Vector2 Scale = Vector2.One;
        [Export] public float Rotation;

        /// <summary>Four corners for keystone correction: bottom-left, bottom-right, top-right, top-left.</summary>
        [Export] public Vector2[] KeystoneCorners = new Vector2[]
        {
            new Vector2(-1f, -1f), // bottom-left
            new Vector2( 1f, -1f), // bottom-right
            new Vector2( 1f,  1f), // top-right
            new Vector2(-1f,  1f)  // top-left
        };

        /// <summary>Normalized rectangle defining the safe output area (0,0 to 1,1).</summary>
        [Export] public Rect2 SafetyZone = new Rect2(0f, 0f, 1f, 1f);

        [Export] public bool Enabled = true;
    }
}
