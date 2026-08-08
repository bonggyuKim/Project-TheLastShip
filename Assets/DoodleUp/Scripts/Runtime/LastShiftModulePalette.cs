using System.Collections.Generic;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 자유 배치 모듈이 씬에 설 때 쓰는 자산. <b>런타임이 자산을 만들지 않고 물어 온다</b>는 것이
    /// 이 에셋이 있는 이유 전부다.
    ///
    /// <b>머티리얼을 직렬화 참조로 드는 것은 취향이 아니라 강제다.</b> 씬 빌더의 구획 머티리얼은
    /// <c>AssetDatabase.CreateAsset</c> 산이고(<c>LastShiftSceneBuilder.CreateMaterial</c>), 그
    /// 경로는 Editor 전용이다. 런타임에서 <c>Shader.Find</c> 로 대신 만들면 <b>빌드에서 그 셰이더가
    /// 스트립돼 방 전체가 분홍색으로 선다</b> — 에디터에서는 정상이라 Play 검증을 통과하고
    /// Player 빌드에서만 드러나는 종류의 실패다. 근거는
    /// <c>docs/tech/free-placement-runtime-chain-estimate-v1.md</c> §4.2.
    ///
    /// <b>프리팹은 여럿이다.</b> 모듈 발자국이 한 종류가 아니므로 kit 을 들고
    /// <see cref="LastShiftModuleAssembler"/> 가 표가 요구하는 형상에 맞는 것을 고른다. 비어 있으면
    /// 조립기가 그레이박스로 세운다 — 아트 에셋이 오기 전에도 배치 경로가 끝까지 도는 쪽을 고른 것이고,
    /// 그 대가는 <see cref="LastShiftModuleAssembler"/> 문서에 적어 뒀다.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Shift/Module Palette", fileName = "LastShiftModulePalette")]
    public sealed class LastShiftModulePalette : ScriptableObject
    {
        /// <summary>조립기가 이 경로에서 찾는다. 아트가 파일을 옮기면 조립이 이유를 말하며 그레이박스로 내려간다.</summary>
        public const string AssetPath = "Assets/DoodleUp/Dressing/LastShiftModulePalette.asset";

        [Tooltip("루트에 LastShiftModuleAnchor 가 붙은 모듈 프리팹. 표가 요구하는 발자국에 맞는 것이 골라진다.")]
        [SerializeField] private List<GameObject> modulePrefabs = new();

        [Tooltip("바닥 슬래브. 비면 그레이박스가 기본 머티리얼로 선다.")]
        [SerializeField] private Material floorMaterial;

        [Tooltip("천장 슬래브.")]
        [SerializeField] private Material ceilingMaterial;

        [Tooltip("벽 판. 구획 벽은 공통 중성색이다 — 구획색은 띠와 라벨이 말한다(아트 브리프 §8.1).")]
        [SerializeField] private Material wallMaterial;

        public IReadOnlyList<GameObject> ModulePrefabs => modulePrefabs;

        public Material FloorMaterial => floorMaterial;
        public Material CeilingMaterial => ceilingMaterial;
        public Material WallMaterial => wallMaterial;

        /// <summary>테스트·부트스트랩이 쓴다. 씬 경로는 Inspector 로 채운 값을 읽는다.</summary>
        public void Configure(IEnumerable<GameObject> prefabs, Material floor, Material ceiling, Material wall)
        {
            modulePrefabs.Clear();
            if (prefabs != null) modulePrefabs.AddRange(prefabs);
            floorMaterial = floor;
            ceilingMaterial = ceiling;
            wallMaterial = wall;
        }
    }
}
