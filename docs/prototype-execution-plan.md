# DOODLE UP — P0 프로토타입 실행계획

상태: DU-01 승인 / DU-02 REV2 완료 승인 / DU-03A REV1 기획 APPROVE·독립 QA PASS 및 PM 완료 승인 / DU-03B·DU-03C 착수 / Gate A 세션은 계측 sample·QA pilot 전까지 보류
정본 갱신일: 2026-07-31  
작업공간: `D:\Project-DoodleUp`  
목표 엔진: Unity 6000.4.0f1

## 0. 프로토타입 운영 기준 (2026-08-01 결정)

P0의 목적은 재미와 입력 사용성을 빠르게 판정하는 것이다. 따라서 Gate A 판정 전 구현 카드에는 출시 수준 증거 절차를 적용하지 않는다.

- 구현 카드 완료 조건은 컴파일, 실제 플레이 가능, 핵심 계약 테스트, 기존 카드 EditMode 회귀까지다.
- RAW CSV, 독립 aggregator, hash 정합, standalone paired runtime, manifest, 장치 provenance는 `DU-06A-A` Gate A 증거팩에서 한 번에 수행한다.
- Gate A 재미 판정 전까지 구현 카드에 `game-qa`를 투입하지 않는다. 구현 카드는 `game-tech-director` 자체 확인과 `project-manager` 승인, 사용자 직접 플레이로 닫는다.
- `game-qa`는 `DU-06A-A` Gate A 증거팩에서 재투입한다. 그 시점에 축적된 acceptance 기준으로 한 번에 검증한다.
- 계약 변경이 필요한 경우에만 `game-planning` 재검토를 요청한다.
- 카드 단위는 하루 안에 플레이로 확인 가능한 크기로 쪼갠다.

이 결정의 위험은 자체 판정 오류를 즉시 잡지 못하는 것이다. 실제로 DU-02와 DU-03A에서 자체 PASS가 틀린 전례가 있으므로, 구현 카드 완료 승인 시 PM은 최소한 컴파일·플레이 가능·기존 카드 EditMode 회귀를 직접 확인한다.

이 기준은 Gate A 판정 이후 hardening 단계에서 원래 증거 수준으로 복원한다.

## 1. 의사결정 원칙

모델·공급자별 의견을 섞지 않고 역할별 결정권으로 통합한다.

- `game-planning`: 입력 비교 공정성, 판정 기준, 사회성 목표
- `game-tech-director`: 하루 구현 가능 범위, 코드 경계, Unity 입력·물리 위험
- `game-qa`: 증거팩, 재현 절차, 로그·영상 산출물
- `project-manager`: 범위, 우선순위, 의존성, Gate 순서와 최종 승인

## 2. P0 목표와 Gate

### Gate A — 솔로 M+K 입력 생존

손 원점·사거리 제한 3D 드로잉이 마우스/키보드에서 실제로 쓸 수 있는지 검증한다. Aim과 Trajectory를 같은 backend·course·reset·physics·ink 조건으로 비교하고 **입력 하나만 채택**한다.

Gate A 필수 범위:

- 1P 이동·점프와 one-click reset 3-task 코스
- 손을 지나는 camera yaw-normal vertical drawing plane: `n=Normalize(ProjectOnPlane(camera.forward, Vector3.up))`, `up=Vector3.up`, `right=Normalize(Cross(up,n))`; stroke 시작 시 hand origin과 `n` snapshot
- 공통 StrokeSession backend
- Aim M+K adapter / Trajectory M+K thin adapter
- 정적 capsule 발판과 build+traverse
- ink reserve/commit/refund와 삭제
- QA raw CSV·로그·설문·영상 증거팩

Gate A 제외:

- 동적 낙하·terrain anchor 분류
- 편측 rope/hinge
- 로컬 2인
- drop shadow·궤도 카메라·완성 UI

### Gate B — 로컬 2인 social smoke

