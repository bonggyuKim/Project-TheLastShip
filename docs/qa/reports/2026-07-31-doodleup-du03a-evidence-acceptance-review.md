# DoodleUp DU-03A 독립 Evidence 수용 검토

- 검토일: 2026-07-31
- 검토자: `game-qa`
- 대상: DU-03A 공통 StrokeSession backend 및 제출 evidence
- 최종 판정: **CHANGES REQUIRED**
- 정본:
  - `D:\Project-DoodleUp\docs\input-comparison-spec-v1.md`
  - `D:\Project-DoodleUp\docs\prototype-execution-plan.md:70-79`
- 제한: 제품 코드·제품 문서 수정 없음, git 작업 없음

## 1. 최종 판정

# CHANGES REQUIRED

제출된 6개 runtime scenario와 21개 EditMode 테스트는 상태·잉크 원자성의 상당 부분을 확인하지만, 정본 DU-03A 수용 범위를 충족하는 실제 runtime evidence로는 부족하다.

수용 차단 결함은 3건이다.

1. **Confirm이 정본 capsule chain을 생성하지 않는다.**
2. **실제 60 fps `LateUpdate`/DrawIntent 경로를 검증하지 않고 probe가 `ProcessIntent`를 직접 호출한다.**
3. **raw가 환급·reset 후 최종 ink/reserve/point 불변값을 담지 않아 독립 재집계로 핵심 계약을 증명할 수 없다.**

## 2. 검증 환경과 제출 증거

- OS: Windows 11 Education `10.0.26200`
- Unity: `6000.4.0f1`
- raw: `D:\Project-DoodleUp\DU03A_Runtime_Raw.csv`
- report: `D:\Project-DoodleUp\DU03A_Verification_Report.txt`
- verification guide: `D:\Project-DoodleUp\docs\qa\du-03a-verification.md`
- compile: `D:\.adk\logs\unity\compile-20260731-230954.log`
- scene: `D:\.adk\logs\unity\method-20260731-231011.log`
- build: `D:\.adk\logs\unity\build-20260731-231025.log`
- runtime: `D:\.adk\logs\unity\du03a-standalone-runtime-final-20260731.log`
- aggregator: `D:\.adk\logs\unity\method-20260731-231137.log`
- EditMode: `D:\.adk\logs\unity\test-20260731-231205.log`
- PlayMode: `D:\.adk\logs\unity\test-20260731-231218.log`
- DU-02 regression: `D:\.adk\logs\unity\method-20260731-231152.log`

QA 독립 SHA256:

| 산출물 | SHA256 |
|---|---|
| DU-03A raw | `6f797fbe8086d4d9fa117c0430ea7e736b742f71a44939ee9dc751a432702a4a` |
| DU-03A report | `1f01bf22074fe61a66756260e707197473ec67f4dc98e4d924a8d92454087fc9` |
| DU-02 regression raw | `3b9d0fcafa89b792b95be2f99ba875079768bd17b1241a4dacc3a48340b01c9a` |
| DU-02 regression report | `c52f1a1427efae88c23da6a15cdb3d9f917150d00b2baad9af44c1945dd94b5d` |

DU-03A raw hash는 제출 report의 `rawSha256`와 일치한다. QA 독립 파싱 결과는 20열, 6행, 필수 scenario set 일치, `result!=PASS` 0행, `candidate_valid=False AND accepted_appended=True` 0행이다.

## 3. 요청 항목별 판정

| 검토 항목 | 판정 | 근거 |
|---|---|---|
| 1. raw SHA256/report 재집계 | **PASS** | raw SHA256 독립 일치, 6개 scenario set과 report 집계 일치 |
| 2. 상태 전이, Pending collider=0, Confirm-only commit | **FAIL** | Pending collider=0 및 명시 Confirm commit은 확인했으나 Confirm 후 필수 capsule chain 생성이 전혀 없음 |
| 3. short cancel 환급, pending confirm/cancel, reach/ink all-or-nothing, R reset pending | **CHANGES REQUIRED** | 실행 코드 assertion은 존재하나 raw의 `ink_after`가 candidate 직후 값이라 cancel/reset 후 환급값을 독립 증명하지 못함 |
| 4. invalid candidate 불변·금지 조합 부재 | **부분 PASS** | 코드와 tests로 length/ink/point 불변 및 금지 조합 부재 확인. raw에는 reserve와 point 전후값이 없음 |
| 5. chargedLength가 simplification 전 length | **PASS** | 생성 코드가 `AcceptedLength`를 보존하고 EditMode 곡선 케이스가 simplified geometry 합과 차이를 검증 |
| 6. snapshot/plane/reach/resampling/dedupe/DP/LateUpdate phase | **FAIL** | 알고리즘 코드와 EditMode 검증은 확인했으나 실제 scene의 intent source가 null이고 runtime probe가 `LateUpdate`를 우회 |
| 7. DU-02 회귀·DU-03A 이후 기능 미선행 | **PASS** | DU-02 runtime/aggregator/tests PASS, Aim/Trajectory 실제 adapter 및 후속 기능 없음 |

