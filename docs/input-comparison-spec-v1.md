# DOODLE UP — DU-01 InputComparisonSpec v1

상태: **InputComparisonSpec v1 Approved (2026-07-31) / Gate A 종료까지 변경 통제**  
작성일: 2026-07-31  
상위 정본: `docs/prototype-execution-plan.md`  
적용 범위: Gate A 솔로 M+K 입력 비교, Gate B 동일 gamepad 2개 social smoke의 입력·환경 계약

## 1. 결론

Gate A에서는 `Aim`과 `Trajectory`에 동일한 손 원점, **camera yaw-normal vertical drawing plane**, 사거리, 잉크, stroke backend, 코스, 물리, reset, 확인·취소 규칙을 적용한다. 두 mode의 유일한 차이는 **다음 candidate point를 만드는 mapping**이다.

- `Aim`: mouse 위치를 stroke 시작 시 고정한 camera yaw-normal vertical plane 위 world point로 변환한다.
- `Trajectory`: draw hold 중 고정 `HandMarker`의 world position을 같은 60 fps `LateUpdate` phase에서 candidate point로 사용한다. cursor steering이나 원격 point 생성은 금지한다.
- 두 mode 중 최소 하나가 Gate A를 통과해야 P0를 계속한다.
- Gate B는 Gate A에서 채택된 mode 하나만 동일 모델 gamepad 2개로 옮겨 social hook을 관찰한다. Gate B 결과를 Gate A M+K 사용성 근거와 섞지 않는다.

## 2. 책임·체크포인트·차단요인

| 항목 | 내용 |
|---|---|
| DRI | `game-planning` — 본 규격, 공정성, Gate 판정표 |
| 승인 | `project-manager` — v1 동결, Gate 진행·중단 |
| 기술 자문 | `game-tech-director` — Unity 단위·입력·물리 구현 가능성 확인 |
| 증거 검토 | `game-qa` — attempts/events/strokes schema, 시행·영상 연결, failure code 재현성 확인 |
| 다음 체크포인트 | DU-02 부트스트랩·리셋 가능한 솔로 코스 → DU-03A 계측 sample → QA pilot |
| 현재 차단요인 | DU-01 문서 동결 차단 없음. 동일 모델 gamepad 2개 미확보는 Gate A를 막지 않고 Gate B만 막는다. |
| 변경 통제 | 승인 후 수치·코스·mapping·assist를 바꾸면 기존 비교 데이터는 폐기하고 양 mode 전부 다시 측정한다. 단순 로그 문구 수정은 예외다. |

권장 산출물 경로: `D:/Project-DoodleUp/docs/input-comparison-spec-v1.md`

## 3. 고정 수치 profile

Unity world `1.0 unit = 1.0 m`로 취급한다. 아래 값은 출시 밸런스가 아니라 첫 비교를 위한 실험 상수다.

### 3.1 캐릭터·이동·물리

| 항목 | v1 값 | 적용 이유 |
|---|---:|---|
| 기준 캐릭터 키 | `1.00 u` | 모든 공간 수치의 기준 |
| 최대 지상 이동속도 | `2.50 u/s` | 짧은 stroke와 이동을 함께 시험할 수 있는 속도 |
| 공중 수평 이동속도 | `2.00 u/s` | 점프 중 과도한 궤적 보정을 방지 |
| 점프 초기속도 | `4.00 u/s` | 약 `0.82 u` 이론 상승고 |
| 중력 | `-9.81 u/s²` | Unity 기본 중력 유지 |
| `fixedDeltaTime` | `0.020 s` | 50 Hz 고정 |
| human test frame cap | `60 fps`, VSync off | tester 간 표시 지연 통제 |
| 이동 차원 | spawn locomotion plane의 horizontal `right` 1축 + world `Y`; `n` depth `0` | Aim/Trajectory 모두 같은 vertical 2.5D 문제를 풀게 함 |

30/60/144 fps는 DU-02의 sampling 재현성 기술 검증에서만 사용한다. 사람 대상 A/B 판정 세션은 60 fps로 고정한다.

### 3.2 카메라·표시 환경

| 항목 | v1 값 |
|---|---:|
| 해상도·화면비 | `1920×1080`, `16:9` |
| projection | Perspective |
| vertical FOV | `60°` |
| camera distance | player root에서 `4.50 u` |
| camera height offset | player root에서 `+1.20 u` |
| camera pitch | 수평 기준 아래로 `10°` |
| camera rotation/zoom | trial 중 고정, 사용자 조작 없음 |
| drawing plane | stroke 시작 시 hand origin을 지나고 camera yaw-normal `n`을 normal로 하는 world-vertical plane snapshot |
| mouse | 동일 장치, `800 DPI`, Windows pointer `6/11`, Enhance Pointer Precision off, in-game sensitivity `1.00` |

FOV·거리·해상도 중 하나라도 달라진 시행은 `TECH_INVALID`로 제외하고 재시행한다.

#### 3.2.1 좌표계·locomotion plane

World up은 `up = Vector3.up`으로 고정한다. Trial spawn에서 다음 값을 계산해 locomotion profile과 manifest에 저장한다.

```text
n     = Normalize(ProjectOnPlane(camera.forward, Vector3.up))
up    = Vector3.up
right = Normalize(Cross(up, n))
```

- `n`은 camera pitch를 제거한 **camera yaw-normal**이다. `ProjectOnPlane` 결과 magnitude가 `<1e-6`이면 profile invalid로 세션을 시작하지 않는다.
- `right` 부호는 위 식을 정본으로 삼는다. 캐릭터의 수평 양의 이동, course의 수평 양의 좌표, drawing plane의 수평축은 모두 `right`다.
- Player root local axes는 `+X=right`, `+Y=up`, `+Z=n`으로 spawn 시 고정하며 trial 중 yaw/pitch/roll 회전을 금지한다.
- Spawn 시 character root 위치 `rootSpawn`으로 locomotion plane `dot(worldPoint - rootSpawn, n)=0`을 고정한다.
- Character root와 고정 child `HandMarker`의 depth는 locomotion/physics 적용 후에도 이 spawn plane에서 `0`이어야 한다. Depth drift 허용오차는 `abs(dot(position-rootSpawn,n)) ≤0.001 u`이며 초과 frame이 생기면 해당 attempt는 `TECH_INVALID/DEPTH_DRIFT`다.
- Gravity와 jump는 world `Y`, 수평 이동은 `right`축만 사용한다. 따라서 camera pitch `-10°`는 이동·점프 축을 기울이지 않는다.
- Stroke 시작 시 그 frame의 `HandMarker.position`을 `planeOrigin`, 고정 `n`을 `planeNormal`로 snapshot한다. Stroke 중 camera transform이 바뀌어도 plane은 다시 계산하지 않는다.

