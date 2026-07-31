using System.Collections;
using DoodleUp.Core;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleUp.Tests.PlayMode
{
    public sealed class Du02TaskStatePlayModeTests
    {
        [UnityTest]
        public IEnumerator CountdownUnlocksAtGoAndTimerStartsAfterward()
        {
            var gameObject = new GameObject("TaskStateTest");
            var state = gameObject.AddComponent<Du02TaskState>();
            state.ResetState(Du02TaskId.T1Horizontal);

            Assert.That(state.InputLocked, Is.True);
            Assert.That(state.CountdownRemaining, Is.EqualTo(3f));
            Assert.That(state.TimerSeconds, Is.Zero);

            state.Tick(3f);
            Assert.That(state.InputLocked, Is.False);
            Assert.That(state.TimerSeconds, Is.Zero);
            state.Tick(0.25f);
            Assert.That(state.TimerSeconds, Is.EqualTo(0.25f));

            Object.Destroy(gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GoalSuccessRequiresStrokeEvidenceAndT3BothBands()
        {
            var gameObject = new GameObject("TaskStateTest");
            var state = gameObject.AddComponent<Du02TaskState>();

            state.ResetState(Du02TaskId.T1Horizontal);
            state.Tick(3f);
            state.SetInsideGoal(true);
            state.Tick(1f);
            Assert.That(state.GoalReached, Is.False);
            state.NotifyCommittedStrokeContact(false, false);
            state.Tick(1f);
            Assert.That(state.GoalReached, Is.True);

            state.ResetState(Du02TaskId.T3Bridge);
            state.Tick(3f);
            state.NotifyCommittedStrokeContact(true, false);
            state.SetInsideGoal(true);
            state.Tick(1f);
            Assert.That(state.GoalReached, Is.False);

            state.ResetState(Du02TaskId.T3Bridge);
            state.Tick(3f);
            state.NotifyCommittedStrokeContact(true, true);
            state.SetInsideGoal(true);
            state.Tick(1f);
            Assert.That(state.GoalReached, Is.True);

            Object.Destroy(gameObject);
            yield return null;
        }
    }
}