## 4. 확인된 정상 계약

### 4.1 상태 및 잉크 backend

다음 구현은 정본과 일치한다.

- `Du03AStrokeSession.cs:154-175`: Idle에서 시작 시 hand origin과 camera yaw-normal plane을 snapshot하고 origin을 첫 accepted point로 삽입
- `Du03AStrokeSession.cs:177-209`: projection → reach → dedupe → resampling → prospective ink 검증 → 전량 append 순서
- `Du03AStrokeSession.cs:200-209`: required ink 부족 시 mutation 전에 반환하므로 all-or-nothing
- `Du03AStrokeSession.cs:212-230`: `<0.20 u` cancel, `>=0.20 u` Pending
- `Du03AStrokeSession.cs:233-245`: 명시 Confirm만 committed data 생성
- `Du03AStrokeSession.cs:248-269`: Drawing/Pending cancel과 reset
- `Du03AStrokeSession.cs:288-299`: cancel 시 drawing/pending reserve 전액 환급
- `Du03AStrokeSession.cs:352-359`: owner ledger 닫힌 식 검증

### 4.2 chargedLength와 simplification

**PASS**

- `Du03AStrokeSession.cs:221-227`은 simplification 결과와 별개로 pre-simplification `AcceptedLength`를 `Du03AStrokeData.ChargedLength`에 전달한다.
- `Du03AStrokeSessionTests.cs:136-152`는 curved accepted path의 charged length가 `0.24`이고 simplified 2-point geometry 길이 합과 실제로 다름을 assertion한다.
- EditMode XML에서 `ChargedLengthIsNotRecomputedFromSimplifiedGeometry` PASS를 확인했다.

### 4.3 invalid candidate 원자성

코드 수준 판정은 **PASS**다.

- ReachInvalid: mutation 전 `Du03AStrokeSession.cs:189-190`에서 반환
- Dedupe/spacing: valid+not-appended (`Du03AStrokeSession.cs:191-198`)
- InkInvalid: prospective 전체 required ink 계산 후 mutation 전 반환 (`Du03AStrokeSession.cs:194-203`)
- 금지 조합 `candidate_valid=False AND accepted_appended=True`: raw 0행
- Runtime:
  - `reach_atomic`: length `0.16→0.16`, ink `4.84→4.84`, append 0
  - `ink_atomic`: length `0→0`, ink `0.15→0.15`, append 0, required `0.24`

다만 raw가 accepted point count와 drawing/pending reserve 전후값을 저장하지 않아 evidence 계약은 보강이 필요하다.

## 5. 수용 차단 결함

### DU03A-R1 — Confirm 후 capsule chain이 생성되지 않음

- 심각도: **BLOCKER**
- 영향: committed stroke에 물리 geometry가 없어 DU-03A의 핵심 산출물과 DU-02 task traversal 연결이 불가능
- 조건:
  1. accepted length `>=0.20 u` stroke를 release해 Pending으로 전환
  2. Confirm 입력
  3. committed stroke root/child collider 확인
- 기대 결과:
  - `Pending → Committed` transaction에서 simplified point pair마다 child `CapsuleCollider` 생성
  - `direction=1`, `radius=0.14`, `height=segmentLength+0.28`, `isTrigger=false`, scale `(1,1,1)`
  - Pending 이전에는 collider 0, Confirm 후 non-degenerate segment 수와 collider 수 일치
- 실제 결과:
  - `Du03AStrokeSession.cs:233-245`는 immutable data를 list에 추가할 뿐 geometry 생성 호출이 없다.
  - `Du03AStrokeDriver.cs:95-104`는 Confirm 후에도 `colliderCreated=False seamOnly=True`를 기록한다.
  - 프로젝트 runtime 코드 전체에서 stroke capsule 생성 구현이 없다.
  - runtime `pending_confirm` 행의 `committed_count=1`이지만 `collider_count=0`이다.
- 위반 정본:
  - `prototype-execution-plan.md:76`: 명시 Confirm만 capsule chain 생성
  - `input-comparison-spec-v1.md:184-196`: committed capsule chain geometry 및 Confirm transaction 계약
