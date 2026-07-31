# DoodleUp DU-01 InputComparisonSpec v1 — QA 승인 검토

- 검토일: 2026-07-31
- 검토자: `game-qa`
- 대상 문서: `D:\Project-DoodleUp\docs\input-comparison-spec-v1.md`
- 상위 정본: `D:\Project-DoodleUp\docs\prototype-execution-plan.md`
- 대조 기준: `D:\Project-DoodleUp\docs\qa\reports\2026-07-31-doodleup-gate-a-b-evidence-pack-review.md`
- 범위: DU-06A-A/B 재현성, CSV, failure code, session SOP, Gate 판정식
- 제외: 구현, Unity 실행, 코드·제품 문서 수정, git 작업

## 1. 최종 판정

# CHANGES REQUIRED

DU-01은 비교 조건·입력 계약·코스·세션 순서 대부분을 충분히 동결했다. 특히 다음은 재현 가능하다.

- mode별 5분 연습, mode별 9 trial, 4명 counterbalance
- `TECH_INVALID` 제외 후 동일 task 즉시 재시행 원칙
- confirm latency를 Draw release 이후로 측정하려는 의도
- 두 mode 모두 PASS일 때 성공률 20%p → 시간 25% → Aim tie-break 순서
- session ID를 이용한 raw/CSV/summary/video 연결 원칙

그러나 현재 상태로는 **같은 raw에서 서로 다른 Gate 판정이 나올 수 있다.** P0 차단은 다음 5건이다.

1. §9.3의 분리 판정은 개인 qualified 기준과 수학적으로 같지 않다.
2. trial당 여러 stroke를 commit할 수 있는데 CSV에는 `draw_release_ms`와 `confirm_ms`가 하나뿐이어서 confirm median을 재계산할 수 없다.
3. `TECH_INVALID` 재시행을 raw 72행 안에서 대체할지, 별도 행으로 보존할지 정의가 없다.
4. `session_id` 일치만으로는 raw trial과 영상 구간을 1:1 연결할 수 없다.
5. 25% 시간 우세의 집계 모집단과 정확한 계산식이 없다.

아래 문구가 반영되기 전에는 `InputComparisonSpec v1 Approved` 및 DU-06A-A 본세션을 승인하지 않는다.

## 2. 정본이어야 하는 Gate A 판정식

### 판정

**QA 권고였던 개인 conjunctive qualified 식을 정본으로 채택해야 한다.**

```text
participant qualified(mode)
  = success_count >= 7/9
    AND participant_confirm_median_ms <= 2,000
    AND range_depth_misunderstanding_count <= 2/9

mode PASS
  = qualified participants >= 3/4
```

§9.3의 현재 분리 판정은 폐기한다.

현재 식:

1. 성공 7/9인 tester가 3명 이상
2. 네 participant confirm median의 중앙값 ≤2초
3. 네 participant 오해 실패 수의 중앙값 ≤2/9

이 식은 세 조건을 서로 다른 사람이 충족해도 mode PASS를 낸다. 예:

| 참가자 | 성공 | confirm median | range+depth 오해 | 개인 qualified |
|---|---:|---:|---:|---|
| P01 | 7/9 | 3.0초 | 3 | FAIL |
| P02 | 7/9 | 3.0초 | 3 | FAIL |
| P03 | 7/9 | 1.0초 | 1 | PASS |
| P04 | 0/9 | 1.0초 | 1 | FAIL |

현재 §9.3은 성공 조건 3명, confirm 중앙값 2.0초, 오해 중앙값 2로 모두 통과할 수 있지만 실제로 세 조건을 동시에 만족한 참가자는 1명뿐이다. 소수의 빠른/명확한 데이터가 느리고 오해한 성공자를 가리는 false PASS다.

개인 conjunctive 식은 Gate A의 원래 문장인 “3/4 테스터가 한 mode 7/9 성공, median confirm≤2초, 오해≤2/9”을 가장 보수적이고 재현 가능하게 해석한다. 표본이 4명뿐이므로 집단 중앙값으로 개인 실패를 숨기면 안 된다.

## 3. 정확한 변경 문구

### 3.1 §9.3 전체 교체

아래로 §9.3을 교체한다.

