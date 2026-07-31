# DoodleUp DU-06A-A / DU-06A-B 증거팩 QA 전문 검토

- 검토일: 2026-07-31
- 검토자: `game-qa`
- 정본: `D:\Project-DoodleUp\docs\prototype-execution-plan.md` (2026-07-31)
- 대상: DU-06A-A Gate A 증거팩, DU-06A-B Gate B 증거팩
- 검토 범위: 작업 분해, 의존성, 차단요인, raw schema, provenance, 실패 재현, 영상·설문, 수용 기준, 세션 운영
- 제외: 기능 구현, 제품 코드 수정, git 작업, Unity 실행

## 1. 결론

**전문 검토 판정: 조건부 승인(카드 보강 필수).**

현재 정본은 시험 규모와 핵심 산출물을 제시하지만, 그대로는 동일 결과를 다시 산출할 수 없다. 아래 P0 정의가 동결된 뒤에만 세션을 시작한다.

1. Gate A `5분 학습`이 **참가자 전체 5분인지 mode별 5분인지** 불명확하다. QA 권고는 공정성을 위해 **참가자×mode별 5분**이다.
2. `confirm≤2초`의 시작·종료 이벤트가 없다.
3. `사거리·깊이 오해 실패`의 판정 주체와 reason taxonomy가 없다.
4. Gate A `3/4 테스터가 7/9 성공`에서 시간·오해 기준을 개인별로 함께 적용하는지 불명확하다.
5. 둘 다 PASS일 때 `시간 25% 우세`의 시간 지표가 confirm time인지 task completion time인지 불명확하다. QA 권고는 **성공 trial의 task completion median**이다.
6. Gate B `각자 stroke 30%+`의 분모가 획 수인지 commit 길이인지 없다. QA 권고는 **committed ink length share**를 primary, stroke count share를 보조 지표로 기록하는 것이다.
7. `타인 stroke 사용 3회+`가 접촉 프레임 수로 부풀지 않도록 usage episode debounce 정의가 필요하다.
8. 구조적 방해·실수 방해·자발적 상호작용은 telemetry만으로 판정할 수 없다. observer annotation 규약이 필요하다.

증거팩 카드의 성공은 제품 Gate PASS가 아니다. 상태를 다음처럼 분리한다.

- `EVIDENCE_COMPLETE`: 정본 build/schema로 필수 증거가 빠짐없이 재계산 가능함.
- `EVIDENCE_INVALID`: provenance, raw row, 영상 연결, schema 검증 중 하나라도 결손.
- `PRODUCT_PASS|FAIL`: DU-07A/DU-07B가 증거팩을 기반으로 별도 판정.

제품이 FAIL한 증거를 정확히 담은 팩도 `EVIDENCE_COMPLETE`일 수 있다.

## 2. 공통 증거팩 규격

### 2.1 권장 디렉터리

```text
Evidence/<card-id>/<pack-version>/
  README.md
  manifest.json
  checksums.sha256
  schemas/
    schema.json
    enums.json
  raw/
    sessions.csv
    attempts.csv              # Gate A
    strokes.csv
    events.csv
    ink_ledger.csv
    performance.csv
    runtime_errors.csv
    surveys.csv
    observer_annotations.csv  # Gate B 필수, Gate A 선택
  derived/
    participant_metrics.csv
    pair_metrics.csv           # Gate B
    gate_summary.csv
    verdict-input.md
  logs/
    session-<id>.log
    repro-<failure-id>.log
  video/
    full/
    clips/
    video-index.csv
```

`raw/`는 세션 종료 후 수정 금지다. 정정이 필요하면 새 pack version을 만들고 원본을 보존한다. `derived/`는 raw로 재생성 가능해야 한다.

### 2.2 버전 규칙

