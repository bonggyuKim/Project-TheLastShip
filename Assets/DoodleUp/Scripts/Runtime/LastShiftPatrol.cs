using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 순회 블록의 방문 추적(정본 §4-2 · 조항 <c>N-3</c>). <b>순서를 안 본다</b> — 어느 방을
    /// 먼저 들어가든 다섯이 다 차면 <c>AI_T_11</c> 이 열린다.
    ///
    /// <b>방이 다섯이고 숙소가 그중 하나다.</b> 깨어난 자리가 숙소라 그 칸은 시작하자마자
    /// 차지만, 빼 두면 "고정 구획 다섯" 이라는 <c>AI_T_01</c> 의 말과 세는 수가 갈린다.
    ///
    /// <b>광장은 안 센다.</b> 광장은 순회의 대상이 아니라 순회가 벌어지는 자리이고,
    /// <c>AI_T_11</c> 이 "광장 재진입" 을 조건으로 갖는 것이 그 증거다 — 세는 쪽에 넣으면
    /// 마지막 방에서 나오기 전에 이미 다 찬다.
    /// </summary>
    public static class LastShiftPatrol
    {
        /// <summary>세는 방. 광장을 뺀 나머지 전부다.</summary>
        public static readonly LastShiftPlazaSpace[] Rooms =
        {
            LastShiftPlazaSpace.CockpitRoom,
            LastShiftPlazaSpace.PowerRoom,
            LastShiftPlazaSpace.LifeSupportRoom,
            LastShiftPlazaSpace.CoolingRoom,
            LastShiftPlazaSpace.Quarters
        };

        /// <summary>설비에 다가섰다고 볼 거리. 방 안쪽 끝벽 앞에 서는 정도다.</summary>
        public const float FixtureReach = 2f;

        /// <summary>전면 스크린을 마주 봤다고 볼 각도. <c>cos 45°</c> 다.</summary>
        public static readonly float ScreenFacingDot = Mathf.Cos(45f * Mathf.Deg2Rad);

        /// <summary>그 거리 안에서만 시야 판정을 한다.</summary>
        public const float ScreenViewDistance = 4f;

        private static readonly bool[] Visited = new bool[Rooms.Length];

        /// <summary>다섯이 다 찼는가.</summary>
        public static bool AllVisited
        {
            get
            {
                foreach (var seen in Visited)
                    if (!seen) return false;
                return true;
            }
        }

        /// <summary>아직 안 들어간 방 수. 로그와 검사가 읽는다.</summary>
        public static int RemainingCount
        {
            get
            {
                var left = 0;
                foreach (var seen in Visited)
                    if (!seen) left++;
                return left;
            }
        }

        public static bool HasVisited(LastShiftPlazaSpace space)
        {
            var index = IndexOf(space);
            return index >= 0 && Visited[index];
        }

        /// <summary>승무원 하나가 지금 이 공간에 있다. 매 프레임 불러도 된다.</summary>
        public static void Observe(LastShiftPlazaSpace space)
        {
            var index = IndexOf(space);
            if (index >= 0) Visited[index] = true;
        }

        public static void Clear()
        {
            for (var i = 0; i < Visited.Length; i++) Visited[i] = false;
        }

        /// <summary>
        /// 그 설비 앞인가. 설비 자리는 <b>방의 먼쪽 끝벽 한가운데</b>이고, 좌표를 적어 두지
        /// 않고 발자국에서 뽑는다(<see cref="LastShiftPlazaLayout.FarWallCenter"/>).
        /// </summary>
        public static bool IsAtFixture(LastShiftPlazaSpace space, Vector3 position)
        {
            var fixturePoint = LastShiftPlazaLayout.FarWallCenter(space);
            var dx = position.x - fixturePoint.x;
            var dz = position.z - fixturePoint.y;
            return dx * dx + dz * dz <= FixtureReach * FixtureReach;
        }

        /// <summary>
        /// 전면 스크린을 마주 보고 섰는가. <b>거리와 각도를 같이 본다</b> — 조종석은 방이
        /// 길어서 거리만 보면 문간에서도 걸리고, 각도만 보면 뒤돌아 걸어가면서도 걸린다.
        /// </summary>
        public static bool IsFacingCockpitScreen(Vector3 position, Vector3 aimDirection)
        {
            var screenPoint = LastShiftPlazaLayout.FarWallCenter(LastShiftPlazaSpace.CockpitRoom);
            var toScreen = new Vector3(screenPoint.x - position.x, 0f, screenPoint.y - position.z);
            var distance = toScreen.magnitude;
            if (distance > ScreenViewDistance) return false;
            // 스크린 바로 앞에 붙으면 방향이 정의되지 않는다. 그 자리는 보고 있는 것으로 친다.
            if (distance <= Mathf.Epsilon) return true;

            var facing = new Vector3(aimDirection.x, 0f, aimDirection.z);
            if (facing.sqrMagnitude <= Mathf.Epsilon) return false;
            return Vector3.Dot(facing.normalized, toScreen / distance) >= ScreenFacingDot;
        }

        private static int IndexOf(LastShiftPlazaSpace space)
        {
            for (var i = 0; i < Rooms.Length; i++)
                if (Rooms[i] == space) return i;
            return -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode() => Clear();
    }
}