```markdown
### 9.3 Continue — participant qualified 및 mode PASS

participant는 mode별 평가 대상 trial 9개에 대해 아래 세 조건을 **모두** 만족할 때만 해당 mode에서 `qualified`다.

1. `success_count >= 7/9`
2. `participant_confirm_median_ms <= 2,000`
3. `range_depth_misunderstanding_count <= 2/9`

`range_depth_misunderstanding_count`는 평가에 포함된 9개 trial 중 primary failure code가 `REACH_MISUNDERSTANDING` 또는 `DEPTH_PLANE_MISUNDERSTANDING`인 trial 수다.

`participant_confirm_median_ms`는 평가에 포함된 각 trial의 `trial_confirm_latency_ms` 중앙값이다. `trial_confirm_latency_ms`는 해당 trial에서 `Pending→Committed`에 성공한 모든 stroke의 `stroke_confirm_latency_ms` 중앙값이다. committed stroke가 없는 trial은 이 지표에서 null이지만, participant가 성공 7/9 조건을 만족하므로 최소 7개의 non-null trial 값이 있어야 한다. non-null trial 값이 7개 미만이면 confirm 조건은 FAIL이다.

같은 mode에서 `qualified participant >= 3/4`일 때만 mode PASS다. 세 조건을 participant 집단의 중앙값으로 따로 판정하지 않는다.

Aim/Trajectory 중 하나 이상 mode PASS면 Gate A를 Continue한다.
```

### 3.2 §9.2 confirm 이벤트 경계 교체

현재 한 줄:

```markdown
- `confirm_latency_ms = Confirm - 유효 stroke의 Draw release`
```

아래로 교체한다.

```markdown
- `pending_enter_us`는 길이 `>=0.20 u`인 유효 stroke의 Draw release를 backend가 처리하여 **같은 stroke가 `Drawing→Pending`으로 전이한 monotonic timestamp**다.
- `commit_us`는 Pending 상태에서 들어온 **첫 accepted 명시적 Confirm 입력**이 같은 stroke를 `Pending→Committed`로 전이한 monotonic timestamp다. Pending 밖의 Confirm, key repeat, 무시된 입력은 종료 이벤트가 아니다.
- `stroke_confirm_latency_ms = (commit_us - pending_enter_us) / 1000`이다.
- Cancelled stroke와 Confirm되지 않은 stroke는 `commit_us`와 `stroke_confirm_latency_ms`가 null이다. null을 `0`으로 대체하지 않는다.
- trial에 committed stroke가 여러 개면 `trial_confirm_latency_ms`는 그 trial의 non-null `stroke_confirm_latency_ms` 중앙값이다.
- latency 계산은 frame time이나 wall clock이 아니라 같은 monotonic clock을 사용한다.
```

이 경계는 `Draw press→Confirm`이 아니라 **Pending ghost를 본 뒤 명시적으로 확정하는 데 걸린 시간**을 측정한다. §4의 release/Confirm 분리 가설과 일치한다.

### 3.3 §9.2 CSV를 trial + stroke + 영상 연결 schema로 교체

현재 최소 CSV는 trial당 여러 stroke와 TECH_INVALID 재시행을 표현하지 못한다. 다음 문구를 추가한다.

```markdown
#### attempts.csv

`schema_version, spec_version, build_id, session_id, participant_id, mode_order, mode, scheduled_trial_id, attempt_id, attempt_no, task_id, block_index, trial_index, result, primary_failure_code, technical_reason_code, evaluation_included, exclusion_reason, trial_start_us, first_draw_us, goal_us, total_completion_ms, invalid_reach_count, invalid_ink_count, cancel_count, delete_count, reset_count, committed_stroke_count, charged_length, ink_remaining, avg_fps, min_fps, device_id_hash, raw_event_seq_start, raw_event_seq_end, video_file, video_in_ms, video_out_ms, observer_note`

#### strokes.csv

`schema_version, session_id, scheduled_trial_id, attempt_id, stroke_id, stroke_index, outcome, cancel_reason, draw_start_us, pending_enter_us, commit_us, stroke_confirm_latency_ms, raw_point_count, accepted_point_count, charged_length, deleted_us, raw_event_seq_start, raw_event_seq_end`

#### result / failure 규칙

- `result` enum은 `SUCCESS | PLAYER_FAIL | TECH_INVALID`다.
- `SUCCESS`는 `primary_failure_code=NONE`이다.
- `PLAYER_FAIL`은 §9.1의 player failure code를 정확히 하나 가진다.
- `TECH_INVALID`는 `primary_failure_code=NONE`, `technical_reason_code` 필수이며 Gate 분모에 포함하지 않는다.
- `PHYSICS_EXPLOSION`, `COLLIDER_MISSING`, `INPUT_DISCONNECT`, `FOCUS_LOSS`, `FRAME_CONTRACT_BREACH`, `LOGGING_FAILURE`, `VIDEO_FAILURE`, `OTHER_TECH`는 `technical_reason_code`다.
- 유효한 stroke 형상에서 tester의 이동 실패는 `TRAVERSAL_FAIL`이고, 물리 폭발·collider 소실·비결정적 snag처럼 tester 책임이 아닌 하네스/제품 이상은 `TECH_INVALID`다. 기존 `PHYSICS_FAIL`을 player failure와 technical failure 양쪽 의미로 사용하지 않는다.

#### raw↔영상 연결

- 모든 attempts row는 `attempt_id`, `raw_event_seq_start/end`, `video_file`, `video_in_ms`, `video_out_ms`를 가져야 한다.
- 모든 strokes row는 `attempt_id`와 raw event sequence 범위를 가져야 한다.
- `session_id` 일치만으로 artifact 연결 완료로 보지 않는다.
- 영상에는 `session_id, attempt_id, mode, task_id, block_index, build_id, elapsed time` overlay를 표시한다.
- raw event, attempts/strokes CSV, 영상 구간 중 하나라도 연결되지 않은 평가 row는 증거팩을 `EVIDENCE_INVALID`로 만든다.
```

