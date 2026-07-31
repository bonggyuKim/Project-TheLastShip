using DoodleUp.Core;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    public sealed class Du02CourseTests
    {
        [Test]
        public void T1HasExactHorizontalGap()
        {
            var lane = Du02CourseDefinition.Get(Du02TaskId.T1Horizontal);
            var startRightEdge = lane.StartCenter.x + lane.StartSize.x * 0.5f;
            var goalLeftEdge = lane.GoalCenter.x - lane.GoalSize.x * 0.5f;
            Assert.That(goalLeftEdge - startRightEdge, Is.EqualTo(0.70f).Within(0.000001f));
            Assert.That(lane.GoalCenter.y, Is.EqualTo(lane.StartCenter.y));
        }

        [Test]
        public void T2HasExactCenterOffset()
        {
            var lane = Du02CourseDefinition.Get(Du02TaskId.T2Rising);
            var offset = lane.GoalCenter - lane.StartCenter;
            Assert.That(offset.x, Is.EqualTo(0.65f).Within(0.000001f));
            Assert.That(offset.y, Is.EqualTo(0.55f).Within(0.000001f));
            Assert.That(offset.z, Is.Zero.Within(0.000001f));
        }

        [Test]
        public void T3HasExactGapAndContactBand()
        {
            var lane = Du02CourseDefinition.Get(Du02TaskId.T3Bridge);
            var startRightEdge = lane.StartCenter.x + lane.StartSize.x * 0.5f;
            var goalLeftEdge = lane.GoalCenter.x - lane.GoalSize.x * 0.5f;
            Assert.That(goalLeftEdge - startRightEdge, Is.EqualTo(0.95f).Within(0.000001f));
            Assert.That(lane.ContactBandWidth, Is.EqualTo(0.12f).Within(0.000001f));
        }

        [Test]
        public void LanesAreIndependentAlongDepth()
        {
            var t1 = Du02CourseDefinition.Get(Du02TaskId.T1Horizontal);
            var t2 = Du02CourseDefinition.Get(Du02TaskId.T2Rising);
            var t3 = Du02CourseDefinition.Get(Du02TaskId.T3Bridge);
            Assert.That(Mathf.Abs(t2.Origin.z - t1.Origin.z), Is.GreaterThanOrEqualTo(4f));
            Assert.That(Mathf.Abs(t3.Origin.z - t2.Origin.z), Is.GreaterThanOrEqualTo(4f));
        }
    }
}