- Gate A schema: `du.evidence.gate-a/1.0.0`
- Gate B schema: `du.evidence.gate-b/1.0.0`
- 호환 불가 컬럼·의미 변경: major 증가
- optional 컬럼 추가: minor 증가
- 문서/enum 설명만 수정: patch 증가
- 모든 CSV 첫 컬럼에 `schema_version`을 넣는다.
- 한 증거팩에 build ID 또는 schema version을 섞지 않는다.
- UTC ISO-8601 시간과 `monotonic_us`를 함께 기록한다. latency 계산에는 monotonic 값만 쓴다.

### 2.3 build·environment·hardware 기록

`manifest.json` 필수 필드:

| 분류 | 필수 필드 |
|---|---|
| 팩 | card_id, pack_version, schema_version, created_utc, operator_id |
| build | build_id, binary_sha256, build_utc, Unity=`6000.4.0f1`, scene_id, course_version, comparison_profile_version |
| runtime | OS edition/build, locale, timezone, Input System package version, fixedDeltaTime, timeScale, quality profile, vSync, targetFrameRate |
| display | resolution, fullscreen mode, refresh rate, render scale |
| machine | pseudonymous machine_id, CPU, GPU, driver, RAM |
| recording | encoder, resolution/fps, capture path, capture overhead 측정값 |
| 시험 | session IDs, participant aliases, mode order/pair allocation, exclusions |

`hardware.csv` 또는 manifest 하위 객체:

- Gate A: keyboard/mouse vendor·model, mouse DPI, polling rate, Windows pointer speed/acceleration.
- Gate B: gamepad vendor·model·firmware·wired/Bluetooth, controller slot, 배터리 상태, USB/Bluetooth topology.
- 동일 모델 여부는 문자열 비교가 아니라 vendor/product/firmware로 검증한다.
- 기기 serial, 이름, 연락처 등 개인정보는 저장하지 않는다. participant는 `P01`~`P04`, pair는 `PAIR01`~`PAIR02`를 쓴다.

소스 revision을 얻기 위한 git 작업은 하지 않는다. 재현 키는 `build_id + binary_sha256 + scene/course/profile version`으로 충족한다.

### 2.4 영상 규격

- 세션 전체 연속 영상 1개 + 중요 실패/상호작용 clip.
- 화면에 session ID, participant/pair alias, mode, task/repetition, build ID, elapsed time을 overlay한다.
- Gate A는 cursor/hand/plane/invalid-red preview/ink 상태가 식별돼야 한다.
- Gate B는 두 플레이어 owner color, join slot, stroke owner, ledger 변화가 식별돼야 한다.
- `video-index.csv`: `session_id, video_file, start_utc, duration_ms, attempt_id|interaction_id, clip_in_ms, clip_out_ms, sha256`.
- 모든 평가 trial 또는 interaction annotation이 영상 timestamp와 연결돼야 한다.
- 녹화는 두 mode/두 pair에 동일 조건으로 적용한다. 녹화가 성능을 바꾸면 capture overhead와 frame-time을 기록한다.
- 참가자 동의와 보존 기간을 세션 시작 전에 확인한다. 얼굴·실명은 기본적으로 녹화하지 않는다.

### 2.5 실패 재현 규격

플레이 실패와 제품/하네스 결함을 분리한다.

- `attempt_failure`: RANGE_MISUNDERSTANDING, DEPTH_MISUNDERSTANDING, CONTROL_MAPPING, FALL, TIMEOUT, PLAYER_CANCEL, OTHER_PLAYER.
- `product_failure`: STROKE_REJECT_UNEXPECTED, PHYSICS_SEAM_SNAG, PHYSICS_EXPLOSION, INK_LEDGER, DEVICE_DISCONNECT, FOCUS_LOSS, CRASH, HANG, OTHER_PRODUCT.
- 각 실패는 `failure_id`로 raw row, 로그, 영상 clip을 연결한다.
- 로그 범위: 실패 5초 전부터 5초 후까지의 event sequence. crash/hang은 마지막 정상 heartbeat와 종료/강제 종료 정보를 포함한다.
- crash, hang, ledger invariant, 진행 불가 결함은 동일 build·환경에서 최대 3회 재현한다. 3회 실패 시에도 `NOT_REPRODUCED_3X`로 남기며 삭제하지 않는다.
- 사용성 실패는 억지 재현하지 않는다. 원 trial, 즉시 사후 질문, observer annotation, clip으로 보존한다.

