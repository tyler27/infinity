using System;
using LazerSystem.Core;

namespace LazerSystem.Timeline.Commands
{
    public class AddBlockCommand : ITimelineCommand
    {
        private readonly LaserCueBlock _block;
        private readonly TimelineTrack _track;
        private readonly LaserShow _show;

        public string Description => "Add Block";

        public AddBlockCommand(LaserCueBlock block, TimelineTrack track, LaserShow show)
        {
            _block = block;
            _track = track;
            _show = show;
        }

        public void Execute()
        {
            _track.AddBlock(_block);
            _show?.TimelineBlocks.Add(_block);
        }

        public void Undo()
        {
            _track.RemoveBlock(_block);
            _show?.TimelineBlocks.Remove(_block);
        }
    }

    public class RemoveBlockCommand : ITimelineCommand
    {
        private readonly LaserCueBlock _block;
        private readonly TimelineTrack _track;
        private readonly LaserShow _show;

        public string Description => "Remove Block";

        public RemoveBlockCommand(LaserCueBlock block, TimelineTrack track, LaserShow show)
        {
            _block = block;
            _track = track;
            _show = show;
        }

        public void Execute()
        {
            _track.RemoveBlock(_block);
            _show?.TimelineBlocks.Remove(_block);
        }

        public void Undo()
        {
            _track.AddBlock(_block);
            _show?.TimelineBlocks.Add(_block);
        }
    }

    public class MoveBlockCommand : ITimelineCommand
    {
        private readonly LaserCueBlock _block;
        private readonly float _oldStart;
        private readonly float _newStart;

        public string Description => "Move Block";

        public MoveBlockCommand(LaserCueBlock block, float oldStart, float newStart)
        {
            _block = block;
            _oldStart = oldStart;
            _newStart = newStart;
        }

        public void Execute()
        {
            _block.StartTime = _newStart;
        }

        public void Undo()
        {
            _block.StartTime = _oldStart;
        }
    }

    public class ResizeBlockCommand : ITimelineCommand
    {
        private readonly LaserCueBlock _block;
        private readonly float _oldStart;
        private readonly float _oldDuration;
        private readonly float _newStart;
        private readonly float _newDuration;

        public string Description => "Resize Block";

        public ResizeBlockCommand(LaserCueBlock block, float oldStart, float oldDuration, float newStart, float newDuration)
        {
            _block = block;
            _oldStart = oldStart;
            _oldDuration = oldDuration;
            _newStart = newStart;
            _newDuration = newDuration;
        }

        public void Execute()
        {
            _block.StartTime = _newStart;
            _block.Duration = _newDuration;
        }

        public void Undo()
        {
            _block.StartTime = _oldStart;
            _block.Duration = _oldDuration;
        }
    }

    public class ModifyBlockPropertyCommand : ITimelineCommand
    {
        private readonly string _description;
        private readonly Action _applyNew;
        private readonly Action _applyOld;

        public string Description => _description;

        public ModifyBlockPropertyCommand(string description, Action applyNew, Action applyOld)
        {
            _description = description;
            _applyNew = applyNew;
            _applyOld = applyOld;
        }

        public void Execute()
        {
            _applyNew();
        }

        public void Undo()
        {
            _applyOld();
        }
    }
}
