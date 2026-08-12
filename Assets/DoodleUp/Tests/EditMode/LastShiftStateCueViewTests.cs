using System.Collections.Generic;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 상태 단서의 보간. 지키는 것은 셋이다 — <b>정상일 때 완전히 안 보이는가</b>,
    /// <b>켜지는 것보다 걷히는 것이 느린가</b>, <b>임계에서 떨려도 안 깜빡이는가</b>.
    /// </summary>
    public sealed class LastShiftStateCueViewTests
    {
        private readonly List<GameObject> spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        /// <summary>
        /// <b>가장 중요한 검사다.</b> 냉각이 멀쩡한데 서리가 보이면 그것이 사용자가 지적한
        /// 그 상태다 — 진하기가 <c>0</c> 이고 렌더러도 꺼져 있어야 한다.
        /// </summary>
        [Test]
        public void ANormalRoomShowsNothingAtAll()
        {
            var view = NewCue();

            Assert.That(view.Amount, Is.Zero);
            foreach (var renderer in view.GetComponentsInChildren<MeshRenderer>(true))
                Assert.That(renderer.enabled, Is.False, "정상인데 판이 그려지고 있다");
        }

        /// <summary>켜지는 데 <c>0.8</c>초, 걷히는 데 <c>1.2</c>초 — 녹는 쪽이 느리다.</summary>
        [Test]
        public void ItFadesInFasterThanItFadesOut()
        {
            Assert.That(LastShiftStateCueView.FadeOutSeconds,
                Is.GreaterThan(LastShiftStateCueView.FadeInSeconds));

            var view = NewCue();
            Tick(view, true, LastShiftStateCueView.FadeInSeconds);
            Assert.That(view.Amount, Is.EqualTo(1f).Within(0.001f), "제 시간에 다 안 떴다");

            Tick(view, false, LastShiftStateCueView.FadeInSeconds);
            Assert.That(view.Amount, Is.GreaterThan(0f),
                "켜지는 시간만큼 껐는데 다 걷혔다 — 걷히는 쪽이 더 느려야 한다");

            Tick(view, false, LastShiftStateCueView.FadeOutSeconds);
            Assert.That(view.Amount, Is.Zero);
        }

        /// <summary>
        /// <b>임계에서 떨려도 안 깜빡인다.</b> 목표만 바뀌고 지금 진하기는 이어서 움직이므로,
        /// 한 프레임씩 참·거짓이 번갈아 와도 값이 <c>0</c> 과 <c>1</c> 을 오가지 않는다.
        /// </summary>
        [Test]
        public void FlippingAtTheThresholdDoesNotFlicker()
        {
            var view = NewCue();
            Tick(view, true, LastShiftStateCueView.FadeInSeconds);
            Assume.That(view.Amount, Is.EqualTo(1f).Within(0.001f));

            const float step = 1f / 60f;
            for (var frame = 0; frame < 60; frame++)
                view.TickForProbe(frame % 2 == 0, step);

            // 한 프레임 분량으로 오갈 뿐, 켜짐과 꺼짐 사이를 뛰지 않는다.
            Assert.That(view.Amount, Is.GreaterThan(0.9f),
                $"임계에서 떨렸더니 판이 사라졌다 — amount={view.Amount:F2}");
        }

        /// <summary>안 보일 때는 렌더러를 끈다 — 알파 0 을 계속 그리지 않는다.</summary>
        [Test]
        public void TheRendererGoesOffWhenFullyClear()
        {
            var view = NewCue();
            Tick(view, true, LastShiftStateCueView.FadeInSeconds);
            foreach (var renderer in view.GetComponentsInChildren<MeshRenderer>(true))
                Assert.That(renderer.enabled, Is.True);

            Tick(view, false, LastShiftStateCueView.FadeOutSeconds);
            foreach (var renderer in view.GetComponentsInChildren<MeshRenderer>(true))
                Assert.That(renderer.enabled, Is.False);
        }

        /// <summary>
        /// 뜨는 조건이 <b>위기</b> 하나다. 불안정에서도 뜨면 "나쁘다" 가 두 단계가 되어,
        /// 판이 떠 있다는 사실 자체가 정보를 잃는다.
        /// </summary>
        [Test]
        public void OnlyCrisisRaisesTheCue()
        {
            Assert.That(LastShiftStateCueView.ShowFrom,
                Is.EqualTo(LastShiftSituationGrade.Crisis));
        }

        private LastShiftStateCueView NewCue()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spawned.Add(go);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var view = go.AddComponent<LastShiftStateCueView>();
            view.Configure(LastShiftZone.Cooling);
            return view;
        }

        private static void Tick(LastShiftStateCueView view, bool show, float seconds)
        {
            const float step = 1f / 60f;
            var left = seconds;
            while (left > 0f)
            {
                var delta = Mathf.Min(step, left);
                view.TickForProbe(show, delta);
                left -= delta;
            }
            // 부동소수 누적으로 경계를 살짝 못 넘는 것을 메운다.
            view.TickForProbe(show, step);
        }
    }
}