### 2.6 공통 기대 로그 체인

```text
EvidenceSessionStart
→ ManifestSnapshot
→ DeviceBound
→ PhaseStart
→ Trial|PairSessionStart
→ StrokeSessionStart
→ PlaneSnapshot
→ PointAppendAccepted|PointAppendRejected(reason)
→ StrokeCommitted|StrokeCancelled
→ InkReserved→InkCommitted|InkRefunded
→ Task|Interaction event
→ Trial|PairSessionEnd
→ SurveySubmitted
→ EvidenceSessionEnd
→ ChecksumWritten
```

`event_seq`는 세션 내 단조 증가해야 한다. `InkCommitted/Refunded` 뒤에는 `LedgerInvariantChecked`가 와야 한다.

## 3. DU-06A-A — Gate A 증거팩

### 3.1 책임자·체크포인트·의존성

- DRI: `game-qa`
- 계측 책임: `game-tech-director`
- 판정표·용어 동결: `game-planning`
- 범위·세션 승인: `project-manager`
- 다음 체크포인트: **Schema v1 + session SOP + 테스트 build provenance 사전검사 완료**
- 선행 의존성: DU-01, DU-02, DU-03A, DU-03B/C, DU-04A, DU-05A 완료 및 동일 build에 통합
- 후행: `EVIDENCE_COMPLETE` 뒤 DU-07A

### 3.2 작업 분해

| ID | 작업 | 책임 | 산출물 | 완료 조건 |
|---|---|---|---|---|
| A-E01 | 비교 계약 동결 | planning/PM | metric dictionary, enum, threshold v1 | 8개 불명확 항목 중 Gate A 5개 해소 |
| A-E02 | schema·manifest 동결 | QA | schema 1.0.0, templates | validator로 필수 컬럼/enum 검증 |
| A-E03 | 계측 준비 검토 | tech/QA | 이벤트 매핑표, 샘플 raw | 기능 구현과 분리된 계측 요청으로 처리; 누락 이벤트 0 |
| A-E04 | 비평가 pilot 1회 | QA | pilot pack | 시간축, 영상 링크, ledger 재계산 확인. pilot 데이터는 본시험 제외 |
| A-E05 | 4명 본세션 | QA | 4 session packs | 두 order group 각 2명, 총 72 평가 trial |
| A-E06 | 실패 재현·무결성 검사 | QA/tech | repro logs, checksums, validation report | 결손/중복/혼합 build 0 |
| A-E07 | 요약 재계산·인계 | QA | derived CSV, evidence sign-off | raw만으로 요약값 일치, DU-07A 전달 |

**금지:** DU-06A-A 카드 안에서 drawing, physics, input, ink 기능을 수정하지 않는다. 계측 누락은 tech 선행 작업으로 분리하고, 새 build가 나오면 pack version을 새로 시작한다.

### 3.3 세션 운영 절차

#### 사전 준비

1. PM 승인된 InputComparisonSpec v1과 build SHA256 확인.
2. machine/profile/course/reset/physics/ink 값 manifest snapshot.
3. participant alias와 order를 사전 배정:
   - P01/P03: Aim→Trajectory
   - P02/P04: Trajectory→Aim
4. 녹화·로그·저장 공간·one-click reset dry-run.
5. pilot 데이터가 raw 본시험 폴더에 없는지 확인.

#### 참가자 세션

