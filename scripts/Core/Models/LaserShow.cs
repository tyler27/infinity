using Godot;
using Godot.Collections;

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
	}

	[GlobalClass]
	public partial class LaserCueBlock : Resource
	{
		[Export] public LaserCue Cue;
		[Export] public int TrackIndex;
		[Export] public float StartTime;
		[Export] public float Duration = 1f;
		[Export] public int ZoneIndex;
	}
}