### 3.3 stroke·잉크

| 항목 | v1 값 | 판정 정의 |
|---|---:|---|
| reach radius | `1.25 u` | stroke 시작 시의 hand origin에서 각 accepted point까지의 직선거리 |
| capsule radius | `0.14 u` | 고정. 지름 `0.28 u`; 가변 두께 금지 |
| distance sample spacing | `0.08 u` | frame 수가 아니라 누적 world distance 기준 |
| point dedupe threshold | `<0.02 u` | 이보다 가까운 candidate는 중복 point로 버림 |
| minimum committed stroke length | `0.20 u` | 미만은 자동 cancel, ink 소비 `0` |
| personal ink cap | `5.00 u` | 자연 회복 없음 |
| ink charge | accepted resampled polyline length `1.00 u = ink 1.00` | 두께와 무관, 양 mode 동일 |
| stroke 시작점 | plane snapshot 시점의 hand origin을 backend가 첫 point로 삽입 | snap assist가 아니라 공통 게임 규칙 |
| 최대 단일 stroke | 별도 상한 없음 | reach와 남은 ink가 상한 역할 |
| simplification | Douglas-Peucker tolerance `0.02 u` | resampling 뒤 적용, 양 mode 동일; mode별 smoothing 금지 |

Charged length는 **simplification 전 accepted resampled polyline length**다. Collider geometry는 별도로 **simplification 후 points**를 사용한다. 따라서 collider segment 길이의 합으로 잉크를 재계산하지 않는다.

Owner별 잉크 불변식은 모든 state transition 직전·직후에 다음 닫힌 식을 만족해야 한다.

```text
availableInk
+ drawingReservedLength
+ pendingReservedLength
+ Σ committedLiveOwnedChargedLength
= 5.00 u
```

- `Idle → Drawing`: 네 항의 합은 유지되며 reserve는 `0`에서 시작한다.
- accepted resampled segment append: 동일 길이를 `availableInk`에서 빼 `drawingReservedLength`로 이전한다.
- `Drawing → Pending`: `drawingReservedLength` 전액을 `pendingReservedLength`로 이전한다.
- `Pending → Committed`: `pendingReservedLength` 전액을 새 stroke의 `committedLiveOwnedChargedLength`로 이전한다.
- Drawing/Pending `Cancel`: 해당 reserve 전액을 `availableInk`로 환급한다.
- committed `Delete`: 삭제 stroke의 charged length를 원소유자의 `availableInk`로 환급하고 live 합에서 제거한다.
- 자연 회복, 소실, 이중 환급, 다른 owner ledger로의 이전은 금지한다.

## 4. 공통 StrokeSession 계약

두 adapter는 아래 상태와 판정을 공유한다.

```text
Idle
  └─ Draw press → Drawing
       ├─ Draw release + length < 0.20 → Cancelled → Idle
       └─ Draw release + length ≥ 0.20 → Pending
            ├─ Confirm → Committed → Idle
            └─ Cancel → Cancelled → Idle
```

1. `Draw press`를 소비하는 `LateUpdate` tick에서 `HandMarker.position`과 camera yaw-normal `n`을 drawing plane으로 snapshot하고 hand origin을 첫 accepted point로 넣는다.
2. 두 adapter는 같은 60 fps `LateUpdate`에서 frame당 정확히 1회 candidate sample을 제출한다. 제출 순서와 script execution order는 profile/manifest에 고정한다.
3. backend가 plane, reach, spacing, dedupe, 남은 ink를 판정하고 raw candidate와 accepted 결과를 typed telemetry로 남긴다.
4. `Draw release`는 **확정이 아니다.** §4.2 규칙으로 취소하거나 반투명 `Pending` ghost로 전이한다.
5. `Pending`에는 collider·Rigidbody·물리 접촉이 없다. 명시적 `Confirm`만 reserve를 committed ledger로 이전하고 capsule chain을 생성한다.
6. `Cancel`은 `Drawing`과 `Pending` 모두에서 가능하며 ghost를 제거하고 해당 reserve 전액을 환급한다.
7. 한 번에 pending stroke는 1개만 허용한다. Pending 중 새 Draw 입력은 무시한다.

### 4.1 HandMarker·candidate sample phase

- P0 `HandMarker`는 player root의 **고정 child Transform**이다. Profile 값은 `localPosition=(+0.35,+0.80,0.00) u`, `localRotation=(0,0,0,1)` identity quaternion, `localScale=(1,1,1)`로 동결한다. `+0.35 u`는 profile의 `right` 방향이다.
- Animation, IK, root motion, procedural sway, frame 중 pose offset으로 `HandMarker` local pose를 바꾸는 것을 금지한다.
- Script execution order는 `PlayerMotor=0`, `CameraRig=+50`, `CandidateSampler=+100`으로 동결한다. `CandidateSampler`만 `LateUpdate`에서 sample을 제출하며 `PlayerMotor`의 Update/FixedUpdate 결과와 고정 camera transform이 반영된 뒤 실행한다.
- Input System callback은 control edge를 latch만 하고 StrokeSession state나 ledger를 직접 바꾸지 않는다. `CandidateSampler.LateUpdate`는 `Draw press snapshot/start → 그 frame candidate 1회 제출·backend 판정 → Draw release 판정` 순서로 처리한다. 따라서 release가 latch된 frame도 candidate sample 1회를 먼저 처리하며 §4.2의 “현재 invalid candidate”는 그 sample 결과를 뜻한다.
- Locomotion과 physics 결과가 적용된 뒤, 두 adapter 모두 60 fps render frame의 `LateUpdate`에서 candidate를 정확히 1회 읽는다.
- `Trajectory`는 해당 tick의 `HandMarker.position`을 raw candidate로 읽는다.
- `Aim`은 같은 tick의 mouse screen position으로 ray를 만들고, stroke에 snapshot된 yaw-normal vertical plane과의 교점을 raw candidate로 읽는다.
- Draw가 inactive이거나 StrokeSession이 candidate를 받지 않는 state면 `sample_phase=NONE`, candidate typed columns는 null이다. 계산을 시도했으나 ray-plane 교점이 없거나 finite하지 않으면 `sample_phase=LATE_UPDATE`, raw candidate는 null, `candidate_valid=false`, `candidate_invalid_reason=NO_PLANE_INTERSECTION|NON_FINITE`다.
- 정상 sample은 `sample_phase=LATE_UPDATE`이며 raw candidate는 필수다. Backend가 append하지 않더라도 accepted typed columns는 null로 남기고 `candidate_valid`와 `candidate_invalid_reason`으로 사유를 기록한다.