1. 공통 안내문 낭독. 특정 mode의 장단점을 암시하지 않는다.
2. 입력 장비와 화면 보정 후 `EvidenceSessionStart`.
3. 첫 mode **고정 5분 학습**. QA 권고는 mode별 5분이며 planning/PM 확정 전 세션 금지.
4. 평가: 수평·상승대각·두 접점 bridge 각 3회, 총 9 trial. task 순서는 모든 참가자에게 동일한 balanced order 또는 사전 고정 Latin order를 사용한다.
5. trial마다 one-click reset. coaching, 추가 연습, 임의 재시도 금지.
6. 첫 mode 설문 후 두 번째 mode도 동일하게 5분 학습→9 trial→설문.
7. 최종 비교 선호 설문, observer note, session 종료.

장비/포커스/녹화 결함으로 무효가 된 trial은 raw에 유지하고 `excluded=true, exclusion_reason`을 기록한다. 동일 mode 전체를 새 session suffix로 재실행하며 유효 trial만 골라 섞지 않는다.

### 3.4 Gate A raw schema 핵심

#### `sessions.csv`

`schema_version, session_id, participant_id, order_group, first_mode, second_mode, build_id, machine_id, started_utc, ended_utc, completed, exclusion_reason`

#### `attempts.csv` — **골든 row 수 72**

`schema_version, session_id, participant_id, order_group, mode, task_id, repetition, attempt_id, attempt_start_us, first_draw_start_us, confirm_us, attempt_end_us, success, completion_ms, confirm_latency_ms, range_misunderstanding, depth_misunderstanding, reject_count, cancel_count, reset_count, failure_id, excluded, video_file, video_in_ms, video_out_ms`

- 각 participant×mode = 9 row
- 각 mode = 36 row
- 전체 = 72 row
- `confirm_latency_ms = confirm_us - first_draw_start_us` 권고. 정확한 시작·종료 이벤트는 planning이 동결한다.

#### `strokes.csv`

`stroke_id, attempt_id, owner_id, mode, outcome, cancel_reason, start_us, end_us, raw_point_count, deduped_point_count, simplified_point_count, length_m, max_reach_m, rejected_append_count, ink_reserved, ink_committed, ink_refunded, deleted_us`

#### `events.csv`

`session_id, event_seq, monotonic_us, frame, fixed_tick, attempt_id, stroke_id, event_type, reason_code, input_device_id_hash, hand_x/y/z, point_x/y/z, distance_from_hand_m, ink_available, live_owned_length`

#### `surveys.csv`

`participant_id, mode, ease_1_7, reach_clarity_1_7, depth_clarity_1_7, control_confidence_1_7, frustration_1_7, preferred_mode, preference_reason, submitted_utc`

척도 anchor 문구를 schema에 포함한다. 빈 응답은 빈 문자열이 아니라 `NOT_ANSWERED` enum으로 남긴다.

### 3.5 Gate A 골든값과 수용 기준

#### 증거팩 수용 (`EVIDENCE_COMPLETE`)

- participant 4명, mode 2개, task 3개, repetition 3회, 유효 평가 row 72개.
- mode 순서 Aim-first 2명 / Trajectory-first 2명.
- 학습 데이터와 평가 데이터 완전 분리.
- 모든 trial에 raw event, attempt summary, video timestamp, survey 연결.
- build/schema/machine provenance 100%, SHA256 검증 PASS.
- `available + liveOwnedLength = inkCap` 재계산 가능. 위반 발생 시 숨기지 않고 제품 결함으로 제출.
- derived 지표를 raw로 재계산했을 때 성공률·median 값이 제출 요약과 정확히 일치.
- 제외/재실행 사유 없는 임의 row 삭제 0.

#### DU-07A에 전달할 권고 판정식

정본 문장의 가장 재현 가능한 해석은 다음이다.

