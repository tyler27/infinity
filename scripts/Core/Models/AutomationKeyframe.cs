using Godot;

namespace LazerSystem.Core
{
	[GlobalClass]
	public partial class AutomationKeyframe : Resource
	{
		/// <summary>Normalized time within the block (0.0–1.0).</summary>
		[Export] public float Time;

		/// <summary>Actual parameter value at this keyframe.</summary>
		[Export] public float Value;

		[Export] public AutomationCurveType CurveType = AutomationCurveType.Linear;

		public AutomationKeyframe() { }

		public AutomationKeyframe(float time, float value, AutomationCurveType curveType = AutomationCurveType.Linear)
		{
			Time = time;
			Value = value;
			CurveType = curveType;
		}

		public AutomationKeyframe DeepClone()
		{
			return new AutomationKeyframe(Time, Value, CurveType);
		}
	}
}
