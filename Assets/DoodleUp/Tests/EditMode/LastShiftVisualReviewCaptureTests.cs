using System.Linq;
using DoodleUp.Editor;
using NUnit.Framework;

namespace DoodleUp.Tests.EditMode
{
    public sealed class LastShiftVisualReviewCaptureTests
    {
        [Test]
        public void EvidenceV2_HasContextAndDiagnosticViewForEveryArea()
        {
            var views = LastShiftVisualReviewCapture.EvidenceV2Views();

            Assert.That(views, Has.Length.EqualTo(16));
            foreach (var area in views.Select(view => view.Area).Distinct())
            {
                var areaViews = views.Where(view => view.Area == area).ToArray();
                Assert.That(areaViews, Has.Length.GreaterThanOrEqualTo(2), area);
                Assert.That(areaViews.Select(view => view.Purpose), Does.Contain("context"), area);
                Assert.That(areaViews.Select(view => view.Purpose), Does.Contain("diagnostic"), area);
            }

            Assert.That(views.Select(view => view.Name).Distinct().Count(), Is.EqualTo(views.Length));
            Assert.That(views.All(view => (view.Target - view.Position).sqrMagnitude > 0.01f), Is.True);
        }
    }
}
