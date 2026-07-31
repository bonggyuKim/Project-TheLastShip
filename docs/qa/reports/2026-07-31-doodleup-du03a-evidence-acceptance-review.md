# DoodleUp DU-03A REV1 독립 Evidence 최종 재판정

- 검토일: 2026-07-31
- 검토자: `game-qa`
- 대상: DU-03A 공통 StrokeSession backend 및 REV1 runtime evidence
- 최종 판정: **PASS**
- 정본:
  - `D:\Project-DoodleUp\docs\input-comparison-spec-v1.md`
  - `D:\Project-DoodleUp\docs\prototype-execution-plan.md:70-79`
- 제한: 제품 코드·제품 문서 수정 없음, git 작업 없음

## 1. 최종 판정

# PASS

DU-03A REV1은 기존 수용 차단 결함 `DU03A-R1`~`R3`을 모두 해소했다. Confirm-only capsule geometry, 실제 scene-wired `LateUpdate` 입력 경로, final ledger/refund/reserve/point evidence를 독립 검증했으며, 14개 필수 scenario와 추가 상태 경계도 모두 수용 기준을 충족했다.

- 차단 결함: **0건**
- 필수 scenario: **14/14 PASS**
- DU-03A EditMode: **14/14 PASS**
- 전체 EditMode: **26/26 PASS**
- PlayMode: **2/2 PASS**
- DU-02 회귀: **PASS**
- DU-03B/DU-03C 선행 구현: **없음**

## 2. 검증 환경과 증거

- OS: Windows 11 Education `10.0.26200`
- Unity: `6000.4.0f1`
- raw: `D:\Project-DoodleUp\DU03A_Runtime_Raw.csv`
- report: `D:\Project-DoodleUp\DU03A_Verification_Report.txt`
- compile: `D:\.adk\logs\unity\compile-20260731-234654.log`
- scene: `D:\.adk\logs\unity\method-20260731-234441.log`
- build: `D:\.adk\logs\unity\build-20260731-234448.log`
- runtime: `D:\.adk\logs\unity\du03a-rev1-runtime-final-20260731.log`
- aggregator: `D:\.adk\logs\unity\method-20260731-234601.log`
- DU-02 regression: `D:\.adk\logs\unity\method-20260731-234623.log`
- EditMode: `D:\.adk\logs\unity\test-20260731-234630.log`
- EditMode XML: `D:\.adk\logs\unity\testresults-20260731-234630.xml`
- PlayMode: `D:\.adk\logs\unity\test-20260731-234638.log`
- PlayMode XML: `D:\.adk\logs\unity\testresults-20260731-234638.xml`

### 2.1 QA 독립 해시

| 산출물 | SHA256 | 판정 |
|---|---|---|
| DU-03A raw | `1bd31b518943cd8fac03263f6bf5c832258a2d9cd9445d60dc4724a59b83083a` | 제출값 및 report 내 `rawSha256`와 일치 |
| DU-03A report | `522937ae51ef557f512177d56e52c33c416c80d67ba8ac2bb2d8bfc796afe872` | 제출값과 일치 |
| DU-02 regression raw | `313973d3daea37d917716ccdb10c0487f8f2347ce56233fafcf0efe58b1b6612` | 회귀 report와 일치 |
| DU-02 regression report | `79953214d3320395d074e5b3baa88383287d9d047e234267a47b57f2d3b0f2cb` | 독립 계산값 |

DU-03A raw는 50열, 14개 scenario이며 scenario 중복, 누락, `result!=PASS` 행이 없다. report는 동일 raw를 집계해 `scenarios=14`, `result=PASS`를 기록한다.

## 3. 수용 기준별 판정

| 수용 기준 | 판정 | 독립 근거 |
|---|---|---|
| R1 Confirm-only capsule geometry | **PASS** | Pending collider 0, Confirm 후 segment 1/collider 1, 모든 geometry 골든값 충족 |
| R2 scene-wired intent → `Driver.LateUpdate` | **PASS** | scene에 deterministic source 연결, frame 2~5 실제 callback chain 및 release-frame `CANDIDATE>RELEASE` 확인 |
| R3 final ledger/refund/reserve/points | **PASS** | 50열 raw의 candidate before/after와 final fields를 독립 재집계, 모든 ledger total `5.000000` |
| 14개 scenario 및 상태 경계 | **PASS** | exact scenario set 14/14, invalid release·Cancel·Pending Draw 거부·Confirm edge 포함 |
| Aim/Trajectory backend parity | **PASS** | 동일 candidate sequence에서 상태, ledger, charged length, simplified points 일치 |
| raw/report/hash 일치 | **PASS** | SHA256 및 scenario 집계 독립 일치 |
| DU-02 회귀 | **PASS** | 30/60/144 fps sampling, reset 6/6, task-state 및 자동 집계 PASS |
| DU-03B/DU-03C 미선행 | **PASS** | 실제 Aim/Trajectory adapter와 고유 mapping 미구현 |

