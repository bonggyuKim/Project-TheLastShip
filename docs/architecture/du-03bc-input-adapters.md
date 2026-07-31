# DU-03B/C M+K input adapter architecture

상태: DU-03BC-IMPL-1 에디터 플레이 프로토타입 구현 완료 (2026-08-01, PM 축소 범위)
정본: `docs/input-comparison-spec-v1.md` §3.3, §4, `docs/prototype-execution-plan.md:81-89`

## 책임 경계

- `DoodleUp.Input.Du03BCInputEdgeLatch`: Input System action을 소유하고 Draw/Confirm/Cancel/Reset edge를 수집한다. stroke edge는 `ConsumeStrokeEdges()`에서 한 번만 소비되며 R은 별도 `ConsumeResetPressed()`로만 소비된다.
- `DoodleUp.Runtime.Du03BCAimInputAdapter`: Draw press에서 HandMarker 원점과 camera yaw-normal plane을 snapshot하고 현재 mouse screen ray와의 교차점을 candidate로 만든다.
- `DoodleUp.Runtime.Du03BCTrajectoryInputAdapter`: driver `LateUpdate`가 호출한 tick의 `HandMarker.position`을 그대로 candidate로 만든다. cursor, remote point, 독립 steering, guide, prediction은 없다.
- `DoodleUp.Runtime.Du03BCAdapterRouter`: `Du03AStrokeDriver`에 보이는 유일한 `IDu03ADrawIntentSource`다. 에디터 플레이에서는 Trajectory로 시작하고 `Tab`으로 Aim/Trajectory를 전환한다. 전환 시 진행 중 stroke를 reset하고 driver mode와 active adapter를 함께 바꾼다.
- `DoodleUp.Runtime.Du03BCResetInputBridge`: R edge를 DU-02 canonical reset transaction으로 연결한다.
- `DoodleUp.Runtime.Du03BCRuntimeProbeRunner`: 실제 router→driver `LateUpdate` 경로와 mapping evidence, release order, parity, reset을 raw CSV로 기록한다.
- 기존 `Du03AStrokeSession`, `Du03AStrokeDriver`, `Du03AStrokeGeometry`는 변경하지 않는다. projection/reach/resampling/dedupe/ledger/Confirm geometry 책임은 계속 DU-03A backend에 있다.

각 concrete `MonoBehaviour`는 Unity player scene 직렬화를 위해 파일명과 일치하는 독립 `.cs` 파일을 가진다. adapter 공통 계약과 base/evidence 자료형만 `Du03BCInputAdapters.cs`에 둔다.

## Input edge와 소비 순서

```text
LMB         → Draw pressed/held/released
E           → Confirm pressed
RMB | Esc   → Cancel pressed
R           → canonical trial reset only
```

- 실제 Input System callback은 edge를 latch하고 monotonically increasing event sequence와 execution path를 보존한다.
- `Du03AStrokeDriver.LateUpdate()`가 router의 `ReadIntent()`를 한 번 호출하여 stroke edge와 candidate를 소비한다.
- adapter에 `Update`/`LateUpdate` loop는 없다.
- Drawing 중 candidate는 frame당 최대 1개다.
- release frame은 candidate를 포함하고 driver event order는 `CANDIDATE>RELEASE`다.
- focus loss 또는 mouse/keyboard disconnect는 stale edge를 clear하고 `[DU03BC_INPUT_CLEAR]`를 남긴다.

## Aim mapping

Draw press 시 다음 plane을 고정한다.

```csharp
planeOrigin = handMarker.position;
planeNormal = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
```

Drawing 중에는 camera가 변해도 snapshot plane을 바꾸지 않는다. screen position은 `Camera.ScreenPointToRay`로 변환하고 ray-plane intersection을 candidate로 제공한다. 독립 교차 계산과의 오차 계약은 `<=1e-5u`다.

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

HandMarker는 player fixed child이며 canonical local pose는 `(0.35,0.80,0)`, identity rotation, unit scale다. reset은 DU-02 player/camera/task reset, DU-03A session reset, adapter plane/edge reset을 같은 transaction으로 복원한다.

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

- `[DU03BC_INPUT]`: control/phase/event sequence
- `[DU03BC_INPUT_CLEAR]`: focus/device/reset stale-edge clear
- `[DU03BC_SAMPLE]`: mode/source/frame/sampleIndex/input sequence
- `[DU03BC_MAPPING]`: candidate/independent expected/error/invalid reason
- `[DU03BC_ROUTE]`: active source 변경
- `[DU03BC_PLAY_MODE]`: `Tab` 전환, 선택 route와 session reset
- `[DU03BC_RESET]`, `[DU03BC_INPUT_RESET]`: adapter 및 canonical R reset

## 에디터 플레이 조작

- 이동: `A` / `D`, 점프: `Space`, lane: `1` / `2` / `3`
- 그리기: `LMB`, 확정: `E`, 취소: `RMB` / `Esc`, trial reset: `R`
- 방식 전환: `Tab` (`Trajectory ↔ Aim`)

PM 축소 결정에 따라 이 카드의 완료 증거는 compile, EditMode/PlayMode와 에디터 직접 플레이 결과다. RAW/aggregator/standalone/hash 증거는 DU-06A-A Gate A로 이관한다.