- tester qualification(mode): `success ≥ 7/9 AND median_confirm ≤ 2,000ms AND (range+depth misunderstanding failures) ≤ 2/9`.
- mode PASS: qualified tester ≥ 3/4.
- aggregate success가 60~79%이며 결함이 표현 문제로 판정된 경우에만 1일 수정 1회. 수정 build 데이터는 이전 build와 합치지 않는다.
- 두 mode 모두 PASS면 aggregate success rate 20%p 이상 우세 mode 채택.
- 성공률 차이가 20%p 미만이고 **성공 trial completion median**이 25% 이상 빠른 mode 채택.
- 둘 다 미달이면 Aim tie-break.

이 판정식은 QA 권고이며 `game-planning`과 PM이 E01에서 동결해야 한다.

### 3.6 Gate A 차단요인

| 차단 | 심각도 | 해소 책임 |
|---|---|---|
| 5분 학습 단위 불명확 | P0 | planning/PM |
| confirm metric 이벤트 정의 없음 | P0 | planning + tech |
| range/depth misunderstanding 판정 taxonomy 없음 | P0 | planning + QA |
| 비교 build에서 course/reset/backend/assist 동일성 확인표 없음 | P0 | tech + QA |
| 영상 동의/보존 정책 미정 | P1 | PM |
| tester 4명과 M+K hardware matrix 미확보 | P1 | PM/QA |

## 4. DU-06A-B — Gate B 증거팩

### 4.1 책임자·체크포인트·의존성

- DRI / session owner: `game-qa`
- 계측 책임: `game-tech-director`
- social taxonomy·판정표: `game-planning`
- 최종 승인: `project-manager`
- 다음 체크포인트: **동일 모델 gamepad 2개 preflight + observer coding sheet 동결**
- 선행 의존성: DU-07A PASS, DU-06B 완료, 동일 모델 gamepad 2개, DU-06A-B schema v1
- 후행: `EVIDENCE_COMPLETE` 뒤 DU-07B

### 4.2 작업 분해

| ID | 작업 | 책임 | 산출물 | 완료 조건 |
|---|---|---|---|---|
| B-E01 | social event dictionary 동결 | planning/QA | usage/hindrance/wait/conflict 정의 | 관찰자 판정 가능한 예/반례 포함 |
| B-E02 | schema·manifest 동결 | QA | schema 1.0.0, sheets | player/pair/stroke ownership 연결 검증 |
| B-E03 | 2-controller 계측 preflight | tech/QA | join/disconnect/focus/ledger 샘플 | controller slot swap 포함 테스트 |
| B-E04 | 비평가 pilot 1쌍 | QA | pilot pack | usage debounce, observer timestamp 오차 검증 |
| B-E05 | 2쌍×10분 본세션 | QA | pair packs 2개 | pair-minutes=20, coaching 0 |
| B-E06 | annotation·repro·무결성 검사 | QA/tech | annotations, repro, checksums | 사건 양방향 링크/owner ledger 검증 |
| B-E07 | pair summary·인계 | QA | pair metrics, evidence sign-off | Gate A 지표와 분리, DU-07B 전달 |

**금지:** DU-06A-B에 협력 hook, 방해 규칙, 입력, owner, delete/refund 기능을 구현하지 않는다. 관측 중 설계 문제가 보여도 증거로 남기고 DU-07B 이후 별도 재설계 카드로 보낸다.

### 4.3 세션 운영 절차

1. 동일 모델/firmware gamepad 2개를 고정 slot에 연결하고 manifest 기록.
2. P01+P02=`PAIR01`, P03+P04=`PAIR02`; 개별 alias와 controller hash를 결합.
3. join, disconnect/reconnect, focus loss/restore, owner color, 독립 ledger preflight.
4. 공통 shared-goal 안내문만 제시한다. **타인 발판 사용·삭제·방해를 하라고 지시하지 않는다.** 자발성 검증이므로 세션 중 coaching 0.
5. 10분 clock은 두 플레이어 join + control 획득 뒤 시작.
6. QA observer는 사건 버튼 또는 annotation sheet로 timestamp와 category만 기록하고 해석은 종료 후 clip review에서 확정.
7. 세션 종료 직후 각자 분리 설문. 상대의 답변을 보지 않게 한다.
8. observer annotation과 telemetry를 reconciliation하고 pair pack을 봉인한다.