### 4.2 사거리 밖·잉크 부족·release

- candidate가 hand origin snapshot에서 `1.25 u`를 초과하면 cursor/끝점을 red로 표시하고 `REACH_INVALID`를 기록한다.
- Invalid candidate는 accepted append와 ink reserve 변경을 하지 않는다. Reach 경계로 자동 clamp하지 않으며, prospective resampling 계산은 아래 원자 판정을 위한 임시 계산일 뿐 state를 바꾸지 않는다.
- Candidate가 다시 유효 범위로 들어오면 마지막 accepted point부터 sampling을 재개한다.
- Raw candidate 하나의 backend 판정은 원자적이다. finite/plane/reach 검증 후 그 candidate가 생성할 prospective resampled points `0..N`과 `requiredInk`를 먼저 계산한다. `requiredInk > availableInk`이면 red `INK_INVALID`로 표시한다. `requiredInk > availableInk`이면 `CANDIDATE_SAMPLE`은 `candidate_valid=false`, `candidate_invalid_reason=INK_INVALID`, `accepted_appended=false`이며 `ACCEPTED_POINT`를 0개 생성하고 reserve를 변경하지 않는다. 검증을 모두 통과하면 prospective points 전부를 순서대로 append한다. 따라서 `candidate_valid=false AND accepted_appended=true` 조합은 금지한다. `SPACING_NOT_REACHED` 또는 `DEDUPE`만 `candidate_valid=true AND accepted_appended=false`다.
- Release tick의 현재 candidate가 invalid이면 그 candidate는 폐기한다. 마지막 accepted point까지의 **simplification 전 resampled length**로만 판정한다.
- Release 시 accepted length `<0.20 u`면 `Drawing → Cancelled → Idle`, ghost 제거, `drawingReservedLength` 전액 환급, charged length `0`이다.
- Release 시 accepted length `≥0.20 u`면 release 순간 candidate의 invalid 여부와 무관하게 `Drawing → Pending`이며 `drawingReservedLength` 전액을 `pendingReservedLength`로 이전한다.
- Release 자체는 commit, charged-length 변경, collider 생성을 하지 않는다.

### 4.3 M+K 공통 조작

| 행동 | 입력 | Aim | Trajectory |
|---|---|---|---|
| 수평 이동 | `A/D` | `-right/+right` 이동 | 같은 root 이동 및 그 결과의 HandMarker 이동이 trajectory를 만듦 |
| depth 입력 | `W/S` | unbound; `n` 이동 금지 | 동일 |
| 점프 | `Space` | 동일 | 동일 |
| Draw | `LMB` hold | `LateUpdate` mouse ray와 snapshot plane 교점을 candidate로 제출 | 같은 `LateUpdate`의 고정 `HandMarker.position`을 candidate로 제출 |
| Confirm | `E` | Pending 확정 | Pending 확정 |
| Cancel | `RMB` 또는 `Esc` | Drawing/Pending 취소 | Drawing/Pending 취소 |
| Delete | `X` | 가장 최근의 자기 committed stroke 삭제 | 동일 |
| Trial reset | `R` | 현재 trial 실패 처리 후 정본 상태 복원 | 동일 |

`Trajectory`에서 mouse/cursor, 보이지 않는 guide point, 목표 방향 보정, 캐릭터 이동과 독립된 hand steering을 사용하면 비교 위반이다.

### 4.4 삭제

Gate A의 `X`는 선택 UI 없이 **가장 최근의 자기 live stroke 1개를 LIFO로 삭제**한다. 삭제 즉시 소유자에게 정확한 charged length를 환급한다. 삭제할 자기 stroke가 없으면 no-op 로그만 남긴다.

Gate B에서는 social smoke를 위해 `<Gamepad>/buttonNorth`가 hand에서 `0.50 u` 이내의 highlighted stroke 1개를 삭제한다. 자기 것과 남의 것 모두 대상이며, 여러 개면 hand와 가장 가까운 stroke를 선택하고 동률이면 생성 시각이 최신인 것을 선택한다. 잉크는 삭제한 사람이 아니라 **원소유자**에게 환급한다.

### 4.5 Committed capsule chain geometry

Confirm된 simplified polyline의 연속된 각 point pair마다 child `CapsuleCollider` 1개를 생성한다.

- Child Transform의 world position을 segment midpoint에 두고 local `Y`축을 segment 방향에 정렬한다. `CapsuleCollider.center=(0,0,0)`이다.
- `CapsuleCollider.direction = 1` (`Y-axis`)로 명시 설정하며 prefab/default 값에 의존하지 않는다.
- `radius = 0.14 u`
- `height = segmentLength + 0.28 u` — 양 끝 hemisphere를 포함해 shared endpoint에서 이웃 capsule cap이 겹친다.
- `isTrigger = false`
- Stroke root와 모든 child의 runtime local/world scale은 `(1,1,1)`이다. Non-uniform·negative scale을 금지한다.
- Segment length가 `≤1e-6 u`인 degenerate segment는 collider를 만들지 않고 telemetry에 기록한다.
- Shared endpoint의 cap overlap으로 collider 사이에 gap이 없어야 한다.
- Pending에는 capsule root·child collider·Rigidbody가 존재하지 않는다. Capsule chain 생성은 accepted Confirm의 `Pending → Committed` transition과 같은 transaction에서만 수행한다.

## 5. Aim/Trajectory 공정 비교 규칙

### 5.1 반드시 같은 것

- executable build, scene, spawn, fixed HandMarker local pose, script execution order, camera, FOV, physics, character motor
- StrokeSession backend와 모든 수치 profile
- 3-task geometry, goal 판정, task timeout, reset 결과
- ghost opacity·색상, valid/invalid 표시, ink UI
- Confirm/Cancel/Delete 키와 설명 문구
- 5분 학습시간, 9개 본 trial, 관찰자 개입 규칙
- attempts/events/strokes 증거 schema와 failure code

### 5.2 달라도 되는 것

오직 candidate point 생성 mapping만 다르다.

- `Aim`: 60 fps `LateUpdate` screen-space mouse → ray → snapshot yaw-normal vertical plane intersection
- `Trajectory`: 같은 `LateUpdate`의 fixed-child `HandMarker.position` → 공통 backend

### 5.3 금지 assist

두 mode 모두 snap, auto-anchor, edge magnet, target attraction, reach clamp, path prediction, 자동 직선화, 자동 Confirm, mode별 smoothing/tolerance, mode별 이동속도, mode별 잉크 할인, course별 hidden correction을 금지한다.

