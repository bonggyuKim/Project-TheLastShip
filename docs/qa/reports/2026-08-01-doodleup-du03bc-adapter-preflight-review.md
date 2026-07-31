# DoodleUp DU-03B/C Adapter 착수 전 QA 전문 검토

- 검토일: 2026-08-01
- 검토자: `game-qa`
- 대상:
  - DU-03B Aim M+K adapter
  - DU-03C Trajectory M+K thin adapter
- 검토 범위: adapter 구현 acceptance 설계만 포함
- 제외 범위:
  - Gate A 본세션 evidence pack(`DU-06A-A`), 참가자 판정, mode 채택
  - DU-05A committed Delete/refund runtime API 및 `X` Delete 동작 acceptance
  - tester mouse provenance(DPI, Windows pointer speed, EPP, 물리 장치 식별) — DU-06A-A preflight로 이관
- 정합성 정정: 2026-08-01 — A12의 `X Delete` 실행 요구를 제거하고 mouse provenance 책임을 DU-06A-A로 분리
- 정본:
  - `D:\Project-DoodleUp\docs\input-comparison-spec-v1.md`
  - `D:\Project-DoodleUp\docs\prototype-execution-plan.md:81-89`
- 제한: 구현·제품 문서 수정 없음, git 작업 없음

## 1. 결론

# READY WITH ACCEPTANCE GATES

DU-03A REV1 완료로 DU-03B/C 착수를 막는 선행 차단요인은 없다. 단, 각 adapter 완료 판정은 단순히 candidate가 생성되거나 코스 3회 통과했다는 결과만으로 내리지 않는다. 다음 네 축을 모두 독립 증명해야 한다.

1. **mapping 정합성:** Aim은 mouse ray와 stroke-start snapshot plane의 교점만, Trajectory는 locomotion/physics 적용 후 fixed-child `HandMarker.position`만 candidate로 사용한다.
2. **phase·edge 정합성:** Input callback은 edge latch만 수행하고, `CandidateSampler=+100`의 실제 `LateUpdate`가 frame당 정확히 1개 sample을 제출한다. release frame도 candidate-first다.
3. **backend 비침범·parity:** adapter는 물리·잉크·소유권·spacing/reach/dedupe/resampling/simplification을 구현하거나 우회하지 않고 같은 DU-03A backend를 사용한다.
4. **회귀 무결성:** DU-03A 14 scenarios와 DU-02 sampling/reset/task-state가 그대로 PASS해야 한다.

### 책임자

- 구현 및 수정: `game-tech-director`
- 독립 acceptance 판정: `game-qa`
- 완료 승인: `project-manager`
- mapping·공정성 계약 변경 승인: `game-planning` + `project-manager`

### 다음 체크포인트

1. DU-03B와 DU-03C를 각각 별도 build/probe scenario로 제출한다.
2. QA가 adapter별 raw, runtime wiring, phase chain, parity, DU-03A/DU-02 회귀를 독립 재집계한다.
3. 두 adapter가 각각 PASS한 뒤에만 동일 코스 3회 통과 체크포인트로 이동한다.
4. Gate A 본세션 evidence pack은 별도 QA 사이클에서 검토한다.

### 현재 차단요인

- 착수 차단요인: **없음**
- 완료 수용 차단요인: 아래 필수 evidence 또는 필수 scenario 누락, mapping 외 backend 차이, 실제 callback 우회, 금지 assist 검출

## 2. 공통 정본 계약

정본상 두 adapter의 유일한 차이는 **candidate point 생성 mapping**이다.

| 항목 | DU-03B Aim | DU-03C Trajectory |
|---|---|---|
| Candidate source | mouse screen position → camera ray → snapshot plane intersection | locomotion/physics 반영 후 fixed-child `HandMarker.position` |
| Sample phase | 60 fps render frame `LateUpdate` | 동일 |
| Sample 수 | Drawing 중 frame당 정확히 1개 | 동일 |
| Plane | stroke 시작 시 hand origin + camera yaw-normal `n` snapshot | backend가 동일 snapshot plane에 projection/validation |
| Input edge | callback에서 latch만 | callback에서 latch만 |
| Backend | DU-03A 공통 backend | 동일 instance/config/backend |
| 금지 | current camera plane 재계산, endpoint clamp, hidden correction | cursor/mouse, remote point, independent hand steering, guide point |