Gate A PASS 이후 동일 모델 gamepad 2개로 남의 낙서를 이용하거나 방해하는 상황이 자발적으로 생기는지 검증한다. Gate B는 **M+K 사용성 재검증이 아니며 core kill gate도 아니다.** 미달 시 협력 hook만 재설계한다.

## 3. 최종 카드와 실행 순서

### DU-01 InputComparisonSpec v1 동결

- 책임자: `game-planning` DRI / `project-manager` 승인 / `game-tech-director` 자문 / `game-qa` evidence review
- 다음 체크포인트: DU-02 부트스트랩·리셋 가능한 솔로 코스
- 차단요인: 없음 — `InputComparisonSpec v1 Approved` 완료
- 수용 기준(승인 완료):
  - reach, thickness, ink cap, sample spacing, minimum stroke length, camera/FOV, 이동속도 수치 profile을 고정한다.
  - Aim/Trajectory는 mapping 외 backend·assist·course·reset을 공유하고 snap/auto-anchor를 끈다.
  - 사거리 밖은 red invalid preview+append 금지, release 자동 확정 금지, 최소 길이 미만은 ink 0 cancel로 처리한다.
  - Gate A 임계치·CSV schema와 Gate B 동일 gamepad 2개 구성을 명시한다.
  - 기술·QA 검토를 통과한 정본은 `docs/input-comparison-spec-v1.md`이며 Gate A 종료까지 변경 통제를 적용한다.

### DU-02 부트스트랩·리셋 가능한 솔로 코스 — 완료 승인 (2026-07-31)

- 책임자: `game-tech-director` 구현 / `game-qa` 독립 수용 판정 / `project-manager` 완료 승인
- 다음 체크포인트: DU-03A 공통 StrokeSession backend의 고스트 1획 commit/cancel
- 차단요인: 없음 — DU-02 REV2 독립 QA PASS
- 수용 기준(승인 완료):
  - Unity 6000.4.0f1 재현 씬과 Input/Stroke/Physics asmdef 경계를 만든다.
  - 동일 spawn/hand/camera reset, 수평·상승대각·두 접점 bridge task를 제공한다.
  - engine/input device/fixedDeltaTime 로그와 실제 30/60/144fps runtime sampling 검증을 남긴다.
  - T1/T2/T3 × R_KEY/LANE_SELECT 6개 경로에서 player rotation·angular velocity·task phase를 포함한 atomic reset을 machine-readable baseline/before/after로 검증한다.
  - 정본 QA 보고서는 `docs/qa/reports/2026-07-31-doodleup-du02-acceptance-review.md`이며 D1~D7 모두 PASS다.

### DU-03A 공통 StrokeSession backend — 완료 승인 (2026-07-31)

- 책임자: `game-tech-director` 구현 / `game-planning` 공정성 승인 / `game-qa` 독립 수용 판정 / `project-manager` 완료 승인
- 다음 체크포인트: DU-03B Aim M+K adapter / DU-03C Trajectory M+K thin adapter
- 차단요인: 없음 — DU-03A REV1 기획 APPROVE 및 독립 QA PASS
- 수용 기준(승인 완료):
  - Idle→Drawing→Pending→Committed|Cancelled 상태머신을 구현한다. release는 simplification 전 accepted resampled length로 Cancelled 또는 Pending을 결정하며, Pending에는 collider가 없고 명시적 Confirm만 capsule chain을 생성한다. stroke 시작 시 hand origin과 camera yaw-normal `n`을 snapshot한다.
  - 고정 child HandMarker와 60 fps LateUpdate candidate phase, hand-origin yaw-normal vertical plane projection, reach validation, 거리 resampling, dedupe, Douglas-Peucker `0.02 u` simplification을 담당한다.
  - `StrokeData(simplifiedPoints,chargedLength,ownerId,mode)`를 불변 데이터로 산출한다. `simplifiedPoints`는 Douglas-Peucker `0.02 u` 적용 후 collider geometry이고, `chargedLength`는 simplification 전 accepted resampled polyline length이며 `simplifiedPoints`에서 재계산하지 않는다.
  - adapter는 물리·잉크·소유권을 모르고 DrawIntent만 전달한다.