공정성은 두 mode를 비슷한 성공률로 보정하는 것이 아니다. **같은 목표·자원·피드백에서 mapping의 구조적 장단점을 그대로 측정하는 것**이다.

## 6. 3-task course

모든 task는 독립 lane이며 회색 박스 geometry, 정적 committed stroke, 동일 마찰재를 사용한다. scoring band는 시각 overlay일 뿐 snap이나 anchor assist를 제공하지 않는다.

| ID | 과제 | 고정 배치 | 성공 조건 |
|---|---|---|---|
| `T1-Horizontal` | 수평 한 걸음 | 출발 edge와 목표 ledge가 같은 높이, 수평 gap `0.70 u` | 유효 stroke commit 후 캐릭터가 그 stroke에 접촉해 목표 zone에서 `1.0 s` 유지 |
| `T2-Rising` | 상승 대각 한 걸음 | 목표 ledge 중심이 출발 edge에서 수평 `+0.65 u`, 수직 `+0.55 u` | 유효 stroke commit 후 그 stroke 접촉을 거쳐 목표 zone에서 `1.0 s` 유지 |
| `T3-Bridge` | 두 접점 bridge | 같은 높이 두 ledge 사이 gap `0.95 u`, 양쪽 접촉 band 폭 `0.12 u` | 하나의 committed stroke가 양 band와 모두 교차하고, 캐릭터가 그 stroke를 밟아 반대편에서 `1.0 s` 유지 |

추가 판정:

- 각 trial 제한시간은 `60 s`다.
- 목표 zone에 점프로 직접 도달해도 stroke contact가 없으면 성공이 아니다.
- `R` reset, 추락 reset volume 진입, timeout은 해당 trial 실패다.
- physics 폭발·콜라이더 소실·입력장치 disconnect처럼 tester 책임이 아닌 오류는 `TECH_INVALID`다. raw attempt는 보존하고 §9.1의 `attempt_no + 1`, 3연속 중단, 새 session 전체 재실행 규칙을 적용한다.

## 7. Reset 정본

각 trial 시작과 `R` 입력은 한 번의 명령으로 다음을 원자적으로 복원한다.

- player position/rotation/velocity, grounded state, hand pose
- camera transform/FOV, camera yaw-normal `n`, cursor 기준점
- HandMarker 고정 local pose와 root/hand depth `0`
- Drawing/Pending/Committed stroke `0`, 모든 reserve `0`, available ink `5.00 u`
- task geometry와 goal state
- StrokeSession `Idle`, timer `0`, event counters `0`

Reset 후 `3 s` countdown 동안 이동·Draw를 잠그고, `GO`부터 시간을 잰다. 관찰자가 수동으로 일부 상태만 고치는 것은 금지한다.

## 8. A/B 세션 순서

### 8.1 참가자와 순서

최소 tester `4명`, 동일 PC·mouse·keyboard를 사용한다.

| tester | 1차 mode | 2차 mode |
|---|---|---|
| P01 | Aim | Trajectory |
| P02 | Trajectory | Aim |
| P03 | Trajectory | Aim |
| P04 | Aim | Trajectory |

UI에는 선호를 유도하는 설명 대신 `Mode A`, `Mode B`와 중립 조작문만 표시한다. 분석 시 실제 mode로 복호화한다.

### 8.2 mode당 절차

1. 중립 설명 읽기: 최대 `2분`, 두 mode에 같은 문장 사용
2. 전용 연습 lane: `5분`, 데이터는 본 판정에서 제외
3. 본 trial: `T1 → T2 → T3`를 1 block으로 하여 `3 block`, 총 `9 trial`
4. mode 종료 즉시 5점 설문
5. `3분` 휴식 후 반대 mode 반복

관찰자는 연습 중 조작키를 다시 읽어줄 수 있으나 본 trial에서는 기술 오류 외 힌트를 주지 않는다. 두 번째 mode만 추가 설명하거나 추가 연습을 주지 않는다.

## 9. Gate A 계측·판정

### 9.1 evaluation row·attempt 결과·failure 정의

Gate A 판정표의 evaluation row는 `4 participants × 2 modes × 9 trial slots = 72개`로 고정한다. 각 slot은 `participant_id + mode + block_index + task_id`로 식별한다.

- `attempts.csv`에는 최초 시도와 모든 재시도를 raw row로 보존한다. 따라서 raw attempt 수는 72개를 초과할 수 있다.
- 판정에는 완료된 session의 각 slot에서 `evaluation_included=true`인 attempt 하나만 사용하며, 정상 완료 시 정확히 72개다.
- `TECH_INVALID` attempt는 삭제하지 않고 `evaluation_included=false`로 보존한 뒤 같은 slot을 `attempt_no + 1`로 즉시 재시행한다.
- 같은 mode session에서 `TECH_INVALID`가 3회 연속 발생하면 그 mode session 전체를 중단한다. 해당 session의 9개 slot attempt를 모두 `evaluation_included=false`로 유지하고, 새 `session_id`에서 그 mode의 9개를 처음부터 다시 실행한다.
- 서로 다른 `session_id`의 유효 row를 골라 9개를 만드는 세션 간 짜깁기는 금지한다.

`result`는 다음 세 값만 허용한다.

- `SUCCESS`: 60초 안에 해당 task 성공 조건 충족
- `PLAYER_FAIL`: tester 행동 또는 사용성 실패로 task 성공 조건 미충족
- `TECH_INVALID`: build, 입력장치, 성능, 물리, 계측 또는 영상 증거 오류로 공정한 판정 불가

`PLAYER_FAIL`의 `player_failure_reason_code`는 `REACH_MISUNDERSTANDING`, `DEPTH_PLANE_MISUNDERSTANDING`, `CONFIRM_MISUNDERSTANDING`, `CANCEL_DELETE_MISUNDERSTANDING`, `TRAVERSAL_FAIL`, `INPUT_MAPPING_FAIL`, `PHYSICS_FAIL`, `TIMEOUT`, `MANUAL_RESET` 중 하나다. `TECH_INVALID`는 별도 `technical_reason_code`를 필수로 기록하며 player failure code와 혼용하지 않는다.

사거리·깊이·Confirm·Cancel/Delete의 **사용성 오해 code**는 다음 세 증거가 모두 같은 원인을 가리킬 때만 확정한다.

1. 해당 attempt의 raw event sequence
2. 연결된 영상 구간
3. trial 종료 직후 중립 질문에 대한 tester 답변

하나라도 불일치하거나 없으면 해당 오해 code로 집계하지 않고 증거에 맞는 다른 code로 분류한다. 사거리·깊이 오해 실패 수는 확정된 `REACH_MISUNDERSTANDING + DEPTH_PLANE_MISUNDERSTANDING`만 센다.

