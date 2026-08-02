# DU-02 부트스트랩·솔로 코스 구조

상태: REV2 구현·standalone runtime 검증 완료 (2026-07-31)
정본: `docs/input-comparison-spec-v1.md`, `docs/prototype-execution-plan.md`

## 책임 경계

- `DoodleUp.Core`: 승인 profile, 좌표계, course 정의, `IDu02TaskState`, reset snapshot 자료형
- `DoodleUp.Input`: Unity Input System을 읽어 프레임 입력 snapshot만 제공. gameplay state를 직접 변경하지 않음
- `DoodleUp.Stroke`: DU-02 candidate sampling seam과 DU-03A 공통 `StrokeSession` backend 제공. 상세는 `docs/architecture/du-03a-stroke-session.md`
- `DoodleUp.Physics`: Rigidbody player motor, depth constraint, grounded 판정
- `DoodleUp.Runtime`: 코스·카메라·HandMarker·task state·reset orchestration·runtime evidence. `Du02CameraRig`의 1인칭 visual camera와 yaw는 `PRETEST_FIRST_PERSON_V2` 에디터 비증거 플레이에서만 활성화한다.
- `DoodleUp.Editor`: 재현 scene 생성, Windows probe build, raw evidence 집계 보고서

`Runtime`만 하위 경계를 조립한다. DU-02 단계에서는 `StrokeSession`을 구현하지 않았고, DU-03A에서 공통 backend, ghost preview, Confirm capsule chain과 deterministic LateUpdate probe source를 추가했다. Aim/Trajectory 완성 adapter와 traverse gameplay는 후속 단계 범위다.

## 데이터 흐름

1. `Du02InputReader.Update`가 A/D, Space, R, 1/2/3을 `Du02InputFrame`으로 latch한다.
2. `Du02RuntimeController.Update`가 lane/R 경로를 호출하고 `Du02TaskState.Tick`을 진행한다. countdown 중 이동 입력은 0으로 잠긴다. Draw 구현 전이므로 Draw lock은 `IDu02TaskState.InputLocked` 계약으로 노출한다.
3. `Du02PlayerMotor.FixedUpdate`가 offset capsule bounds 중심에서 아래로 ground probe한 뒤 world Y gravity와 world X 이동을 적용하고 world Z를 활성 lane spawn depth로 고정한다. Player capsule의 runtime frictionless material은 접촉 마찰이 매 tick 설정하는 목표 X 속도를 상쇄하지 않게 한다.
4. `Du02CandidateSamplingSeam.LateUpdate`가 HandMarker와 drawing plane을 render frame당 1회 관찰한다. `Du02RuntimeFrameProbe.LateUpdate`가 같은 frame phase에서 독립 frame 수를 기록한다.
5. `Du02ResetCoordinator.ResetToLane`이 fixed timestep, player transform/rotation/linear·angular velocity, HandMarker pose/scale, camera/FOV와 pretest visual yawOffset=0, task state, timer/countdown, goal/stroke/ink, sampling sequence를 한 호출에서 복원한다.
6. `Du02RuntimeProbeRunner`는 standalone batchmode에서 30/60/144fps를 각각 실제 10초 관찰하고 T1/T2/T3의 R/lane-select reset을 교란 후 비교한다. Reset probe의 `before`는 non-identity rotation, non-zero angular velocity, probe-only `ProbePerturbed` phase를 가져야 하며 `after`는 identity/zero/`Idle`로 복원되어야 한다.
7. `Du02Verification.RunFromRaw`는 27열 `DU02_Runtime_Raw.csv`만 파싱해 최종 보고서를 집계한다. 각 reset 행의 baseline/before/after hash와 rotation/angular velocity/phase 복원을 독립 확인하며 expectation helper는 runtime actual 산출에 사용하지 않는다.

## Task-state와 success seam

Reset 직후 canonical state:

- `phase=Idle`
- `countdown=3.0 s`
- `timer=0`
- `inputLocked=true`
- `goal=false`
- `strokeCount=0`
- `ink=5`

Countdown이 0이 되는 순간 `[DU02_TASK_GO]`가 발생하고 같은 tick의 초과 시간부터 timer가 증가한다. 성공 판정은 goal 내부에서 연속 1초 hold와 committed-stroke-contact evidence를 요구한다. T3는 start/goal 양 contact band evidence가 추가로 필요하다. 이 API는 실제 StrokeSession이 들어오기 전 검증 가능한 placeholder seam이며 DU-03A에서 실제 commit 소유자와 연결한다.

