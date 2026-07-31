# DU-03A StrokeSession QA 검증

상태: standalone runtime 검증 기준 (2026-07-31)

## 로그

| Tag | 의미 | PASS 기준 |
|---|---|---|
| `[DU03A_STATE]` | 상태 전이 | release는 Pending 또는 Cancelled, Confirm만 Committed |
| `[DU03A_CANDIDATE]` | candidate transaction | invalid면 appended=False와 length/ink 불변, spacing/dedupe는 valid+not-appended |
| `[DU03A_COMMIT]` | immutable StrokeData 생성 | chargedLength는 accepted resampled length, `colliderCreated=False seamOnly=True` |
| `[DU03A_RESET]` | R/lane reset | Idle, live=0, pending=0, ink=5 |
| `[DU03A_RUNTIME]` | 독립 runtime scenario | 각 행 `result=PASS` |
| `[DU03A_RUNTIME_PROBE_COMPLETE]` | raw 저장 | scenarios=6, result=PASS |
| `[DU03A_VERIFY]` | raw 재집계 | scenarios=6, result=PASS |

## Raw schema

`DU03A_Runtime_Raw.csv`는 20열이다.

```text
scenario,state_before,state_after,candidate_valid,accepted_appended,reason,appended_count,required_ink,length_before,length_after,ink_before,ink_after,pending_count,committed_count,terminal_state,charged_length,simplified_points,collider_count,atomic_unchanged,result
```

필수 6개 scenario:

1. `short_cancel`: `0.16u` release → Cancelled→Idle, pending/live=0, 환급
2. `pending_confirm`: `0.24u` release → Pending, collider=0, 명시 Confirm 후 committed=1
3. `pending_cancel`: Pending 취소 후 pending/live=0, 환급
4. `reach_atomic`: ReachInvalid, append=0, length/ink/point count 불변
5. `ink_atomic`: requiredInk가 availableInk를 초과하면 prospective points 전체 append=0, reserve 불변
6. `r_reset_pending`: Pending 상태에서 R reset 후 Idle, pending/live=0, ink=5

## 재현

프로젝트: `D:/Project-DoodleUp`, Unity `6000.4.0f1`. Unity Editor를 닫고 순차 실행한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 compile '' EditMode D:/Project-DoodleUp
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 method DoodleUp.Editor.Du02SceneBuilder.RebuildSoloCourse EditMode D:/Project-DoodleUp
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 build DoodleUp.Editor.Du02PlayerBuild.BuildWindowsProbe EditMode D:/Project-DoodleUp
D:/Project-DoodleUp/Builds/DU02_RuntimeProbe/DoodleUp-DU02-Probe.exe -batchmode -nographics -logFile D:/.adk/logs/unity/du03a-runtime.log
Copy-Item "$env:USERPROFILE/AppData/LocalLow/DoodleUp/Doodle Up DU-02/DU03A_Runtime_Raw.csv" D:/Project-DoodleUp/DU03A_Runtime_Raw.csv -Force
$env:DU03A_RUNTIME_RAW_PATH = 'D:/Project-DoodleUp/DU03A_Runtime_Raw.csv'
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 method DoodleUp.Editor.Du03AVerification.RunFromRaw EditMode D:/Project-DoodleUp
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 test '' EditMode D:/Project-DoodleUp
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 test '' PlayMode D:/Project-DoodleUp
```

## DU-02 회귀

같은 standalone run이 `DU02_Runtime_Raw.csv`도 다시 생성한다. `Du02Verification.RunFromRaw`로 30/60/144 sampling, 6 reset, task-state를 재집계한다. runtime log에서 `[DU02_DEPTH_DRIFT]`, `[DU02_PROVENANCE_INVALID]`, exception, `result=FAIL`은 0건이어야 한다.
