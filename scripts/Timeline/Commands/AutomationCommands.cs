using LazerSystem.Core;

namespace LazerSystem.Timeline.Commands
{
	public class AddKeyframeCommand : ITimelineCommand
	{
		private readonly AutomationLane _lane;
		private readonly AutomationKeyframe _keyframe;
		private int _insertedIndex = -1;

		public string Description => "Add Keyframe";

		public AddKeyframeCommand(AutomationLane lane, AutomationKeyframe keyframe)
		{
			_lane = lane;
			_keyframe = keyframe;
		}

		public void Execute()
		{
			_lane.InsertKeyframe(_keyframe.Time, _keyframe.Value, _keyframe.CurveType);
			// Find where it was inserted
			_insertedIndex = _lane.FindKeyframeNear(_keyframe.Time, 0.0001f);
		}

		public void Undo()
		{
			if (_insertedIndex >= 0 && _insertedIndex < _lane.Keyframes.Count)
				_lane.RemoveKeyframe(_insertedIndex);
		}
	}

	public class RemoveKeyframeCommand : ITimelineCommand
	{
		private readonly AutomationLane _lane;
		private readonly AutomationKeyframe _keyframe;
		private readonly int _index;

		public string Description => "Remove Keyframe";

		public RemoveKeyframeCommand(AutomationLane lane, int index)
		{
			_lane = lane;
			_index = index;
			_keyframe = lane.Keyframes[index].DeepClone();
		}

		public void Execute()
		{
			_lane.RemoveKeyframe(_index);
		}

		public void Undo()
		{
			_lane.InsertKeyframe(_keyframe.Time, _keyframe.Value, _keyframe.CurveType);
		}
	}

	public class MoveKeyframeCommand : ITimelineCommand
	{
		private readonly AutomationLane _lane;
		private readonly int _index;
		private readonly float _oldTime, _oldValue;
		private readonly float _newTime, _newValue;

		public string Description => "Move Keyframe";

		public MoveKeyframeCommand(AutomationLane lane, int index, float oldTime, float oldValue, float newTime, float newValue)
		{
			_lane = lane;
			_index = index;
			_oldTime = oldTime;
			_oldValue = oldValue;
			_newTime = newTime;
			_newValue = newValue;
		}

		public void Execute()
		{
			if (_index >= 0 && _index < _lane.Keyframes.Count)
			{
				_lane.Keyframes[_index].Time = _newTime;
				_lane.Keyframes[_index].Value = _newValue;
			}
		}

		public void Undo()
		{
			if (_index >= 0 && _index < _lane.Keyframes.Count)
			{
				_lane.Keyframes[_index].Time = _oldTime;
				_lane.Keyframes[_index].Value = _oldValue;
			}
		}
	}

	public class ChangeCurveTypeCommand : ITimelineCommand
	{
		private readonly AutomationKeyframe _keyframe;
		private readonly AutomationCurveType _oldType;
		private readonly AutomationCurveType _newType;

		public string Description => "Change Curve Type";

		public ChangeCurveTypeCommand(AutomationKeyframe keyframe, AutomationCurveType oldType, AutomationCurveType newType)
		{
			_keyframe = keyframe;
			_oldType = oldType;
			_newType = newType;
		}

		public void Execute()
		{
			_keyframe.CurveType = _newType;
		}

		public void Undo()
		{
			_keyframe.CurveType = _oldType;
		}
	}
}
