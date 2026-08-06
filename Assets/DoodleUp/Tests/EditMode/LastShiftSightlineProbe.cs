using System.Collections.Generic;
using DoodleUp.Runtime;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 판독 기하 검사의 공용 자. <b>선분 대 AABB</b> 로 재고, 좌표를 하나도 들고 있지 않다 —
    /// 배플 면 x 나 게이지 z 를 여기 박으면 <see cref="LastShiftShipDimensions.BaffleOffsetT"/>
    /// 가 움직일 때 검사가 조용히 통과한다.
    ///
    /// 평면 하나가 아니라 상자로 재는 이유. 두 개구부를 모두 지나는 직선은 배플 중심 평면에서
    /// 반드시 두 개구부 구간을 같은 비율로 보간한 구간 안에 있으므로, 그 평면 하나만으로도
    /// A3 증명은 <b>이미 완결이다</b>(중심 평면은 판 두께 안의 단면이다). 상자로 다시 재는 것은
    /// 증명을 보강하려는 것이 아니라 <b>그 보간 대수 자체가 틀렸을 때를 잡으려는 것</b>이다 —
    /// 대수와 독립적인 두 번째 계산이라 서로의 오류를 덮지 않는다.
    ///
    /// 모든 판정은 xz 평면에서 한다. 배플·벌크헤드가 바닥에서 천장까지 서 있으므로 y 는
    /// 결과를 바꾸지 않는다.
    /// </summary>
    public static class LastShiftSightlineProbe
    {
        private const float Epsilon = 0.0001f;

        /// <summary>
        /// 선분이 축 정렬 상자와 교차하는가. 슬래브(slab) 법이며 <b>경계에 스치는 것은 교차로
        /// 세지 않는다</b> — 통행 차선이 배플 하한과 정확히 맞닿게 설계돼 있어서, 접촉을
        /// 교차로 세면 정상 배치가 FAIL 한다.
        /// </summary>
        public static bool SegmentHitsBox(
            Vector2 from, Vector2 to, float minX, float maxX, float minZ, float maxZ)
        {
            var enter = 0f;
            var exit = 1f;
            var delta = to - from;
            if (!ClipAxis(from.x, delta.x, minX, maxX, ref enter, ref exit)) return false;
            if (!ClipAxis(from.y, delta.y, minZ, maxZ, ref enter, ref exit)) return false;
            // 구간이 열려 있어야 관통이다. enter == exit 는 꼭짓점·모서리를 스친 경우다.
            return exit - enter > Epsilon;
        }

        private static bool ClipAxis(float origin, float delta, float min, float max, ref float enter, ref float exit)
        {
            if (Mathf.Abs(delta) < Epsilon)
                return origin > min + Epsilon && origin < max - Epsilon;
            var near = (min - origin) / delta;
            var far = (max - origin) / delta;
            if (near > far) (near, far) = (far, near);
            enter = Mathf.Max(enter, near);
            exit = Mathf.Min(exit, far);
            return enter < exit;
        }

        /// <summary>통로에 선 배플 상자를 선분이 지나는가.</summary>
        public static bool BaffleBlocks(Vector2 from, Vector2 to, int passage)
        {
            var half = LastShiftShipDimensions.BaffleThickness * 0.5f;
            var centerX = LastShiftShipDimensions.BaffleCenterX(passage);
            return SegmentHitsBox(from, to,
                centerX - half, centerX + half,
                LastShiftShipDimensions.BaffleMinZ(passage), LastShiftShipDimensions.BaffleMaxZ(passage));
        }

        /// <summary>
        /// 벌크헤드가 선분을 막는가. 개구부 평면을 지날 때 그 개구부의 z 구간 안이 아니면
        /// 벽이다. 선분의 끝이 평면 위에 놓인 경우(게이지가 그 평면에 붙어 있다)는 통과로
        /// 세지 않는다 — 아직 벽을 건너지 않았다.
        /// </summary>
        public static bool BulkheadBlocks(Vector2 from, Vector2 to)
        {
            for (var opening = 0; opening < LastShiftShipDimensions.OpeningCount; opening++)
            {
                var planeX = LastShiftShipDimensions.OpeningX(opening);
                var lower = Mathf.Min(from.x, to.x);
                var upper = Mathf.Max(from.x, to.x);
                if (planeX <= lower + Epsilon || planeX >= upper - Epsilon) continue;
                var t = (planeX - from.x) / (to.x - from.x);
                var z = Mathf.Lerp(from.y, to.y, t);
                if (z < LastShiftShipDimensions.OpeningMinZ(opening) - Epsilon ||
                    z > LastShiftShipDimensions.OpeningMaxZ(opening) + Epsilon)
                    return true;
            }
            return false;
        }

        /// <summary>게이지 목표점. 개구부 인방 <b>전폭</b>의 좌우 끝 두 점이다.</summary>
        public static Vector2[] GaugeTargets(int opening) => new[]
        {
            new Vector2(LastShiftShipDimensions.OpeningX(opening), LastShiftShipDimensions.OpeningMinZ(opening)),
            new Vector2(LastShiftShipDimensions.OpeningX(opening), LastShiftShipDimensions.OpeningMaxZ(opening))
        };

        /// <summary>
        /// 이 자리에서 게이지가 <b>전폭으로</b> 읽히는가. 한쪽 끝만 보이면 등급 칸이 잘리므로
        /// 판독으로 세지 않는다. 조건 셋: 게이지 앞면 쪽에 서 있고, 벌크헤드에 막히지 않고,
        /// 어느 통로의 배플에도 막히지 않는다.
        /// </summary>
        public static bool GaugeReadableFrom(Vector2 eye, int opening)
        {
            var facing = LastShiftShipDimensions.GaugeFacingX(opening);
            if ((eye.x - LastShiftShipDimensions.OpeningX(opening)) * facing <= Epsilon) return false;
            foreach (var target in GaugeTargets(opening))
            {
                if (BulkheadBlocks(eye, target)) return false;
                for (var passage = 0; passage < 2; passage++)
                    if (BaffleBlocks(eye, target, passage)) return false;
            }
            return true;
        }

        /// <summary>
        /// 이 자리에서 동시 판독 가능한 구역 수. 자기가 선 구역은 방 안을 직접 보므로 항상
        /// 세고, 나머지는 <b>전폭으로 읽히는 게이지</b>가 가리키는 구역이다.
        ///
        /// 게이지는 개구부 1·2 에만 있다. 개구부 0·3 은 방과 통로가 같은 구역이라 게이지가
        /// 자기 구역을 가리키게 되고, 허용된 t 구간 전체에서 필요 관찰자 거리가 통로 밖으로
        /// 나가 어차피 읽히지 않는다.
        /// </summary>
        public static int SimultaneousZones(Vector2 eye, out LastShiftZone[] zones)
        {
            var seen = new System.Collections.Generic.HashSet<LastShiftZone>
            {
                LastShiftZoneAtlas.Resolve(new Vector3(eye.x, 0f, eye.y))
            };
            foreach (var opening in GaugeOpenings)
            {
                if (!GaugeReadableFrom(eye, opening)) continue;
                var beyond = LastShiftShipDimensions.SpaceCenterXAfter(opening);
                if (LastShiftShipDimensions.GaugeFacingX(opening) > 0f)
                    beyond = LastShiftShipDimensions.SpaceCenterXBefore(opening);
                seen.Add(LastShiftZoneAtlas.Resolve(new Vector3(beyond, 0f, 0f)));
            }
            zones = new LastShiftZone[seen.Count];
            seen.CopyTo(zones);
            return zones.Length;
        }

        /// <summary>게이지가 달린 개구부. 통로 쪽 단면이라 가리키는 구역이 자기 구역과 다르다.</summary>
        /// <summary>
        /// 게이지가 붙은 개구부. 번호를 적어 두면 개구부가 넷에서 다섯이 되며 번호가 밀렸을 때
        /// (§3) 방-방 문(게이지 없음)을 게이지로 세고도 그럴듯한 값이 나온다. 치수 정본에 묻는다.
        /// </summary>
        public static readonly int[] GaugeOpenings = BuildGaugeOpenings();

        private static int[] BuildGaugeOpenings()
        {
            var list = new List<int>();
            for (var opening = 0; opening < LastShiftShipDimensions.OpeningCount; opening++)
                if (LastShiftShipDimensions.HasGauge(opening)) list.Add(opening);
            return list.ToArray();
        }
    }
}
