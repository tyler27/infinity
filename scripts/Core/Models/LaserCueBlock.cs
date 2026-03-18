using Godot;

namespace LazerSystem.Core
{
	[GlobalClass]
	public partial class LaserCueBlock : Resource
	{
		[Export] public LaserCue Cue;
		[Export] public int TrackIndex;
		[Export] public float StartTime;
		[Export] public float Duration = 1f;
		[Export] public int ZoneIndex;
		[Export] public float FadeInDuration;
		[Export] public float FadeOutDuration;
		[Export] public bool Muted;
		[Export] public bool Locked;

		public LaserCueBlock DeepClone()
		{
			var clonedCue = new LaserCue
			{
				CueName = Cue?.CueName,
				PatternType = Cue?.PatternType ?? LaserPatternType.Beam,
				Color = Cue?.Color ?? Colors.White,
				Intensity = Cue?.Intensity ?? 1f,
				Size = Cue?.Size ?? 0.5f,
				Rotation = Cue?.Rotation ?? 0f,
				Speed = Cue?.Speed ?? 1f,
				Spread = Cue?.Spread ?? 0f,
				Count = Cue?.Count ?? 1,
				Frequency = Cue?.Frequency ?? 1f,
				Amplitude = Cue?.Amplitude ?? 0.5f,
				Position = Cue?.Position ?? Godot.Vector2.Zero,
				IldaAssetPath = Cue?.IldaAssetPath
			};

			return new LaserCueBlock
			{
				Cue = clonedCue,
				TrackIndex = TrackIndex,
				StartTime = StartTime,
				Duration = Duration,
				ZoneIndex = ZoneIndex,
				FadeInDuration = FadeInDuration,
				FadeOutDuration = FadeOutDuration,
				Muted = Muted,
				Locked = Locked
			};
		}
	}
}
