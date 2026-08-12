using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 항해 지도(<c>M</c>). 재는 것은 셋이다 — <b>못 열 때 안 열리는가</b>, <b>배가 지도 안에
    /// 다 들어가는가</b>, <b>표식이 실제로 보는 쪽을 가리키는가</b>.
    ///
    /// <b>투영 자체는 여기서 다시 안 잰다</b> — <see cref="LastShiftHullSchematic"/> 의 왕복
    /// 검사가 이미 있다. 여기서는 그 자를 화면에 놓은 결과만 본다.
    /// </summary>
    public sealed class LastShiftMapViewTests
    {
        /// <summary>세로가 짧은 흔한 화면. 정사각 판정이 여기서 갈린다.</summary>
        private static readonly Vector2 Screen = new(1920f, 1080f);

        [SetUp]
        public void SetUp()
        {
            LastShiftMapView.Clear();
            LastShiftWakeSequence.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            LastShiftMapView.Clear();
            LastShiftWakeSequence.Clear();
        }

        /// <summary>
        /// <b>도입부 중에는 안 열린다.</b> 기상 연출은 화면을 암전으로 덮고 조작을 잠근
        /// 상태라, 그 위에 지도가 뜨면 잠긴 채로 지도만 보게 된다.
        /// </summary>
        [Test]
        public void TheMapStaysShutDuringTheOpening()
        {
            LastShiftWakeSequence.Begin();
            Assume.That(LastShiftWakeSequence.IsRunning, Is.True);

            LastShiftMapView.Toggle();

            Assert.That(LastShiftMapView.IsOpen, Is.False, "도입부 중에 지도가 열렸다");
        }

        /// <summary>
        /// 열어 둔 지도는 <b>도입부가 나중에 시작해도</b> 닫힌다. 여는 순간만 보면 다음
        /// 기항에서 지도를 켜 둔 채로 연출이 시작하는 경우를 놓친다.
        /// </summary>
        [Test]
        public void AnOpenMapClosesWhenTheOpeningStarts()
        {
            LastShiftMapView.Toggle();
            Assume.That(LastShiftMapView.IsOpen, Is.True);

            LastShiftWakeSequence.Begin();
            LastShiftMapView.Tick();

            Assert.That(LastShiftMapView.IsOpen, Is.False, "연출이 시작했는데 지도가 남아 있다");
        }

        /// <summary>같은 키가 열고 닫는다.</summary>
        [Test]
        public void TheSameKeyOpensAndCloses()
        {
            LastShiftMapView.Toggle();
            Assert.That(LastShiftMapView.IsOpen, Is.True);
            LastShiftMapView.Toggle();
            Assert.That(LastShiftMapView.IsOpen, Is.False);
        }

        /// <summary>
        /// <b>지도 자리는 정사각형이다.</b> 화면 비율에 맞춰 늘리면 배가 넓은 화면에서만
        /// 납작해 보이고, 그러면 눈으로 잰 거리가 화면마다 달라진다.
        /// </summary>
        [Test]
        public void ThePlanIsSquareAndCentred()
        {
            var rect = LastShiftMapView.PlanRect(Screen);

            Assert.That(rect.width, Is.EqualTo(rect.height).Within(0.01f), "지도 자리가 안 정사각이다");
            Assert.That(rect.center.x, Is.EqualTo(Screen.x * 0.5f).Within(0.01f));
            Assert.That(rect.center.y, Is.EqualTo(Screen.y * 0.5f).Within(0.01f));
            Assert.That(rect.height, Is.LessThanOrEqualTo(Screen.y),
                "지도가 화면 짧은 변보다 크다 — 위아래가 잘린다");
        }

        /// <summary>
        /// <b>배가 지도 밖으로 안 나간다.</b> 방 여섯이 전부 지도 사각형 안에 들어와야
        /// "지도를 보고 길을 정한다" 가 성립한다 — 하나라도 잘리면 그 방이 없는 것처럼 읽힌다.
        /// </summary>
        [Test]
        public void EveryRoomLandsInsideThePlan()
        {
            var plan = LastShiftMapView.PlanRect(Screen);
            var schematic = LastShiftMapView.Schematic(Screen);

            foreach (var footprint in LastShiftPlazaLayout.Footprints)
            {
                var rect = schematic.ToScreenRect(
                    footprint.MinX, footprint.MaxX, footprint.MinZ, footprint.MaxZ);

                Assert.That(plan.Contains(new Vector2(rect.xMin, rect.yMin)), Is.True,
                    $"{footprint.Space} 의 한 귀퉁이가 지도 밖이다 — {rect}");
                Assert.That(plan.Contains(new Vector2(rect.xMax, rect.yMax)), Is.True,
                    $"{footprint.Space} 의 반대 귀퉁이가 지도 밖이다 — {rect}");
            }
        }

        /// <summary>
        /// <b>표식이 실제로 보는 쪽을 가리키는가.</b> 회전각을 접다가 부호를 하나 놓치면
        /// 표식이 좌우 대칭으로 엉뚱한 데를 가리키고, 그 상태가 정면에서만 맞아 보인다 —
        /// 그래서 네 방향을 전부 잰다. 화면 <c>y</c> 는 아래로 자라므로 "위" 가 더 작은 값이다.
        /// </summary>
        [Test]
        public void TheNosePointsWhereTheCrewIsFacing()
        {
            var schematic = LastShiftMapView.Schematic(Screen);
            var stand = new Vector3(1f, 0f, -2f);
            var here = schematic.ToScreen(stand);

            var forward = LastShiftMapView.NosePoint(schematic, stand, Vector3.forward);
            Assert.That(forward.y, Is.LessThan(here.y), "선수(+z)를 보는데 코가 지도 아래로 갔다");
            Assert.That(forward.x, Is.EqualTo(here.x).Within(0.01f));

            var back = LastShiftMapView.NosePoint(schematic, stand, Vector3.back);
            Assert.That(back.y, Is.GreaterThan(here.y), "-z 를 보는데 코가 지도 위로 갔다");

            var right = LastShiftMapView.NosePoint(schematic, stand, Vector3.right);
            Assert.That(right.x, Is.GreaterThan(here.x), "+x 를 보는데 코가 왼쪽으로 갔다");
            Assert.That(right.y, Is.EqualTo(here.y).Within(0.01f));

            var left = LastShiftMapView.NosePoint(schematic, stand, Vector3.left);
            Assert.That(left.x, Is.LessThan(here.x), "-x 를 보는데 코가 오른쪽으로 갔다");
        }

        /// <summary>위아래를 보고 있어도 코가 안 짧아진다 — 평면 성분만 쓴다.</summary>
        [Test]
        public void LookingUpDoesNotShortenTheNose()
        {
            var schematic = LastShiftMapView.Schematic(Screen);
            var stand = Vector3.zero;

            var level = LastShiftMapView.NosePoint(schematic, stand, Vector3.forward);
            var tilted = LastShiftMapView.NosePoint(schematic, stand, new Vector3(0f, 3f, 1f));

            Assert.That(tilted, Is.EqualTo(level).Using(new Vector2Comparer(0.01f)),
                "위를 봤더니 코가 짧아졌다 — y 성분이 평면 길이에 섞였다");
        }

        /// <summary>표식 사각형의 한가운데가 그 점이다.</summary>
        [Test]
        public void TheMarkerCentresOnItsPoint()
        {
            var rect = LastShiftMapView.MarkerRect(new Vector2(200f, 340f), 16f);

            Assert.That(rect.center.x, Is.EqualTo(200f).Within(0.001f));
            Assert.That(rect.center.y, Is.EqualTo(340f).Within(0.001f));
            Assert.That(rect.width, Is.EqualTo(16f).Within(0.001f));
        }

        /// <summary>내 표식이 남의 것보다 크다 — 넷이 겹쳤을 때 크기로 먼저 갈린다.</summary>
        [Test]
        public void TheSelfMarkerIsTheBiggerOne()
        {
            Assert.That(LastShiftMapView.SelfMarkerSize,
                Is.GreaterThan(LastShiftMapView.CrewMarkerSize));
        }

        /// <summary>
        /// 테두리는 <b>속을 비운다</b>. 네 조각 중 어느 것도 사각형 한가운데를 안 덮어야
        /// 그 위에 얹은 표식이 배경에 안 묻힌다.
        /// </summary>
        [Test]
        public void TheOutlineLeavesTheMiddleEmpty()
        {
            var rect = new Rect(100f, 100f, 200f, 160f);
            var bands = new Rect[4];

            LastShiftMapView.OutlineBands(rect, LastShiftMapView.RoomOutline, bands);

            foreach (var band in bands)
                Assert.That(band.Contains(rect.center), Is.False,
                    $"테두리 조각이 방 한가운데를 덮는다 — {band}");
        }

        /// <summary>
        /// <b>스폰 자리가 지도에서도 숙소 안이다.</b> 좌표를 옮긴 것(기상=숙소)이 지도에도
        /// 같이 반영됐는지는 여기서만 보인다 — 둘이 각자 좌표를 들면 조용히 갈라진다.
        /// </summary>
        [Test]
        public void TheSpawnPointFallsInsideQuartersOnThePlan()
        {
            var schematic = LastShiftMapView.Schematic(Screen);
            var quarters = LastShiftPlazaLayout.Of(LastShiftPlazaSpace.Quarters);
            var room = schematic.ToScreenRect(
                quarters.MinX, quarters.MaxX, quarters.MinZ, quarters.MaxZ);

            var marker = schematic.ToScreen(LastShiftShipDimensions.SpawnPoint);

            Assert.That(room.Contains(marker), Is.True,
                $"지도에서 스폰 표식이 숙소 밖이다 — 표식 {marker}, 숙소 {room}");
        }

        private sealed class Vector2Comparer : System.Collections.Generic.IEqualityComparer<Vector2>
        {
            private readonly float tolerance;
            public Vector2Comparer(float tolerance) => this.tolerance = tolerance;

            public bool Equals(Vector2 a, Vector2 b) =>
                Mathf.Abs(a.x - b.x) <= tolerance && Mathf.Abs(a.y - b.y) <= tolerance;

            public int GetHashCode(Vector2 value) => value.GetHashCode();
        }
    }
}