공통 고정값과 실행 순서:

- `HandMarker.localPosition=(+0.35,+0.80,0.00)`
- `HandMarker.localRotation=identity`
- `HandMarker.localScale=(1,1,1)`
- animation/IK/root motion/procedural sway 금지
- `PlayerMotor=0`, `CameraRig=+50`, `CandidateSampler=+100`
- 처리 순서: press snapshot/start → candidate 1회 → release
- draw inactive 또는 candidate 비수신 state: `sample_phase=NONE`, candidate typed fields null
- 정상 sample: `sample_phase=LATE_UPDATE`, raw candidate non-null

## 3. 독립 acceptance 실행 구성

### 3.1 제출 단위

각 adapter는 다음을 별도 산출물로 제출한다.

- 독립 compile/scene/build/runtime 로그
- adapter acceptance raw CSV
- machine verifier report
- EditMode 및 PlayMode XML
- DU-03A regression raw/report
- DU-02 regression raw/report
- build binary, scene, profile, input-actions checksum

`input-actions` checksum은 adapter 관련 edge와 mapping 설정이 동일 build에 고정됐음을 증명한다. 향후 `X Delete` binding의 존재 여부는 inventory로만 기록하며 DU-03B/C에서 Delete action 소비나 backend API를 요구하지 않는다. 물리 mouse vendor/DPI/Windows pointer/EPP provenance는 이 제출 단위의 필수 acceptance 항목이 아니다.

Aim과 Trajectory를 한 행에서 mode flag만 바꿔 synthetic 계산한 결과는 수용하지 않는다. 실제 scene component wiring을 mode별로 확인하고 실제 runtime callback을 통과해야 한다.

### 3.2 실행 환경

- Unity `6000.4.0f1` 또는 승인된 동일 프로젝트 버전
- Input System package version 기록
- Windows x64 Development standalone
- `1920×1080`, Perspective, vertical FOV `60°`
- 60 fps cap, VSync off
- `fixedDeltaTime=0.020 s`
- camera distance/height/pitch는 정본 profile과 일치
- Deterministic Aim mapping acceptance는 주입한 `screen_position`, camera matrix/transform, snapshot plane으로 좌표를 재현하므로 tester의 물리 mouse vendor, DPI, Windows pointer `6/11`, EPP 상태를 완료 조건으로 요구하지 않는다.
- Tester mouse provenance는 사람 대상 Gate A 입력 조건과 manifest 무결성을 위한 `DU-06A-A` preflight에서 확인한다.
- 해상도/FOV/camera/profile 불일치는 mapping 골든값을 오염시키므로 adapter acceptance에서 FAIL 또는 재실행한다. 물리 mouse provenance 누락만으로 deterministic adapter acceptance를 `TECH_INVALID` 처리하지 않는다.

## 4. DU-03B Aim adapter 필수 scenario

