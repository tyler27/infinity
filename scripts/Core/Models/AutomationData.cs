using Godot;
using Godot.Collections;

namespace LazerSystem.Core
{
	[GlobalClass]
	public partial class AutomationData : Resource
	{
		[Export] public Array<AutomationLane> Lanes = new();

		public AutomationLane GetLane(string parameterName)
		{
			if (Lanes == null) return null;
			foreach (var lane in Lanes)
			{
				if (lane.ParameterName == parameterName)
					return lane;
			}
			return null;
		}

		public AutomationLane GetOrCreateLane(string parameterName, float defaultValue, float min, float max)
		{
			var lane = GetLane(parameterName);
			if (lane != null) return lane;

			lane = new AutomationLane
			{
				ParameterName = parameterName,
				DefaultValue = defaultValue,
				MinValue = min,
				MaxValue = max
			};
			Lanes.Add(lane);
			return lane;
		}

		public bool HasAnyAutomation
		{
			get
			{
				if (Lanes == null) return false;
				foreach (var lane in Lanes)
				{
					if (lane.Keyframes != null && lane.Keyframes.Count > 0)
						return true;
				}
				return false;
			}
		}

		public AutomationData DeepClone()
		{
			var clone = new AutomationData { Lanes = new Array<AutomationLane>() };
			if (Lanes != null)
			{
				foreach (var lane in Lanes)
				{
					clone.Lanes.Add(lane.DeepClone());
				}
			}
			return clone;
		}
	}
}