연결 끊김·focus loss는 자동으로 session을 실패 처리하지 않는다. 복구 시간을 기록하고 10분 clock 정책(`wall-clock` 또는 `active-play`)을 E01에서 동결한다. QA 권고는 실제 사회적 노출량 비교를 위해 **active-play 10분**이다.

### 4.4 Gate B raw schema 핵심

#### `sessions.csv`

`schema_version, session_id, pair_id, player1_id, player2_id, build_id, machine_id, gamepad_model_key, started_utc, active_play_ms, wall_clock_ms, completed, exclusion_reason`

#### `strokes.csv`

Gate A 공통 필드 + `pair_id, creator_player_id, committed_length_m, used_by_owner_count, used_by_other_count, delete_requester_id, refund_recipient_id`.

#### `events.csv`

`session_id, event_seq, monotonic_us, pair_id, actor_player_id, target_player_id, stroke_id, stroke_owner_id, event_type, reason_code, contact_begin_us, contact_end_us, ledger_before, ledger_delta, ledger_after, video_ms`

Gate B 필수 event enum:

- PLAYER_JOINED, PLAYER_DISCONNECTED, PLAYER_RECONNECTED, FOCUS_LOST, FOCUS_RESTORED
- STROKE_COMMITTED, FOREIGN_STROKE_CONTACT_BEGIN/END, FOREIGN_STROKE_USE_EPISODE
- DELETE_REQUESTED, DELETE_COMMITTED, REFUND_APPLIED_TO_ORIGINAL_OWNER
- INPUT_WAIT_BEGIN/END, INPUT_CONFLICT
- LEDGER_INVARIANT_CHECKED

`FOREIGN_STROKE_USE_EPISODE` 권고 정의: 상대 stroke에 올라서 실제 이동/목표 진전에 사용한 구간. 같은 stroke 접촉이 2초 이내 재발하면 같은 episode로 묶는다. 단순 collider contact frame은 사용 횟수로 세지 않는다. planning 승인 필요.

#### `observer_annotations.csv`

`interaction_id, session_id, pair_id, observer_id, start_ms, end_ms, actor_id, target_id, category, intentionality, valence, confidence, telemetry_event_seq_start/end, video_file, clip_in/out_ms, note`

category:

- `STRUCTURAL_HELP`
- `STRUCTURAL_HINDRANCE`
- `ACCIDENTAL_HINDRANCE`
- `DELETION_HELP`
- `DELETION_HINDRANCE`
- `RESOURCE_CONFLICT`
- `INPUT_WAIT`
- `COMMUNICATION_NEGOTIATION`
- `OTHER`

intentionality/valence는 observer 추론만으로 확정하지 않는다. 영상 + 해당 참가자의 사후 설문이 일치하면 `CONFIRMED`, 아니면 `AMBIGUOUS`로 남긴다.

#### `surveys.csv`

`participant_id, pair_id, helped_other_count_selfreport, used_other_stroke_count_selfreport, hindered_intentionally_count, hindered_accidentally_count, cooperation_1_7, conflict_1_7, fun_1_7, agency_1_7, would_replay_1_7, memorable_event_timestamp_or_note, open_comment`

### 4.5 Gate B 골든값과 수용 기준

#### 증거팩 수용 (`EVIDENCE_COMPLETE`)

