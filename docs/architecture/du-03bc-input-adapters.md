# DU-03B/C M+K input adapter architecture

상태: DU-03BC-DRAW-CAMERA-1 Drawing camera look + spatial stroke 반영 (2026-08-02)
정본: `docs/input-comparison-spec-v1.md` §3.3, §4, `docs/prototype-execution-plan.md:81-89`

2026-08-01 사용자 플레이 결정으로 기존 `E` Confirm UX를 폐기했다. LMB release가 같은 `LateUpdate`에서 candidate 처리 후 geometry transaction까지 완료하며, 이 문서의 이전 Pending/Confirm 설명보다 이 결정이 우선한다.

## 책임 경계

- `DoodleUp.Input.Du03BCInputEdgeLatch`: Input System action을 소유하고 Draw press/held/release, Cancel, Reset edge를 수집한다. release가 Commit edge이며 실제 `E` action은 없다. stroke edge는 `ConsumeStrokeEdges()`에서 한 번만 소비되며 R 전체 reset과 Q ink reset은 각각 별도 소비된다.
- `DoodleUp.Runtime.Du03BCAimInputAdapter`: Draw press에서 HandMarker 원점과 spawn gameplay `n0` plane을 snapshot하고 현재 visual camera mouse screen ray와의 교차점을 candidate로 만든다. `PRETEST_FIRST_PERSON_V2`의 1인칭 위치와 visual yaw는 ray만 바꾸며 gameplay plane은 바꾸지 않는다.
- `DoodleUp.Runtime.Du03BCTrajectoryInputAdapter`: driver `LateUpdate`가 호출한 tick의 `HandMarker.position`을 그대로 candidate로 만든다. cursor, remote point, 독립 steering, guide, prediction은 없다.
- `DoodleUp.Runtime.Du03BCAdapterRouter`: `Du03AStrokeDriver`에 보이는 유일한 `IDu03ADrawIntentSource`다. 샌드박스에서는 ArmDirect로 시작하고 `Tab`으로 ArmDirect→Aim→Trajectory→ArmDirect를 순환한다. 전환 시 진행 중 stroke를 reset하고 driver mode와 active adapter를 함께 바꾼다. Gate A 씬은 ArmDirect adapter를 구성하지 않으며 기본 Aim과 기존 Aim↔Trajectory 순환을 유지한다.
- `DoodleUp.Runtime.Du03BCArmDirectInputAdapter`: 비증거 `PRETEST_ARM_DIRECT_V1` 전용이며 `Spatial` stroke mode를 사용한다. Drawing 중 camera yaw/pitch로 회전하는 현재 `ArmPitchAnchor` neutral tip과 camera right/up을 매 frame 사용하고, 선택적 probe offset만 `0.0025u/pixel`로 누적한다. candidate는 고정 `n0` plane에 projection하지 않아 yaw/pitch 변화가 연속 3D world 궤적이 된다. ArmDirect에는 legacy `ReachRadius` 거부를 적용하지 않으며 authoritative HandMarker가 candidate를 계속 따라간다.
- `DoodleUp.Runtime.Du02CameraRig`: `LateUpdate`마다 Player의 현재 XYZ를 follow pivot으로 갱신하고 active profile의 local pose를 재적용한다. Aim/Trajectory는 `(0,+1.20,-1.25)`와 pitch `-10°`를 유지한다. ArmDirect는 `(0,+1.20,0)`, mouse sensitivity `0.12°/pixel`, pitch clamp `±80°`의 FPS look을 사용하며 reset/respawn 후에도 같은 target과 active orientation을 유지한다. ArmDirect mouse look은 Idle과 Drawing에서 허용되고, Aim/Trajectory의 기존 Drawing freeze는 유지된다. Player Rigidbody/root는 gameplay `n0`와 물리 축 보존을 위해 회전하지 않는다. `BodyYawAnchor`가 camera yaw를 따르고 그 자식 `ArmPitchAnchor`가 어깨 `(0.34,+0.92,+0.18)`에서 camera pitch를 따른다. `ArmVisualRoot`와 authoritative `HandMarker`는 `ArmPitchAnchor`의 자식이라 상완·전완·손·neutral pose·reach origin이 같은 yaw+pitch 회전을 공유하고, `BodyVisual`은 yaw만 따른다.
- `DoodleUp.Runtime.Du03BCResetInputBridge`: R edge를 DU-02 canonical reset transaction으로 연결한다.
- `DoodleUp.Runtime.DuSandboxController`: 비증거 샌드박스의 W/S depth locomotion과 sandbox reset을 소유한다. Aim/Trajectory는 Drawing/Pending에서 기존 depth lock을 유지한다. ArmDirect는 Drawing 중에도 camera-relative W/A/S/D와 Space를 Idle과 동일하게 허용하고 Rigidbody Z 고정 및 depth-drift 검사를 적용하지 않는다. Gate A controller와 evidence runner에는 연결하지 않는다.
- `DoodleUp.Runtime.Du03BCPlayabilityVisuals`: `PRETEST_FIRST_PERSON_V2` 1인칭 시점에서 물리 없는 파란 BodyVisual과 상완·전완·손을 표시한다. Aim/Trajectory Drawing에는 기존 `1.25u` reach circle을 유지하지만, 범위 제한이 없는 ArmDirect `Spatial`에서는 원을 숨긴다. 상완·전완과 단일 authoritative HandMarker는 같은 `ArmPitchAnchor` 아래 있어 yaw+pitch를 함께 따르며 visual proxy는 없다.
- `DoodleUp.Runtime.Du03BCRuntimeProbeRunner`: 실제 router→driver `LateUpdate` 경로와 mapping evidence, release order, parity, reset을 raw CSV로 기록한다.
- `Du03AStrokeSession`의 projection/reach/resampling/dedupe/ledger와 `Du03AStrokeGeometry`의 capsule transaction 책임은 유지한다. `Du03AStrokeDriver`는 LMB release에서 backend의 순간적인 Pending을 거쳐 geometry를 준비·검증하고 즉시 commit한다.

