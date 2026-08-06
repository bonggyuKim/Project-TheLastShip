using System.Collections.Generic;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 씬 드레싱의 데이터 정본. <b>씬 빌더는 이 에셋만 읽는다</b> — 소품 목록을 Editor 코드에
    /// 두면 어떤 프리팹을 어디에 놓을지 정하는 사람(art)이 매번 코드를 고쳐야 하고,
    /// 그러면 "art 는 코드를 안 쓴다" 는 역할분담이 첫 소품에서 깨진다.
    ///
    /// 이 에셋은 Inspector 에서 채운다. 항목 하나가 소품 하나이고, 공간은 이름으로 고르고
    /// (<see cref="LastShiftDressingSpace"/>), 자리는 방 치수 대비 단위좌표로 적는다.
    /// 프리팹·머티리얼 칸은 드래그로 채운다 — 문자열 경로가 아니라 참조라 에셋을 옮겨도
    /// 안 끊긴다.
    ///
    /// <b>제약은 저장 시점이 아니라 빌드 시점에 걸린다.</b> 브리프 4대 제약은
    /// <see cref="LastShiftDressingRules"/> 가 검사하고, 위반이 있으면 씬 빌드가 실패한다.
    /// Inspector 에서 못 저장하게 막지 않는 이유는 작업 중간 상태를 저장할 수 있어야
    /// 하기 때문이다 — 막는 자리는 씬으로 나가는 문 하나면 충분하다.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Shift/Dressing Set", fileName = "LastShiftDressingSet")]
    public sealed class LastShiftDressingSet : ScriptableObject
    {
        /// <summary>빌더가 이 경로에서 찾는다. art 가 파일을 옮기면 빌드가 이유를 말하며 실패한다.</summary>
        public const string AssetPath = "Assets/DoodleUp/Dressing/LastShiftDressingSet.asset";

        [SerializeField] private List<LastShiftDressingProp> props = new();

        public IReadOnlyList<LastShiftDressingProp> Props => props;

        /// <summary>부트스트랩·테스트에서만 쓴다. 씬 빌드 경로는 Inspector 로 채운 값을 읽는다.</summary>
        public void ReplaceAll(IEnumerable<LastShiftDressingProp> replacement)
        {
            props.Clear();
            props.AddRange(replacement);
        }
    }
}