- 수용 기준:
  1. Pending collider/root/Rigidbody 0을 유지한다.
  2. Confirm 직후 simplified polyline의 non-degenerate pair마다 정확히 1개 capsule을 생성한다.
  3. radius/height/direction/trigger/scale 골든값을 machine-readable raw와 runtime object inspection으로 검증한다.
  4. Confirm 외 release/cancel/reset 경로에서는 capsule 생성이 0건이어야 한다.
- 수정 책임자: `game-tech-director`

### DU03A-R2 — 실제 LateUpdate/DrawIntent 경로를 runtime evidence가 우회

- 심각도: **BLOCKER**
- 영향: 정본의 frame phase, 입력 latch 순서, frame당 1 candidate, press→candidate→release 순서를 standalone에서 증명하지 못함
- 조건:
  1. 제출 scene/build 실행
  2. `Du03AStrokeDriver`의 intent source와 runtime probe 호출 경로 확인
- 기대 결과:
  - 실제 `IDu03ADrawIntentSource`가 edge를 latch
  - `[DefaultExecutionOrder(100)]`의 `LateUpdate`가 frame당 한 번 `ReadIntent()` 및 candidate 처리
  - press snapshot/start → candidate 1회 → release 순서를 frame/sequence/raw로 확인
- 실제 결과:
  - `Du02SceneBuilder.cs:51-52`가 `strokeDriver.Configure(..., null, ...)`로 intent source를 null로 연결한다.
  - `Du03AStrokeDriver.cs:135-140`의 실제 `LateUpdate`는 source가 null이므로 즉시 반환한다.
  - `Du03ARuntimeProbeRunner.cs:61-69`가 `ProcessIntent`를 직접 호출한다.
  - `ProcessIntent`는 호출 위치와 무관하게 `LateUpdateSequence++`하므로 (`Du03AStrokeDriver.cs:79-83`) sequence 자체가 LateUpdate 실행 증거가 아니다.
  - raw schema에는 render frame, sample phase, press/release latch 순서, samples-per-frame가 없다.
- 위반 정본:
  - `prototype-execution-plan.md:77`
  - `input-comparison-spec-v1.md:132-150`
- 수용 기준:
  1. 실제 runtime intent source를 연결한다. adapter 고유 mapping은 DU-03B/C 범위를 선행하지 않도록 deterministic probe source여도 되지만 반드시 `LateUpdate`를 통해 소비돼야 한다.
  2. 최소 press/release 포함 scenario에서 `render_frame`, `late_update_sequence`, `sample_phase=LATE_UPDATE`, `candidate_count_this_frame=1`, 처리 순서를 raw로 남긴다.
  3. release frame도 candidate가 먼저 처리됨을 non-trivial 입력으로 증명한다.
  4. direct method invocation 결과를 실제 LateUpdate 결과로 표시하지 않는다.
- 수정 책임자: `game-tech-director`

### DU03A-R3 — raw가 cancel/reset 후 최종 환급 상태를 기록하지 않음

- 심각도: **HIGH / 수용 차단**
- 영향: report 재집계만으로 short cancel, pending cancel, R reset의 잉크·reserve 환급을 독립 확인할 수 없음
- 조건:
  1. `DU03A_Runtime_Raw.csv`의 `short_cancel`, `pending_cancel`, `r_reset_pending` 확인
  2. terminal 상태와 `ink_after` 비교
- 기대 결과:
  - short/pending cancel 후 final available ink `5.00`, drawing/pending reserve `0`
  - Pending R reset 후 final available ink `5.00`, reserves `0`, accepted/live/pending points/strokes `0`
  - 전후값을 raw에서 독립 비교 가능
- 실제 결과:
  - `short_cancel.ink_after=4.840000`
  - `pending_cancel.ink_after=4.760000`
  - `r_reset_pending.ink_after=4.760000`
  - 이 값은 terminal transition 전 `Du03ACandidateResult.AvailableInkAfter`이며, 같은 행의 `state_after/terminal/pending_count`는 transition 후 값이다.
  - `Du03ARuntimeProbeRunner.cs:152-163`이 candidate 시점 값과 final session 상태를 한 행에 혼합한다.
  - raw에는 final available ink, drawing reserve, pending reserve, accepted point count 전후값이 없다.
  - aggregator `Du03AVerification.cs:35-47`도 cancel/reset 환급값을 검사하지 않는다.
- 수용 기준:
  1. `candidate_*`와 `final_*` 필드를 분리하거나 event별 row로 기록한다.
  2. cancel/reset scenario에 `final_available_ink=5.000000`, `final_drawing_reserved=0`, `final_pending_reserved=0`을 포함한다.
  3. invalid 원자성 scenario에는 point/length/ink/drawing reserve/pending reserve의 before/after를 포함하고 전부 불변임을 aggregator가 검사한다.
  4. report가 위 값들을 재집계해 실패 시 `result=FAIL`을 내야 한다.
