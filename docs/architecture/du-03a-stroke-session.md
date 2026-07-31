# DU-03A 공통 StrokeSession backend

상태: 구현·standalone runtime 검증 완료 (2026-07-31)
정본: `docs/input-comparison-spec-v1.md` §3.3, §4, `docs/prototype-execution-plan.md:70-79`

## 책임 경계

- `DoodleUp.Stroke.Du03AStrokeSession`: 상태머신, plane projection, reach, 거리 resampling, dedupe, ink 원자 판정, simplification, immutable `Du03AStrokeData`를 소유한다.
- `DoodleUp.Stroke.Du03AStrokeDriver`: `LateUpdate`에서 adapter의 `Du03ADrawIntent`만 소비하고 backend 호출, ghost `LineRenderer`, 상태·candidate 로그를 담당한다.
- `IDu03ADrawIntentSource`: adapter 경계다. adapter는 candidate mapping과 input edge만 제공하고 물리·잉크·ownership을 알지 않는다.
- `DoodleUp.Runtime.Du02ResetCoordinator`: R/lane reset 때 `Du03AStrokeDriver.ResetSession`을 호출해 live/pending stroke와 reserve를 제거한다.
- `DoodleUp.Runtime.Du03ARuntimeProbeRunner`: standalone에서 commit/cancel, Pending 무충돌, atomic reject, R reset을 raw로 검증한다.

DU-03A는 Aim/Trajectory 완성 adapter, capsule collider chain, 삭제, 완성 ink ledger UI를 구현하지 않는다. Confirm은 immutable committed `StrokeData`까지만 생성하며 `[DU03A_COMMIT] colliderCreated=False seamOnly=True`로 범위를 명시한다.

## 상태 전이

```text
Idle --Draw press--> Drawing
Drawing --release, acceptedLength<0.20--> Cancelled --> Idle
Drawing --release, acceptedLength>=0.20--> Pending
Pending --Confirm--> Committed --> Idle
Drawing|Pending --Cancel--> Cancelled --> Idle
```

- `LastTerminalState`가 `Committed|Cancelled` 결과를 보존하고 안정 상태는 다시 `Idle`이다.
- Pending은 `Du03AStrokeData`와 ghost preview만 가지며 collider/Rigidbody가 없다.
- Pending 중 새 Draw는 backend `TryBegin`에서 거부한다.

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
