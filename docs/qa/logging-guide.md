# DoodleUp DU-02 QA Logging Guide

상태: REV2 standalone runtime 검증 기준 (2026-07-31)

## 로그 형식

고정 tag와 `key=value` payload를 사용한다. 수치와 Vector는 invariant culture로 출력한다.

| Tag | 발생 조건 | PASS 기준 |
|---|---|---|
| `[DU02_PROVENANCE]` | scene 시작 1회 | Unity/Input System, build ID, hash, `MainCamera=True`, layers `8/9/10`, `runtimeConfigurationValid=True` |
| `[DU02_COURSE]` | scene 시작 | 정확히 3건, T1 gap `0.70`, T2 offset, T3 gap `0.95`/band `0.12` |
| `[DU02_TASK_RESET]` | 모든 reset | Idle, countdown `3`, timer `0`, lock true, goal false, stroke 0, ink 5 |
| `[DU02_TASK_GO]` | countdown 종료 | timer `0`, lock false |
| `[DU02_RESET]` | scene/R/lane reset | generation 증가, canonical transform/state, depthError `0` |
| `[DU02_RUNTIME_SAMPLE]` | fps probe 종료 | frame=sample, duplicate=0, missing=0, elapsed≥10초 |
| `[DU02_RUNTIME_RESET]` | 교란 후 R/lane reset | beforeHash≠baselineHash, afterHash=baselineHash, before rotation/angular velocity/phase 비정상, after identity/zero/Idle, restoration flags=True |
| `[DU02_RUNTIME_TASK_STATE]` | success seam probe | T1/T2 contact+hold 성공, T3 단일 band 실패/양 band 성공 |
| `[DU02_RUNTIME_PROBE_COMPLETE]` | raw 파일 저장 완료 | result=PASS |
| `[DU02_DEPTH_DRIFT]` | depth tolerance 초과 | clean probe에서 0건 |
| `[DU02_GROUND]` | 초기 ground 판정 또는 grounded 상태 전이 | offset capsule bounds 중심·probe 거리와 상태 확인; 매 frame 출력 금지 |
| `[DU_SANDBOX_RESET]` | 비증거 sandbox scene 시작 또는 R reset | active profile `PRETEST_DEPTH_LOCOMOTION_V1` 또는 `PRETEST_ARM_DIRECT_V1`, depth 0, ink 5.00, committed collider 0 |
| `[DU_SANDBOX_STROKE_DEPTH]` | sandbox Idle→Drawing 전이 | root/hand depth snapshot과 `n0=(0,0,1)` |
| `[DU_SANDBOX_INVALID]` | Drawing/Pending depth 오차 `>0.001u` | 정상 sandbox 플레이에서 0건; `TECH_INVALID/DRAW_DEPTH_DRIFT` 원인 확인 |

## 재현 명령