### DU-03B Aim M+K adapter / DU-03C Trajectory M+K thin adapter — 착수 (2026-08-01)

- 책임자: `game-tech-director` 구현 / `game-qa` 독립 판정 / `project-manager` 완료 승인 / mapping·공정성 계약 변경은 `game-planning` + `project-manager`
- 다음 체크포인트: 공통 input latch·execution manifest·adapter evidence schema 확정 후 각 source 구현
- 차단요인: 없음 — DU-03A 완료 승인, 기술 `CONDITIONAL READY`, QA `READY WITH ACCEPTANCE GATES`
- 실행 순서:
  1. 공통 M+K input-edge latch(LMB Draw, `E` Confirm, `RMB`/`Esc` Cancel), execution manifest, adapter evidence schema를 먼저 고정한다. `R`은 trial reset 소유이며 Cancel로 소비하지 않는다.
  2. Aim source와 Trajectory source, 각 mapping 단위 테스트는 병렬로 진행한다.
  3. scene active-source 선택과 reset wiring은 한 곳에서 순차 통합한다.
  4. 동일 build로 두 mode paired runtime evidence를 생성한다.
  5. 각 mode가 T1/T2/T3를 동일 조건에서 3회 통과한다.
  6. DU-03A 14/14와 DU-02 sampling/reset/task-state 회귀를 확인한다.
- 수용 기준:
  - Aim은 60 fps LateUpdate에서 mouse ray를 stroke 시작 시 hand origin과 camera yaw-normal `n`으로 snapshot한 world-vertical plane에 투영한다. Trajectory는 같은 LateUpdate에서 locomotion/physics 적용 후 고정 child `HandMarker.position`을 읽는다.
  - Trajectory는 cursor steering·원격 point를 금지하며, Aim/Trajectory 모두 같은 candidate sample phase와 공통 backend를 사용한다.
  - backend 수정 없이 adapter만 교체하고 성공시간·reject·cancel 지표를 동일하게 산출한다. 두 adapter는 `IDu03ADrawIntentSource`만 구현하고 자체 상태머신·잉크·물리·소유권·geometry를 갖지 않으며, 기존 `Du03AStrokeDriver`의 단일 LateUpdate가 intent를 소비한다.
  - 실제 Input System edge가 latch되고 실제 LateUpdate에서 정확히 1회 소비된다. Drawing 중 frame당 candidate는 정확히 1개이고 release frame은 `CANDIDATE>RELEASE`다.
  - 정상 raw candidate는 독립 계산값과 오차 `1e-5 u` 이내다. Aim은 ray-plane 교점, Trajectory는 같은 tick `HandMarker.position`과 일치하며 Trajectory candidate는 mouse 입력에 영향받지 않는다.
  - mapping 실패는 숫자 0이 아니라 null과 `NO_PLANE_INTERSECTION`/`NON_FINITE`로 기록하고 ledger·state를 변경하지 않는다.
  - QA 필수 scenario는 DU-03B `A01~A12`, DU-03C `T01~T12`이며 정본 QA 기준은 `docs/qa/reports/2026-08-01-doodleup-du03bc-adapter-preflight-review.md`다.
  - `X` committed Delete/refund와 tester mouse 장치 provenance는 이 카드 범위가 아니며 각각 DU-05A와 DU-06A-A에서 수용한다. 두 adapter가 Delete/refund나 backend API를 구현·호출하면 즉시 FAIL이다.

### DU-04A 최소 정적 capsule 발판