| ID | 절차 | 기대 결과·골든값 | 차단 조건 |
|---|---|---|---|
| A01 Press snapshot | camera yaw가 pitch를 포함한 상태에서 LMB press를 latch하고 실제 `LateUpdate` 실행 | `plane_origin=press tick HandMarker.position`; `plane_normal=Normalize(ProjectOnPlane(camera.forward,up))`; origin/normal snapshot 1회 | current camera normal 사용, pitch 포함 normal, callback에서 session 직접 변경 |
| A02 Center ray intersection | screen center 및 알려진 screen point를 제공 | runtime ray와 snapshot plane의 독립 교점이 raw candidate와 좌표 오차 `≤1e-5 u` | accepted point를 candidate로 기록, ray 교점과 불일치 |
| A03 Off-center 2D mapping | 좌/우/상/하 screen point sequence 제출 | plane local `right/up` 방향·부호가 정본과 일치, candidate가 snapshot plane 위 `abs(dot(p-origin,n))≤1e-5` | 축 반전, world horizontal plane, hidden clamp |
| A04 Camera-change freeze | stroke 시작 후 camera transform을 변경하고 같은 screen point sample | plane origin/normal은 press snapshot을 유지; 새 camera transform은 ray 생성에만 반영되고 plane 재계산 없음 | stroke 중 plane origin/normal 변경 |
| A05 No intersection | plane과 평행한 ray가 되도록 deterministic camera/screen probe | candidate xyz null, `candidate_valid=false`, `candidate_invalid_reason=NO_PLANE_INTERSECTION`, ledger/state 불변 | `(0,0,0)` 대입, stale candidate 재사용, 예외 |
| A06 Non-finite guard | deterministic mapping seam으로 non-finite ray/intersection 입력 | candidate xyz null, `NON_FINITE`, append 0, ledger/state 불변 | NaN raw 저장, backend mutation |
| A07 Frame cardinality | Drawing 120 render frames 동안 고정 mouse 입력 | 각 frame별 `CANDIDATE_SAMPLE` 정확히 1개, duplicate/missing 0, monotonically increasing frame/sequence | Update+LateUpdate 이중 sample, callback sample |
| A08 Release-frame candidate-first | release latch frame에 non-trivial 새 mouse point 제공 | event order `CANDIDATE>RELEASE`, candidate count 1, 해당 candidate가 length/terminal branch에 반영 | release 선처리, sample 누락 |
| A09 Inactive/Pending behavior | Idle, Pending, Confirm 직후에 mouse 이동 | `sample_phase=NONE`, candidate fields null, backend mutation 없음 | Pending에서도 candidate 제출 |
| A10 Reach recovery | reach 밖 mouse point 후 범위 안 point 제출 | 첫 sample `REACH_INVALID`, append/reserve 불변; 다음 valid sample은 마지막 accepted point부터 재개 | reach clamp 또는 invalid 뒤 state 오염 |
| A11 Ink atomic | 남은 잉크보다 여러 resample point가 필요한 mouse point 제출 | `INK_INVALID`, accepted 0개, points/length/ledger 전부 불변 | 부분 append |
| A12 Adapter input edges | LMB/E/RMB 또는 Esc/R을 실제 Input System edge로 실행 | Draw/Confirm/Cancel/Reset edge가 callback에서 latch되고 정본 LateUpdate/reset 경로에서 정확히 1회 소비; hold 중 repeated edge 0 | legacy polling 혼용, callback에서 backend 직접 변경, repeated hold edge |

### Aim 합격 핵심

- raw candidate는 `Camera.ScreenPointToRay`와 snapshot plane 교점의 독립 계산값과 일치해야 한다.
- ray 실패 시 candidate 필드는 숫자 0이 아니라 null이다.
- adapter가 reach clamp, smoothing, snapping, target attraction을 수행하면 즉시 FAIL이다.
- `X`는 이번 카드에서 committed Delete를 실행하면 안 된다. Input Actions asset에 향후 `X Delete` binding이 미리 존재하는지는 비차단 inventory로 기록할 수 있으나, DU-03B/C acceptance에서는 callback 소비·backend API 호출·refund를 요구하거나 검증하지 않는다.
- `X Delete`의 실제 동작, LIFO 대상 선택, audit log, 원소유자 refund는 `DU-05A` 완료 acceptance로 이관한다.

## 5. DU-03C Trajectory adapter 필수 scenario

