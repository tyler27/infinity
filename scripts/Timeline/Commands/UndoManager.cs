using System.Collections.Generic;
using Godot;

namespace LazerSystem.Timeline.Commands
{
    public class UndoManager
    {
        private static UndoManager _instance;
        public static UndoManager Instance => _instance ??= new UndoManager();

        private readonly Stack<ITimelineCommand> _undoStack = new();
        private readonly Stack<ITimelineCommand> _redoStack = new();

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public void ExecuteCommand(ITimelineCommand command)
        {
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear();
        }

        public void Undo()
        {
            if (_undoStack.Count == 0) return;
            var cmd = _undoStack.Pop();
            cmd.Undo();
            _redoStack.Push(cmd);
            GD.Print($"[Undo] {cmd.Description}");
        }

        public void Redo()
        {
            if (_redoStack.Count == 0) return;
            var cmd = _redoStack.Pop();
            cmd.Execute();
            _undoStack.Push(cmd);
            GD.Print($"[Redo] {cmd.Description}");
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
}