## 4. R1 — Confirm-only capsule geometry

### 절차

1. accepted length `0.24 u` stroke를 release한다.
2. Pending 상태에서 collider/root/Rigidbody가 생성되지 않았는지 확인한다.
3. 다음 실제 `LateUpdate`에 Confirm intent를 전달한다.
4. 생성된 committed root와 child `CapsuleCollider`를 runtime에서 검사한다.

### 기대 결과와 골든값

- Pending collider: `0`
- Confirm 성공 후 collider 수: `segment_count - degenerate_skipped`
- `direction=1`
- `radius=0.14`
- `height=segmentLength+0.28`
- `center=(0,0,0)`
- `isTrigger=false`
- root/child scale `(1,1,1)`
- child midpoint 정렬 및 local Y축 segment 정렬
- shared endpoint gap `≤0.000001`

### 실제 결과

`pending_confirm` raw:

| 필드 | 실제값 | 판정 |
|---|---:|---|
| `pending_colliders` | `0` | PASS |
| `segment_count` | `1` | PASS |
| `collider_count` | `1` | PASS |
| `degenerate_skipped` | `0` | PASS |
| `capsule_direction` | `1` | PASS |
| `capsule_radius` | `0.140000` | PASS |
| `capsule_height` | `0.520000` | PASS |
| `expected_capsule_height` | `0.520000` | PASS |
| `capsule_center_zero` | `True` | PASS |
| `capsule_non_trigger` | `True` | PASS |
| `root_scale_one` / `child_scale_one` | `True` / `True` | PASS |
| `midpoint_aligned` / `y_axis_aligned` | `True` / `True` | PASS |
| `max_shared_endpoint_gap` | `0.000000000` | PASS |

Runtime Confirm 로그도 `chargedLength=0.240000`, `simplifiedPoints=2`, `segments=1`, `colliders=1`, `degenerateSkipped=0`, `maxSharedEndpointGap=0`을 기록한다.

**결론:** `DU03A-R1` 종료.

## 5. R2 — 실제 scene-wired LateUpdate 경로

### 절차

1. scene builder가 `Du03ADeterministicIntentSource`를 `Du03AStrokeDriver`에 연결했는지 확인한다.
2. source queue에 press, candidate, release-frame candidate, Confirm을 순서대로 넣는다.
3. `ReadCount` 증가와 `LateUpdateProcessed` callback을 함께 기다린다.
4. frame/sequence/phase/candidate count/event order를 raw 및 runtime log와 대조한다.

### 기대 로그 체인

```text
PRESS
CANDIDATE
CANDIDATE>RELEASE
CONFIRM_COMMIT
```

release frame은 candidate를 정확히 1회 처리한 뒤 release해야 한다.

### 실제 결과

```text
renderFrame=2 sequence=1 samplePhase=LATE_UPDATE candidateCount=0 order=PRESS
renderFrame=3 sequence=2 samplePhase=LATE_UPDATE candidateCount=1 order=CANDIDATE
renderFrame=4 sequence=3 samplePhase=LATE_UPDATE candidateCount=1 order=CANDIDATE>RELEASE
renderFrame=5 sequence=4 samplePhase=LATE_UPDATE candidateCount=0 order=CONFIRM_COMMIT
```

`pending_confirm` raw의 핵심 값:

- `render_frame=4`
- `late_update_sequence=3`
- `sample_phase=LATE_UPDATE`
- `candidate_count_this_frame=1`
- `event_order=CANDIDATE>RELEASE`
- release-frame candidate `accepted_appended=True`

**결론:** 실제 scene-wired source가 `Driver.LateUpdate`를 통과하며 release-frame candidate-first 계약이 재현됐다. `DU03A-R2` 종료.

## 6. R3 — final ledger/refund/reserve/points

### 독립 재집계 식

```text
final_available
+ final_drawing_reserved
+ final_pending_reserved
+ final_committed_charged
= 5.000000
```

14개 scenario 모두 위 식을 충족한다.

### 대표 최종값

