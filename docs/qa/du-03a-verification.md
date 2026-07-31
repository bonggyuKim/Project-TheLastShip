# DU-03A StrokeSession REV1 QA 검증

상태: REV1 standalone runtime 검증 완료 (2026-07-31)

## 로그

| Tag | 의미 | PASS 기준 |
|---|---|---|
| `[DU03A_STATE]` | 상태 전이 | release는 Pending 또는 Cancelled, Confirm만 Committed |
| `[DU03A_CANDIDATE]` | candidate transaction | invalid면 appended=False와 point/length/ink/reserve 불변 |
| `[DU03A_LATE_UPDATE]` | 실제 callback 처리 | `samplePhase=LATE_UPDATE`, frame당 candidate 0 또는 1, release frame `CANDIDATE>RELEASE` |
| `[DU03A_CAPSULE_SKIP]` | degenerate segment | length `<=1e-6`, collider 미생성 |
| `[DU03A_COMMIT]` | Confirm capsule transaction | segment/collider/skip/gap 및 immutable charged length 기록 |
| `[DU03A_RESET]` | R/lane reset | Idle, live/pending/collider 0, ink 5 |
| `[DU03A_RUNTIME]` | 독립 runtime scenario | terminal ledger 합 5.00, 각 행 `result=PASS` |
| `[DU03A_RUNTIME_PROBE_COMPLETE]` | raw 저장 | scenarios=14, result=PASS |
| `[DU03A_VERIFY]` | raw 재집계 | scenarios=14, result=PASS |

## Raw schema와 독립 검사

`DU03A_Runtime_Raw.csv`는 50열, 14개 scenario다. 주요 필드군:

- candidate atomicity: `candidate_points_*`, `candidate_length_*`, `candidate_available_*`, `candidate_drawing_*`, `candidate_pending_*`
- terminal ledger: `final_available`, `final_drawing_reserved`, `final_pending_reserved`, `final_committed_charged`, `final_ledger_total`
- capsule: `pending_colliders`, `segment_count`, `collider_count`, `degenerate_skipped`, direction/radius/height/center/trigger/scale/alignment/gap
- actual callback: `render_frame`, `late_update_sequence`, `sample_phase`, `candidate_count_this_frame`, `event_order`

Aggregator는 모든 행에서 다음을 직접 재계산한다.

```text
final_available + final_drawing_reserved + final_pending_reserved + final_committed_charged = 5.00
```

필수 scenario:

1. `short_cancel`
2. `pending_confirm`
3. `pending_cancel`
4. `reach_atomic`
5. `ink_atomic`
6. `r_reset_pending`
7. `invalid_release_under_min`
8. `invalid_release_over_min`
9. `drawing_cancel`
10. `pending_new_draw_reject`
11. `out_of_state_confirm`
12. `confirm_release_same_frame`
13. `mode_parity_aim`
14. `mode_parity_trajectory`

`pending_confirm`은 Pending collider 0, actual LateUpdate release frame `CANDIDATE>RELEASE`, Confirm 후 capsule geometry 골든값과 committed charged length를 함께 검증한다. Aim/Trajectory parity는 동일 projected candidate sequence의 mode 이외 state/ledger/point/charged 결과를 비교한다.

## Capsule golden contract

- simplified non-degenerate point pair당 child `CapsuleCollider` 1개
- `direction=1`, local Y가 segment 방향
- `radius=0.14`
- `height=segmentLength+0.28`
- `center=(0,0,0)`, `isTrigger=false`
- root/child local·world scale `(1,1,1)`
- child world position=segment midpoint
- degenerate `<=1e-6` skip
- shared endpoint gap `0`
- Pending에는 collider/root/Rigidbody `0`; accepted Confirm에서만 생성

## 재현

프로젝트: `D:/Project-DoodleUp`, Unity `6000.4.0f1`. Unity Editor를 닫고 순차 실행한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 compile '' EditMode D:/Project-DoodleUp
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 method DoodleUp.Editor.Du02SceneBuilder.RebuildSoloCourse EditMode D:/Project-DoodleUp
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 build DoodleUp.Editor.Du02PlayerBuild.BuildWindowsProbe EditMode D:/Project-DoodleUp
D:/Project-DoodleUp/Builds/DU02_RuntimeProbe/DoodleUp-DU02-Probe.exe -batchmode -nographics -logFile D:/.adk/logs/unity/du03a-rev1-runtime.log
Copy-Item "$env:USERPROFILE/AppData/LocalLow/DoodleUp/Doodle Up DU-02/DU03A_Runtime_Raw.csv" D:/Project-DoodleUp/DU03A_Runtime_Raw.csv -Force
Copy-Item "$env:USERPROFILE/AppData/LocalLow/DoodleUp/Doodle Up DU-02/DU02_Runtime_Raw.csv" D:/Project-DoodleUp/DU02_Runtime_Raw.csv -Force
$env:DU03A_RUNTIME_RAW_PATH = 'D:/Project-DoodleUp/DU03A_Runtime_Raw.csv'
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 method DoodleUp.Editor.Du03AVerification.RunFromRaw EditMode D:/Project-DoodleUp
$env:DU02_RUNTIME_RAW_PATH = 'D:/Project-DoodleUp/DU02_Runtime_Raw.csv'
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 method DoodleUp.Editor.Du02Verification.RunFromRaw EditMode D:/Project-DoodleUp
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 test '' EditMode D:/Project-DoodleUp
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 test '' PlayMode D:/Project-DoodleUp
```

## 현재 검증 결과

- compile: C# error 0
- Windows standalone build: PASS
- standalone: DU-03A 14/14, DU-02 sampling/reset/task-state PASS
- raw aggregator: DU-03A PASS, DU-02 PASS
- EditMode: 26/26 PASS
- PlayMode: 2/2 PASS
- runtime exception, `[DU02_DEPTH_DRIFT]`, `[DU02_PROVENANCE_INVALID]`, `result=FAIL`: 0

## 범위 제한

DU-03A는 공통 backend, deterministic intent source evidence, ghost와 Confirm capsule chain까지만 포함한다. DU-03B/C 완성 adapter, delete, traverse/별도 물리 gameplay 확장은 포함하지 않는다.