프로젝트: `D:/Project-DoodleUp`, Unity `6000.4.0f1`. Unity Editor가 프로젝트를 열고 있으면 먼저 닫는다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 compile '' EditMode D:/Project-DoodleUp
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 method DoodleUp.Editor.Du02SceneBuilder.RebuildSoloCourse EditMode D:/Project-DoodleUp
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 build DoodleUp.Editor.Du02PlayerBuild.BuildWindowsProbe EditMode D:/Project-DoodleUp
D:/Project-DoodleUp/Builds/DU02_RuntimeProbe/DoodleUp-DU02-Probe.exe -batchmode -nographics -logFile D:/.adk/logs/unity/du02-runtime.log
```

Player가 exit 0이면 다음 raw 파일을 프로젝트 루트로 복사한다.

```powershell
Copy-Item "$env:USERPROFILE/AppData/LocalLow/DoodleUp/Doodle Up DU-02/DU02_Runtime_Raw.csv" D:/Project-DoodleUp/DU02_Runtime_Raw.csv -Force
$env:DU02_RUNTIME_RAW_PATH = 'D:/Project-DoodleUp/DU02_Runtime_Raw.csv'
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 method DoodleUp.Editor.Du02Verification.RunFromRaw EditMode D:/Project-DoodleUp
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 test '' EditMode D:/Project-DoodleUp
```

## Raw CSV 독립 재계산

`DU02_Runtime_Raw.csv` header는 다음 27열이다.

```text
record_type,requested_fps,observed_frames,observed_samples,duplicate_frames,missing_frames,elapsed_seconds,reset_generation,task,reset_path,baseline_hash,before_hash,after_hash,before_differs,after_equal,baseline_rotation,before_rotation,after_rotation,baseline_angular_velocity,before_angular_velocity,after_angular_velocity,baseline_phase,before_phase,after_phase,rotation_restored,angular_velocity_restored,phase_restored
```

QA는 최종 report와 별개로 다음을 직접 확인한다.

1. `sampling` 행이 요청 fps `30/60/144`마다 정확히 1개인지 확인한다.
2. 각 행에서 `observed_frames > 0`, `observed_frames == observed_samples`, duplicate/missing `0`, elapsed `>=10.0`인지 확인한다.
3. fps target은 속도 요청값이지 정확한 frame total 보장이 아니다. 예: 144fps 10초 실행에서 scheduler에 따라 1439 frame이어도 frame/sample equality와 elapsed 조건을 만족하면 PASS다.
4. `reset` 행은 T1/T2/T3 × `R_KEY`/`LANE_SELECT` 정확히 6개인지 확인한다.
5. 각 reset 행에서 `before_differs=True`, baseline/before hash 불일치, `after_equal=True`, baseline/after hash 일치를 확인한다.
6. 각 reset 행의 `before`가 non-identity rotation, non-zero angular velocity, `ProbePerturbed` phase인지 확인한다.
7. 각 reset 행의 `baseline`과 `after`가 identity rotation, zero angular velocity, `Idle` phase이며 세 restoration flag가 모두 `True`인지 확인한다.
8. runtime log에서 provenance 1건, course 3건, scene-start reset, R/lane reset, task-state 4건, completion 1건을 확인한다.
9. runtime log에서 `[DU02_DEPTH_DRIFT]`, `[DU02_PROVENANCE_INVALID]`, exception, `result=FAIL`이 0건인지 확인한다.

## Success seam 회귀

- T1/T2: committed stroke contact + goal inside + 1초 hold → `goalReached=True`
- T3 음성: start band만 + goal inside + 1초 tick → `goalReached=False`
- T3 양성: start/goal band + goal inside + 1초 hold → `goalReached=True`
- 모든 reset 직후 goal/stroke/ink/timer/countdown/phase는 canonical state로 복원돼야 한다.

## 샌드박스 수동 확인

자동 검증은 compile, adapter mapping, backend reach atomicity, depth-lock 회귀까지 담당한다. 실제 조작감은 사용자가 `DU_Sandbox.unity`에서 확인한다.

1. `DU_Sandbox.unity` Play 시작 즉시 `ArmDirect`이며 camera root-local `(0,+1.20,0)`, hand `(0,+1.20,+1.25)`가 적용된다.
2. Idle에서 mouse를 움직이면 camera yaw/pitch가 회전하고 pitch `±80°`에서 더 이상 뒤집히지 않는다. cursor는 Game View focus 동안 lock된다.
3. WASD는 camera yaw 기준으로 이동하며 위/아래를 보더라도 이동 속도와 지면 방향에는 pitch가 섞이지 않는다.
4. LMB press/hold 동안 camera orientation은 고정되고 mouse delta는 손의 frozen right/up 방향에만 적용되어 stroke preview가 생긴다.
5. reach 경계 밖에서는 red invalid preview가 보이고 손은 마지막 유효 지점에 멈추며, 안으로 돌아오면 즉시 재개한다.
6. LMB release 후 stroke가 commit되고 손이 neutral로 복귀하며 다음 mouse 움직임부터 camera look이 재개된다. RMB/Esc cancel도 같은 소유권 복구를 한다.
7. Drawing 중 W/S는 잠기지만 A/D와 Space는 유지되고, release 후 W로 committed stroke depth에 접근할 수 있다.
8. R reset은 depth `0`, ink `5.00`, committed collider `0`, active mode canonical hand pose와 camera yaw/pitch `0°`를 복원한다.

## 범위 제한

DU-02 증거 범위는 기존 sampling/reset/task-state다. LAST SHIFT 온보딩 튜토리얼의 `[LAST_SHIFT_TUTORIAL]`·`[LAST_SHIFT_PATROL]` 판정 로그와 그 해석식은 `docs/qa/tutorial-onboarding-judgment-log-spec.md`가 정본이다(해석식이 코드에 없으므로 그 문서가 유일한 근거다). DU-03A 공통 StrokeSession backend와 Confirm capsule chain 검증은 `docs/qa/du-03a-verification.md`, DU-03B/C Aim/Trajectory adapter와 M+K edge 검증은 `docs/qa/du-03bc-verification.md`를 따른다. 실제 traverse gameplay 판정은 별도 후속 범위다.