| Scenario | Available | Drawing | Pending | Committed | Total | 판정 |
|---|---:|---:|---:|---:|---:|---|
| `short_cancel` | 5.000000 | 0 | 0 | 0 | 5.000000 | 전액 환급 PASS |
| `pending_confirm` | 4.760000 | 0 | 0 | 0.240000 | 5.000000 | commit charge PASS |
| `pending_cancel` | 5.000000 | 0 | 0 | 0 | 5.000000 | 전액 환급 PASS |
| `reach_atomic` | 4.840000 | 0.160000 | 0 | 0 | 5.000000 | invalid no-mutation PASS |
| `ink_atomic` | 0.200001 | 0 | 0 | 4.800000 | 5.000001* | 허용 오차 내 PASS |
| `r_reset_pending` | 5.000000 | 0 | 0 | 0 | 5.000000 | canonical reset PASS |

`*` raw의 `final_ledger_total`은 부동소수점 계산 결과를 계약값 `5.000000`으로 기록하며 verifier 허용 오차 `0.0001` 이내다.

### invalid atomicity

`reach_atomic`, `ink_atomic`, `invalid_release_under_min`, `invalid_release_over_min`에서 다음 before/after를 독립 비교했다.

- accepted point count
- accepted length
- available ink
- drawing reserve
- pending reserve

모든 invalid candidate에서:

- `candidate_valid=False`
- `accepted_appended=False`
- `atomic_unchanged=True`
- points/length/available/drawing/pending before = after

금지 조합 `candidate_valid=False AND accepted_appended=True`는 0건이다.

**결론:** cancel/reset의 terminal 상태와 candidate 시점 값이 분리돼 독립 재집계가 가능하다. `DU03A-R3` 종료.

## 7. 14개 scenario 판정

| Scenario | 핵심 검증 | 판정 |
|---|---|---|
| `short_cancel` | minimum 미만 release 시 Idle/Cancelled 및 전액 환급 | PASS |
| `pending_confirm` | 실제 LateUpdate release, Pending collider 0, Confirm geometry | PASS |
| `pending_cancel` | Pending Cancel 전액 환급 | PASS |
| `reach_atomic` | reach invalid all-or-nothing | PASS |
| `ink_atomic` | ink invalid all-or-nothing | PASS |
| `r_reset_pending` | Pending R reset canonical state | PASS |
| `invalid_release_under_min` | 마지막 accepted length `<0.20` 기준 Cancelled | PASS |
| `invalid_release_over_min` | invalid release candidate라도 기존 accepted length `>=0.20`이면 Pending | PASS |
| `drawing_cancel` | Drawing Cancel 및 reserve 환급 | PASS |
| `pending_new_draw_reject` | Pending 중 새 Draw 거부, 상태/ledger 불변 | PASS |
| `out_of_state_confirm` | Idle Confirm no-op, geometry/ledger 불변 | PASS |
| `confirm_release_same_frame` | 선행 Confirm 거부 후 candidate→release 정상 처리 | PASS |
| `mode_parity_aim` | Aim backend 결과 | PASS |
| `mode_parity_trajectory` | Trajectory backend 결과 및 Aim parity | PASS |

### Confirm edge

`confirm_release_same_frame`의 실제 순서는 `CONFIRM_REJECTED>CANDIDATE>RELEASE`다. Drawing 상태의 무효 Confirm이 같은 frame의 유효 candidate/release를 소비하지 않으며 최종 Pending으로 전환한다.

### Mode parity

Aim과 Trajectory에 동일 candidate sequence를 전달했을 때 다음 값이 일치한다.

- state after
- accepted point count 및 accepted length
- available/drawing/pending ledger
- final committed charge 및 ledger total
- charged length
- simplified point count

이는 DU-03A backend parity만 검증하며 DU-03B/DU-03C의 실제 입력 mapping을 선행하지 않는다.

## 8. 빌드·테스트·회귀

### 8.1 DU-03A REV1

- Compile: **PASS** — Tundra success, return code 0, C# compile error 0
- Scene rebuild: **PASS** — return code 0
- Windows player build: **PASS** — `Build Finished, Result: Success`, return code 0
- Runtime: **14/14 PASS** — exception, `result=FAIL`, depth drift, provenance invalid 0
- Aggregator: **PASS** — `scenarios=14 result=PASS`
- EditMode: **26/26 PASS**
  - DU-03A: **14/14 PASS**
  - DU-02: **12/12 PASS**
- PlayMode: **2/2 PASS**

### 8.2 DU-02 회귀