- 수정 책임자: `game-tech-director`

## 6. 빌드·테스트·회귀 결과

### 제출 실행 결과

- Compile: **PASS**, C# compile error 0, Tundra success, return code 0
- Scene rebuild: **PASS**, return code 0
- Windows player build: **PASS**, `Build Finished, Result: Success`, return code 0
- DU-03A aggregator: 제출 규칙 기준 **PASS**, scenarios 6, return code 0
- EditMode: **21/21 PASS**
  - DU-03A tests: **9/9 PASS**
  - DU-02 tests: **12/12 PASS**
- PlayMode: **2/2 PASS**
- Runtime DU-03A scenario: 제출 probe 기준 **6/6 PASS**
- Runtime exception / `result=FAIL`: 0

비차단 경고:

- `Assets/DoodleUp/Scripts/Runtime/Du02GoalZone.cs:19`의 obsolete API `CS0618`
- build log의 초기 licensing handshake 및 Bee client connection 메시지는 최종 build success/return code 0과 함께 종료되어 본 판정의 기능 차단으로 보지 않음

### DU-02 회귀

**PASS**

- sampling:
  - 30 fps: frames/samples `300/300`, elapsed `10.025609`, duplicate/missing 0
  - 60 fps: `600/600`, elapsed `10.016693`, duplicate/missing 0
  - 144 fps: `1438/1438`, elapsed `10.000553`, duplicate/missing 0
- reset: T1/T2/T3 × R_KEY/LANE_SELECT 6/6 PASS
- task-state: 4/4 PASS
- `Du02Verification`: `samplingRows=3 resetRows=6 d3r1=PASS result=PASS`
- depth drift, provenance invalid, runtime exception, `result=FAIL`: 0

## 7. DU-03A 이후 기능 미선행

**PASS**

- 실제 Aim adapter 없음
- 실제 Trajectory adapter 없음
- `IDu03ADrawIntentSource` 구현 없음
- mouse ray mapping, locomotion-driven trajectory mapping 없음
- DU-03B/DU-03C 기능은 선행되지 않음

단, DU-03A 자체 정본 범위인 Confirm capsule chain은 후속 기능이 아니라 현재 카드의 필수 구현이다.

## 8. 책임자·다음 체크포인트·차단요인

- QA evidence 판정 책임자: `game-qa` — **CHANGES REQUIRED**
- 수정 책임자: `game-tech-director`
- 완료 승인 책임자: `project-manager`
- 차단요인:
  - DU03A-R1 Confirm capsule chain 미구현
  - DU03A-R2 실제 LateUpdate/DrawIntent runtime evidence 부재
  - DU03A-R3 환급·reserve·point 불변성 raw 불충분
- 다음 체크포인트:
  1. 기술 책임자가 R1~R3 보강
  2. 새 standalone build에서 DU-03A raw/report 재생성
  3. QA가 hash, capsule geometry, LateUpdate frame chain, refund/atomic fields를 독립 재집계
  4. PASS 전까지 DU-03B/DU-03C 착수 보류

## 9. QA 사인오프

- 최종: **CHANGES REQUIRED**
- 제출 raw hash/report 정합성: PASS
- backend 상태·ledger 코드: 부분 PASS
- Confirm capsule chain: FAIL
- 실제 LateUpdate phase evidence: FAIL
- cancel/reset 환급 raw evidence: FAIL
- chargedLength pre-simplification 계약: PASS
- invalid mutation 금지 코드: PASS
- DU-02 회귀: PASS
- DU-03A 이후 기능 미선행: PASS
- 제품 코드 수정: 수행하지 않음
- 제품 문서 수정: 수행하지 않음
- QA 보고서 작성: 수행
- git 작업: 수행하지 않음

## 10. QA 프로세스 노트

- `result=PASS`와 assertion boolean을 그대로 신뢰하지 않고 raw 열의 시간 의미를 코드와 대조했다. 그 결과 candidate 직후 `ink_after`와 terminal 이후 상태가 한 행에 혼합된 사실을 확인했다.
- method 이름이나 sequence 증가만으로 LateUpdate 실행을 인정하지 않고 scene wiring과 실제 callback 경로를 확인했다.
- `committed_count=1`을 capsule chain 생성으로 간주하지 않고 runtime object 생성 코드와 collider count를 별도 확인했다.
- 제출 테스트가 모두 PASS여도 정본의 미검증·미구현 수용 기준이 있으면 카드 PASS로 승격하지 않았다.
