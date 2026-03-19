using Godot;
using Godot.Collections;

namespace LazerSystem.Core
{
	[GlobalClass]
	public partial class AutomationLane : Resource
	{
		[Export] public string ParameterName;
		[Export] public float DefaultValue;
		[Export] public float MinValue;
		[Export] public float MaxValue = 1f;
		[Export] public Array<AutomationKeyframe> Keyframes = new();

		public float Evaluate(float normalizedTime)
		{
			if (Keyframes == null || Keyframes.Count == 0)
				return DefaultValue;

			// Clamp to range
			normalizedTime = Mathf.Clamp(normalizedTime, 0f, 1f);

			// Before first keyframe
			if (normalizedTime <= Keyframes[0].Time)
				return Keyframes[0].Value;

			// After last keyframe
			if (normalizedTime >= Keyframes[^1].Time)
				return Keyframes[^1].Value;

			// Binary search for the segment
			int left = 0;
			int right = Keyframes.Count - 1;
			while (right - left > 1)
			{
				int mid = (left + right) / 2;
				if (Keyframes[mid].Time <= normalizedTime)
					left = mid;
				else
					right = mid;
			}

			var kfA = Keyframes[left];
			var kfB = Keyframes[right];

			float segmentLength = kfB.Time - kfA.Time;
			if (segmentLength <= 0f)
				return kfA.Value;

			float t = (normalizedTime - kfA.Time) / segmentLength;

			return Interpolate(kfA.Value, kfB.Value, t, kfA.CurveType);
		}

		private static float Interpolate(float a, float b, float t, AutomationCurveType curveType)
		{
			switch (curveType)
			{
				case AutomationCurveType.Step:
					return a;
				case AutomationCurveType.EaseIn:
					t = t * t;
					break;
				case AutomationCurveType.EaseOut:
					t = 1f - (1f - t) * (1f - t);
					break;
				case AutomationCurveType.EaseInOut:
					t = t < 0.5f
						? 4f * t * t * t
						: 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
					break;
				// Linear: t stays as-is
			}

			return a + (b - a) * t;
		}

		public void InsertKeyframe(float time, float value, AutomationCurveType curve = AutomationCurveType.Linear)
		{
			var kf = new AutomationKeyframe(time, value, curve);

			// Sorted insert
			for (int i = 0; i < Keyframes.Count; i++)
			{
				if (Keyframes[i].Time > time)
				{
					Keyframes.Insert(i, kf);
					return;
				}
			}
			Keyframes.Add(kf);
		}

		public void RemoveKeyframe(int index)
		{
			if (index >= 0 && index < Keyframes.Count)
				Keyframes.RemoveAt(index);
		}

		public int FindKeyframeNear(float normalizedTime, float tolerance)
		{
			for (int i = 0; i < Keyframes.Count; i++)
			{
				if (Mathf.Abs(Keyframes[i].Time - normalizedTime) <= tolerance)
					return i;
			}
			return -1;
		}

		public AutomationLane DeepClone()
		{
			var clone = new AutomationLane
			{
				ParameterName = ParameterName,
				DefaultValue = DefaultValue,
				MinValue = MinValue,
				MaxValue = MaxValue,
				Keyframes = new Array<AutomationKeyframe>()
			};
			foreach (var kf in Keyframes)
			{
				clone.Keyframes.Add(kf.DeepClone());
			}
			return clone;
		}
	}
}