| Target FPS | Frames | Samples | Elapsed | Duplicate | Missing | 판정 |
|---:|---:|---:|---:|---:|---:|---|
| 30 | 300 | 300 | 10.032598 | 0 | 0 | PASS |
| 60 | 600 | 600 | 10.016270 | 0 | 0 | PASS |
| 144 | 1439 | 1439 | 10.000288 | 0 | 0 | PASS |

- T1/T2/T3 × R_KEY/LANE_SELECT reset: **6/6 PASS**
- reset은 rotation/angular velocity/phase를 실제 교란한 뒤 baseline hash와 상태를 복구함
- task-state 회귀: **PASS**
- DU-02 aggregator: `samplingRows=3 resetRows=6 d3r1=PASS result=PASS`

## 9. 범위 검증

**PASS**

- 실제 Aim adapter 없음
- 실제 Trajectory adapter 없음
- mouse ray 기반 Aim mapping 없음
- locomotion-driven Trajectory mapping 없음
- deterministic probe source는 DU-03A runtime evidence 전용이며 공통 backend 계약만 구동함
- DU-03B/DU-03C 기능은 선행되지 않음

## 10. 발견 결함 및 관찰

### 수용 차단 결함

- 없음

### 비차단 관찰 — direct row의 phase 표기

- 심각도: **LOW / 비차단**
- 조건: 실제 callback 대신 backend helper로 실행되는 scenario raw 확인
- 기대: callback을 통과하지 않은 row는 phase가 `DIRECT` 또는 별도 execution-path 필드로 명확히 구분됨
- 실제: `render_frame=0`, `late_update_sequence=0`, `event_order=DIRECT`로 direct 실행임을 식별할 수 있으나 `sample_phase`는 `LATE_UPDATE`로 고정 표기됨
- 영향: 핵심 R2 증거인 `pending_confirm`은 frame/sequence 양수와 실제 callback chain을 제공하므로 수용 판정에는 영향 없음. 다만 후속 자동 분석에서 `sample_phase` 단독 필터를 사용하면 direct row를 실제 LateUpdate로 오인할 수 있음
- 권고 책임자: `game-tech-director`
- 권고 수용 기준: direct 실행 row는 `sample_phase=DIRECT` 또는 별도 `execution_path=DIRECT|CALLBACK`으로 구분

## 11. 책임자·다음 체크포인트·차단요인

- QA 최종 판정 책임자: `game-qa` — **PASS**
- 기술 수정 책임자: `game-tech-director` — R1~R3 완료
- 완료 승인 책임자: `project-manager`
- 차단요인: **없음**
- 다음 체크포인트:
  1. `project-manager`가 DU-03A 완료를 승인한다.
  2. 승인 후 별도 범위로 DU-03B/DU-03C 착수 여부를 결정한다.
  3. 후속 evidence schema 정리 시 direct row phase 표기를 명확히 한다. 이는 DU-03A 완료 차단 조건이 아니다.

## 12. QA 사인오프

- 최종: **PASS**
- R1 Confirm-only capsule chain: PASS
- R2 실제 scene-wired LateUpdate evidence: PASS
- R3 final refund/reserve/point evidence: PASS
- 14개 scenario: 14/14 PASS
- invalid mutation 금지: PASS
- chargedLength pre-simplification 계약: PASS
- Aim/Trajectory backend parity: PASS
- raw/report/hash 정합성: PASS
- compile/build/runtime/aggregator: PASS
- EditMode/PlayMode: PASS
- DU-02 회귀: PASS
- DU-03B/DU-03C 미선행: PASS
- 제품 코드 수정: 수행하지 않음
- 제품 문서 수정: 수행하지 않음
- QA 보고서 작성: 수행
- git 작업: 수행하지 않음

## 13. QA 프로세스 노트

- 제출된 `result=PASS`만 신뢰하지 않고 raw 50열을 독립 파싱해 scenario set, ledger, atomic before/after, geometry 골든값을 재집계했다.
- `committed_count=1`과 물리 geometry 생성을 분리해 Pending collider 0 및 Confirm 후 runtime object를 각각 확인했다.
- sequence 숫자만으로 callback 실행을 인정하지 않고 scene wiring, source `ReadCount`, `LateUpdateProcessed`, render frame과 event order를 함께 대조했다.
- cancel/reset은 candidate snapshot이 아니라 `final_*` 필드로 terminal 환급 상태를 판정했다.
- DU-02 raw/report와 테스트를 함께 재검증해 공통 runtime 변경의 회귀가 없음을 확인했다.
- 실제 adapter가 없는 것을 확인해 DU-03B/DU-03C 범위 선행 여부를 별도로 차단 검토했다.
