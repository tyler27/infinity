namespace LazerSystem.Timeline.Commands
{
    public interface ITimelineCommand
    {
        string Description { get; }
        void Execute();
        void Undo();
    }
}
