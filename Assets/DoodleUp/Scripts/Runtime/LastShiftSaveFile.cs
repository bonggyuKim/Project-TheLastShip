using System;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 세이브 파일 하나가 어떻게 읽혔는가. <b>세 갈래뿐이고 조용한 부분 로드가 없다</b>
    /// (<c>docs/tech/save-backbone-feasibility-v1.md</c> §4.4).
    /// </summary>
    public enum LastShiftSaveLoadOutcome
    {
        /// <summary>파일 그대로 실렸다. 구간이 없는 기항 세이브도 이쪽이다 — 키 부재는 오류가 아니다.</summary>
        Loaded,

        /// <summary>
        /// <c>schemaB</c> 가 안 맞아 구간층을 버렸다. 항해는 온전하고 이번 구간만 시작으로 되돌아간다.
        /// <b>안내해야 하는 유일한 경우다</b>(§4.4).
        /// </summary>
        SegmentDropped,

        /// <summary><c>schemaA</c> 불일치 또는 파싱 실패. 명시적 실패이고 아무것도 싣지 않는다.</summary>
        Failed
    }

    /// <summary>
    /// 아이템 한 개의 저장 자리. <b>속도를 안 싣는다</b>(조항 S-7) — 복원은 그 자리에 정지
    /// 배치이고, 그래서 저장 시점의 소유자 권위 지연(§7.3, 최대 <c>30</c>cm)이 판정에 안 닿는다.
    /// </summary>
    [Serializable]
    public struct LastShiftItemSave
    {
        /// <summary><see cref="LastShiftItemRole"/>. 씬 인스턴스를 이 값으로 다시 찾는다 — 파일에 씬 참조가 없다.</summary>
        public int Role;

        public Vector3 Position;
        public Quaternion Rotation;
        public bool Secured;
        public bool SecuredByCrew;
    }

    /// <summary>
    /// 승무원 한 명의 저장 자리. 위치와 산소만이다 — 시야각·입력은 상태가 아니라 그 순간의 조작이다.
    /// </summary>
    [Serializable]
    public struct LastShiftCrewSave
    {
        /// <summary><see cref="LastShiftPlayerSlot"/>. 아이템이 역할로 붙는 것과 같은 규약이다.</summary>
        public int Slot;

        public Vector3 Position;
        public Quaternion Rotation;
        public float SuitOxygen;
        public bool IsDead;
        public bool IsDraining;
    }

    /// <summary>
    /// A층 — 캠페인. <b>이 층은 시뮬 튜닝과 무관하게 안정</b>하므로 <c>schemaA</c> 가 드물게 오른다(§4.1).
    ///
    /// 담는 방식이 <see cref="LastShiftPlacementReplication"/> 재사용인 것이 요점이다. 복제가 이미
    /// "표·구역 오버레이·원장·항해를 값으로 담고 값에서 되세우는" 순수 함수를 갖고 있고, 그것이
    /// 정확히 파일이 필요로 하는 것이다 — 같은 사실을 두 벌로 담으면 한쪽만 늘어나는 자리가 생긴다.
    /// </summary>
    [Serializable]
    public sealed class LastShiftCampaignSave
    {
        /// <summary>원장·항해 진행. <b>여기 <c>LatchCount</c> 는 판정 순간에 접힌 값이다</b>(§4.3 불변식).</summary>
        public LastShiftPlacementLedger Ledger;

        /// <summary>배치물 표. 씬 참조도 핸들도 없다 — 핸들은 프로세스 안에서만 뜻이 있다.</summary>
        public LastShiftPlacementRecord[] Modules = Array.Empty<LastShiftPlacementRecord>();
    }

    /// <summary>
    /// B층 — 구간 런타임. <b>튜닝 때마다 흔들리는 층</b>이라 <c>schemaB</c> 가 자주 오르고,
    /// 안 맞으면 통째로 버려도 캠페인이 성립한다(§4.2).
    /// </summary>
    [Serializable]
    public sealed class LastShiftSegmentSave
    {
        public LastShiftNetworkSnapshot Snapshot;

        /// <summary>상황 래치 위상. 음수가 비활성이다 — 스냅샷 구조체 밖인 근거는 1단계 노트 §3.</summary>
        public float[] SituationLatchDwell = Array.Empty<float>();

        public LastShiftItemSave[] Items = Array.Empty<LastShiftItemSave>();
        public LastShiftCrewSave[] Crew = Array.Empty<LastShiftCrewSave>();
    }

    /// <summary>
    /// 세이브 파일 한 벌. <c>docs/tech/save-backbone-feasibility-v1.md</c> §4.4 의 권고 형태 그대로다.
    ///
    /// <b><see cref="HasSegment"/> 가 키 부재를 대신한다.</b> <see cref="JsonUtility"/> 는 <c>null</c>
    /// 중첩 객체를 표현하지 못하고 빈 객체로 되살리므로, "없음" 을 값으로 적지 않으면 기항 세이브와
    /// "전부 <c>0</c> 인 구간" 을 구분할 방법이 없다. 쓰기는 그래도 키 자체를 빼므로
    /// (<see cref="LastShiftSaveFormat.Write"/>) 두 신호가 언제나 같은 말을 한다.
    /// </summary>
    [Serializable]
    public sealed class LastShiftSaveFile
    {
        // 기본값이 0 인 것이 의도다. 키가 통째로 없는 파일은 스키마 0 으로 읽혀 명시적 실패가
        // 된다 — 여기에 현재 스키마를 기본값으로 두면 헤더 없는 파일이 조용히 통과한다.
        public int SchemaA;
        public int SchemaB;

        /// <summary>구간층이 있는가. 기항에서 저장한 세이브는 <c>false</c> 이고 그것이 정상 경로다(§4.4).</summary>
        public bool HasSegment;

        public LastShiftCampaignSave Campaign = new();
        public LastShiftSegmentSave Segment = new();
    }

    /// <summary>파일 하나를 읽은 결과. 실패도 값으로 돌려준다 — 예외로 던지면 부르는 쪽이 안내를 못 짠다.</summary>
    public readonly struct LastShiftSaveLoad
    {
        public LastShiftSaveLoad(LastShiftSaveLoadOutcome outcome, LastShiftSaveFile file, string reason)
        {
            Outcome = outcome;
            File = file;
            Reason = reason;
        }

        public LastShiftSaveLoadOutcome Outcome { get; }

        /// <summary>실린 내용. <see cref="LastShiftSaveLoadOutcome.Failed"/> 면 <c>null</c> 이다.</summary>
        public LastShiftSaveFile File { get; }

        /// <summary>왜 이 결과인가. 안내 문구가 아니라 로그용이다.</summary>
        public string Reason { get; }

        public bool CanRestore => Outcome != LastShiftSaveLoadOutcome.Failed;
    }

    /// <summary>
    /// 저장 포맷 — <b>로컬 <c>JSON</c> 한 벌</b>(협의 <c>1</c>, §2.1). 대상이 전부
    /// <c>[Serializable]</c> 구조체와 원시값이고 사전도 다형성도 순환 참조도 없으므로
    /// <see cref="JsonUtility"/> 가 못 하는 것이 대상에 없다. 의존성을 늘리지 않는다.
    ///
    /// <b>여기는 디스크를 모른다.</b> 문자열을 만들고 문자열을 읽을 뿐이라 워커 스레드에서 돌 수
    /// 있고(§1.4-라·§7.5), EditMode 에서 씬 없이 전부 잰다. 파일 경로와 쓰기는
    /// <see cref="LastShiftSaveService"/> 몫이다.
    /// </summary>
    public static class LastShiftSaveFormat
    {
        /// <summary>캠페인층 스키마. 안 맞으면 <b>명시적 실패</b>다 — 조용한 부분 로드를 하지 않는다(§4.4).</summary>
        public const int SchemaA = 1;

        /// <summary>구간층 스키마. 튜닝 때마다 오르고, 안 맞으면 구간을 버리고 A만 싣는다(§4.2).</summary>
        public const int SchemaB = 1;

        /// <summary>기본 파일 이름. 위치는 <c>Application.persistentDataPath</c> 다(§2.1).</summary>
        public const string DefaultFileName = "lastshift-save.json";

        /// <summary>
        /// 파일 한 벌을 <c>JSON</c> 으로 적는다. <see cref="LastShiftSaveFile.HasSegment"/> 가 거짓이면
        /// <c>segment</c> 키 자체를 빼므로, 기항 세이브는 §4.4 가 그린 모양 그대로 나온다.
        /// </summary>
        public static string Write(LastShiftSaveFile file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));

            var campaign = JsonUtility.ToJson(file.Campaign);
            var head =
                $"{{\"SchemaA\":{file.SchemaA},\"SchemaB\":{file.SchemaB}," +
                $"\"HasSegment\":{(file.HasSegment ? "true" : "false")},\"Campaign\":{campaign}";

            if (!file.HasSegment) return head + "}";
            return head + ",\"Segment\":" + JsonUtility.ToJson(file.Segment) + "}";
        }

        /// <summary>
        /// <c>JSON</c> 한 벌을 읽는다. 스키마 판정이 여기 있고, <b>버릴 것은 여기서 버린다</b> —
        /// 복원 쪽이 스키마를 다시 보면 두 곳이 같은 판정을 다르게 낼 수 있다.
        /// </summary>
        public static LastShiftSaveLoad Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new LastShiftSaveLoad(LastShiftSaveLoadOutcome.Failed, null, "empty");

            LastShiftSaveFile parsed;
            try
            {
                parsed = JsonUtility.FromJson<LastShiftSaveFile>(json);
            }
            catch (Exception error)
            {
                return new LastShiftSaveLoad(LastShiftSaveLoadOutcome.Failed, null, "parse:" + error.GetType().Name);
            }

            if (parsed == null)
                return new LastShiftSaveLoad(LastShiftSaveLoadOutcome.Failed, null, "null");
            if (parsed.SchemaA != SchemaA)
                return new LastShiftSaveLoad(
                    LastShiftSaveLoadOutcome.Failed, null, $"schemaA {parsed.SchemaA}!={SchemaA}");

            parsed.Campaign ??= new LastShiftCampaignSave();
            parsed.Campaign.Modules ??= Array.Empty<LastShiftPlacementRecord>();
            parsed.Segment ??= new LastShiftSegmentSave();

            if (parsed.HasSegment && parsed.SchemaB != SchemaB)
            {
                // 구간을 버린다. 목적지가 이미 생성 가능한 상태라(§4.2) 버린 자리를 무엇으로
                // 채울지 고민할 것이 없다 — 복원이 구간 시작을 만들어 낸다.
                parsed.HasSegment = false;
                parsed.Segment = new LastShiftSegmentSave();
                return new LastShiftSaveLoad(
                    LastShiftSaveLoadOutcome.SegmentDropped, parsed, $"schemaB {parsed.SchemaB}!={SchemaB}");
            }

            parsed.Segment.SituationLatchDwell ??= Array.Empty<float>();
            parsed.Segment.Items ??= Array.Empty<LastShiftItemSave>();
            parsed.Segment.Crew ??= Array.Empty<LastShiftCrewSave>();
            return new LastShiftSaveLoad(LastShiftSaveLoadOutcome.Loaded, parsed, parsed.HasSegment ? "full" : "port");
        }
    }
}
