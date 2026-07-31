using DoodleUp.Stroke;
using NUnit.Framework;

namespace DoodleUp.Tests.EditMode
{
    public sealed class Du02SamplingTests
    {
        [TestCase(30, 10f, 300)]
        [TestCase(60, 10f, 600)]
        [TestCase(144, 10f, 1440)]
        public void LateUpdateSeamProducesOneSamplePerRenderFrame(int frameRate, float durationSeconds, int expected)
        {
            var actual = Du02SamplingExpectation.ExpectedSamples(frameRate, durationSeconds);
            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