### 9.2 증거 파일·연결 계약

단일 CSV에 요약하지 않고 최소 `attempts.csv`, `events.csv`, `strokes.csv`로 분리한다. 모든 시간은 wall clock이 아닌 동일 프로세스의 **monotonic clock** 기준이다.

#### `attempts.csv`

```text
spec_version,build_id,session_id,attempt_id,attempt_no,evaluation_included,
participant_id,mode_order,mode,task_id,block_index,trial_index,
result,player_failure_reason_code,technical_reason_code,
trial_start_us,goal_us,total_completion_ms,
raw_event_seq_start,raw_event_seq_end,video_file,video_in_ms,video_out_ms,
invalid_reach_count,invalid_ink_count,cancel_count,delete_count,reset_count,
committed_stroke_count,charged_length,ink_remaining,avg_fps,min_fps,
device_id,post_trial_question_id,post_trial_answer,observer_note
```

#### `events.csv`

```text
spec_version,build_id,session_id,attempt_id,raw_event_seq,event_time_us,
event_type,stroke_id,input_control,input_phase,state_before,state_after,
sample_phase,source_candidate_event_seq,candidate_valid,candidate_invalid_reason,
candidate_x,candidate_y,candidate_z,accepted_appended,
accepted_x,accepted_y,accepted_z,hand_x,hand_y,hand_z,
plane_origin_x,plane_origin_y,plane_origin_z,
plane_normal_x,plane_normal_y,plane_normal_z,
cumulative_resampled_length,drawing_reserved_length,pending_reserved_length,
reserved_ink,available_ink,reason_code
```

Candidate 계측 의미는 다음으로 고정한다. 좌표는 모두 world-space `u`, 길이·잉크는 `u`다.

- 각 sampling tick은 정확히 1개의 `CANDIDATE_SAMPLE` row를 만든다. `sample_phase=LATE_UPDATE`, `source_candidate_event_seq=raw_event_seq`, `hand_*`와 snapshot `plane_origin_*`, `plane_normal_*`가 필수다.
- 계산 가능한 raw point를 얻었으면 `CANDIDATE_SAMPLE.candidate_*`에 그대로 기록한다. Reach/ink/spacing/dedupe 결과와 무관하게 raw candidate를 지우거나 accepted point로 덮어쓰지 않는다.
- Ray-plane 교점 실패 또는 non-finite면 `candidate_*`는 null, `candidate_valid=false`, `candidate_invalid_reason=NO_PLANE_INTERSECTION|NON_FINITE`다.
- `candidate_valid`는 raw candidate가 finite이고 plane·reach·ink 조건을 통과했는지를 뜻한다. Reach/ink reject는 `candidate_valid=false`, `candidate_invalid_reason=REACH_INVALID|INK_INVALID`다.
- Spacing 미도달이나 dedupe는 invalid candidate가 아니다. 이 경우 `candidate_valid=true`, `candidate_invalid_reason=null`, `accepted_appended=false`, `reason_code=SPACING_NOT_REACHED|DEDUPE`다.
- 한 raw candidate에서 거리 resampling point가 여러 개 생길 수 있으므로 backend는 `0..N`개의 `ACCEPTED_POINT` row를 별도로 만든다. 각 row는 `sample_phase=NONE`, `source_candidate_event_seq=CANDIDATE_SAMPLE.raw_event_seq`, `accepted_appended=true`, non-null `accepted_*`를 갖고 candidate/hand/plane columns는 null이다.
- Accepted point가 0개면 `CANDIDATE_SAMPLE.accepted_appended=false`이고 별도 `ACCEPTED_POINT` row가 없다. 1개 이상이면 sample row의 `accepted_appended=true`로 요약하되 sample row의 `accepted_*`는 항상 null이다.
- `cumulative_resampled_length`는 각 `ACCEPTED_POINT` append 직후의 simplification 전 누적 길이다. `reserved_ink = drawing_reserved_length + pending_reserved_length`다.
- `CANDIDATE_SAMPLE`의 `cumulative_resampled_length`와 ledger columns는 해당 candidate 처리 **직전** snapshot이다. 각 `ACCEPTED_POINT` row는 자신의 append **직후** snapshot이다. State/input transition row는 transition **직후** snapshot이다. `CANDIDATE_SAMPLE.accepted_appended`는 처리 결과 요약 필드지만 candidate row의 pre-processing ledger snapshot 의미를 바꾸지 않는다.
- `ACCEPTED_POINT`의 `candidate_valid`, `candidate_invalid_reason`, `candidate_*` 및 hand/plane columns는 null이다. `sample_phase=NONE`이고 `source_candidate_event_seq`로 원본 `CANDIDATE_SAMPLE`을 참조한다.
- 위 순서로 `raw_event_seq`가 증가할 때 `cumulative_resampled_length`, drawing/pending reserve, reserved ink, available ink 변화가 initial ledger부터 결정적으로 재생 가능해야 한다. 원자 판정 실패 candidate 뒤에는 ledger 값이 직전 snapshot과 동일해야 한다.
- Draw inactive 또는 candidate 비수신 state의 input/state event는 `sample_phase=NONE`이다. Candidate/hand/plane columns는 null이며, accepted columns는 `ACCEPTED_POINT` row에서만 non-null이다. 숫자 `0`을 null/미측정 의미로 사용하지 않는다.

#### `strokes.csv`

```text
spec_version,build_id,session_id,attempt_id,stroke_id,stroke_index,
pending_enter_us,commit_us,confirm_latency_ms,commit_input_event_seq,
committed,charged_length,resampled_point_count,simplified_point_count
```

각 attempt는 `attempt_id`, `raw_event_seq_start/end`, `video_file`, `video_in_ms`, `video_out_ms`로 raw event와 영상 구간에 연결되어야 한다. stroke는 `attempt_id + stroke_id`와 `commit_input_event_seq`로 해당 event에 연결한다. raw event, stroke, 영상, 즉시 사후질문 중 판정에 필요한 연결이 누락되거나 범위가 맞지 않으면 해당 evidence pack은 `EVIDENCE_INVALID`이며 Gate 판정을 진행하지 않는다.

#### `evidence-manifest.json`

Evidence root에는 다음 필드와 checksum map을 가진 manifest 1개가 필수다.

