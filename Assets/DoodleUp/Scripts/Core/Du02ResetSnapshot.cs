using System;
using UnityEngine;

namespace DoodleUp.Core
{
    [Serializable]
    public readonly struct Du02ResetSnapshot : IEquatable<Du02ResetSnapshot>
    {
        public readonly Du02TaskId TaskId;
        public readonly Vector3 PlayerPosition;
        public readonly Quaternion PlayerRotation;
        public readonly Vector3 Velocity;
        public readonly Vector3 AngularVelocity;
        public readonly bool Grounded;
        public readonly Vector3 HandLocalPosition;
        public readonly Quaternion HandLocalRotation;
        public readonly Vector3 CameraPosition;
        public readonly Quaternion CameraRotation;
        public readonly float CameraFov;
        public readonly float FixedDeltaTime;
        public readonly Vector3 HandLocalScale;
        public readonly Du02ScaffoldPhase Phase;
        public readonly float CountdownRemaining;
        public readonly float TimerSeconds;
        public readonly bool InputLocked;
        public readonly bool GoalReached;
        public readonly int StrokeCount;
        public readonly float AvailableInk;
        public readonly long SamplingSequence;

        public Du02ResetSnapshot(
            Du02TaskId taskId,
            Vector3 playerPosition,
            Quaternion playerRotation,
            Vector3 velocity,
            bool grounded,
            Vector3 handLocalPosition,
            Quaternion handLocalRotation,
            Vector3 cameraPosition,
            Quaternion cameraRotation,
            float cameraFov,
            float fixedDeltaTime,
            Vector3 handLocalScale = default,
            Du02ScaffoldPhase phase = Du02ScaffoldPhase.Idle,
            float countdownRemaining = 0f,
            float timerSeconds = 0f,
            bool inputLocked = false,
            bool goalReached = false,
            int strokeCount = 0,
            float availableInk = 5f,
            long samplingSequence = 0,
            Vector3 angularVelocity = default)
        {
            TaskId = taskId;
            PlayerPosition = playerPosition;
            PlayerRotation = playerRotation;
            Velocity = velocity;
            AngularVelocity = angularVelocity;
            Grounded = grounded;
            HandLocalPosition = handLocalPosition;
            HandLocalRotation = handLocalRotation;
            CameraPosition = cameraPosition;
            CameraRotation = cameraRotation;
            CameraFov = cameraFov;
            FixedDeltaTime = fixedDeltaTime;
            HandLocalScale = handLocalScale == default ? Vector3.one : handLocalScale;
            Phase = phase;
            CountdownRemaining = countdownRemaining;
            TimerSeconds = timerSeconds;
            InputLocked = inputLocked;
            GoalReached = goalReached;
            StrokeCount = strokeCount;
            AvailableInk = availableInk;
            SamplingSequence = samplingSequence;
        }

        public bool Equals(Du02ResetSnapshot other)
        {
            return TaskId == other.TaskId
                && PlayerPosition == other.PlayerPosition
                && PlayerRotation == other.PlayerRotation
                && Velocity == other.Velocity
                && AngularVelocity == other.AngularVelocity
                && Grounded == other.Grounded
                && HandLocalPosition == other.HandLocalPosition
                && HandLocalRotation == other.HandLocalRotation
                && CameraPosition == other.CameraPosition
                && CameraRotation == other.CameraRotation
                && CameraFov.Equals(other.CameraFov)
                && FixedDeltaTime.Equals(other.FixedDeltaTime)
                && HandLocalScale == other.HandLocalScale
                && Phase == other.Phase
                && CountdownRemaining.Equals(other.CountdownRemaining)
                && TimerSeconds.Equals(other.TimerSeconds)
                && InputLocked == other.InputLocked
                && GoalReached == other.GoalReached
                && StrokeCount == other.StrokeCount
                && AvailableInk.Equals(other.AvailableInk)
                && SamplingSequence == other.SamplingSequence;
        }

        public override bool Equals(object obj) => obj is Du02ResetSnapshot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)TaskId;
                hash = (hash * 397) ^ PlayerPosition.GetHashCode();
                hash = (hash * 397) ^ PlayerRotation.GetHashCode();
                hash = (hash * 397) ^ Velocity.GetHashCode();
                hash = (hash * 397) ^ AngularVelocity.GetHashCode();
                hash = (hash * 397) ^ Grounded.GetHashCode();
                hash = (hash * 397) ^ HandLocalPosition.GetHashCode();
                hash = (hash * 397) ^ HandLocalRotation.GetHashCode();
                hash = (hash * 397) ^ CameraPosition.GetHashCode();
                hash = (hash * 397) ^ CameraRotation.GetHashCode();
                hash = (hash * 397) ^ CameraFov.GetHashCode();
                hash = (hash * 397) ^ FixedDeltaTime.GetHashCode();
                hash = (hash * 397) ^ HandLocalScale.GetHashCode();
                hash = (hash * 397) ^ (int)Phase;
                hash = (hash * 397) ^ CountdownRemaining.GetHashCode();
                hash = (hash * 397) ^ TimerSeconds.GetHashCode();
                hash = (hash * 397) ^ InputLocked.GetHashCode();
                hash = (hash * 397) ^ GoalReached.GetHashCode();
                hash = (hash * 397) ^ StrokeCount;
                hash = (hash * 397) ^ AvailableInk.GetHashCode();
                hash = (hash * 397) ^ SamplingSequence.GetHashCode();
                return hash;
            }
        }
    }
}
