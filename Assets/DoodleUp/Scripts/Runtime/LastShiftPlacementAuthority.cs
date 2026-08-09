using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 배치 커서를 누가 잡고 있는가. <b>한 번에 한 명이다.</b>
    /// 자유 배치 확장 검토 §12-9(2인 이상에서 커서 소유권)에 대한 tech 답이고, 근거 정본은
    /// <c>docs/tech/free-placement-cursor-ownership-v1.md</c> 다.
    ///
    /// <b>왜 잠그는가 — 표 때문이 아니다.</b> 표는 이미 안전하다:
    /// <see cref="LastShiftCompartments.TryRegister"/> 가 확정하는 순간 다시 판정하므로, 둘이
    /// 같은 자리에 동시에 넣어도 두 번째는 그 자리에서 겹침으로 물린다. 잠그는 이유는 셋이다.
    /// <list type="number">
    /// <item><b>씬이 전역이다.</b> <see cref="LastShiftModuleAssembler.Rebuild"/> 는 배치된 방
    /// 전체를 지우고 다시 세운다. 둘이 번갈아 확정하면 서로의 방이 매번 지워졌다 서고,
    /// 그 사이에 <see cref="LastShiftBakedDoorways"/> 가 구멍을 메웠다 다시 뚫는다.</item>
    /// <item><b>미리보기가 하나다.</b> 커서가 둘이면 화면의 유령 상자가 누구 것인지 안 갈리고,
    /// 확정된 방과 남의 미리보기가 같은 자리에 겹쳐 보인다.</item>
    /// <item><b>배치는 기항에서만 일어난다</b>(추정 §8, <c>voyage-run-structure-v1.md</c> §4).
    /// 판 밖이라 실시간 경합이 아니고, 넷이 말로 합의하는 자리다(§6 사례 E) — 커서를
    /// 넘기는 것이 그 합의를 화면에 드러내는 가장 싼 방법이다.</item>
    /// </list>
    ///
    /// <b>네트워크 동기화는 아직 여기 없다.</b> 이 클래스는 정적 전역이라 한 프로세스 안에서만
    /// 성립한다 — 호스트가 권위를 갖고 클라이언트의 요청을 이 함수로 옮기는 배선이 붙어야
    /// 진짜 2인 이상에서 돈다. 그 배선이 없는 지금도 이 잠금은 <b>같은 클라이언트가 화면 둘을
    /// 여는 것</b>을 막고, 무엇보다 <b>소유권 규약을 코드에 고정</b>한다. 추정 §8 이 "배치가
    /// 판 안에서 가능해지면 추정 전체가 무효" 라고 적은 그 전제가 여기 걸려 있다.
    /// </summary>
    public static class LastShiftPlacementAuthority
    {
        /// <summary>아무도 안 잡고 있는 상태.</summary>
        public const int NoHolder = -1;

        /// <summary>지금 잡고 있는 클라이언트. <see cref="NoHolder"/> 면 비어 있다.</summary>
        public static int HolderId { get; private set; } = NoHolder;

        public static bool IsHeld => HolderId != NoHolder;

        public static bool IsHeldBy(int clientId) => clientId != NoHolder && HolderId == clientId;

        /// <summary>
        /// 커서를 잡는다. <b>먼저 잡은 쪽이 갖는다</b> — 나중 요청은 물린다. 뺏는 규칙을 안
        /// 두는 것이 의도다: 뺏기가 되면 확정 직전에 커서가 사라지는 자리가 생기고, 그건
        /// 판정을 통과한 배치가 이유 없이 안 들어가는 것으로 보인다.
        ///
        /// 이미 자기가 잡고 있으면 <c>true</c> 다 — 화면을 다시 여는 것이 실패가 아니어야 한다.
        /// </summary>
        public static bool TryClaim(int clientId)
        {
            if (clientId == NoHolder) return false;
            if (IsHeld) return HolderId == clientId;

            HolderId = clientId;
            return true;
        }

        /// <summary>자기가 잡은 것만 놓는다. 남의 것을 놓으려 하면 <c>false</c> 다.</summary>
        public static bool Release(int clientId)
        {
            if (!IsHeldBy(clientId)) return false;

            HolderId = NoHolder;
            return true;
        }

        /// <summary>
        /// 주인을 무시하고 푼다. <b>호스트 전용이고, 잡은 사람이 나간 자리를 위한 것이다</b> —
        /// 접속이 끊긴 클라이언트가 커서를 들고 나가면 아무도 배치를 못 하게 되고, 그 상태는
        /// 기항을 벗어날 방법이 없다.
        ///
        /// <see cref="LastShiftNetworkPlacement"/> 가 <c>OnClientDisconnectCallback</c> 에서
        /// 이것을 부른다 — 그 배선이 붙으면서 이 함수의 "호스트 전용" 이 주석이 아니라 실제
        /// 경로가 됐다.
        /// </summary>
        public static void Revoke() => HolderId = NoHolder;

        /// <summary>
        /// 서버가 정한 주인을 그대로 앉힌다. <b>클라이언트 전용 복원 문이다</b> —
        /// <see cref="LastShiftSandboxController.ApplyNetworkSnapshot"/> 와 같은 규약이고,
        /// 여기로 들어오는 값은 이미 서버에서 <see cref="TryClaim"/> 을 통과한 결과다.
        ///
        /// <b>여기서 다시 판정하지 않는다.</b> 클라이언트가 자기 판정으로 주인을 거르면
        /// 서버가 넘긴 커서를 받는 쪽만 화면이 안 열리는, 어느 쪽 화면에도 안 보이는 어긋남이
        /// 생긴다. 권위는 서버 하나다.
        /// </summary>
        public static void ApplyNetworkHolder(int clientId) =>
            HolderId = clientId < 0 ? NoHolder : clientId;

        /// <summary>
        /// 정적 상태라 초기화 훅이 있어야 한다 — 도메인 리로드를 끈 에디터에서는 플레이를
        /// 멈춰도 정적 필드가 안 죽으므로 지난 판의 주인이 다음 판에서 커서를 들고 있다.
        /// <see cref="LastShiftPlacedModules"/>·<see cref="LastShiftCompartments"/> 와 같은 이유다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode() => Revoke();
    }
}