| ID | 절차 | 기대 결과·골든값 | 차단 조건 |
|---|---|---|---|
| T01 Fixed-child pose | scene load/reset 후 HandMarker hierarchy/local pose 검사 | player root의 fixed child, local position/rotation/scale 정본값, animation/IK/root-motion 영향 0 | 독립 world object, pose writer 존재 |
| T02 Post-motor sample | A/D 이동과 jump를 deterministic하게 실행하고 같은 frame의 motor 후 marker 및 candidate 기록 | candidate = 해당 `LateUpdate`의 `HandMarker.position`, 좌표 오차 `≤1e-5 u`; motor/physics 적용 뒤 sample | 이전 frame marker, Update 선샘플 |
| T03 Horizontal trajectory | +right와 -right 이동 중 draw hold | candidate delta의 수평 부호/크기가 marker delta와 동일, depth drift `≤0.001 u` | cursor/remote offset, 목표 방향 보정 |
| T04 Vertical trajectory | jump 상승/하강 중 draw hold | candidate Y가 marker Y와 동일하고 gravity 결과를 따라감 | 예측점·smoothing·trajectory guide 사용 |
| T05 Stationary dedupe | marker 정지 상태로 여러 frame draw | 매 frame sample은 1개이나 backend 결과는 `DEDUPE` 또는 `SPACING_NOT_REACHED`, accepted/reserve 불필요 증가 없음 | synthetic movement, frame당 point 강제 추가 |
| T06 Cursor independence | mouse를 극단적으로 이동시키며 marker trajectory는 동일하게 재생 | candidate sequence/hash가 mouse 입력 유무와 완전히 동일 | mouse/right-stick이 candidate에 영향 |
| T07 No independent hand steering | 이동 입력 없이 cursor/right-stick/기타 steering 입력 | HandMarker/candidate 이동 0, remote point 생성 0 | hand steering 또는 guide point 생성 |
| T08 Frame cardinality | Drawing 120 render frames 동안 locomotion sequence 실행 | 실제 `LateUpdate`마다 `CANDIDATE_SAMPLE` 정확히 1개, duplicate/missing 0 | FixedUpdate sample 수에 종속, callback 직접 sample |
| T09 Release-frame post-motor candidate | release latch와 같은 frame에 motor로 marker를 이동 | event order `CANDIDATE>RELEASE`; terminal length가 release-frame marker sample을 포함 | release 선처리, pre-motor sample |
| T10 Inactive/Pending behavior | Idle/Pending에서 이동·jump | `sample_phase=NONE`, candidate null, stroke backend mutation 없음 | draw inactive sample row를 정상 candidate로 기록 |
| T11 Reach recovery | 이동으로 marker를 reach 밖으로 보낸 후 범위 안으로 복귀 | `REACH_INVALID`에서 append/ledger 불변; 복귀 후 마지막 accepted point부터 재개 | clamp/auto-anchor |
| T12 Reset pose/path | Drawing/Pending 및 이동 중 R reset | player/velocity/phase/HandMarker fixed pose 및 StrokeSession canonical state 복원; stale sample 0 | reset 직후 이전 marker/candidate 소비 |

### Trajectory 합격 핵심

- 각 sample에서 `candidate_x/y/z == hand_x/y/z`가 오차 `≤1e-5 u`로 성립해야 한다.
- mouse/cursor/right-stick 변화에 candidate sequence가 영향을 받지 않아야 한다.
- marker 이외의 remote point provider, guide point, independent steering, prediction이 하나라도 발견되면 FAIL이다.

## 6. 공통 edge·state scenario

두 adapter 모두 아래 공통 scenario를 실제 adapter 입력 경로로 통과해야 한다.

1. press와 candidate가 같은 frame
2. candidate와 release가 같은 frame
3. accepted length `<0.20 u` release → Cancelled/Idle 및 전액 환급
4. accepted length `≥0.20 u` release → Pending, collider 0
5. Drawing Cancel
6. Pending Cancel
7. Pending 중 새 Draw 거부
8. Idle Confirm no-op
9. Drawing 상태의 Confirm+release same frame → Confirm 거부 후 candidate→release
10. Pending Confirm → capsule chain 생성 및 ledger commit
11. reset 중 held input → canonical reset 후 stale edge 재소비 없음
12. focus loss/device disconnect → latch 해제 또는 `TECH_INVALID`, stuck draw 없음

## 7. Adapter acceptance raw schema

Gate A 본세션의 `attempts.csv/events.csv/strokes.csv` 전체 팩은 이번 범위 밖이다. 다만 adapter acceptance raw는 향후 본세션 schema와 의미가 충돌하지 않아야 한다. 최소 두 파일을 권고한다.

### 7.1 `DU03BC_Adapter_Runtime_Raw.csv`

