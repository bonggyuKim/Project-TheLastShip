using System.Collections;
using DoodleUp.Runtime;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DoodleUp.Tests.PlayMode
{
    /// <summary>
    /// 자유 배치의 <b>네트워크 배선</b>을 잰다 — 복제 컴포넌트가 실제로 스폰되고, 권위가
    /// 서버 하나이고, 확정이 복제 목록에 실리는가.
    ///
    /// <b>값 왕복은 여기서 안 잰다.</b> 그건 세션 없이 도는 EditMode
    /// (<c>LastShiftPlacementReplicationTests</c>) 몫이다. 여기서만 볼 수 있는 것은 셋이다:
    /// <list type="number">
    /// <item><b>컴포넌트가 스폰 전에 붙는가.</b> <see cref="NetworkObject"/> 는 자식
    /// <see cref="NetworkBehaviour"/> 목록을 스폰 시점에 굳히므로, 런타임 설치 훅이 한 프레임만
    /// 늦어도 변수도 RPC 도 배선되지 않는다 — 그런데 그 실패는 컴파일에도 EditMode 에도 안 잡힌다.</item>
    /// <item><b>커서를 안 잡은 사람이 못 놓는가.</b> 권위 검사가 서버 몸통 안에 있어야 하고,
    /// 호스트가 자기 경로로 빠져나가면 그 검사는 2인 이상에서만 드러난다.</item>
    /// <item><b>커서를 든 채로 나간 자리가 풀리는가.</b> 안 풀리면 아무도 배치를 못 하는
    /// 기항에 갇힌다.</item>
    /// </list>
    ///
    /// <b>한 프로세스로 잰다.</b> 에디터 하나는 프로세스 하나라 host 이면서 client 를 함께 띄울
    /// 수 없다. 재려는 것이 전부 서버 쪽 판정이므로 <c>ServerPlace</c>·<c>ServerRemoveLast</c> 를
    /// 클라이언트 번호를 바꿔 가며 직접 부른다 — 같은 파일의 앞선 테스트가
    /// <c>TryGrabFromServer</c> 를 그렇게 재는 것과 같은 방식이다.
    /// </summary>
    public sealed class LastShiftPlacementNetworkPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP02A_NETWORK.unity";
        private const ushort PlacementTestPort = 7983;

        /// <summary>커서를 안 잡은 남. 호스트(<c>0</c>)와 겹치지 않는 아무 번호면 된다.</summary>
        private const ulong OtherClientId = 7;

        [UnityTearDown]
        public IEnumerator ShutDownSession()
        {
            LastShiftPlacementAuthority.Revoke();
            LastShiftCompartments.ClearModules();
            LastShiftPlacedModules.Clear();
            LastShiftVoyage.Clear();

            var manager = Object.FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
            if (manager != null && manager.IsListening)
            {
                manager.Shutdown();
                var deadline = Time.realtimeSinceStartup + 5f;
                while (manager != null && manager.IsListening && Time.realtimeSinceStartup < deadline)
                    yield return null;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator HostOwnsPlacementAndReplicatesItToTheModuleList()
        {
            LastShiftNetworkSession.AutoStartHost = false;
            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            for (var frame = 0; frame < 5; frame++) yield return null;

            var session = Object.FindFirstObjectByType<LastShiftNetworkSession>(FindObjectsInactive.Include);
            Assert.That(session, Is.Not.Null);
            session.OverridePort(PlacementTestPort);
            Assert.That(session.StartHost(), Is.True);

            // 씬 내 NetworkObject 는 host 시작 직후 한 프레임에 전부 spawn 되지 않는다.
            yield return WaitFor(
                () => LastShiftNetworkPlacement.Active != null && LastShiftNetworkPlacement.Active.IsSpawned,
                "placement-spawned");

            var placement = LastShiftNetworkPlacement.Active;
            var host = session.NetworkManager.LocalClientId;

            // 기항이 열려 있어야 살 수 있다. 항해 루프 대신 여기서 직접 연다 — 재려는 것은
            // 배치 경로이고 구간 판정은 다른 파일이 잰다.
            LastShiftCompartments.ClearModules();
            LastShiftPlacedModules.Clear();
            LastShiftMaintenance.Clear();
            LastShiftMaintenance.ArriveAtPort(LastShiftMaintenance.MaxLatches);
            var balanceBefore = LastShiftMaintenance.Balance;

            // ── 커서를 안 잡으면 못 놓는다 ──────────────────────────────────
            LastShiftPlacementAuthority.Revoke();
            var refused = placement.ServerPlace(host, LastShiftModuleCatalog.Corridor, 1, 0f, 0f, -1, true);
            Assert.That(refused.Result, Is.EqualTo(LastShiftPlacementCommandResult.NotCursorHolder));
            Assert.That(LastShiftCompartments.ModuleCount, Is.Zero);
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(balanceBefore),
                "커서도 없이 물린 요청이 여력을 태웠다.");

            // ── 남의 커서로도 못 놓는다 ────────────────────────────────────
            Assert.That(LastShiftPlacementAuthority.TryClaim((int)OtherClientId), Is.True);
            var stolen = placement.ServerPlace(host, LastShiftModuleCatalog.Corridor, 1, 0f, 0f, -1, true);
            Assert.That(stolen.Result, Is.EqualTo(LastShiftPlacementCommandResult.NotCursorHolder));

            // ── 커서를 잡은 사람이 놓는다 ──────────────────────────────────
            LastShiftPlacementAuthority.Revoke();
            Assert.That(LastShiftPlacementAuthority.TryClaim((int)host), Is.True);

            var anchor = HullAnchor(LastShiftModuleCatalog.Corridor);
            var placed = placement.ServerPlace(
                host, LastShiftModuleCatalog.Corridor, 1, anchor.x, anchor.z, -1, true);
            Assert.That(placed.Accepted, Is.True, placed.Message);
            Assert.That(LastShiftCompartments.ModuleCount, Is.EqualTo(1));
            Assert.That(LastShiftMaintenance.Balance,
                Is.EqualTo(balanceBefore - LastShiftModuleCatalog.At(LastShiftModuleCatalog.Corridor).MaintenanceCost));

            // ── 확정이 복제 목록에 실린다 ──────────────────────────────────
            yield return WaitFor(() => placement.ReplicatedModuleCount == 1, "module-replicated");

            var record = placement.ReplicatedModuleAt(0);
            var spec = LastShiftCompartments.At(LastShiftCompartments.FixedCount);
            Assert.That(record.MinX, Is.EqualTo(spec.MinX).Within(0.0005f));
            Assert.That(record.MaxZ, Is.EqualTo(spec.MaxZ).Within(0.0005f));
            Assert.That(record.CatalogIndex, Is.EqualTo(LastShiftModuleCatalog.Corridor),
                "종류가 안 실렸다 — 클라이언트 방은 발자국만 같고 효과가 없다.");
            Assert.That(record.Cost, Is.EqualTo(placed.Cost));
            Assert.That(record.PortIndex, Is.EqualTo(LastShiftMaintenance.PortIndex));

            // ── 원장·커서가 값 하나로 실린다 ───────────────────────────────
            yield return WaitFor(
                () => placement.Ledger.Balance == LastShiftMaintenance.Balance &&
                      placement.Ledger.CursorHolder == (int)host,
                "ledger-replicated");
            Assert.That(placement.Ledger.PortIndex, Is.EqualTo(LastShiftMaintenance.PortIndex));

            // ── 커서를 든 채로 나가면 호스트가 푼다 ────────────────────────
            placement.ReleaseCursorOfDepartedClient(host);
            Assert.That(LastShiftPlacementAuthority.IsHeld, Is.False,
                "커서를 든 사람이 나갔는데 안 풀렸다 — 아무도 배치를 못 하는 기항이 된다.");
            yield return WaitFor(
                () => placement.Ledger.CursorHolder == LastShiftPlacementAuthority.NoHolder,
                "cursor-revoke-replicated");

            // ── 철거도 같은 문을 지난다 ────────────────────────────────────
            var orphaned = placement.ServerRemoveLast(host);
            Assert.That(orphaned.Result, Is.EqualTo(LastShiftPlacementCommandResult.NotCursorHolder));

            Assert.That(LastShiftPlacementAuthority.TryClaim((int)host), Is.True);
            var removed = placement.ServerRemoveLast(host);
            Assert.That(removed.Accepted, Is.True, removed.Message);
            Assert.That(removed.Refunded, Is.EqualTo(placed.Cost),
                "같은 기항 안이라 전액이 돌아와야 한다(조항 M-4).");
            Assert.That(LastShiftCompartments.ModuleCount, Is.Zero);

            yield return WaitFor(() => placement.ReplicatedModuleCount == 0, "removal-replicated");
        }

        /// <summary>
        /// 선체 좌현 면에 딱 붙는 최소 모서리. 회전 <c>1</c> 이 기준 자세의 <c>MinX</c> 문을
        /// <c>MaxZ</c> 면으로 보내고, 그 면이 선체를 향한다 — EditMode 배치 테스트와 같은 표본이다.
        /// </summary>
        private static Vector3 HullAnchor(int catalogIndex)
        {
            var depth = LastShiftModuleCatalog.At(catalogIndex).Footprint.Rotated(1).WidthZ;
            return new Vector3(0f, 0f, -LastShiftShipDimensions.HalfWidth - depth);
        }

        private static IEnumerator WaitFor(
            System.Func<bool> predicate, string phase, float timeoutSeconds = 10f)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, $"timed out waiting for {phase}");
        }
    }
}