```text
schema_version, spec_version, generated_at_utc,
unity_version, input_system_package_version,
scene_hash_sha256, course_hash_sha256, profile_hash_sha256,
binary_sha256, input_actions_hash_sha256,
hand_marker_local_pose, script_execution_order,
operating_system, cpu, gpu, ram_mb, display_refresh_hz,
resolution, mouse_vendor_product, mouse_dpi, pointer_settings,
gamepad_devices[], artifact_checksums_sha256{}
```

- `gamepad_devices[]`는 Gate B에만 필수이며 `device_identity_mode`, player slot, Input System device id, interface, manufacturer, product, serial presence와 serial hash를 기록한다. `NO_SERIAL`이면 serial hash는 null이다. Raw serial은 증거팩에 저장하지 않는다.
- `artifact_checksums_sha256`는 `attempts.csv`, `events.csv`, `strokes.csv`, raw event log, summary, 설문, 각 video file을 상대경로→SHA-256으로 연결한다.
- Scene/course/profile/input-actions는 canonical bytes의 SHA-256을 사용한다. Hash 입력 직렬화 방식과 schema version도 manifest에 기록한다.
- 필수 필드 누락, 선언 checksum 불일치, CSV의 build/session과 manifest 불일치는 `EVIDENCE_INVALID`다.

### 9.3 Confirm latency 산식

Confirm latency는 **같은 stroke**에서 backend가 `Drawing → Pending`으로 전이한 `pending_enter_us`부터, 최초로 accepted된 명시적 Confirm이 `Pending → Committed`를 만든 `commit_us`까지다.

```text
confirm_latency_ms = (commit_us - pending_enter_us) / 1000
```

- `Pending` 상태 밖의 Confirm 입력은 latency 표본에서 제외한다.
- key repeat와 중복 Confirm event는 제외한다.
- `Pending → Committed`를 만들지 못한 rejected Confirm도 제외한다.
- 한 trial에 committed stroke가 여러 개면 그 trial의 유효 `confirm_latency_ms` 중앙값을 `trial_confirm_median_ms`로 쓴다.
- participant의 mode별 Confirm 지표는 9개 trial 중 non-null인 `trial_confirm_median_ms`들의 중앙값이다.
- non-null trial이 `7개 미만`이면 그 participant는 Confirm 요건 FAIL이며 `participant qualified=false`다.

`total_completion_ms = (goal_us - trial_start_us) / 1000`이며 `evaluation_included=true AND result=SUCCESS`인 attempt에만 mode 시간 비교 표본으로 사용한다.

### 9.4 Continue — participant qualified와 mode PASS

participant는 한 mode에서 다음 세 조건을 **동시에** 만족할 때만 `qualified(mode)=true`다.

1. 고정된 9개 evaluation row 중 `SUCCESS ≥7/9`
2. non-null trial이 7개 이상이고 §9.3의 participant Confirm latency median `≤2000 ms`
3. 확정된 `REACH_MISUNDERSTANDING + DEPTH_PLANE_MISUNDERSTANDING ≤2/9`

`mode PASS = qualified participant ≥3/4`다. 성공·Confirm·오해 조건을 서로 다른 participant 집단 통계로 따로 통과시키는 기존 집단 분리식은 사용하지 않는다.

Aim/Trajectory 중 하나 이상 PASS면 Gate A를 Continue한다.

### 9.5 두 mode 모두 PASS일 때 채택

두 mode 모두 PASS일 때 아래 우선순위를 순서대로 적용한다.

1. **성공률 우선:** 72개 중 해당 mode의 36개 `evaluation_included` row를 분모로 한 success rate가 상대보다 `20 percentage points 이상` 높으면 그 mode를 채택한다. 상대 mode가 시간 지표에서 우세해도 이 결정을 뒤집지 않는다.
2. **시간 우세:** 성공률 차이가 20%p 미만일 때만, 각 mode의 `evaluation_included=true AND result=SUCCESS` row에서 `total_completion_ms` 중앙값을 구한다. candidate median이 `≤0.75 × other median`이면 candidate를 채택한다.
3. 성공률 우세도 시간 우세도 없으면 `Aim`을 tie-break로 채택한다.

채택 후 다른 mode는 P0 범위에서 제거하며, 서로의 기능을 혼합한 제3안을 만들지 않는다.

### 9.6 1일 수정 1회

둘 다 PASS하지 못했지만 한 mode의 success rate가 `60% 이상 80% 미만`이고, 증거 3종으로 확정된 사용성 오해 code가 실패의 최빈 `player_failure_reason_code`라면 표시·문구만 `1 working day` 수정할 수 있다.

- 허용: cursor 상태, red invalid 가독성, Confirm 안내 문구
- 금지: mapping, reach, ink, thickness, physics, 이동, course, assist 변경
- 수정 후 두 mode가 아니라 **후보 mode 하나만** 새 `session_id`에서 전체 4명×9 trial로 한 번 재검증한다.

### 9.7 Stop

다음 중 하나면 Gate A FAIL로 P0 core 구현을 중단하고 입력 컨셉을 재설계한다.

- 두 mode 모두 success rate `<60%`
- 둘 다 `qualified participant ≤2/4`이고 1일 수정 조건도 불충족
- 증거 3종으로 확정된 사거리·drawing plane의 구조적 오해가 최빈 실패인데 표시 수정 후에도 mode PASS 실패
- mode session 재실행 뒤에도 정확한 72개 evaluation row와 완전한 연결 증거를 만들지 못함
- evidence pack이 `EVIDENCE_INVALID`이고 보완 또는 재실행으로 복구하지 못함

`Trajectory`만 FAIL하고 Aim이 PASS하면 컨셉 실패가 아니라 Trajectory 폐기다. Gate A FAIL 전에는 동적 낙하, 로컬 2인, rope/hinge로 범위를 넓히지 않는다.

## 10. Gate B — 동일 gamepad 2개 구성

Gate B는 Gate A PASS와 mode 채택 뒤에만 연다.

### 10.1 하드웨어·pairing·재연결

- 동일 제조사·동일 모델·동일 firmware의 gamepad `2개`
- 둘 다 유선 연결, 배터리·Bluetooth 차이 제거
- stick deadzone `0.15`, trigger actuation `0.10`, 진동 off
- Preflight에서 두 device의 serial을 조회하고 session의 `device_identity_mode`를 한 번 고정한다.
  - `SERIAL`: 두 serial이 모두 non-empty이고 서로 다름. Raw serial은 저장하지 않고 `serial_hash = SHA256(UTF8(serial))`만 manifest에 기록한다.
  - `NO_SERIAL`: 하나 이상이 empty이거나 두 값이 같아 장치를 유일하게 구분하지 못함. 두 장치의 시작 시 Input System device id는 기록하되 영구 identity로 간주하지 않는다.
