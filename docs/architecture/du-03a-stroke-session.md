# DU-03A 공통 StrokeSession backend

상태: REV1 backend + DU-03BC LMB release commit UX 반영 (2026-08-01)
정본: `docs/input-comparison-spec-v1.md` §3.3, §4, `docs/prototype-execution-plan.md:70-79`

## 책임 경계

- `DoodleUp.Stroke.Du03AStrokeSession`: 상태머신, plane projection, reach, 거리 resampling, dedupe, ink 원자 판정, simplification, immutable `Du03AStrokeData`를 소유한다.
- `DoodleUp.Stroke.Du03AStrokeDriver`: `LateUpdate`에서 adapter의 `Du03ADrawIntent`만 소비하고 backend 호출, ghost `LineRenderer`, release geometry transaction, 상태 로그를 담당한다. 프레임별 candidate/LateUpdate 로그는 `verboseSamplingLogging`이 켜진 증거 수집 때만 출력하며 기본값은 off다. `LateUpdateProcessed` evidence 발행은 로그 설정과 무관하게 유지된다.
- `DoodleUp.Stroke.Du03AStrokeGeometry`: accepted release에서 비활성 root/child capsule chain을 준비·검증하고 session commit 성공 후 활성화한다. backend의 Pending은 안전한 transaction을 위한 순간 상태이며 실제 M+K UX에서는 같은 call 안에 commit된다.
- `IDu03ADrawIntentSource`: adapter 경계다. adapter는 candidate mapping과 input edge만 제공하고 물리·잉크·ownership을 알지 않는다.
- `DoodleUp.Runtime.Du03ADeterministicIntentSource`: DU-03A standalone evidence 전용 deterministic source다.
- `DoodleUp.Runtime.Du03BCAdapterRouter`: DU-03B/C 구현 이후 driver가 참조하는 단일 source다. deterministic evidence/Aim/Trajectory adapter 중 하나를 route하며 상세 계약은 `du-03bc-input-adapters.md`에 있다.
- `DoodleUp.Runtime.Du02ResetCoordinator`: R/lane reset 때 `Du03AStrokeDriver.ResetSession`과 active adapter reset을 호출해 live/pending stroke, collider root, reserve, plane snapshot과 stale edges를 제거한다.
- `DoodleUp.Runtime.Du03ARuntimeProbeRunner`: 실제 `LateUpdate` 경로, commit/cancel, capsule geometry, terminal ledger, atomic reject, R reset과 mode parity를 raw로 검증한다.

DU-03A는 삭제와 완성 ink ledger UI를 구현하지 않는다. Aim/Trajectory adapter와 traverse 물리 연결은 DU-03B/C에서 담당하며, capsule chain transaction은 공통 backend/geometry로 유지한다.

## 상태 전이

```text
Idle --Draw press--> Drawing
Drawing --release, acceptedLength<0.20--> Cancelled --> Idle
Drawing --LMB release, acceptedLength>=0.20-->
  internal Pending
  --> geometry prepare/validate
  --> Committed
  --> Idle
Drawing --Cancel--> Cancelled --> Idle
```

- `Du03AStrokeSession.Release()`와 `Confirm()`을 직접 사용하는 backend 테스트에서는 Pending을 독립 관찰할 수 있다. 실제 DU-03B/C M+K driver는 같은 release 처리에서 둘을 transaction으로 연결하며 `E` 입력을 요구하지 않는다.
- `LastTerminalState`가 `Committed|Cancelled` 결과를 보존하고 안정 상태는 다시 `Idle`이다.
- internal Pending에는 collider/root/Rigidbody가 없다. Drawing preview는 opaque cyan, reach/ink invalid 구간은 red로 표시하며 release commit 후 preview는 숨기고 committed capsule visual을 남긴다.
- accepted release는 simplified point pair별 capsule을 비활성 상태로 준비·검증한 뒤 session commit과 같은 transaction에서 활성화한다.
- Capsule은 local Y 정렬, `direction=1`, `radius=0.14`, `height=segmentLength+0.28`, `center=0`, non-trigger, root/child scale one을 강제한다. 각 collider segment에는 같은 중심·축·외형 치수를 가진 collider 없는 capsule visual child를 1개 둔다. 불투명 cyan material을 공유하며 Rigidbody나 추가 Collider를 만들지 않는다. `<=1e-6` segment는 collider와 visual 모두 생략하고 skip telemetry를 남긴다.
- backend Pending 중 새 Draw는 `TryBegin`에서 거부한다.
- probe/internal API의 out-of-state Confirm은 같은 frame의 candidate/release 처리를 삼키지 않는다. 실제 입력 경로에는 `E` binding이 없고 release auto-commit 성공 시에만 frame 처리를 종료한다.

## Candidate transaction

1. 시작 frame의 `HandMarker.position`을 `planeOrigin`으로 snapshot한다.
2. `n=Normalize(ProjectOnPlane(camera.forward, Vector3.up))`을 `planeNormal`으로 snapshot한다. magnitude `<1e-6`이면 시작을 거부한다.
3. raw candidate를 snapshot plane에 투영한다.
4. finite와 reach `1.25u`를 검증한다.
5. 마지막 accepted point부터 spacing `0.08u` prospective points와 `requiredInk`를 전부 선계산한다.
6. `requiredInk > availableInk`이면 append/reserve 변경 없이 `InkInvalid`다. 통과하면 prospective points 전부를 append한다.
7. accepted point와 candidate 거리 `<0.02u`는 `Dedupe`, spacing 미도달은 `SpacingNotReached`이며 둘 다 valid+not-appended다.

`AvailableInk + DrawingReservedLength + PendingReservedLength + committed chargedLength = initialInk` 불변식을 모든 mutation 뒤 검사한다.

## StrokeData

`Du03AStrokeData(SimplifiedPoints, ChargedLength, OwnerId, Mode)`는 생성 시 point 배열을 복사한다.

- `ChargedLength`: simplification 전 accepted resampled length
- `SimplifiedPoints`: Douglas-Peucker tolerance `0.02u` 적용 결과
- geometry 길이에서 charged length를 재계산하지 않는다.

## Reset 계약

R/lane reset 후:

- session `Idle`
- `pending=0`
- `committedLive=0`
- drawing/pending reserve `0`
- available ink `5.00`

DU-02의 player/camera/task/sampling canonical reset과 같은 transaction에서 실행된다.
