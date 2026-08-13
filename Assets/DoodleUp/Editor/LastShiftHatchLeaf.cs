using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>
    /// <b>단문 경첩 규격.</b> 여닫는 판을 한 짝만 두고, 닫히면 그 한 짝이 구멍 한가운데를 덮되
    /// 구멍 <b>모서리</b>를 축으로 통째로 젖혀지게 만든다(방위·용도 브리프 §6 #11).
    ///
    /// <b>양문을 버린 이유는 대칭을 유지하는 비용이다.</b> 두 짝이면 좌우 오프셋·여닫이 곡선·
    /// 맞물리는 면을 영원히 맞춰 둬야 하고, 한쪽만 어긋나도 닫힌 문에 틈이 남는다. 실제로
    /// 압력문 문짝이 구멍 한가운데에서 <c>0.19m</c> 밀려 있었고 그 비대칭이 이 처방의 발단이다.
    /// 한 짝이면 맞출 상대가 없어서 어긋날 자리 자체가 사라진다.
    ///
    /// <b>계층에 경첩 마디를 새로 끼우지 않는다.</b> FBX 안쪽 계층은 아트 소관이라 임포터가
    /// 갈라 놓으면 재수출 때마다 갈린다. 대신 판이 <b>도는 점</b>만 따로 들고 판의 위치와 회전을
    /// 함께 굽는다 — 결과는 경첩 마디를 둔 것과 같고, 아트가 판 원점을 어디에 두든 성립한다.
    /// 압력문처럼 판이 곧 메시인 경우에도 그대로 쓸 수 있는 것이 이 형태의 요점이다.
    /// </summary>
    public readonly struct LastShiftHatchLeaf
    {
        /// <summary>
        /// 호를 직선 구간으로 쪼갤 때 키 하나가 감당하는 각. <c>10°</c> 면 반지름 <c>1m</c> 에서
        /// 현 오차가 <c>1mm</c> 아래라 눈에 안 잡히고, 90°짜리 문도 키 열 개면 끝난다.
        /// </summary>
        public const float DegreesPerKey = 10f;

        // ── 키트 로컬 축 ────────────────────────────────────────────────────
        // 이 키트의 로컬 축은 Unity 축이 아니다. 축 변환을 모델 루트에만 남기므로
        // (LastShiftModularKitImporter.ConfigureModelAxisConversion 의 bakeAxisConversion = false)
        // 루트 아래는 Blender 축 그대로다 — z 가 위, y 가 깊이, x 가 폭이다. 여기를 Unity 축으로
        // 착각하면 문이 서는 대신 옆으로 눕는다.

        /// <summary>키트 로컬에서 위. 선 문짝의 경첩 축이 이것이다.</summary>
        public static readonly Vector3 KitUp = Vector3.forward;

        /// <summary>키트 로컬에서 깊이(문틀을 통과하는 방향).</summary>
        public static readonly Vector3 KitDepth = Vector3.up;

        /// <summary>키트 로컬에서 폭. 누운 뚜껑의 경첩 축이 이것이다.</summary>
        public static readonly Vector3 KitWidth = Vector3.right;

        /// <summary>판이 도는 점. 판이 사는 좌표계(부모 로컬)에서 잰다.</summary>
        public readonly Vector3 Hinge;

        /// <summary>닫힘(<c>0</c>) 자세의 자리. 이때 판이 구멍 한가운데를 덮는다.</summary>
        public readonly Vector3 ClosedPosition;

        /// <summary>닫힘(<c>0</c>) 자세의 방향.</summary>
        public readonly Quaternion ClosedRotation;

        /// <summary>경첩 축. 판이 사는 좌표계의 방향이다.</summary>
        public readonly Vector3 Axis;

        /// <summary>열림(<c>1</c>)에서 젖혀지는 각.</summary>
        public readonly float Degrees;

        public LastShiftHatchLeaf(Vector3 hinge, Vector3 closedPosition, Quaternion closedRotation,
            Vector3 axis, float degrees)
        {
            Hinge = hinge;
            ClosedPosition = closedPosition;
            ClosedRotation = closedRotation;
            Axis = axis;
            Degrees = degrees;
        }

        /// <summary>구멍 한가운데를 덮는 판을 그 구멍 모서리에 매단다.</summary>
        /// <param name="openingCenter">구멍 중심. 판이 닫히면 판 중심이 여기 온다.</param>
        /// <param name="leafCenterOffset">판 마디 원점에서 판 중심까지. 아트가 정한 값이라 안 건드린다.</param>
        /// <param name="hingeDirection">구멍 중심에서 경첩 쪽으로. 길이가 곧 경첩까지 거리다.</param>
        public static LastShiftHatchLeaf AtOpening(Vector3 openingCenter, Vector3 leafCenterOffset,
            Vector3 hingeDirection, Vector3 axis, float degrees) =>
            new(openingCenter + hingeDirection, openingCenter - leafCenterOffset,
                Quaternion.identity, axis, degrees);

        /// <summary>호를 직선으로 근사할 때 필요한 키 수. 각이 크면 키도 따라 는다.</summary>
        public int KeyCount => Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(Degrees) / DegreesPerKey) + 1);

        /// <summary>
        /// <paramref name="openAmount"/>(<c>0</c> 닫힘 ~ <c>1</c> 열림)에서 판이 놓이는 자세.
        ///
        /// 회전만 굽는 것이 아니라 <b>자리도 같이</b> 굽는다 — 경첩이 판 원점이 아니라 구멍
        /// 모서리에 있으므로, 판은 제자리에서 도는 것이 아니라 그 점을 중심으로 호를 그린다.
        /// </summary>
        public void Pose(float openAmount, out Vector3 position, out Quaternion rotation)
        {
            var swing = Quaternion.AngleAxis(Degrees * openAmount, Axis);
            position = Hinge + swing * (ClosedPosition - Hinge);
            rotation = swing * ClosedRotation;
        }
    }
}