`device_id`는 원시 serial/개인정보가 아닌 세션 내 pseudonymous hash인 `device_id_hash`로 바꾼다.

### 3.4 72 attempt rows와 TECH_INVALID 재시행 문구 추가

§8.2 뒤에 아래를 추가한다.

```markdown
### 8.3 scheduled trial, raw attempt, TECH_INVALID 재시행

본시험에는 `4 participant × 2 mode × 3 task × 3 block = 72`개의 고정 `scheduled_trial_id`가 있다. 각 scheduled trial은 Gate 계산에 포함되는 non-TECH_INVALID attempt를 정확히 1개 가져야 한다.

- 유효 평가 row: 정확히 72개 (`evaluation_included=true`).
- raw attempts row: `72 + TECH_INVALID 재시행 수`이므로 72개 이상이다.
- TECH_INVALID row는 삭제하거나 후속 성공 row로 덮어쓰지 않는다. `evaluation_included=false`, `exclusion_reason`, `technical_reason_code`, 원본 로그·영상 링크를 보존한다.
- TECH_INVALID 발생 시 one-click reset 후 같은 participant/mode/task/block의 `scheduled_trial_id`를 `attempt_no+1`로 즉시 재시행하며 coaching이나 추가 연습을 제공하지 않는다.
- 같은 scheduled trial에서 TECH_INVALID가 3회 연속이면 해당 mode 세션을 중단하고 `SESSION_TECH_INVALID`로 표시한다. 원인을 제거한 새 session ID에서 그 mode의 9개 scheduled trial 전체를 처음부터 재실행한다. 서로 다른 session에서 유효 row만 골라 하나의 9-trial mode 데이터로 합치지 않는다.
- 본시험 시작 전에 생성된 연습·pilot row는 별도 session/phase이며 72개 평가 row에 포함하지 않는다.
```

따라서 “72 attempt rows”는 **raw 파일 총 행 수가 아니라 Gate에 포함되는 고정 평가 행 수**다. TECH_INVALID도 raw 증거에서 지워지면 안 된다.

### 3.5 §9.4 25% 시간 우세 문구 교체

아래로 §9.4를 교체한다.

```markdown
### 9.4 두 mode 모두 PASS일 때 채택

계산 모집단은 `evaluation_included=true`인 72개 평가 row뿐이다. TECH_INVALID, 연습, pilot row는 제외한다.

- mode별 `pooled_success_rate = SUCCESS row 수 / 36`.
- mode별 `successful_completion_median_ms`는 해당 mode의 SUCCESS row에 대한 `total_completion_ms = goal_us - trial_start_us` 중앙값이다.
- candidate mode가 다른 mode보다 25% 이상 빠르다는 뜻은 `candidate_median_ms <= 0.75 × other_median_ms`다.

채택은 아래 우선순위를 순서대로 적용한다.

1. pooled success rate 차이의 절댓값이 `>=20 percentage points`면 성공률이 높은 mode를 채택한다.
2. 성공률 차이가 `<20 percentage points`이고 한 mode가 위 식으로 `>=25%` 빠르면 더 빠른 mode를 채택한다.
3. 둘 다 해당하지 않으면 Aim을 채택한다.

1번이 성립하면 시간 지표가 반대 mode를 가리켜도 1번 결과를 유지한다. 성공률·시간 지표가 서로 다른 mode를 가리킨다는 이유로 별도 tie-break를 다시 적용하지 않는다.
```