## Scene·course

Scene: `Assets/Scenes/DU02_SoloCourse.unity`

- T1: 동일 높이, edge gap `0.70 u`
- T2: 목표 ledge center offset `(+0.65,+0.55) u`
- T3: 동일 높이, edge gap `0.95 u`, 양 contact band 폭 `0.12 u`
- Player root spawn Y는 `startCenter.y + LedgeSize.y/2 = 0.10u`다. Capsule `center.y=0.50`, `height=1.00`이므로 collider bottom과 start ledge top이 정확히 일치한다.
- HandMarker canonical local transform: position `(0,+0.980,0)`, rotation identity, scale `(1,1,1)`
- Layers: Player `8`, Course `9`, Goal `10`; camera tag `MainCamera`

## Pretest 1인칭 visual camera

- 에디터 비배치 Play의 `PRETEST_FIRST_PERSON_V2`에서만 visual camera를 player root 기준 `(0,+1.20,-1.25)`에 두고 pitch `-10°`를 적용한다. HandMarker `(0,+0.980,0)`과 조합하면 center ray가 hand 원점에서 gameplay depth `0` plane과 만난다.
- Left/Right hold는 `60°/s`, spawn 기준 `-30°~+30°`, 무가속·무관성으로 1인칭 visual yaw를 바꾼다. 입력은 `StrokeSession.Idle`에서만 허용되며 Drawing/Pending 동안 freeze되고 R/lane reset에서 `yawOffset=0`으로 복원된다.
- Player root, HandMarker fixed-child pose, world-X locomotion, depth plane과 gameplay `n0=(0,0,1)`은 변하지 않는다. Aim은 1인칭 visual camera ray를 gameplay `n0` plane에 교차한다.
- `(0,+1.20,-1.25)` 어깨너머 시점에서 보이는 파란 BodyVisual과 단일 authoritative HandMarker의 주황 HandVisual, Drawing 중 HandMarker 중심 `1.25u` reach circle을 표시한다. 모든 visual primitive는 물리 성분이 없고 visual hand proxy는 두지 않는다.
- `[DU02_PROVENANCE]`과 최초 yaw event는 `profile_id=PRETEST_FIRST_PERSON_V2`, `camera_visual_yaw`, `gameplay_n0`을 남긴다. pretest가 활성인 실행은 `[DU02_PROVENANCE_INVALID] reason=TECH_INVALID/CAMERA_YAW_INPUT_ENABLED`로 Gate A 본 측정에서 제외한다.
- standalone/배치 실행은 1인칭과 yaw 입력을 비활성화하고 기존 `DU02_PROFILE_V1` 고정 카메라를 유지한다.

## 중요 상태 전이

- Scene 시작 → T1 `SCENE_START`, generation 1
- 1/2/3 또는 probe lane API → `LANE_SELECT` → atomic reset
- R 또는 probe reset API → `R_KEY` → 현재 lane atomic reset
- countdown 3초 종료 → GO, movement/Draw lock 해제, timer 시작
- goal 1초 + stroke evidence 충족 → success
- depth error `>0.001 u` → `[DU02_DEPTH_DRIFT]` error

## 검증 산출물

- `DU02_Runtime_Raw.csv`: 3 sampling rows + 6 reset rows
- `DU02_Runtime_Summary.txt`: standalone runner의 human-readable raw mirror
- `DU02_Verification_Report.txt`: raw CSV 재집계 결과
- `[DU02_PROVENANCE]`: engine/Input System/device/fixedDeltaTime/build ID/hash/tag/layer
- `[DU02_COURSE]`: lane 3건
- `[DU02_RESET]`: canonical 상태와 reset generation
- `[DU02_RUNTIME_SAMPLE]`: 실제 observed frame/sample/elapsed
- `[DU02_RUNTIME_RESET]`: baseline/before/after stable hash, rotation/angular velocity/phase와 field restoration flags
- `[DU02_RUNTIME_TASK_STATE]`: countdown/goal/contact 조건 검증
- `[DU02_DEPTH_DRIFT]`: clean probe에서는 0건이어야 함
