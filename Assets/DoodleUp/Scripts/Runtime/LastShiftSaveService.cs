using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 저장 한 번이 지금 어디까지 왔는가. <b><see cref="Saved"/> 는 쓰기 완료에만 걸린다</b>
    /// (<c>docs/tech/save-backbone-feasibility-v1.md</c> §1.4-마-2) — 캡처 완료로 걸면
    /// 디스크가 가득 찬 경우에도 화면이 "저장됨" 을 적는다.
    /// </summary>
    public enum LastShiftSaveStatus
    {
        Idle,

        /// <summary>캡처는 끝났고 직렬화·쓰기가 워커에서 돌고 있다. 시뮬은 그동안 계속 돈다(조항 S-10).</summary>
        Writing,

        Saved,
        Failed
    }

    /// <summary>
    /// 저장 버튼과 디스크 사이. <b>게임 상태를 모른다</b> — 담고 푸는 것은
    /// <see cref="LastShiftSaveCapture"/> 이고 여기가 아는 것은 시점·스레드·재진입뿐이다.
    ///
    /// <b>캡처가 <c>LateUpdate</c> 인 것이 요구사항이다</b>(§7.4). 들린 아이템의 포즈가
    /// <see cref="LastShiftNetworkGrabbable"/> 의 <c>LateUpdate</c> 에서 홀더 소켓에 붙으므로,
    /// <c>Update</c> 에서 캡처하면 아이템이 홀더보다 한 프레임 뒤인 조합이 파일에 남는다.
    /// 실행 순서를 <see cref="DefaultExecutionOrderValue"/> 로 고정해 그 뒤에 선다.
    ///
    /// <b>저장이 판을 안 멈춘다</b>(조항 S-10). 캡처는 메인 스레드 동기 값 복사이고
    /// (§1.4-나 tearing 불가), 그 결과가 전부 값 타입이라 직렬화·쓰기를 워커로 넘겨도
    /// 이후의 플레이가 이미 뜬 스냅샷을 건드릴 방법이 없다(§1.4-다).
    /// </summary>
    [DefaultExecutionOrder(DefaultExecutionOrderValue)]
    public sealed class LastShiftSaveService : MonoBehaviour
    {
        /// <summary>
        /// 프로젝트에서 가장 늦다(지금 최대가 <c>300</c> 이다). 캡처는 그 프레임의 포즈가 전부
        /// 확정된 뒤여야 하므로 뒤에 서는 것 자체가 규약이고, 앞에 서면 §7.4 가 막으려는
        /// 한 프레임 어긋난 조합이 그대로 파일에 들어간다.
        /// </summary>
        public const int DefaultExecutionOrderValue = 500;

        [SerializeField] private LastShiftSandboxController sandbox;

        /// <summary>구간 밖(기항)에서 저장할 때 <c>false</c> 로 둔다 — 그러면 캠페인만 담긴다(§4.4).</summary>
        [SerializeField] private bool includeSegment = true;

        private string filePath;
        private bool saveRequested;
        private Task<string> writing;

        /// <summary>지금 상태. 화면이 "저장됨" 을 적는 근거는 이 값 하나다.</summary>
        public LastShiftSaveStatus Status { get; private set; } = LastShiftSaveStatus.Idle;

        /// <summary>마지막 실패 사유. 성공했으면 비어 있다.</summary>
        public string LastError { get; private set; } = string.Empty;

        /// <summary>완료한 저장 수. 재진입 가드가 실제로 합쳤는지를 테스트가 이 값으로 본다.</summary>
        public int CompletedSaveCount { get; private set; }

        /// <summary>쓰기가 돌고 있는가. <b>이 플래그가 재진입 가드다</b>(§1.4-마-1).</summary>
        public bool IsWriting => writing != null;

        /// <summary>저장 요청이 접수돼 아직 캡처를 기다리는가.</summary>
        public bool HasPendingRequest => saveRequested;

        public string FilePath => filePath;

        private void Awake()
        {
            if (sandbox == null) sandbox = GetComponent<LastShiftSandboxController>();
            // persistentDataPath 는 메인 스레드에서만 읽을 수 있다. 워커에 넘길 것은 이미
            // 확정된 문자열이어야 하므로 여기서 한 번 접는다.
            if (string.IsNullOrEmpty(filePath))
                filePath = Path.Combine(Application.persistentDataPath, LastShiftSaveFormat.DefaultFileName);
        }

        /// <summary>테스트와 슬롯 분리가 쓰는 경로 주입. 부르지 않으면 <see cref="Awake"/> 의 기본 경로다.</summary>
        public void Configure(LastShiftSandboxController controller, string path, bool segment = true)
        {
            sandbox = controller;
            includeSegment = segment;
            if (!string.IsNullOrEmpty(path)) filePath = path;
        }

        /// <summary>
        /// 저장을 요청한다. <b>여기서 캡처하지 않는다</b> — 캡처 시점은 <see cref="LateUpdate"/> 이고
        /// (§7.4), 입력이 언제 들어오든 파일에 담기는 것은 그 프레임의 확정된 포즈다.
        ///
        /// <b>쓰기 중 요청은 버리지 않고 합친다.</b> 플래그가 하나뿐이므로 쓰기가 도는 동안 몇 번을
        /// 눌러도 요청 하나로 접히고, 그 하나는 쓰기가 끝난 다음 프레임에 새로 캡처해서 나간다 —
        /// 마지막 요청만 남기기(§1.4-마-1)가 이 모양이다. 요청을 그냥 버리면 누른 사람은
        /// 눌렀는데 아무 일도 안 일어난 판을 본다.
        /// </summary>
        public void RequestSave()
        {
            saveRequested = true;
        }

        private void LateUpdate()
        {
            PumpWrite();
            if (!saveRequested || writing != null) return;

            saveRequested = false;
            BeginSave();
        }

        private void BeginSave()
        {
            string json;
            try
            {
                // 메인 스레드 몫은 여기까지다 — Transform·Rigidbody 읽기가 캡처 안에 있고(§1.4-라),
                // 그 뒤로는 전부 값이라 스레드 안전이다.
                json = LastShiftSaveFormat.Write(LastShiftSaveCapture.Capture(sandbox, includeSegment));
            }
            catch (Exception error)
            {
                Fail("capture:" + error.Message);
                return;
            }

            Status = LastShiftSaveStatus.Writing;
            LastError = string.Empty;
            var path = filePath;
            writing = Task.Run(() => WriteFile(path, json));
        }

        /// <summary>
        /// 워커 몫 — 직렬화된 문자열을 디스크에 얹는다. <b>임시 파일에 쓰고 갈아 끼운다</b>:
        /// 쓰는 중에 프로세스가 죽어도 기존 세이브가 반쪽으로 남지 않는다.
        /// </summary>
        /// <returns>실패 사유. 성공이면 <c>null</c> 이다 — 예외를 워커 밖으로 안 던진다.</returns>
        private static string WriteFile(string path, string json)
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                var temporary = path + ".tmp";
                File.WriteAllText(temporary, json);
                if (File.Exists(path)) File.Delete(path);
                File.Move(temporary, path);
                return null;
            }
            catch (Exception error)
            {
                return error.GetType().Name + ":" + error.Message;
            }
        }

        /// <summary>
        /// 워커가 끝났는지 본다. <b>완료 판정이 메인 스레드에 있어야 한다</b> — 상태는 화면이
        /// 읽는 값이고, 워커에서 직접 쓰면 그 프레임의 화면이 무엇을 볼지가 정해지지 않는다.
        /// </summary>
        private void PumpWrite()
        {
            if (writing == null || !writing.IsCompleted) return;

            var task = writing;
            writing = null;

            if (task.IsFaulted)
            {
                Fail("write:" + (task.Exception?.GetBaseException().Message ?? "unknown"));
                return;
            }
            if (!string.IsNullOrEmpty(task.Result))
            {
                Fail(task.Result);
                return;
            }

            Status = LastShiftSaveStatus.Saved;
            LastError = string.Empty;
            CompletedSaveCount++;
            Debug.Log($"[LAST_SHIFT_SAVE] result=PASS path={filePath} saves={CompletedSaveCount}");
        }

        private void Fail(string reason)
        {
            Status = LastShiftSaveStatus.Failed;
            LastError = reason;
            Debug.LogError($"[LAST_SHIFT_SAVE] result=FAIL reason={reason} path={filePath}");
        }

        /// <summary>
        /// 파일을 읽어 되세운다. <b>동기다</b> — 이어하기는 로딩 화면 뒤이고 예산이 <c>10</c>초라
        /// (§3.3), 저장과 달리 체감 정지 <c>0</c> 요구가 없다.
        /// </summary>
        public LastShiftSaveRestoreReport LoadAndRestore(
            Transform moduleYard = null, LastShiftModulePalette palette = null)
        {
            string json;
            try
            {
                json = File.Exists(filePath) ? File.ReadAllText(filePath) : null;
            }
            catch (Exception error)
            {
                Fail("read:" + error.Message);
                return new LastShiftSaveRestoreReport(LastShiftSaveLoadOutcome.Failed, false, false, 0, 0, 0, 0);
            }

            var load = LastShiftSaveFormat.Read(json);
            var report = LastShiftSaveCapture.Restore(load, sandbox, moduleYard, palette);
            Debug.Log(
                $"[LAST_SHIFT_SAVE_RESTORE] outcome={report.Outcome} segment={report.SegmentRestored} " +
                $"modules={report.ModulesBuilt} reassemble={report.ReassembleMilliseconds:F2}ms " +
                $"inject={report.InjectionMilliseconds:F4}ms pose={report.PoseMilliseconds:F3}ms " +
                $"total={report.TotalMilliseconds:F2}ms");
            return report;
        }
    }
}
