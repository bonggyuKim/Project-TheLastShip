using System;
using Unity.Netcode;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 스냅샷을 주입한 뒤 <b>누가 계속 계산하는가</b>. 주입과 권위 이관은 서로 다른 결정인데
    /// 이 카드 전까지는 한 덩어리였다 — <see cref="LastShiftSandboxController.ApplyNetworkSnapshot(in LastShiftNetworkSnapshot)"/>
    /// 가 언제나 "나는 클라이언트다" 를 같이 켰다.
    ///
    /// 세이브 복원은 정확히 반대를 요구한다. 값은 남의 것에서 받아 오지만 그 다음 tick 부터는
    /// <b>이 프로세스가 호스트로서 계속 굴려야 한다</b>. 그래서 두 경우를 인자로 가른다
    /// (<c>docs/tech/save-backbone-feasibility-v1.md</c> §1.3-가).
    /// </summary>
    public enum LastShiftStateAuthority
    {
        /// <summary>
        /// 서버가 계속 계산하고 이쪽은 받아 표시만 한다. 멀티플레이 클라이언트 경로이며
        /// 기본값이다 — 기존 호출부의 뜻이 전부 이것이다.
        /// </summary>
        Replicated,

        /// <summary>
        /// 값만 받고 판정 권위는 이쪽이 갖는다. 세이브 복원 경로다. 받은 뒤 파생값
        /// (<see cref="LastShiftSandboxController.UncontainedSystemMask"/> 등)을 직접 다시 계산하므로,
        /// 그 계산의 입력인 손상 마스크·수리 장부까지 스냅샷이 날라야 한다.
        /// </summary>
        Local
    }

    /// <summary>
    /// 계통 하나의 수리 장부 한 줄. 스냅샷이 <see cref="LastShiftRepairLedger.SacrificeMask"/> 만
    /// 나르던 시절에는 클라이언트가 표시만 하면 됐지만, 세이브는 판정을 <b>이어서</b> 내야 하므로
    /// 진행 중인 작업 채널의 잔여 시간과 임시 우회 만료까지 전부 필요하다(§1.3-나).
    /// </summary>
    [Serializable]
    public struct LastShiftRepairEntrySnapshot : IEquatable<LastShiftRepairEntrySnapshot>
    {
        public LastShiftRepairMode Mode;
        public LastShiftRepairMode ChannelMode;
        public bool HasCompletedRepair;
        public bool Sacrificed;
        public bool ChannelActive;
        public float BypassRemainingSeconds;
        public float ChannelRemainingSeconds;

        /// <summary>
        /// 중첩 구조체는 <see cref="BufferSerializer{TReaderWriter}.SerializeValue{T}"/> 의 제네릭
        /// 오버로드에 맡기지 않고 직접 편다. 오버로드 해석이 enum·INetworkSerializable·unmanaged
        /// 사이에서 갈리는 자리라, 필드를 하나 늘릴 때 조용히 다른 오버로드로 붙는 것보다
        /// 이렇게 눈에 보이게 두는 편이 싸다.
        /// </summary>
        public void Serialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Mode);
            serializer.SerializeValue(ref ChannelMode);
            serializer.SerializeValue(ref HasCompletedRepair);
            serializer.SerializeValue(ref Sacrificed);
            serializer.SerializeValue(ref ChannelActive);
            serializer.SerializeValue(ref BypassRemainingSeconds);
            serializer.SerializeValue(ref ChannelRemainingSeconds);
        }

        public bool Equals(LastShiftRepairEntrySnapshot other)
        {
            return Mode == other.Mode &&
                   ChannelMode == other.ChannelMode &&
                   HasCompletedRepair == other.HasCompletedRepair &&
                   Sacrificed == other.Sacrificed &&
                   ChannelActive == other.ChannelActive &&
                   BypassRemainingSeconds.Equals(other.BypassRemainingSeconds) &&
                   ChannelRemainingSeconds.Equals(other.ChannelRemainingSeconds);
        }
    }

    /// <summary>
    /// 구간 런타임(B층) 상태를 값으로 접은 한 벌. 이름이 <c>Network</c> 로 남아 있는 것은
    /// 멀티플레이가 이 경로를 먼저 냈기 때문이고, 지금은 <b>소비자가 둘</b>이다 — 소켓과 파일.
    /// 목적지가 달라도 요구는 같다("지금 상태 전부를 값으로 접어 다른 곳에서 되살린다").
    ///
    /// <b>참조가 하나도 없는 것이 규약이다.</b> <c>float</c>·<c>bool</c>·<c>int</c>·<c>enum</c>·
    /// <c>Vector3</c> 뿐이라 캡처가 곧 복사이고, 그래서 저장 버튼을 누른 뒤에도 시뮬이 계속 돌 수
    /// 있다 — 이미 뜬 스냅샷을 이후의 플레이가 건드릴 방법이 없다(§1.4-다). 여기에
    /// <c>GameObject</c>·<c>Transform</c> 을 넣는 순간 그 성질이 깨지고 저장이 시뮬을 세워야 한다.
    /// </summary>
    [Serializable]
    public struct LastShiftNetworkSnapshot : INetworkSerializable, IEquatable<LastShiftNetworkSnapshot>
    {
        public LastShiftPreset Preset;
        public LastShiftShipState ShipState;
        public LastShiftDominantProblem FirstProblem;
        public LastShiftDominantProblem CurrentProblem;
        public float CoolingScore;
        public float BatteryScore;
        public float LeakScore;
        public float DockingSecondsRemaining;
        public int ResetGeneration;
        public int ImpactApplicationCount;
        public byte SecuredItemMask;
        public bool HasAppliedImpact;
        public LastShiftVerdict Verdict;
        public byte SacrificedSystemMask;
        public float ThrustCeiling;
        public bool HeatProtectionEngaged;
        public bool SteeringDelayed;
        public bool OxygenPumpRunning;

        /// <summary>S-O3 전선 사이렌(N9). 모든 클라이언트가 같은 시점에 울려야 국소 정보 예외가 성립한다.</summary>
        public bool SirenActive;

        /// <summary>
        /// N0 구역 압력 중 엔진실·산소실. 조종석 압력은 <see cref="ShipState"/>.OxygenPressure 가
        /// 이미 나르므로(그 필드가 조종석 파생값이다) 여기서 다시 보내지 않는다.
        /// 클라이언트 HUD 3칸(N10)과 클라이언트 쪽 진공 판정이 이 둘을 읽는다.
        /// </summary>
        public float PowerPressure;
        public float CoolingPressure;
        public float LifeSupportPressure;

        /// <summary>N0b 구역 문. 닫힌 경계는 압력 교환이 0 이므로 클라이언트도 같은 상태를 봐야 한다.</summary>
        public bool Boundary0DoorOpen;
        public bool Boundary1DoorOpen;
        public bool Boundary2DoorOpen;

        /// <summary>
        /// 갑판 승강구 해치. 문 셋과 별도 필드인 것은 별도 상태이기 때문이다 — 이쪽은 압력
        /// 평준화에 안 들어가고(§24), 클라이언트가 같은 값을 봐야 하는 이유도 다르다:
        /// 열린 해치는 갑판에 뚫린 <b>구멍</b>이라 차단 콜라이더가 서버와 어긋나면 한쪽에서만
        /// 빠지는 바닥이 된다.
        /// </summary>
        public bool ForeHatchOpen;
        public bool AftHatchOpen;

        /// <summary>
        /// T2 판독이 읽는 미억제 손상 계통. 손상 판정과 수리 완료 플래그는 서버에만 있으므로
        /// 클라이언트는 이 마스크 없이는 같은 등급을 낼 수 없다 — 고쳐도 게이지가 안 내려간다.
        ///
        /// <b><see cref="LastShiftStateAuthority.Local"/> 로 주입할 때는 이 값을 쓰지 않는다.</b>
        /// 그쪽은 호스트가 계속 계산해야 하므로 결과가 아니라 입력(<see cref="DamagedSystemMask"/>·
        /// 수리 장부)을 받아야 한다.
        /// </summary>
        public byte UncontainedSystemMask;

        // ── 이하 세이브 복원용 확장(§1.3-나). 네트워크는 이 값들 없이도 굴러갔다 —
        //    클라이언트는 표시만 하지 판정을 안 내기 때문이다. 세이브는 판정을 이어서 내므로
        //    전부 필요하다. 클라이언트도 같이 받는 것은 손해가 아니다(전부 값 타입 수십 바이트).

        /// <summary>
        /// 계통별 수리 장부 전체. 빠진 상태 중 제일 컸다 — 스냅샷은 성능 포기 마스크만 날랐고
        /// <b>진행 중인 수리 채널 잔여 시간·임시 우회 만료·완료 플래그가 통째로 없었다</b>.
        /// 이게 없으면 복원 직후 "0.8초 남은 안전 복구" 가 사라지고 60초짜리 임시 우회가 영구화된다.
        /// </summary>
        public LastShiftRepairEntrySnapshot CoolingRepair;
        public LastShiftRepairEntrySnapshot PowerRepair;
        public LastShiftRepairEntrySnapshot OxygenRepair;

        /// <summary>
        /// 장부 이력 카운터. 엔트리에서 파생되지 않는 유일한 값들이라 따로 나른다 —
        /// 결과 화면 요약 4칸(<c>CT-01</c> §5.5)이 "임시 수리 3회 · 재이탈 2회" 를 여기서 읽는다.
        /// (<c>SacrificeCount</c> 는 엔트리의 <c>Sacrificed</c> 로 다시 셀 수 있으므로 안 보낸다.)
        /// </summary>
        public int QuickBypassCount;
        public int BypassLapseCount;

        /// <summary>
        /// 충격 시점에 확정된 손상 계통. <see cref="UncontainedSystemMask"/> 의 <b>입력</b>이며,
        /// 권위를 되찾는 복원에서는 결과가 아니라 이 입력이 있어야 다음 tick 계산이 성립한다.
        /// </summary>
        public byte DamagedSystemMask;

        /// <summary>
        /// 조종 홀드(추력·자세·잔여 8초). 없으면 복원 직후 조종 입력이 되살아나는 시점이 달라진다.
        /// </summary>
        public float ControlHoldThrustDemand;
        public float ControlHoldAttitudeDegrees;
        public float ControlHoldRemainingSeconds;

        /// <summary>
        /// 전력 부족 조향 지연의 잔여와 그 지연이 끝나면 커밋될 대기 입력. 안 나르면
        /// 복원이 지연을 리셋해서 "전력이 없으면 조향이 늦다" 는 규칙이 저장 한 번으로 지워진다.
        /// </summary>
        public float SteeringDelayRemainingSeconds;
        public float PendingThrustDemand;
        public float PendingAttitudeDegrees;
        public bool HasPendingControl;

        /// <summary>
        /// 엔진 보호 잠금 누적 시간. 판정 원인 줄이 "왜 추력이 낮았는가" 를 이 값으로 답한다.
        /// </summary>
        public float HeatProtectionSeconds;

        /// <summary>마지막으로 승무원이 죽은 구역. 질식 판정 원인 줄의 <c>○○실</c> 자리다.</summary>
        public LastShiftZone CrewDeathZone;
        public bool HasCrewDeathZone;

        /// <summary>
        /// 도킹 트리거 <b>진입 엣지</b>의 기준값. 도킹 판정은 상주가 아니라 진입으로 나므로,
        /// 트리거 안에서 저장하고 복원했을 때 이 값이 <c>false</c> 로 초기화되면 가만히 서
        /// 있는 것만으로 다음 tick 에 도킹 판정이 난다.
        /// </summary>
        public bool CrewAtDockingTrigger;

        /// <summary>
        /// 적용된 운석. <c>LastResult</c> 재계산의 입력이라 기본값으로 복원하면 판정 점수가
        /// 저장 전후로 달라진다. <see cref="HasAppliedImpact"/> 가 거짓이면 의미 없는 값이다.
        /// </summary>
        public Vector3 MeteorImpactPoint;
        public Vector3 MeteorImpactVector;
        public float MeteorMass;
        public float MeteorSpeed;

        /// <summary>
        /// 냉각 밸브를 잡고 있는 승무원의 슬롯 비트마스크(<see cref="LastShiftPlayerSlot"/>).
        /// 사람 수가 아니라 <b>누구인가</b> 를 담는 이유는 복원 때문이다 — 수만 알면 다시 붙일
        /// 대상을 못 고르고, 잡은 채로 저장한 판이 복원 후 냉각이 끊긴 채로 이어진다.
        /// </summary>
        public byte CoolingValveHolderMask;

        /// <summary>
        /// 판정이 확정된 뒤 흐른 시간. 절대 시각(<c>Time.unscaledTime</c>)은 프로세스마다 다르므로
        /// 경과로 접어서 나른다. <see cref="LastShiftStateAuthority.Replicated"/> 경로는 이 값을
        /// 쓰지 않는다 — 클라이언트에서 판정 시각은 "스냅샷이 처음 도착한 순간" 이 맞다.
        /// </summary>
        public float SecondsSinceVerdict;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Preset);
            serializer.SerializeValue(ref ShipState.ThrustDemand);
            serializer.SerializeValue(ref ShipState.BusPower);
            serializer.SerializeValue(ref ShipState.OxygenPressure);
            serializer.SerializeValue(ref ShipState.HullIntegrity);
            serializer.SerializeValue(ref ShipState.EngineHeat);
            serializer.SerializeValue(ref ShipState.ShipAttitudeDegrees);
            serializer.SerializeValue(ref ShipState.ExistingDamage);
            serializer.SerializeValue(ref ShipState.FuelReserve);
            serializer.SerializeValue(ref ShipState.DockProgress);
            serializer.SerializeValue(ref FirstProblem);
            serializer.SerializeValue(ref CurrentProblem);
            serializer.SerializeValue(ref CoolingScore);
            serializer.SerializeValue(ref BatteryScore);
            serializer.SerializeValue(ref LeakScore);
            serializer.SerializeValue(ref DockingSecondsRemaining);
            serializer.SerializeValue(ref ResetGeneration);
            serializer.SerializeValue(ref ImpactApplicationCount);
            serializer.SerializeValue(ref SecuredItemMask);
            serializer.SerializeValue(ref HasAppliedImpact);
            serializer.SerializeValue(ref Verdict);
            serializer.SerializeValue(ref SacrificedSystemMask);
            serializer.SerializeValue(ref ThrustCeiling);
            serializer.SerializeValue(ref HeatProtectionEngaged);
            serializer.SerializeValue(ref SteeringDelayed);
            serializer.SerializeValue(ref OxygenPumpRunning);
            serializer.SerializeValue(ref SirenActive);
            serializer.SerializeValue(ref PowerPressure);
            serializer.SerializeValue(ref CoolingPressure);
            serializer.SerializeValue(ref LifeSupportPressure);
            serializer.SerializeValue(ref Boundary0DoorOpen);
            serializer.SerializeValue(ref Boundary1DoorOpen);
            serializer.SerializeValue(ref Boundary2DoorOpen);
            serializer.SerializeValue(ref ForeHatchOpen);
            serializer.SerializeValue(ref AftHatchOpen);
            serializer.SerializeValue(ref UncontainedSystemMask);
            CoolingRepair.Serialize(serializer);
            PowerRepair.Serialize(serializer);
            OxygenRepair.Serialize(serializer);
            serializer.SerializeValue(ref QuickBypassCount);
            serializer.SerializeValue(ref BypassLapseCount);
            serializer.SerializeValue(ref DamagedSystemMask);
            serializer.SerializeValue(ref ControlHoldThrustDemand);
            serializer.SerializeValue(ref ControlHoldAttitudeDegrees);
            serializer.SerializeValue(ref ControlHoldRemainingSeconds);
            serializer.SerializeValue(ref SteeringDelayRemainingSeconds);
            serializer.SerializeValue(ref PendingThrustDemand);
            serializer.SerializeValue(ref PendingAttitudeDegrees);
            serializer.SerializeValue(ref HasPendingControl);
            serializer.SerializeValue(ref HeatProtectionSeconds);
            serializer.SerializeValue(ref CrewDeathZone);
            serializer.SerializeValue(ref HasCrewDeathZone);
            serializer.SerializeValue(ref CrewAtDockingTrigger);
            serializer.SerializeValue(ref MeteorImpactPoint);
            serializer.SerializeValue(ref MeteorImpactVector);
            serializer.SerializeValue(ref MeteorMass);
            serializer.SerializeValue(ref MeteorSpeed);
            serializer.SerializeValue(ref CoolingValveHolderMask);
            serializer.SerializeValue(ref SecondsSinceVerdict);
        }

        /// <summary>
        /// 스냅샷 동등성. <b>필드를 늘리면 반드시 여기도 늘어야 한다</b> —
        /// <see cref="Unity.Netcode.NetworkVariable{T}"/> 이 변경 감지에 이 함수를 쓰므로,
        /// 빠뜨린 필드는 값이 바뀌어도 영영 전송되지 않는다.
        /// </summary>
        public bool Equals(LastShiftNetworkSnapshot other)
        {
            return Preset == other.Preset &&
                   ShipState.ThrustDemand.Equals(other.ShipState.ThrustDemand) &&
                   ShipState.BusPower.Equals(other.ShipState.BusPower) &&
                   ShipState.OxygenPressure.Equals(other.ShipState.OxygenPressure) &&
                   ShipState.HullIntegrity.Equals(other.ShipState.HullIntegrity) &&
                   ShipState.EngineHeat.Equals(other.ShipState.EngineHeat) &&
                   ShipState.ShipAttitudeDegrees.Equals(other.ShipState.ShipAttitudeDegrees) &&
                   ShipState.ExistingDamage.Equals(other.ShipState.ExistingDamage) &&
                   ShipState.FuelReserve.Equals(other.ShipState.FuelReserve) &&
                   ShipState.DockProgress.Equals(other.ShipState.DockProgress) &&
                   FirstProblem == other.FirstProblem &&
                   CurrentProblem == other.CurrentProblem &&
                   CoolingScore.Equals(other.CoolingScore) &&
                   BatteryScore.Equals(other.BatteryScore) &&
                   LeakScore.Equals(other.LeakScore) &&
                   DockingSecondsRemaining.Equals(other.DockingSecondsRemaining) &&
                   ResetGeneration == other.ResetGeneration &&
                   ImpactApplicationCount == other.ImpactApplicationCount &&
                   SecuredItemMask == other.SecuredItemMask &&
                   HasAppliedImpact == other.HasAppliedImpact &&
                   Verdict == other.Verdict &&
                   SacrificedSystemMask == other.SacrificedSystemMask &&
                   ThrustCeiling.Equals(other.ThrustCeiling) &&
                   HeatProtectionEngaged == other.HeatProtectionEngaged &&
                   SteeringDelayed == other.SteeringDelayed &&
                   OxygenPumpRunning == other.OxygenPumpRunning &&
                   SirenActive == other.SirenActive &&
                   PowerPressure.Equals(other.PowerPressure) &&
                   CoolingPressure.Equals(other.CoolingPressure) &&
                   LifeSupportPressure.Equals(other.LifeSupportPressure) &&
                   Boundary0DoorOpen == other.Boundary0DoorOpen &&
                   Boundary1DoorOpen == other.Boundary1DoorOpen &&
                   Boundary2DoorOpen == other.Boundary2DoorOpen &&
                   ForeHatchOpen == other.ForeHatchOpen &&
                   AftHatchOpen == other.AftHatchOpen &&
                   UncontainedSystemMask == other.UncontainedSystemMask &&
                   CoolingRepair.Equals(other.CoolingRepair) &&
                   PowerRepair.Equals(other.PowerRepair) &&
                   OxygenRepair.Equals(other.OxygenRepair) &&
                   QuickBypassCount == other.QuickBypassCount &&
                   BypassLapseCount == other.BypassLapseCount &&
                   DamagedSystemMask == other.DamagedSystemMask &&
                   ControlHoldThrustDemand.Equals(other.ControlHoldThrustDemand) &&
                   ControlHoldAttitudeDegrees.Equals(other.ControlHoldAttitudeDegrees) &&
                   ControlHoldRemainingSeconds.Equals(other.ControlHoldRemainingSeconds) &&
                   SteeringDelayRemainingSeconds.Equals(other.SteeringDelayRemainingSeconds) &&
                   PendingThrustDemand.Equals(other.PendingThrustDemand) &&
                   PendingAttitudeDegrees.Equals(other.PendingAttitudeDegrees) &&
                   HasPendingControl == other.HasPendingControl &&
                   HeatProtectionSeconds.Equals(other.HeatProtectionSeconds) &&
                   CrewDeathZone == other.CrewDeathZone &&
                   HasCrewDeathZone == other.HasCrewDeathZone &&
                   CrewAtDockingTrigger == other.CrewAtDockingTrigger &&
                   // Vector3 의 == 는 1e-5 근사 비교다. 여기서 그걸 쓰면 "저장→로드 후 전
                   // 필드 비트 동일"(§2.2 합격선)을 검사하는 왕복 테스트가 미세한 어긋남을
                   // 통과시킨다. Equals 는 성분별 정확 비교이므로 이쪽을 쓴다.
                   MeteorImpactPoint.Equals(other.MeteorImpactPoint) &&
                   MeteorImpactVector.Equals(other.MeteorImpactVector) &&
                   MeteorMass.Equals(other.MeteorMass) &&
                   MeteorSpeed.Equals(other.MeteorSpeed) &&
                   CoolingValveHolderMask == other.CoolingValveHolderMask &&
                   SecondsSinceVerdict.Equals(other.SecondsSinceVerdict);
        }
    }
}