각 concrete `MonoBehaviour`는 Unity player scene 직렬화를 위해 파일명과 일치하는 독립 `.cs` 파일을 가진다. adapter 공통 계약과 base/evidence 자료형만 `Du03BCInputAdapters.cs`에 둔다.

## Input edge와 소비 순서

```text
LMB press   → Draw 시작
LMB hold    → candidate 누적
LMB release → release-frame candidate 처리 후 즉시 Commit
RMB | Esc   → Drawing 취소
R           → canonical trial reset (player/camera/ink/strokes)
Q           → ink/stroke reset only (player/camera pose 유지)
```

- 실제 Input System callback은 edge를 latch하고 monotonically increasing event sequence와 execution path를 보존한다.
- `Du03AStrokeDriver.LateUpdate()`가 router의 `ReadIntent()`를 한 번 호출하여 stroke edge와 candidate를 소비한다.
- adapter에 `Update`/`LateUpdate` loop는 없다.
- Drawing 중 candidate는 frame당 최대 1개다.
- release frame은 candidate를 포함하고 driver event order는 `CANDIDATE>RELEASE>AUTO_COMMIT`이다. accepted length가 `0.20u` 미만이면 기존대로 cancel되어 `AUTO_COMMIT`이 없다.
- focus loss 또는 mouse/keyboard disconnect는 stale edge를 clear하고 `[DU03BC_INPUT_CLEAR]`를 남긴다.

## Aim mapping

Draw press 시 다음 gameplay plane을 고정한다.

```csharp
planeOrigin = handMarker.position;
planeNormal = gameplayN0; // current course: Vector3.forward
```

Gate A 고정 카메라에서는 `gameplayN0`가 spawn camera yaw-normal과 같다. `PRETEST_FIRST_PERSON_V2`에서는 visual camera만 player eye 위치로 이동하고 yaw하므로 screen position은 현재 1인칭 visual camera의 `Camera.ScreenPointToRay`로 변환하되 ray는 고정 `gameplayN0` snapshot plane과 교차한다. Drawing 중에는 visual camera와 plane snapshot을 모두 freeze한다. 독립 교차 계산과의 오차 계약은 `<=1e-5u`다.

- parallel ray: raw candidate 없음, `NO_PLANE_INTERSECTION`
- non-finite ray/screen/intersection: raw candidate 없음, `NON_FINITE`
- immutable DU-03A intent에는 invalid enum이 없으므로 source evidence에 이유를 남기고 non-finite candidate를 backend에 전달해 atomic reject시킨다.

## Trajectory mapping

`ReadIntent()` 시점의 `HandMarker.position`이 raw/expected candidate 양쪽이다.

```text
mappingSource=HAND_MARKER
mappingError=0
mouseInfluence=False
remotePoint=False
```

HandMarker는 player fixed child이며 canonical local pose는 `(0,+0.980,0)`, identity rotation, unit scale다. reset은 DU-02 player/camera/task reset, DU-03A session reset, adapter plane/edge reset을 같은 transaction으로 복원한다.

## 비증거 depth locomotion 샌드박스

`Assets/Scenes/DU_Sandbox.unity`는 `DU02_SoloCourse.unity`와 분리된 조작감 확인 씬이며 profile은 `PRETEST_DEPTH_LOCOMOTION_V1`이다. 40×40u floor, Player, 카메라/손/stroke runtime만 포함하고 Goal·lane·probe/evidence runner는 포함하지 않는다.

- Idle의 WASD는 현재 camera yaw 기준 right/forward를 world XZ로 변환하며 camera pitch는 이동 벡터에 사용하지 않는다. 지상 `2.50u/s`, 공중 `2.00u/s`다.
- A/D와 W/S 벡터는 magnitude `1`로 clamp하여 대각선 속도가 증가하지 않는다.
- Aim/Trajectory는 Draw press로 Idle→Drawing이 되면 root depth와 hand depth를 snapshot한다. Drawing/Pending에서는 W/S를 0으로 잠그고 해당 depth에서 root/hand 오차가 `0.001u`를 넘으면 `[DU_SANDBOX_INVALID] reason=TECH_INVALID/DRAW_DEPTH_DRIFT`를 한 번 기록한다. ArmDirect는 이 snapshot·lock·검사에서 제외한다.
- release auto-commit 또는 Cancel로 Idle에 복귀하면 W/S가 다시 허용된다. R은 root depth `0`, HandMarker canonical local pose, ink `5.00`, committed geometry를 함께 복원한다.
- Gate A의 fixed-depth `Du02RuntimeController`, 72행, qualified 판정, mode 선택에는 샌드박스 결과를 유입하지 않는다.