- 2쌍, 참가자 4명, pair당 active-play 10분, 총 pair-minutes=20.
- 동일 모델 gamepad 2개 사용이 manifest로 입증됨.
- player↔device↔owner color↔ledger 연결 누락 0.
- 각 player stroke share를 committed length와 count 양쪽으로 산출. primary 분모는 E01에서 동결.
- pair당 `FOREIGN_STROKE_USE_EPISODE` 수와 원 clip 100% 연결.
- delete 요청→commit→원소유자 refund 로그 체인을 사건별 재구성 가능.
- 구조/실수 방해, wait/conflict 사건은 raw telemetry + observer annotation + clip로 연결.
- disconnect/focus event가 없더라도 `0`으로 명시; 필드/파일 생략 금지.
- Gate A mode 성공률·confirm 지표를 Gate B summary에 합산하지 않는다.
- raw→pair summary 재계산 일치, checksums PASS.

#### DU-06B 계약 검증값

- 각 player stroke contribution ≥30%: primary 지표 정의는 planning 승인 전 미확정. QA 권고는 committed length share.
- 다른 플레이어 stroke 사용 ≥3 episode/pair.
- owner/delete/refund 불일치 = 0.
- interaction 사건은 수량과 맥락을 기록하되, 현 정본에는 social hook PASS 최소 사건 수가 없다. QA가 임의로 threshold를 추가하지 않는다.
- Gate B 미달은 Gate A core kill로 전파하지 않는다.

### 4.6 Gate B 차단요인

| 차단 | 심각도 | 해소 책임 |
|---|---|---|
| stroke 30% 분모 미정 | P0 | planning/PM |
| foreign use episode debounce 미정 | P0 | planning/QA |
| 자발 interaction / 구조·실수 방해 taxonomy 미정 | P0 | planning/QA |
| DU-07A PASS 및 DU-06B 완료 전 | P0 | 선행 카드 |
| 동일 모델 gamepad 2개·firmware 일치 미확보 | P0 | PM/tech |
| session clock이 wall/active-play인지 미정 | P1 | planning/QA |
| 사후 설문·영상 동의/보존 정책 미정 | P1 | PM |
| 표본 2쌍의 과대해석 위험 | P1 | planning/PM — smoke evidence로만 사용 |

## 5. 최종 책임·체크포인트 표

| 카드 | 책임자 | 다음 체크포인트 | 현재 판정 | 핵심 차단 |
|---|---|---|---|---|
| DU-06A-A | game-qa DRI / tech 계측 / planning 판정표 / PM 승인 | Schema v1 + SOP + provenance preflight | 조건부 승인 | metric definition 5건, tester/hardware, consent |
| DU-06A-B | game-qa DRI / tech 계측 / planning social taxonomy / PM 승인 | 2-controller preflight + observer coding sheet | 조건부 승인 | DU-07A/DU-06B, event definition 4건, gamepad 2개 |

## 6. QA 사인오프

- 카드 구조: **CONDITIONAL PASS**
- 세션 실행 준비: **BLOCKED**
- 차단 해제 조건:
  1. planning/PM이 본 보고서의 P0 metric·taxonomy 정의를 승인 또는 대체값으로 동결한다.
  2. tech가 필수 이벤트를 샘플 raw로 제공하고 QA schema validator를 통과한다.
  3. QA pilot에서 raw↔영상↔설문↔요약 재계산이 100% 연결된다.
- 기능 구현: 수행하지 않음.
- git 작업: 수행하지 않음.

## 7. QA 프로세스 노트

1. **pilot은 본시험 표본에 포함하지 않는다.** pilot에서 schema나 build가 바뀌면 version을 증가시킨다.
2. **결손을 평균으로 메우지 않는다.** missing은 reason code로 보존하고 EVIDENCE_INVALID 여부를 판단한다.
3. **사용성 실패와 기술 결함을 한 failure rate로 합치지 않는다.** Gate A 입력 생존 판단이 엔진/장비 문제로 왜곡된다.
4. **Gate B 관찰을 Gate A 지표에 섞지 않는다.** 정본의 core kill 경계다.
5. **2쌍은 social smoke이지 통계 검증이 아니다.** 유지/재설계 신호까지만 사용하며 일반화하지 않는다.