- `Join`을 accepted한 순서로 `P1`, `P2`를 고정한다. `SERIAL`에서는 `player slot → serial_hash → current device id`, `NO_SERIAL`에서는 session 시작 시 `player slot → initial device id` binding을 유지한다.
- Disconnect 시 양 player 입력과 timer를 즉시 pause한다.
  - `SERIAL`: 기존 serial_hash와 같은 device만 원 slot에 re-pair하고 resume한다. 반대 slot이나 새 serial로 자동 대체하지 않는다. Timeout `30 s` 안에 두 binding이 복구되지 않으면 `TECH_INVALID/DEVICE_RECONNECT_TIMEOUT`으로 전체 10분을 폐기한다.
  - `NO_SERIAL`: 어느 device든 disconnect event가 발생하는 즉시 `TECH_INVALID/DEVICE_IDENTITY_UNAVAILABLE`로 해당 10분 전체를 폐기한다. 재연결 device의 description·manufacturer·product·연결 순서로 slot을 추정하지 않는다.
- Application focus loss는 device disconnect와 분리한다. 양 입력·timer를 pause하고, focus 복귀 뒤 기존 paired device id가 둘 다 여전히 paired인지 확인한다. 그대로면 resume하며, device id 변화나 disconnect event가 있으면 위 identity mode별 disconnect 정책을 적용한다.
- `TECH_INVALID` 종료 후에는 새 `session_id`에서 pair preflight와 10분 세션 전체를 재시작한다. 이전 세션 이벤트를 이어 붙이지 않는다.
- player별 색상, ownerId, ink ledger를 분리한다.
- gameplay 중 M+K 입력은 operator의 비상 종료 외 무시한다.

동일 제조사·모델·firmware의 gamepad 2개를 확보하지 못하면 Gate B는 `BLOCKED`다. Serial 미제공 자체는 시작 차단이 아니지만 `NO_SERIAL` session의 disconnect는 복구하지 않고 전체 재시작한다. 다른 두 기종을 섞은 세션으로 대체 판정하지 않는다.

### 10.2 Input System logical paths

Xbox glyph 이름을 정본 입력으로 사용하지 않는다. 아래 Unity Input System logical control path를 binding 정본으로 사용하며, 표시 glyph는 장치별 UI 표현일 뿐이다.

| 행동 | logical path |
|---|---|
| Join | `<Gamepad>/startButton` |
| 이동 | `<Gamepad>/leftStick` |
| Aim cursor | `<Gamepad>/rightStick` — Gate A 채택 mode가 Aim일 때만 사용 |
| 점프 | `<Gamepad>/buttonSouth` |
| Draw | `<Gamepad>/rightTrigger` hold |
| Confirm | `<Gamepad>/buttonWest` |
| Cancel | `<Gamepad>/buttonEast` |
| highlighted stroke 삭제 | `<Gamepad>/buttonNorth` |
| pair reset chord | 두 paired device 각각의 `<Gamepad>/leftShoulder` + `<Gamepad>/rightShoulder` |

Pair reset shoulder actuation threshold는 `0.50`으로 고정한다. **두 paired device의 네 shoulder control이 모두 `≥0.50`이 된 최초 monotonic timestamp**부터 `1.000 s` 연속 hold했을 때 1회 발생한다. 네 control 중 하나라도 `<0.50`으로 release되거나 paired device가 disconnect/focus loss 상태가 되면 hold timer를 즉시 `0`으로 reset한다. Reset 발화 후 네 control이 모두 `<0.50`으로 release되기 전에는 재발화하지 않는다.

Aim cursor 속도는 plane 위 `2.20 u/s`로 고정한다. 채택 mode가 Trajectory라면 `<Gamepad>/rightStick`으로 remote point나 hand steering을 만들지 않는다.

### 10.3 Gate B 시행·판정 경계

- `2쌍 × 10분`, pair마다 동일 코스·채택 mode 사용
- 각 player가 전체 committed stroke의 `30% 이상` 생성
- pair당 타인 stroke를 실제 이동에 사용한 사건 `3회 이상`
- 타인 stroke 삭제, 원소유자 환급, 구조 또는 실수 방해 사건을 로그·영상으로 기록
- Gate B 미달은 Gate A 결정을 뒤집지 않는다. 협력 hook만 유지/재설계로 판정한다.

## 11. 상위 실행계획 동기화 제안

상위 정본 `docs/prototype-execution-plan.md`는 본 개정 승인 시 아래 문구를 **정확히 대체**해야 한다. 본 DU-01 문서에서는 상위 정본을 직접 수정하지 않는다. 아래 `기존 문구` 코드 블록은 검색·교체 식별자일 뿐 활성 계약이 아니며, `대체 문구`만 승인 후 유효하다.

### 11.1 Gate A drawing plane 표현

§2 Gate A 필수 범위의 기존 문구:

```text
- 손을 지나는 camera-normal drawing plane
```

대체 문구:

```text
- 손을 지나는 camera yaw-normal vertical drawing plane: n=Normalize(ProjectOnPlane(camera.forward, Vector3.up)), up=Vector3.up, right=Normalize(Cross(up,n)); stroke 시작 시 hand origin과 n snapshot
```

DU-03B/C 수용 기준의 기존 문구:

```text
- Aim은 mouse를 hand 통과 camera-normal plane에 투영한다.
```

대체 문구:

```text
- Aim은 60 fps LateUpdate에서 mouse ray를 stroke 시작 시 hand origin과 camera yaw-normal n으로 snapshot한 world-vertical plane에 투영한다. Trajectory는 같은 LateUpdate에서 locomotion/physics 적용 후 고정 child HandMarker.position을 읽는다.
```

### 11.2 DU-03A Pending 상태

DU-03A 수용 기준의 기존 문구:

```text
- Idle→Drawing→Committed|Cancelled 상태머신과 stroke 시작 시 plane snapshot을 구현한다.
```

대체 문구:

```text
- Idle→Drawing→Pending→Committed|Cancelled 상태머신을 구현한다. release는 simplification 전 accepted resampled length로 Cancelled 또는 Pending을 결정하며, Pending에는 collider가 없고 명시적 Confirm만 capsule chain을 생성한다. stroke 시작 시 hand origin과 camera yaw-normal n을 snapshot한다.
```

DU-03A 수용 기준의 기존 문구:

```text
- hand-origin projection, reach validation, 거리 sampling, dedupe, simplification을 담당한다.
```

대체 문구:

```text
- 고정 child HandMarker와 60 fps LateUpdate candidate phase, hand-origin yaw-normal vertical plane projection, reach validation, 거리 resampling, dedupe, Douglas-Peucker 0.02u simplification을 담당한다.
```

