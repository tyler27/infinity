using Godot;

namespace LazerSystem.Timeline
{
    [GlobalClass]
    public partial class TimeMarker : Resource
    {
        [Export] public string Name;
        [Export] public float Time;
        [Export] public Color MarkerColor = Colors.Yellow;
    }
}
