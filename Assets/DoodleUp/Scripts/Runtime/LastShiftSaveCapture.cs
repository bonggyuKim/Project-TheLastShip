using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 복원 한 번이 어디에 얼마를 썼는가. <c>docs/tech/save-backbone-feasibility-v1.md</c> §3.3 이
    /// "측정 자리는 (가)/(나)/(다) 를 나눠 찍는다 — 넘었을 때 어디가 범인인지 알아야 한다" 고
    /// 요구한 것이고, 그래서 <b>합계 하나만 들고 있지 않는다</b>.
    /// </summary>
    public readonly struct LastShiftSaveRestoreReport
    {
        public LastShiftSaveRestoreReport(
            LastShiftSaveLoadOutcome outcome, bool campaignComplete, bool segmentRestored, int modulesBuilt,
            double reassembleMilliseconds, double injectionMilliseconds, double poseMilliseconds)
        {
            Outcome = outcome;
            CampaignComplete = campaignComplete;
            SegmentRestored = segmentRestored;
            ModulesBuilt = modulesBuilt;
            ReassembleMilliseconds = reassembleMilliseconds;
            InjectionMilliseconds = injectionMilliseconds;
            PoseMilliseconds = poseMilliseconds;
        }

        public LastShiftSaveLoadOutcome Outcome { get; }

        /// <summary>표의 모든 줄이 판정을 통과했는가. 거짓이면 파일과 규칙이 이미 갈렸다는 신호다.</summary>
        public bool CampaignComplete { get; }

        /// <summary>구간층을 실었는가. 거짓이면 구간 시작으로 되돌렸다(§4.2 폴백).</summary>
        public bool SegmentRestored { get; }

        /// <summary>씬에 다시 세운 모듈 수. 씬 조립을 건너뛰면 <c>0</c> 이다.</summary>
        public int ModulesBuilt { get; }

        /// <summary>(가) 배치물 재조립. <b>10초 예산을 다 쓰는 유일한 조각</b>이다(§3.2).</summary>
        public double ReassembleMilliseconds { get; }

        /// <summary>(나) B층 주입. 1단계 실측으로 <c>0.0021</c>ms 였다.</summary>
        public double InjectionMilliseconds { get; }

        /// <summary>(다) 물리 정지 배치 — 아이템과 승무원.</summary>
        public double PoseMilliseconds { get; }

        public double TotalMilliseconds => ReassembleMilliseconds + InjectionMilliseconds + PoseMilliseconds;
    }

    /// <summary>
    /// 세이브의 <b>담기와 되세우기</b>. 파일도 소켓도 모르고, 값 한 벌과 씬 사이만 오간다.
    ///
    /// <b>왜 서비스와 갈랐는가.</b> <see cref="LastShiftSaveService"/> 는 디스크·스레드·재진입을
    /// 알고, 여기는 게임 상태만 안다. 붙여 두면 "저장→로드 후 B층 전 필드 동일"(§2.2 합격선)을
    /// 재려고 파일을 쓰고 지워야 하고, 그러면 그 합격선이 디스크 상태에 매달린다.
    ///
    /// <b>캡처는 메인 스레드여야 한다.</b> 아이템·승무원 포즈가 <c>Transform</c> 읽기라
    /// 그렇다(§1.4-라). 그 읽기가 끝나고 나면 결과는 전부 값이라 직렬화·쓰기를 워커로 넘길 수 있다.
    /// </summary>
    public static class LastShiftSaveCapture
    {
        private static readonly List<LastShiftPlacementRecord> records = new();

        /// <summary>
        /// 지금 상태를 파일 한 벌로 접는다. <paramref name="sandbox"/> 가 <c>null</c> 이거나
        /// 구간이 돌고 있지 않으면 캠페인만 담는다 — 그게 기항 세이브다(§4.4).
        ///
        /// <b>A층은 접힌 값을 복사할 뿐 재계산하지 않는다</b>(§4.3 불변식 · §7.6). 구간 <b>중</b>에
        /// 저장할 때 래치 수를 그 자리에서 다시 세어 넣으면, 구간을 버리고 폴백한 뒤 기항 수입이
        /// 달라진다 — <see cref="LastShiftPlacementReplication.CaptureLedger"/> 가 읽는 것이
        /// 판정 순간에 접힌 <see cref="LastShiftVoyage.LastLatchCount"/> 인 것이 그 규약이다.
        /// </summary>
        public static LastShiftSaveFile Capture(LastShiftSandboxController sandbox, bool includeSegment = true)
        {
            var ledger = LastShiftPlacementReplication.CaptureLedger();
            // 커서 주인은 세션 안에서만 뜻이 있는 값이다. 파일에 실으면 다음 판이 접속하지도 않은
            // 클라이언트를 커서 주인으로 들고 시작한다.
            ledger.CursorHolder = LastShiftPlacementAuthority.NoHolder;

            LastShiftPlacementReplication.Capture(records);
            var file = new LastShiftSaveFile
            {
                SchemaA = LastShiftSaveFormat.SchemaA,
                SchemaB = LastShiftSaveFormat.SchemaB,
                Campaign = new LastShiftCampaignSave
                {
                    Ledger = ledger,
                    Modules = records.ToArray()
                }
            };

            if (sandbox == null || !includeSegment) return file;

            file.HasSegment = true;
            file.Segment = new LastShiftSegmentSave
            {
                Snapshot = sandbox.CaptureRuntimeSnapshot(),
                SituationLatchDwell = sandbox.CaptureSituationLatches(),
                Items = CaptureItems(sandbox),
                Crew = CaptureCrew(sandbox)
            };
            return file;
        }

        private static LastShiftItemSave[] CaptureItems(LastShiftSandboxController sandbox)
        {
            var items = sandbox.Items;
            if (items == null) return Array.Empty<LastShiftItemSave>();

            var saved = new List<LastShiftItemSave>(items.Length);
            foreach (var item in items)
            {
                if (item == null) continue;
                saved.Add(new LastShiftItemSave
                {
                    Role = (int)item.Role,
                    Position = item.transform.position,
                    Rotation = item.transform.rotation,
                    Secured = item.Secured,
                    SecuredByCrew = item.SecuredByCrew
                });
            }
            return saved.ToArray();
        }

        private static LastShiftCrewSave[] CaptureCrew(LastShiftSandboxController sandbox)
        {
            var players = sandbox.Players;
            if (players == null) return Array.Empty<LastShiftCrewSave>();

            var saved = new List<LastShiftCrewSave>(players.Length);
            foreach (var player in players)
            {
                if (player == null) continue;
                var oxygen = player.GetComponent<LastShiftCrewOxygen>();
                saved.Add(new LastShiftCrewSave
                {
                    Slot = (int)player.PlayerSlot,
                    Position = player.transform.position,
                    Rotation = player.transform.rotation,
                    SuitOxygen = oxygen != null ? oxygen.SuitOxygen : LastShiftRecoveryTuning.SuitOxygenInitial,
                    IsDead = oxygen != null && oxygen.IsDead,
                    IsDraining = oxygen != null && oxygen.IsDraining
                });
            }
            return saved.ToArray();
        }

        /// <summary>
        /// 파일 한 벌에서 배를 되세운다. 순서가 <b>A층 표 → 씬 조립 → B층 주입 → 포즈</b> 인 것이
        /// 요점이다 — 표가 서기 전에 조립하면 지난 판의 방을 세우고, 조립 전에 포즈를 앉히면
        /// 방 안에 있어야 할 물건이 허공에 선다.
        ///
        /// <paramref name="moduleYard"/> 가 <c>null</c> 이면 씬 조립을 건너뛴다. EditMode 가
        /// 표·원장·B층만 재는 경로이고, 그 경로가 씬을 요구하면 §2.2 합격선을 씬 없이 못 잰다.
        /// </summary>
        public static LastShiftSaveRestoreReport Restore(
            in LastShiftSaveLoad load,
            LastShiftSandboxController sandbox,
            Transform moduleYard = null,
            LastShiftModulePalette palette = null)
        {
            if (!load.CanRestore || load.File == null)
                return new LastShiftSaveRestoreReport(
                    LastShiftSaveLoadOutcome.Failed, false, false, 0, 0, 0, 0);

            var file = load.File;
            var campaign = file.Campaign ?? new LastShiftCampaignSave();

            var clock = Stopwatch.StartNew();
            var complete = LastShiftPlacementReplication.Apply(campaign.Modules);
            LastShiftPlacementReplication.ApplyLedger(campaign.Ledger);
            var built = 0;
            if (moduleYard != null) built = LastShiftModuleAssembler.Rebuild(moduleYard, palette);
            var reassemble = clock.Elapsed.TotalMilliseconds;

            clock.Restart();
            var segmentRestored = false;
            if (file.HasSegment && sandbox != null)
            {
                var segment = file.Segment ?? new LastShiftSegmentSave();
                sandbox.ApplyNetworkSnapshot(
                    segment.Snapshot, LastShiftStateAuthority.Local, segment.SituationLatchDwell);
                segmentRestored = true;
            }
            else if (sandbox != null)
            {
                RestartSegment(campaign.Ledger.SegmentIndex, sandbox);
            }
            var injection = clock.Elapsed.TotalMilliseconds;

            clock.Restart();
            if (segmentRestored && sandbox != null) RestorePoses(file.Segment, sandbox);
            clock.Stop();

            return new LastShiftSaveRestoreReport(
                load.Outcome, complete, segmentRestored, built,
                reassemble, injection, clock.Elapsed.TotalMilliseconds);
        }

        /// <summary>
        /// B층 폴백 — <b>구간 시작은 저장해서 얻는 상태가 아니라 만들어 낼 수 있는 상태다</b>(§4.2).
        /// 그래서 버린 자리를 두 줄로 채운다: 회차만 옮기고(원장은 안 건드린다) 프리셋을 다시 만든다.
        ///
        /// 플레이어가 잃는 것은 그 구간의 진행분 하나이고 캠페인은 온전하다.
        /// </summary>
        private static void RestartSegment(int segmentIndex, LastShiftSandboxController sandbox)
        {
            LastShiftVoyage.EnterSegment(segmentIndex);
            sandbox.ResetPreset(LastShiftVoyage.CurrentPreset);
        }

        /// <summary>
        /// (다) 물리 정지 배치. 아이템은 역할로, 승무원은 슬롯으로 붙는다 — 파일에 씬 참조가
        /// 없으므로 씬에 없는 역할·슬롯은 조용히 건너뛴다.
        /// </summary>
        private static void RestorePoses(LastShiftSegmentSave segment, LastShiftSandboxController sandbox)
        {
            if (segment == null) return;

            var items = sandbox.Items;
            if (items != null && segment.Items != null)
            {
                foreach (var saved in segment.Items)
                {
                    foreach (var item in items)
                    {
                        if (item == null || (int)item.Role != saved.Role) continue;
                        item.RestoreFromSave(saved.Position, saved.Rotation, saved.Secured, saved.SecuredByCrew);
                        break;
                    }
                }
            }

            var players = sandbox.Players;
            if (players == null || segment.Crew == null) return;
            foreach (var saved in segment.Crew)
            {
                foreach (var player in players)
                {
                    if (player == null || (int)player.PlayerSlot != saved.Slot) continue;
                    // 산소를 먼저 앉힌다. ResetPlayer 가 유령 여부로 콜라이더를 가르므로,
                    // 죽은 채로 저장된 승무원이 살아 있는 몸으로 한 프레임 서는 자리를 없앤다.
                    LastShiftCrewOxygen.Ensure(player)
                        .ApplyReplicated(saved.SuitOxygen, saved.IsDead, saved.IsDraining);
                    player.ResetPlayer(saved.Position, saved.Rotation);
                    break;
                }
            }
        }
    }
}
