using DoodleUp.Core;
using UnityEngine;

namespace DoodleUp.Runtime
{
    public sealed class Du02TaskState : MonoBehaviour, IDu02TaskState
    {
        public const float CountdownDuration = 3f;
        public const float GoalHoldDuration = 1f;
        public const float InitialInk = 5f;

        private bool hasCommittedStrokeContact;
        private bool startBandContact;
        private bool goalBandContact;
        private bool insideGoal;
        private float goalHoldSeconds;
        private bool goLogged;

        public Du02TaskId TaskId { get; private set; }
        public Du02ScaffoldPhase Phase { get; private set; } = Du02ScaffoldPhase.Idle;
        public float CountdownRemaining { get; private set; } = CountdownDuration;
        public float TimerSeconds { get; private set; }
        public bool InputLocked => CountdownRemaining > 0f;
        public bool GoalReached { get; private set; }
        public int StrokeCount { get; private set; }
        public float AvailableInk { get; private set; } = InitialInk;
        public float GoalHoldSeconds => goalHoldSeconds;

        public void ResetState(Du02TaskId taskId)
        {
            TaskId = taskId;
            Phase = Du02ScaffoldPhase.Idle;
            CountdownRemaining = CountdownDuration;
            TimerSeconds = 0f;
            GoalReached = false;
            StrokeCount = 0;
            AvailableInk = InitialInk;
            hasCommittedStrokeContact = false;
            startBandContact = false;
            goalBandContact = false;
            insideGoal = false;
            goalHoldSeconds = 0f;
            goLogged = false;
            Debug.Log($"[DU02_TASK_RESET] task={TaskId} phase={Phase} countdown={CountdownRemaining:F6} timer={TimerSeconds:F6} inputLocked={InputLocked} goal={GoalReached} strokeCount={StrokeCount} ink={AvailableInk:F6}");
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (CountdownRemaining > 0f)
            {
                var consumedByCountdown = Mathf.Min(CountdownRemaining, unscaledDeltaTime);
                CountdownRemaining -= consumedByCountdown;
                unscaledDeltaTime -= consumedByCountdown;
                if (CountdownRemaining <= 0f && !goLogged)
                {
                    CountdownRemaining = 0f;
                    goLogged = true;
                    Debug.Log($"[DU02_TASK_GO] task={TaskId} timer={TimerSeconds:F6} inputLocked={InputLocked}");
                }

                if (unscaledDeltaTime <= 0f) return;
            }

            TimerSeconds += unscaledDeltaTime;
            if (!insideGoal || !HasRequiredStrokeEvidence())
            {
                goalHoldSeconds = 0f;
                return;
            }

            goalHoldSeconds += unscaledDeltaTime;
            if (goalHoldSeconds >= GoalHoldDuration && !GoalReached)
            {
                GoalReached = true;
                Debug.Log($"[DU02_TASK_SUCCESS] task={TaskId} goalHold={goalHoldSeconds:F6} strokeCount={StrokeCount} startBand={startBandContact} goalBand={goalBandContact}");
            }
        }

        public void NotifyCommittedStrokeContact(bool startBand, bool goalBand)
        {
            StrokeCount = Mathf.Max(StrokeCount, 1);
            hasCommittedStrokeContact = true;
            startBandContact |= startBand;
            goalBandContact |= goalBand;
        }

        public void SetInsideGoal(bool value)
        {
            insideGoal = value;
            if (!value) goalHoldSeconds = 0f;
        }

        public void PerturbForResetProbe()
        {
            Phase = Du02ScaffoldPhase.ProbePerturbed;
            CountdownRemaining = 0f;
            TimerSeconds = 19.25f;
            GoalReached = true;
            StrokeCount = 3;
            AvailableInk = 1.25f;
            hasCommittedStrokeContact = true;
            startBandContact = true;
            goalBandContact = true;
            insideGoal = true;
            goalHoldSeconds = GoalHoldDuration;
        }

        private bool HasRequiredStrokeEvidence()
        {
            if (!hasCommittedStrokeContact || StrokeCount <= 0) return false;
            return TaskId != Du02TaskId.T3Bridge || (startBandContact && goalBandContact);
        }
    }
}
