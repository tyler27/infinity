using LazerSystem.Core;
using LazerSystem.Patterns;

namespace LazerSystem.Timeline
{
	public static class AutomationEvaluator
	{
		public static void Apply(AutomationData data, float normalizedTime, PatternParameters target)
		{
			if (data?.Lanes == null)
				return;

			foreach (var lane in data.Lanes)
			{
				if (lane.Keyframes == null || lane.Keyframes.Count == 0)
					continue;

				var paramDef = AutomatableParameter.Find(lane.ParameterName);
				if (paramDef == null)
					continue;

				float value = lane.Evaluate(normalizedTime);
				paramDef.Value.ApplyToParams(target, value);
			}
		}
	}
}