현재 §9.4 3번의 “두 지표가 서로 다른 mode를 가리키면 Aim”은 앞의 우선순위와 충돌할 수 있어 삭제해야 한다.

### 3.6 §9.1 failure 판정 운영 문구 추가

```markdown
- primary failure code는 trial 종료 직후 observer가 provisional로 1개 기록하고, 세션 종료 후 raw event와 영상 clip을 대조하여 final로 확정한다.
- `REACH_MISUNDERSTANDING`과 `DEPTH_PLANE_MISUNDERSTANDING`은 관찰자 추정만으로 확정하지 않는다. 해당 행동의 raw event/영상이 있고 즉시 사후 질문에서 참가자의 기대가 code 정의와 일치해야 한다. 불일치하거나 근거가 부족하면 `INPUT_MAPPING_FAIL` 또는 적합한 다른 code를 사용한다.
- 복수 원인이 보여도 직접적인 trial 실패 원인 하나만 primary로 선택한다. 보조 관찰은 `observer_note`에 남긴다.
- failure code 변경 이력은 원 raw를 덮어쓰지 않고 annotation revision으로 보존한다.
```

## 4. 항목별 판정

| 검토 항목 | 현재 판정 | 근거 |
|---|---|---|
| mode별 5분 학습 | PASS | §8.2가 mode마다 5분을 명시 |
| 4명 counterbalance | PASS | Aim-first 2 / Trajectory-first 2 |
| 9 trial/person/mode | PASS | 3 task×3 block 명시 |
| 72 평가 row | FAIL | 총수 및 TECH_INVALID와의 관계 미정 |
| failure taxonomy | PARTIAL | primary 1개는 명시, 판정 절차와 TECH/player 경계 부족 |
| confirm 2초 경계 | PARTIAL | release 기준은 있으나 same-stroke event/다중 commit schema 없음 |
| 개인 qualified | FAIL | 집단 분리 median으로 false PASS 가능 |
| mode PASS | FAIL | 개인 conjunctive qualified와 불일치 |
| 25% 시간 우세 | PARTIAL | 성공 trial median은 명시, 모집단·정확한 0.75식·우선순위 충돌 미정 |
| TECH_INVALID 재시행 | PARTIAL | 즉시 재시행은 명시, raw 보존·행 수·연속 실패 중단 미정 |
| raw↔영상 | FAIL | session_id만으로 attempt 구간 연결 불가 |
| build/environment/hardware provenance | PARTIAL | 핵심 profile은 동결됐으나 증거팩 manifest/SHA256 연결이 없음 |
| Gate B 경계 | PASS WITH FOLLOW-UP | 동일 gamepad, 분리 ledger, Gate A 비혼합 명시. social event schema는 DU-06A-B에서 별도 동결 필요 |

## 5. 수용 기준 및 체크포인트

### DU-01 QA 승인 기준

다음이 모두 문서에 반영되어야 한다.

- [ ] §9.3을 개인 conjunctive qualified 식으로 교체
- [ ] same-stroke Pending→Committed confirm latency 정의
- [ ] 다중 stroke를 보존하는 `strokes.csv` 추가
- [ ] 72 scheduled trial / 72 evaluation-included row / raw 72+invalid 분리
- [ ] TECH_INVALID raw 보존, 3회 연속 시 mode session 재시작
- [ ] attempt ID + raw event sequence + video in/out 연결
- [ ] 25% 시간 우세의 SUCCESS-only 모집단과 `<=0.75×` 식 명시
- [ ] result와 player/technical failure code 분리
- [ ] build manifest 및 binary SHA256를 DU-06A-A 증거팩과 연결

### 책임자

- 정본·판정식 변경: `game-planning`
- 문서 승인 및 변경 통제: `project-manager`
- 계측 가능성 확인: `game-tech-director`
- 변경본 재검토: `game-qa`

### 다음 체크포인트

`input-comparison-spec-v1.md` 변경본에서 위 9개 항목 반영 확인 → tech가 attempts/strokes/events 샘플 1세트를 제시 → QA가 같은 raw로 판정 결과를 재계산 → `InputComparisonSpec v1 Approved`.

## 6. QA 사인오프

- 최종: **CHANGES REQUIRED**
- DU-01 완료 승인: **보류**
- DU-06A-A pilot: **보류**
- DU-06A-A 본세션: **차단**
- 코드/Unity/git 작업: 수행하지 않음