### 11.3 DU-03A StrokeData 출처 분리

DU-03A 수용 기준의 기존 문구:

```text
- `StrokeData(points,length,ownerId,mode)`를 불변 데이터로 산출한다.
```

대체 문구:

```text
- `StrokeData(simplifiedPoints,chargedLength,ownerId,mode)`를 불변 데이터로 산출한다. `simplifiedPoints`는 Douglas-Peucker `0.02 u` 적용 후 collider geometry이고, `chargedLength`는 simplification 전 accepted resampled polyline length이며 `simplifiedPoints`에서 재계산하지 않는다.
```

### 11.4 DU-05A reserve 포함 불변식

DU-05A 수용 기준의 기존 문구:

```text
- `available + liveOwnedLength = inkCap` 불변식 테스트를 통과한다.
```

대체 문구:

```text
- owner별 `availableInk + drawingReservedLength + pendingReservedLength + Σ committedLiveOwnedChargedLength = inkCap` 불변식을 모든 transition 전후에 통과한다. charged length는 simplification 전 accepted resampled polyline length이며 Confirm은 pending reserve를 committed로 이전하고 Drawing/Pending Cancel과 committed Delete는 원소유자에게 정확히 환급한다.
```

## 12. 수용 기준 checklist

DU-01은 다음이 모두 확인될 때만 완료다.

- [x] reach `1.25 u`, radius `0.14 u`, ink cap `5.00 u`, spacing `0.08 u`, minimum length `0.20 u` 동결
- [x] **기술 검토 1 반영:** camera yaw-normal `n`, world `up`, `right`, spawn depth plane, world-Y gravity/jump와 `0.001 u` depth drift 판정 명시
- [x] **기술 검토 2 반영:** fixed-child HandMarker local pose, animation/IK/root-motion 금지, 양 adapter 60 fps `LateUpdate` 1회, execution order manifest 명시
- [x] **기술 검토 3 반영:** invalid release 판정, simplification 전 charged length, drawing/pending reserve 포함 닫힌 ledger transition 명시
- [x] **기술 검토 4 반영:** Confirm 후 segment별 local-Y capsule, radius/height/center/scale/isTrigger/cap overlap 및 Pending collider 없음 명시
- [x] **기술 검토 5 반영:** candidate/accepted/hand/plane/length/reserve typed columns, `sample_phase`와 null/invalid 의미, evidence manifest와 SHA-256 연결 명시
- [x] **기술 검토 6 반영:** Gate B Input System logical paths, 네 shoulder `1.000 s` chord, SERIAL/NO_SERIAL 재연결 정책과 전체 session 재시작 명시
- [x] **REV3 잔여 1 반영:** raw candidate별 prospective points·requiredInk 선계산, ink 부족 시 accepted 0/reserve 불변, 성공 시 전부 append하는 원자성 명시
- [x] **REV3 잔여 2 반영:** CANDIDATE_SAMPLE 처리 직전·ACCEPTED_POINT append 직후·transition 직후 ledger snapshot과 event sequence 재생 계약 명시
- [x] **REV3 잔여 3 반영:** `CapsuleCollider.direction=1 (Y-axis)` 명시 설정
- [x] **REV3 잔여 4 반영:** DU-03A `StrokeData(simplifiedPoints,chargedLength,ownerId,mode)` 상위 정본 대체안과 출처 분리 명시
- [x] FOV `60°`, camera·이동·점프·physics·60 fps profile 동결
- [x] Aim/Trajectory의 유일한 차이가 candidate point mapping임을 기술 검토
- [x] snap/auto-anchor/reach clamp/자동 Confirm 및 mode별 assist 금지
- [x] invalid reach red preview, append·ink 금지, 재진입 규칙 명시
- [x] release와 Confirm 분리, minimum 미만 ink 0 cancel, 명시적 cancel·delete·refund 명시
- [x] T1/T2/T3 geometry, 성공, timeout, reset 정본 명시
- [x] 4명 counterbalanced A/B, mode당 5분 연습+9 trial, 고정 evaluation row 72개 명시
- [x] participant qualified가 `성공≥7/9 AND Confirm median≤2000 ms AND reach+depth 오해≤2/9`이며 mode PASS가 qualified `≥3/4`임을 명시
- [x] Confirm latency의 monotonic backend timestamp, same-stroke 전이, key repeat/Pending 밖 제외, trial→participant median, non-null trial 7개 요건 명시
- [x] `TECH_INVALID` raw 보존·즉시 재시행·3연속 mode session 중단·새 session 9개 전체 재실행·세션 간 짜깁기 금지 명시
- [x] `attempts.csv`·`events.csv`·`strokes.csv`와 event·stroke·영상·사후질문 연결 계약 및 `EVIDENCE_INVALID` 명시
- [x] `result=SUCCESS|PLAYER_FAIL|TECH_INVALID`, player failure와 technical reason code 분리 및 사용성 오해 3증거 확정 규칙 명시
- [x] 두 mode PASS 선택에서 성공률 20%p 우선, 성공률 미결정 때만 SUCCESS 완료시간 중앙값 `≤0.75×` 적용, Aim tie-break 명시
- [x] Continue, 1일 수정 1회, Stop 기준 명시
- [x] Gate B 동일 gamepad 2개, device 고정 pair, 독립 ledger·색상 명시
- [x] Gate B 결과를 Gate A 근거와 분리
- [x] §11의 상위 `prototype-execution-plan.md` 대체 문구를 PM이 동기화
- [x] PM이 `InputComparisonSpec v1 Approved`를 기록

## 13. 트레이드오프와 P0 경계

- `1.25 u` reach와 `5.00 u` ink는 출시 밸런스가 아니다. Gate A 중 조정하면 비교가 무효가 되므로 체감이 다소 거칠어도 먼저 판정을 끝낸다.
- 고정 camera yaw-normal vertical plane은 진짜 3D 자유곡선을 포기하지만, mouse가 결정할 수 없는 depth를 제거하고 world-Y jump와 좌표계 충돌 없이 입력 가설을 검증한다.
- Trajectory의 순환 문제를 assist로 보정하지 않는다. 길을 만들기 위해 먼저 움직여야 하는 구조가 실제로 생존 가능한지를 보는 것이 비교 목적이다.
- Gate B의 gamepad adapter는 사회성 smoke용이다. 그 결과로 M+K mode를 재평가하지 않는다.
- P0에서는 동적 낙하·terrain anchor, rope/hinge, 궤도 카메라, drop shadow, IK, 온라인, 아트·사운드, 화살, 전투, 로그라이트, 저장을 본 규격에 추가하지 않는다.