- 책임자: `game-tech-director`
- 다음 체크포인트: 발판 10회 왕복
- 차단요인: DU-03A
- 수용 기준:
  - root 1개+child capsule chain, gap 없음, runtime scale=1을 지킨다.
  - fixedDeltaTime과 physics material을 고정하고 seam 관통·snag·폭발 0회를 확인한다.

### DU-05A Ink ledger·삭제 환급

- 책임자: `game-tech-director` DRI / `game-planning` 수치 승인
- 다음 체크포인트: draw→밟기→delete→redraw
- 차단요인: DU-03A·DU-04A / DU-01 잉크 수치
- 수용 기준:
  - reserve→commit→refund와 자연 회복 0을 적용한다.
  - owner별 `availableInk + drawingReservedLength + pendingReservedLength + Σ committedLiveOwnedChargedLength = inkCap` 불변식을 모든 transition 전후에 통과한다. charged length는 simplification 전 accepted resampled polyline length이며 Confirm은 pending reserve를 committed로 이전하고 Drawing/Pending Cancel과 committed Delete는 원소유자에게 정확히 환급한다.
  - cancel/delete 시 원소유자에게 즉시 환급하고 audit log를 남긴다.

### DU-06A-A Gate A 증거팩

- 책임자: `game-qa` DRI / `game-tech-director` 계측 / `game-planning` 판정표 / `project-manager` 승인
- 다음 체크포인트: Schema v1 + session SOP + build provenance preflight
- 차단요인: DU-01 metric/taxonomy 동결, tech sample raw schema 통과, QA pilot 100% 연결
- 수용 기준(QA 전문 검토 CONDITIONAL PASS):
  - `EVIDENCE_COMPLETE`와 제품 PASS/FAIL을 분리한다. 제품 FAIL을 정확히 담은 팩도 완료할 수 있다.
  - manifest에 build_id·binary SHA256·Unity/scene/course/profile/schema/environment/hardware를 고정한다.
  - `monotonic_us+event_seq`와 raw↔로그↔영상↔설문/annotation을 연결하고 checksums를 남긴다.
  - 4명×2 mode×3 task×3회=72 attempt rows, mode별 5분 학습, 순서 2명씩 교차한다.
  - tester qualified는 7/9 성공 AND 성공 trial confirm median≤2초 AND range+depth 오해≤2/9로 계산하고, 3/4 qualified이면 mode PASS다.
  - raw는 세션 종료 후 불변이며 derived는 raw에서 재산출한다. 기능 수정은 이 카드에 넣지 않는다.
  - 비평가 pilot에서 raw↔영상↔설문↔요약 재계산 100% 연결 후 본세션을 승인한다.

### DU-07A Gate A — 솔로 M+K 입력 생존 판정

- 책임자: `game-planning` 판정 DRI / `project-manager` gate 승인 / `game-qa` evidence / `game-tech-director` 자문
- 다음 체크포인트: Gate A 증거팩 리뷰 회의
- 차단요인: DU-06A-A
- 수용 기준:
  - Aim/Trajectory 각각 PASS/FAIL을 판정하고 입력 하나만 채택한다.
  - Continue: 3/4 테스터가 한 mode 7/9 성공, median confirm≤2초, 사거리·깊이 오해 실패 median≤2/9.
  - 성공률 60~79%이고 표현 문제면 1일 수정 1회만 허용한다.
  - 둘 다 통과 시 성공률 20%p 또는 시간 25% 우세 mode를 택하고, 차이가 작으면 Aim을 tie-break로 택한다.

### DU-04B 동적 낙하·terrain anchor 분류

- 책임자: `game-tech-director`
- 다음 체크포인트: anchored/airborne 각 10회
- 차단요인: DU-07A PASS·terrain mask/tolerance/self-contact 정책
- 수용 기준:
  - LayerMask·trigger ignore·self/character 제외·skin tolerance로 분류한다.
  - 루트 Rigidbody 1개+child colliders로 공중 낙하 안정성을 확인한다.