```text
scenario,adapter_mode,render_frame,late_update_sequence,sample_index_in_frame,
input_event_seq,input_control,input_phase,draw_pressed_latched,draw_released_latched,
confirm_latched,cancel_latched,session_state_before,session_state_after,
sample_phase,event_order,mapping_source,
mouse_screen_x,mouse_screen_y,ray_origin_x,ray_origin_y,ray_origin_z,
ray_direction_x,ray_direction_y,ray_direction_z,ray_intersection_t,
hand_x,hand_y,hand_z,marker_local_x,marker_local_y,marker_local_z,
plane_origin_x,plane_origin_y,plane_origin_z,
plane_normal_x,plane_normal_y,plane_normal_z,
raw_candidate_x,raw_candidate_y,raw_candidate_z,
independent_expected_x,independent_expected_y,independent_expected_z,mapping_error,
candidate_valid,candidate_invalid_reason,accepted_appended,appended_point_count,
accepted_count_before,accepted_count_after,length_before,length_after,
available_before,available_after,drawing_before,drawing_after,pending_before,pending_after,
backend_instance_id,backend_profile_hash,adapter_config_hash,
depth_drift,mouse_influence_detected,remote_point_detected,atomic_unchanged,result
```

### null 규칙

- Aim의 mouse/ray 필드는 정상 mapping 시 필수, Trajectory에서는 null이다.
- Trajectory의 hand 및 marker pose 필드는 Drawing sample에서 필수다.
- raw candidate를 계산하지 못한 경우 candidate xyz와 expected xyz는 null이어야 한다.
- 미측정/null을 숫자 `0`으로 표현하지 않는다.
- `sample_phase=NONE`이면 candidate·ray·expected typed fields는 null이다.

### 7.2 `DU03BC_Adapter_Verification_Report.txt`

최소 집계:

```text
adapter=<Aim|Trajectory>
raw=<absolute path>
rawSha256=<sha256>
binarySha256=<sha256>
sceneSha256=<sha256>
profileSha256=<sha256>
inputActionsSha256=<sha256>
framesObserved=<n>
samplesObserved=<n>
duplicateFrames=<n>
missingFrames=<n>
mappingMaxError=<float>
releaseCandidateFirst=<True|False>
backendParity=<PASS|FAIL>
du03aRegression=<PASS|FAIL>
du02Regression=<PASS|FAIL>
scenarios=<n>
result=<PASS|FAIL>
```

## 8. 필수 runtime 로그 체인

로그 문자열만으로 PASS를 주지는 않지만 raw와 대조 가능한 다음 machine-readable chain이 필요하다.

```text
[DU03BC_INPUT] frame=... seq=... mode=... control=... phase=... latched=...
[DU03BC_SAMPLE] frame=... lateSeq=... mode=... sampleIndex=1 source=MOUSE_RAY|HAND_MARKER phase=LATE_UPDATE
[DU03BC_MAPPING] frame=... source=... candidate=... expected=... error=... reason=...
[DU03A_CANDIDATE] ...
[DU03A_STATE] before=... after=... reason=...
[DU03A_LATE_UPDATE] renderFrame=... sequence=... candidateCount=... order=...
```

필수 관찰 규칙:

- 같은 render frame의 input latch → adapter sample → DU-03A candidate/state event를 sequence로 연결한다.
- release frame은 `sampleIndex=1`과 `order=CANDIDATE>RELEASE`를 남긴다.
- callback 또는 `Update`에서 backend mutation 로그가 나오면 FAIL이다.
- direct helper 실행은 `sample_phase=DIRECT` 또는 별도 `execution_path=DIRECT`로 표시한다. 실제 callback evidence와 혼합하지 않는다.

## 9. 후속 카드로 이관한 항목

### 9.1 DU-05A — committed Delete/refund

DU-03B/C는 candidate mapping-only adapter다. 따라서 다음은 DU-03B/C 수용 범위가 아니다.

- `X` 입력으로 최근 자기 committed stroke 선택
- committed stroke 삭제 backend API
- live committed ledger 제거
- 원소유자 charged length 환급
- delete audit log
- delete→redraw 불변식

