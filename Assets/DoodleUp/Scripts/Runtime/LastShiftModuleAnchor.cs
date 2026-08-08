using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 발자국의 네 면 중 하나. 문이 놓인 면을 가리키는 데 쓴다.
    ///
    /// <see cref="LastShiftDoorPlane"/> 과 갈라 두는 것이 의도다 — <c>DoorPlane</c> 은 평면의
    /// <b>법선 축</b>만 말하므로 <c>AlongX</c> 하나가 <see cref="MinX"/>·<see cref="MaxX"/> 둘을
    /// 덮는다. 조립기는 그 둘을 구분해야 한다: 같은 축에 놓인 두 면은 회전이 <c>180°</c> 다르고,
    /// 안 가르면 모듈 절반이 문을 반대쪽으로 두고 선다.
    /// </summary>
    public enum LastShiftModuleFace
    {
        MinX = 0,
        MaxX = 1,
        MinZ = 2,
        MaxZ = 3
    }

    /// <summary>
    /// 모듈 하나의 발자국과 문. <b>씬 참조가 없다</b> — 값 넷이고, 그래서 프리팹을 안 띄우고
    /// EditMode 에서 조립 판정 전체를 잴 수 있다.
    ///
    /// <b>원점 규약은 "발자국 중심, 바닥 <c>y = 0</c>" 이다.</b> 씬 빌더가 고정 구획을 세울 때
    /// 쓰는 것과 같은 규약이다(<c>LastShiftSceneBuilder.CreateCompartment</c> 가
    /// <c>localPosition = (CenterX, 0, CenterZ)</c> 를 준다). 프리팹을 이 규약으로 안 만들면
    /// 아트가 넣은 방이 절반 파묻히거나 떠서 서고, 그건 배치 좌표를 아무리 고쳐도 안 낫는다.
    /// </summary>
    public readonly struct LastShiftModuleFootprint
    {
        public LastShiftModuleFootprint(
            float lengthX, float widthZ, LastShiftModuleFace doorFace, float doorOffset)
        {
            LengthX = lengthX;
            WidthZ = widthZ;
            DoorFace = doorFace;
            DoorOffset = doorOffset;
        }

        public float LengthX { get; }
        public float WidthZ { get; }

        /// <summary>문이 놓인 면.</summary>
        public LastShiftModuleFace DoorFace { get; }

        /// <summary>
        /// 그 면의 자유축 위 문 중심. <b>발자국 중심 기준 상대값</b>이다 — 절대 좌표로 두면
        /// 프리팹이 자기가 어디 놓일지 알아야 하고, 그러면 프리팹 하나를 한 자리에만 쓴다.
        /// </summary>
        public float DoorOffset { get; }

        /// <summary>문이 놓인 면이 x 축에 법선을 둔 면인가. 자유축이 z 라는 뜻이다.</summary>
        public bool DoorOnXFace => DoorFace == LastShiftModuleFace.MinX || DoorFace == LastShiftModuleFace.MaxX;

        /// <summary>
        /// 문 중심의 발자국 로컬 <c>(x, z)</c>. 조립기가 회전을 고르는 자리에서 이 한 점만
        /// 비교하면 면·부호·오프셋이 한 번에 맞춰진다 — 셋을 따로 맞추면 세 군데서 갈린다.
        /// </summary>
        public Vector2 DoorPoint => DoorFace switch
        {
            LastShiftModuleFace.MinX => new Vector2(-LengthX * 0.5f, DoorOffset),
            LastShiftModuleFace.MaxX => new Vector2(LengthX * 0.5f, DoorOffset),
            LastShiftModuleFace.MinZ => new Vector2(DoorOffset, -WidthZ * 0.5f),
            _ => new Vector2(DoorOffset, WidthZ * 0.5f)
        };

        /// <summary>
        /// 문 구멍이 자기 면 안에 다 들어가는가. 넘치면 모서리에 틈이 남아 그레이박스가
        /// 안 닫힌다 — <see cref="LastShiftCompartments.DoorSitsOnOwnBoundary"/> 가 고정 구획에
        /// 대해 묻는 것과 같은 물음이고, 프리팹은 표를 안 거치므로 여기서 한 번 더 물어야 한다.
        /// </summary>
        public bool DoorFits
        {
            get
            {
                if (LengthX <= 0f || WidthZ <= 0f) return false;
                var freeHalf = (DoorOnXFace ? WidthZ : LengthX) * 0.5f;
                return Mathf.Abs(DoorOffset) + LastShiftZoneDoor.OpeningWidth * 0.5f <= freeHalf + Epsilon;
            }
        }

        /// <summary>
        /// 구획 제원에서 발자국을 뽑는다. 표에 들어온 칸이 요구하는 <b>목표 형상</b>이 이것이고,
        /// 조립기는 프리팹 발자국을 회전시켜 여기에 맞춘다.
        /// </summary>
        public static LastShiftModuleFootprint Of(in LastShiftCompartmentSpec spec)
        {
            var face = spec.DoorPlane == LastShiftDoorPlane.AlongX
                ? (Mathf.Abs(spec.DoorPlaneCoordinate - spec.MinX) <= Epsilon
                    ? LastShiftModuleFace.MinX
                    : LastShiftModuleFace.MaxX)
                : (Mathf.Abs(spec.DoorPlaneCoordinate - spec.MinZ) <= Epsilon
                    ? LastShiftModuleFace.MinZ
                    : LastShiftModuleFace.MaxZ);

            var offset = spec.DoorPlane == LastShiftDoorPlane.AlongX
                ? spec.DoorCenter - spec.CenterZ
                : spec.DoorCenter - spec.CenterX;

            return new LastShiftModuleFootprint(spec.LengthX, spec.WidthZ, face, offset);
        }

        private const float Epsilon = 0.001f;
    }

    /// <summary>
    /// 모듈 프리팹의 루트에 붙는 계약. <b>아트가 코드를 안 거치고 채우는 유일한 자리다</b> —
    /// 방 크기와 문이 어느 면 어디에 있는지를 Inspector 에 적으면, 조립기가 그 값으로 회전을
    /// 골라 표가 요구하는 자리에 세운다.
    ///
    /// <b>이 값이 프리팹 형상과 맞는지는 여기서 안 잰다.</b> 메시를 재서 발자국을 유추하면
    /// 아트가 넣은 장식 하나가 경계 밖으로 <c>5cm</c> 삐져나온 순간 방 크기가 바뀐다.
    /// 선언한 값이 정본이고, 형상이 그 선언과 다르면 그건 아트 쪽 수정이다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftModuleAnchor : MonoBehaviour
    {
        [Tooltip("방 내부 x 길이. 벽 판 두께는 여기 안 든다 — 표의 LengthX 와 같은 값이다.")]
        [SerializeField] private float lengthX = 4f;

        [Tooltip("방 내부 z 폭. 표의 WidthZ 와 같은 값이다.")]
        [SerializeField] private float widthZ = 4f;

        [Tooltip("안쪽 문이 놓인 면. 이 면이 부모(또는 선체)를 향한다.")]
        [SerializeField] private LastShiftModuleFace doorFace = LastShiftModuleFace.MinX;

        [Tooltip("그 면 위 문 중심. 방 중심 기준 상대값이고, 0 이면 면 한가운데다.")]
        [SerializeField] private float doorOffset;

        public LastShiftModuleFootprint Footprint => new(lengthX, widthZ, doorFace, doorOffset);

        /// <summary>테스트·부트스트랩이 쓴다. 씬 경로는 Inspector 로 채운 값을 읽는다.</summary>
        public void Configure(float footprintLengthX, float footprintWidthZ,
            LastShiftModuleFace face, float offset)
        {
            lengthX = footprintLengthX;
            widthZ = footprintWidthZ;
            doorFace = face;
            doorOffset = offset;
        }
    }
}
