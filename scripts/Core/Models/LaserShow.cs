using Godot;
using Godot.Collections;
using LazerSystem.Timeline;

namespace LazerSystem.Core
{
	[GlobalClass]
	public partial class LaserShow : Resource
	{
		[Export] public string ShowName;

		[Export] public AudioStream AudioClip;
		[Export] public float Bpm = 120f;
		[Export] public float Offset;

		[Export] public Array<LaserCueBlock> TimelineBlocks = new Array<LaserCueBlock>();
		[Export] public Array<TimeMarker> Markers = new();
	}
}
