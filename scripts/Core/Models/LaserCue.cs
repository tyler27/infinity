using Godot;

namespace LazerSystem.Core
{
    [GlobalClass]
    public partial class LaserCue : Resource
    {
        [Export] public string CueName;

        [Export] public LaserPatternType PatternType = LaserPatternType.Beam;

        [Export] public Color Color = Colors.White;

        [Export(PropertyHint.Range, "0,1")]
        public float Intensity = 1f;

        [Export(PropertyHint.Range, "0,1")]
        public float Size = 0.5f;

        [Export] public float Rotation;

        [Export] public float Speed = 1f;

        [Export(PropertyHint.Range, "0,1")]
        public float Spread;

        [Export] public int Count = 1;

        [Export] public float Frequency = 1f;
        [Export] public float Amplitude = 0.5f;

        [Export] public Vector2 Position = Vector2.Zero;

        [Export] public string IldaAssetPath;

        [Export] public int GridPage;
        [Export] public int GridRow;
        [Export] public int GridColumn;
    }
}
