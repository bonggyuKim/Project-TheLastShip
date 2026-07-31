namespace DoodleUp.Core
{
    public enum Du02ScaffoldPhase
    {
        Idle,
        ProbePerturbed
    }

    public interface IDu02TaskState
    {
        Du02TaskId TaskId { get; }
        Du02ScaffoldPhase Phase { get; }
        float CountdownRemaining { get; }
        float TimerSeconds { get; }
        bool InputLocked { get; }
        bool GoalReached { get; }
        int StrokeCount { get; }
        float AvailableInk { get; }
        void ResetState(Du02TaskId taskId);
    }
}