DU-03B/C에서 위 기능을 새로 구현하거나 adapter가 backend API를 직접 호출하면 DU-03A immutable/backend 경계 위반으로 **FAIL**이다. `X Delete`는 DU-05A에서 기능·binding·LIFO·refund를 함께 수용한다.

### 9.2 DU-06A-A — tester mouse provenance

다음은 deterministic adapter mapping의 수학적 정확성보다 사람 대상 세션의 입력 조건·재현성에 관한 provenance이므로 DU-06A-A preflight로 분리한다.

- mouse vendor/product
- `800 DPI`
- Windows pointer `6/11`
- Enhance Pointer Precision off
- in-game sensitivity `1.00`
- manifest 환경값과 실제 tester 장치의 일치

DU-03B Aim acceptance는 deterministic screen-position 주입과 runtime camera/plane 값으로 mapping을 검증한다. 따라서 위 tester provenance가 아직 수집되지 않았다는 이유만으로 DU-03B를 막지 않는다. 단, 실제 LMB edge가 Input System을 통해 latch되는지는 adapter 구현 acceptance에 남긴다.

## 10. Backend parity 판정

### 10.1 동일해야 하는 항목

동일한 world-space candidate sequence를 adapter mapping 직후 seam에 재생했을 때 Aim과 Trajectory는 다음이 동일해야 한다.

- DU-03A backend instance type 및 profile hash
- candidate valid/reason 및 appended point sequence
- accepted resampled points와 cumulative length
- available/drawing/pending/committed ledger 전이
- release terminal branch
- simplified points와 `chargedLength`
- Pending collider 0 및 Confirm geometry
- Cancel/reset/invalid atomic 결과

### 10.2 달라도 되는 항목

- raw candidate를 만들기 전의 source data
  - Aim: screen position, ray origin/direction, intersection `t`
  - Trajectory: HandMarker world position, motor/physics frame state

### 10.3 차단 기준

다음 중 하나면 **CHANGES REQUIRED**다.

- adapter별 backend fork, 별도 tolerance/smoothing/reach/ink 값
- mode별 state/ledger/geometry 코드 경로
- Aim/Trajectory 동일 candidate sequence에서 backend 결과 불일치
- adapter가 accepted point 또는 ledger를 직접 수정
- DU-03B/C adapter가 committed Delete/refund API를 구현하거나 호출
- mode별 hidden assist

## 11. 회귀 기준

### 11.1 DU-03A 필수 회귀

- 기존 14 scenario exact set: **14/14 PASS**
- raw 50열 및 report hash 정합
- ledger total `5.00 u`
- invalid atomicity
- Pending collider 0, Confirm geometry 골든값
- real `LateUpdate` release-frame candidate-first
- Aim/Trajectory backend parity
- EditMode DU-03A **14/14 PASS**

Adapter 추가 때문에 deterministic DU-03A probe가 실제 adapter로 대체되거나 의미가 바뀌면 안 된다. 공통 backend regression과 adapter mapping acceptance는 분리 유지한다.

### 11.2 DU-02 필수 회귀

- standalone sampling:
  - 30 fps `300/300`
  - 60 fps `600/600`
  - 144 fps는 10초 관찰에서 frame/sample 1:1
  - elapsed `≥10 s`, duplicate/missing 0
- T1/T2/T3 × R_KEY/LANE_SELECT reset **6/6 PASS**
- rotation/angular velocity/task phase 실제 교란 후 canonical 복원
- task-state **4/4 PASS**
- EditMode DU-02 **12/12 PASS**
- PlayMode **2/2 PASS**

### 11.3 Adapter 자체 테스트 최소선

각 adapter별:

- EditMode mapping/edge/null/invalid 테스트 최소 8건
- PlayMode 실제 Input System latch→LateUpdate wiring 최소 3건
- standalone runtime 필수 scenario 전부 PASS
- compile error, runtime exception, assertion failure 0

테스트 개수는 보조 지표이며 필수 scenario나 raw 계약을 대체하지 않는다.

## 12. 차단요인·결함 우선순위

### BLOCKER