### DU-06B 로컬 2인·Gate B social smoke

- 책임자: `game-tech-director` DRI / `game-qa` 세션 owner
- 다음 체크포인트: 동일 모델 gamepad 2개로 2쌍×10분
- 차단요인: DU-07A PASS·동일 모델 gamepad 2개
- 수용 기준:
  - device 고정 pair, 명시적 join, disconnect/focus 복구를 제공한다.
  - owner/color/ledger를 격리하고 타인 발판 이용·삭제·원소유자 환급을 검증한다.
  - 각자 stroke 30%+, pair당 타인 stroke 사용 3회+와 상호작용 사건을 기록한다.
  - 결과를 Gate A M+K 사용성 근거로 섞지 않는다.

### DU-06A-B Gate B 증거팩

- 책임자: `game-qa` DRI·session owner / `game-tech-director` 계측 / `game-planning` social taxonomy / `project-manager` 승인
- 다음 체크포인트: 동일 모델 gamepad 2개 preflight + observer coding sheet 동결
- 차단요인: DU-07A PASS·DU-06B 완료, social taxonomy 동결, 동일 gamepad 2개, tech raw·QA pilot preflight
- 수용 기준(QA 전문 검토 CONDITIONAL PASS):
  - `EVIDENCE_COMPLETE`와 사회성 PASS/FAIL을 분리하고 기능 구현을 이 카드에 넣지 않는다.
  - build provenance·schema/environment/hardware를 manifest에 고정하고 raw 불변·derived 재산출을 지킨다.
  - 2-controller preflight→비평가 pilot 1쌍→2쌍×active-play 10분→annotation/repro/무결성→pair summary 순서로 운영한다.
  - stroke 30%의 1차 지표는 committed ink length share로 계산하고 횟수는 보조 지표로 쓴다.
  - foreign-use는 목표 진전에 실제 사용한 episode로 세며, 같은 stroke의 2초 이내 재접촉은 1회이고 단순 contact frame은 제외한다.
  - 자발성 검증을 위해 타인 발판 사용·방해를 지시하지 않는다.
  - raw↔로그↔영상↔annotation 연결과 checksums 100% 통과 후 판정에 인계한다.

### DU-07B P0 최종·사회성 판정

- 책임자: `game-planning` DRI / `project-manager` 최종 승인 / `game-qa` evidence / `game-tech-director` 자문
- 다음 체크포인트: Gate B 증거팩 리뷰 회의
- 차단요인: DU-06A-B
- 수용 기준:
  - core 입력 계속/중단은 Gate A 결정을 유지한다.
  - 협력 hook 유지/재설계와 다음 milestone·risk를 기록한다.
  - Gate B 미달은 core kill이 아니라 social hook 재설계로 분리한다.

### DU-04C 편측 rope/hinge 안정화 — P0 제외 후보

- 책임자: `game-tech-director`
- 다음 체크포인트: Gate B 이후 별도 승인
- 차단요인: P0 필수 아님·DU-04B·DU-07B
- 수용 기준: 편측 pivot과 joint 안정성 로그. 별도 승인 전 착수하지 않는다.

## 4. 최종 의존성

```text
DU-01
  → DU-02
    → DU-03A
      → DU-03B / DU-03C / DU-04A
      → DU-04A → DU-05A
        → DU-06A-A → DU-07A (Gate A)
          → DU-04B (조건부 동적 물리)
          → DU-06B → DU-06A-B → DU-07B (Gate B/최종)
            → DU-04C (별도 승인)
```

## 5. P0 제외

온라인/host authority, 4인, matchmaking/join UI, 완성 아트·사운드·VFX·메뉴·저장, 화살·전투·적·로그라이트, 완성 궤도카메라, drop shadow, 팔 IK, 가변 두께·복수 펜·자연 회복, pooling/ECS/Jobs, 네트워크 압축, 시소·추·방패·사출, 편측 rope/hinge 완성도.
