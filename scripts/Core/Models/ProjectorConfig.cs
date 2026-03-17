using Godot;

namespace LazerSystem.Core
{
	[GlobalClass]
	public partial class ProjectorConfig : Resource
	{
		[Export] public string ProjectorName;

		[Export] public string IpAddress = "192.168.0.1";
		[Export] public int ArtNetUniverse;

		[Export] public string SerialNumber;

		[Export] public bool Enabled = true;

		[Export] public Color GizmoColor = Colors.Green;
	}
}