- 실제 scene adapter가 아닌 deterministic source/direct helper만 검증
- frame당 0개 또는 2개 이상 candidate sample
- release가 candidate보다 먼저 처리됨
- Aim plane이 press snapshot을 유지하지 않음
- Trajectory candidate가 `HandMarker.position`과 불일치
- Trajectory에 cursor/remote point/independent steering 존재
- adapter가 backend/ledger/geometry를 직접 변경
- DU-03B/C에서 committed Delete/refund 동작을 구현하거나 backend API를 호출
- mapping 외 mode 차이 또는 hidden assist

### HIGH

- raw candidate와 accepted point가 구분되지 않음
- null/invalid reason 또는 before/after ledger가 없어 원자성 재집계 불가
- backend profile/hash parity 증거 누락
- DU-03A 또는 DU-02 회귀 실패
- reset/focus loss에서 stale edge나 stuck draw 발생

### MEDIUM

- manifest에 execution order, HandMarker pose, input-actions/profile checksum 누락
- mapping error 골든값 또는 mouse-independence 비교가 machine-readable하지 않음
- direct row와 callback row의 execution path가 모호함

## 13. 카드별 최종 수용 기준

### DU-03B PASS 조건

1. A01~A12 전부 PASS
2. 실제 mouse Input System edge가 latch되고 실제 `LateUpdate`에서 소비됨
3. 모든 정상 raw candidate가 독립 ray-plane 교점과 `≤1e-5 u` 오차
4. no-intersection/non-finite null 규칙과 원자성 PASS
5. snapshot plane freeze, 1 sample/frame, release candidate-first PASS
6. backend parity 및 DU-03A/DU-02 회귀 PASS
7. 금지 assist 0건

### DU-03C PASS 조건

1. T01~T12 전부 PASS
2. 실제 locomotion/physics 후 fixed-child HandMarker를 실제 `LateUpdate`에서 읽음
3. 모든 sample에서 candidate와 HandMarker world position 오차 `≤1e-5 u`
4. cursor/mouse influence 및 remote point/independent steering 0건
5. 1 sample/frame, release-frame post-motor candidate-first, reset stale input 0건
6. backend parity 및 DU-03A/DU-02 회귀 PASS
7. depth drift `≤0.001 u`, 금지 assist 0건

## 14. QA 사인오프

- 착수 준비도: **READY WITH ACCEPTANCE GATES**
- DU-03A 선행 차단: 해소
- DU-03B Aim acceptance 절차: 정의 완료
- DU-03C Trajectory acceptance 절차: 정의 완료
- raw schema/로그 계약: 정의 완료
- backend parity 기준: 정의 완료
- DU-03A/DU-02 회귀 기준: 정의 완료
- Gate A 본세션 evidence pack: 의도적으로 제외
- X Delete/refund acceptance: DU-05A로 이관
- Tester mouse DPI/Windows pointer/EPP provenance: DU-06A-A preflight로 이관
- 구현·제품 문서 수정: 수행하지 않음
- git 작업: 수행하지 않음

## 15. QA 프로세스 노트

- 코스 성공은 mapping correctness를 단독 증명하지 않으므로 좌표 골든값, phase cardinality, edge order, forbidden influence를 별도 검증한다.
- Aim과 Trajectory의 source data가 다르므로 동일 screen/movement를 억지로 비교하지 않는다. mapping 이후 동일 world candidate sequence에서 backend parity를 검증한다.
- `LateUpdate`라는 method명이나 sequence 증가만으로 실제 callback을 인정하지 않는다. scene wiring, input latch, frame, sample index, backend event order를 함께 확인한다.
- Gate A 본세션의 참가자·attempts/strokes/video evidence는 이번 adapter 구현 acceptance에 포함하지 않았다.
- `X Delete`는 정본 조작표에 존재하지만 구현 의존성은 DU-05A다. 입력 표의 존재를 현재 카드 구현 의무로 확대하지 않고 inventory와 기능 acceptance를 구분했다.
- Mouse DPI/Windows pointer/EPP는 실제 tester 이동량에 영향을 주지만 deterministic screen-position→world mapping의 수학적 정확성에는 필요하지 않다. 따라서 실제 LMB edge 검증은 DU-03B에 유지하고 장치 provenance는 DU-06A-A preflight로 분리했다.