## Arm Direct 샌드박스 모드

`PRETEST_ARM_DIRECT_V1`은 `DU_Sandbox.unity`의 제3 입력 방식이다.

- camera root-local `(0,+1.20,0)`, 시작 yaw `n0`, 시작 pitch `0°`, vertical FOV `60`이며 Play 시작 route는 ArmDirect다.
- Idle mouse delta는 camera yaw/pitch를 `0.12°/pixel`로 회전시키고 pitch를 `±80°`로 clamp한다. Play focus를 얻으면 cursor를 lock/hide하며 focus loss 또는 component disable 시 unlock/show한다.
- neutral HandMarker의 Player/body 기준 pose는 `(0,+1.20,+1.25)`다. 실제 hierarchy에서는 어깨 위치의 `ArmPitchAnchor` 기준 offset `(-0.34,+0.28,+1.07)`을 사용하며, `BodyYawAnchor` yaw와 `ArmPitchAnchor` pitch를 함께 따라간다. draw press에서 현재 world hand origin을 reach origin으로 snapshot한다.
- Drawing 중 같은 mouse delta가 camera yaw/pitch를 갱신하고, adapter는 갱신된 `ArmPitchAnchor`의 world neutral tip을 frame당 candidate로 사용한다. camera orientation이나 plane basis를 press 시점에 고정하지 않는다.
- ArmDirect `Spatial` candidate는 gameplay `n0` plane에 투영하지 않는다. Drawing 중 root 이동도 허용하며 손과 stroke는 camera yaw/pitch 및 player 이동에 따라 world XYZ에서 연속 이동한다.
- ArmDirect `Spatial` candidate에는 legacy `1.25u` reach 제한을 적용하지 않는다. desired tip 거리와 무관하게 공통 backend의 resampling·dedupe·ink 검사를 거쳐 append하며 HandMarker는 desired tip을 계속 따라간다.
- LMB release는 기존 auto-commit을 수행하고 HandMarker를 neutral pose로 복귀시킨다.
- Drawing 중에도 camera-relative W/A/S/D와 점프를 Idle과 동일하게 허용한다. ArmDirect route에서는 Rigidbody Z 고정과 root/hand depth-drift 검사를 사용하지 않는다.
- `Du03AStrokeSession`, ledger, geometry state machine은 수정하지 않는다.

## 데이터 흐름과 QA 관찰점

```text
InputSystem callbacks
  → Du03BCInputEdgeLatch
  → Du03BCAdapterRouter.ReadIntent
  → active adapter mapping
  → Du03AStrokeDriver.LateUpdate
  → immutable Du03AStrokeSession backend
```

주요 로그:

- `[DU03BC_INPUT]`: control/phase/event sequence. `verboseInputLogging`이 켜진 증거 수집 때만 출력하며 기본값은 off다. sequence 증가는 로그 설정과 무관하게 유지된다.
- `[DU03BC_INPUT_CLEAR]`: focus/device/reset stale-edge clear
- `[DU03BC_SAMPLE]`: mode/source/frame/sampleIndex/input sequence. `verboseMappingLogging`이 켜진 증거 수집 때만 출력하며 기본값은 off다.
- `[DU03BC_MAPPING]`: candidate/independent expected/error/invalid reason. `verboseMappingLogging`이 켜진 증거 수집 때만 출력하며 기본값은 off다.
- `[DU03BC_ROUTE]`: active source 변경
- `[DU03BC_PLAY_MODE]`: `Tab` 전환, 선택 route와 session reset
- `[DU03BC_RESET]`, `[DU03BC_INPUT_RESET]`: adapter 및 canonical R reset

## 에디터 플레이 조작

- 이동: `A` / `D`, 점프: `Space`, lane: `1` / `2` / `3`
- 그리기/확정: `LMB` press/drag/release, 취소: Drawing 중 `RMB` / `Esc`, 잉크·그림만 초기화: `Q`, 전체 trial reset: `R`
- Player capsule은 bounds 중심에서 아래로 probe하여 offset collider의 ground를 판정하고, runtime frictionless material로 접지 마찰이 목표 X 속도를 상쇄하지 않게 한다.
- 샌드박스 시작 방식: `ArmDirect`; 방식 전환: `Tab` (`ArmDirect → Aim → Trajectory → ArmDirect`); Gate A 씬은 기존 `Aim → Trajectory → Aim`

PM 축소 결정에 따라 이 카드의 완료 증거는 compile, EditMode/PlayMode와 에디터 직접 플레이 결과다. RAW/aggregator/standalone/hash 증거는 DU-06A-A Gate A로 이관한다.
