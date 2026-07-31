# DoodleUp DU-02 REV2 최종 독립 QA 수용 검토

- 검토일: 2026-07-31
- 검토자: `game-qa`
- 대상: DU-02 부트스트랩·리셋 가능한 솔로 코스 REV2
- 이전 REV1 판정: `CHANGES REQUIRED` — D3-R1 단일 차단요인
- REV2 최종 판정: **PASS**
- 정본:
  - `D:\Project-DoodleUp\docs\prototype-execution-plan.md` DU-02
  - `D:\Project-DoodleUp\docs\input-comparison-spec-v1.md`
  - `D:\Project-DoodleUp\docs\architecture\du-02-bootstrap.md`
  - `D:\Project-DoodleUp\docs\qa\logging-guide.md`
- 제한: 제품 코드·제품 문서 수정 없음, git 작업 없음. 제출된 증거·구현을 독립 검토하고 raw를 별도 재계산함

## 1. 최종 판정

# PASS

REV1의 유일한 차단요인 D3-R1이 종료됐다.

T1/T2/T3 × `R_KEY`/`LANE_SELECT` 6개 조합 모두에서 다음 상태 전이를 machine-readable raw로 확인했다.

- rotation: identity → non-identity → identity
- angular velocity: zero → `(1.5,-2.25,3.75)` → zero
- phase: Idle → ProbePerturbed → Idle
- `beforeHash != baselineHash`
- `beforeDiffers=True`
- `afterHash == baselineHash`
- `afterEqual=True`
- `rotationRestored=True`
- `angularVelocityRestored=True`
- `phaseRestored=True`

D1/D2/D4~D7에도 회귀가 없고 DU-03A 이후 기능도 선행되지 않았다. **DU-02 QA 차단요인은 0건**이다.

## 2. 검증 환경과 증거

- OS: Windows 11 Education `10.0.26200`
- Unity: `6000.4.0f1 (8cf496087c8f)`
- Input System: `1.17.0`
- Standalone: Windows x64 Development Build, Null graphics, batch mode
- compile: `D:\.adk\logs\unity\compile-20260731-223445.log`
- scene: `D:\.adk\logs\unity\method-20260731-223457.log`
- build: `D:\.adk\logs\unity\build-20260731-223505.log`
- runtime: `D:\.adk\logs\unity\du02-standalone-runtime-rev2-final-20260731.log`
- aggregator: `D:\.adk\logs\unity\method-20260731-223850.log`
- EditMode: `D:\.adk\logs\unity\test-20260731-223917.log`
- PlayMode: `D:\.adk\logs\unity\test-20260731-223933.log`
- EditMode XML: `D:\.adk\logs\unity\testresults-20260731-223917.xml`
- PlayMode XML: `D:\.adk\logs\unity\testresults-20260731-223933.xml`
- raw: `D:\Project-DoodleUp\DU02_Runtime_Raw.csv`
- report: `D:\Project-DoodleUp\DU02_Verification_Report.txt`
- executable: `D:\Project-DoodleUp\Builds\DU02_RuntimeProbe\DoodleUp-DU02-Probe.exe`

QA 독립 SHA256:

| 산출물 | SHA256 |
|---|---|
| raw CSV | `c10862b8b7ab33d747b33d815f62a26a65b144d1051cdc01a97e60cd2fccb924` |
| report | `ee77572b2bde11b3f97a44cf161ef8f0cc75900d9da6165ee6541dbbc30f9edc` |
| executable | `ea4801dcff125a88a733fc266f84564a7117944619b2b20d1200399fefc5358a` |
| built runtime assembly | `e4283d14f256b71c5a12d92fd627eb287f01256c30cf987155ae8f2d844c3bcb` |

Runtime provenance의 executable/runtime assembly SHA256은 QA 독립 계산과 일치한다.

## 3. 수용 기준별 결과

| 수용 기준 | 판정 | 근거 |
|---|---|---|
| 1. D3-R1 네 기준 | **PASS** | 6개 reset 조합 전부 baseline/before/after, 실제 교란, 실제 복원, equality/hash/assertion, machine-readable evidence 충족 |
| 2. D1/D2/D4~D7 회귀 없음 | **PASS** | compile/build/tests/runtime/provenance/task-state/sampling 모두 통과 |
| 3. raw hash·report 집계 독립 일치 | **PASS** | raw SHA256가 제출값·report·QA 계산에서 일치, 3 sampling 및 6 reset row 독립 재계산 PASS |
| 4. DU-03A 이후 기능 미선행 | **PASS** | 실제 StrokeSession/Aim/Trajectory/stroke capsule chain/ink ledger/backend mutation 없음 |

## 4. D3-R1 네 수용 기준 독립 확인

### AC-D3-1 — 3 lanes×2 paths 모두 실제 non-canonical before 교란

**PASS**

Raw row는 T1/T2/T3와 `R_KEY`/`LANE_SELECT`의 정확한 Cartesian set 6건이며 중복·누락이 없다.

모든 row의 골든값과 실제값:

| 필드 | baseline | before | after |
|---|---|---|---|
| rotation | `0|0|0|1` | `0.192268|0.151299|0.230600|0.941788` | `0|0|0|1` |
| angular velocity | `0|0|0` | `1.5|-2.25|3.75` | `0|0|0` |
| phase | `Idle` | `ProbePerturbed` | `Idle` |

구현 대조:

- `Du02RuntimeProbeRunner.cs:105~118`에서 reset 전 baseline과 perturbation 후 before를 별도 캡처한다.
- `Du02RuntimeProbeRunner.cs:157~170`에서 non-identity rotation, non-zero angular velocity, ProbePerturbed phase를 실제 주입한다.
- `Du02PlayerMotor.cs:27~31,50~63`이 probe state를 Rigidbody transform/velocity에 적용한다.
- `Du02TaskState.cs:93~106`이 phase를 ProbePerturbed로 변경한다.

### AC-D3-2 — reset 후 canonical 복원

**PASS**

모든 6개 row에서:

- `afterHash == baselineHash`
- `afterEqual=True`
- rotation identity 복원
- angular velocity zero 복원
- phase Idle 복원
- 세 restored flag 모두 True

`Du02RuntimeProbeRunner.cs:120~133`은 reset 이후 after snapshot을 캡처하고 세 필드를 명시적으로 assertion한다. `Du02PlayerMotor.cs:44~48`은 reset 시 rotation identity, angular velocity zero와 회전 고정 constraint를 복원한다.

### AC-D3-3 — equality/hash/assertion에 angular velocity와 phase 포함

**PASS**

- `Du02ResetSnapshot.cs:13,22`에 AngularVelocity와 Phase가 존재한다.
- `Du02ResetSnapshot.cs:77~100` equality가 AngularVelocity와 Phase를 비교한다.
- `Du02ResetSnapshot.cs:104~129` hash가 AngularVelocity와 Phase를 포함한다.
- `Du02RuntimeProbeRunner.cs:173~185` stable hash 문자열에 AngularVelocity와 Phase가 포함된다.
- `Du02RuntimeProbeRunner.cs:113~130` before/after assertion이 rotation·angular velocity·phase를 명시적으로 검사한다.
- EditMode `SnapshotDetectsAngularVelocityAndProbePhasePerturbation`도 PASS했다 (`testresults-...223917.xml:76`).

### AC-D3-4 — QA 독립 확인 가능한 machine-readable evidence

**PASS**

- Raw CSV는 27열로 baseline/before/after hash, rotation, angular velocity, phase, restored flag를 저장한다.
- QA가 aggregator와 독립된 PowerShell 파서로 9개 data row를 검사했다.
- 결과:
  - sampling row `3`
  - reset row `6`
  - 예상 reset key set 일치
  - sampling 실패 row `0`
  - D3-R1 실패 row `0`
- report는 raw hash와 3 sampling/6 reset PASS를 그대로 재집계하며 최종 `result=PASS`다.

## 5. D1/D2/D4~D7 회귀 검증

### D1 — Parser/import/C# error

**PASS**

- 제출된 compile/scene/build/aggregator/EditMode/PlayMode 로그에서 `Parser Failure`, `Unable to parse`, `error CS####`, compiler abort 0건
- compile Tundra success, return code 0
- scene build return code 0
- player build `Build Finished, Result: Success`, return code 0
- runtime provenance: MainCamera true, Player/Course/Goal `8/9/10`, `runtimeConfigurationValid=True`

비차단 경고: `Du02GoalZone.cs:19` obsolete API `CS0618` 경고가 남아 있으나 error가 아니며 DU-02 기능·증거 수용을 막지 않는다.

### D2 — 실제 sampling

**PASS**

QA 독립 raw 재계산:

| fps request | observed frame | sample | duplicate | missing | elapsed | 판정 |
|---:|---:|---:|---:|---:|---:|---|
| 30 | 299 | 299 | 0 | 0 | 10.000514s | PASS |
| 60 | 599 | 599 | 0 | 0 | 10.000304s | PASS |
| 144 | 1438 | 1438 | 0 | 0 | 10.005168s | PASS |

각 target FPS는 정확한 frame total 보장이 아니다. 골든값은 monotonic elapsed≥10초, frame=sample, duplicate/missing=0이다.

### D4 — atomic reset 전체 범위

**PASS**

Player pose/linear·angular velocity, hand pose/scale, camera/FOV, countdown/timer/goal/stroke/ink/phase/input lock, sampling sequence가 reset snapshot과 hash에 포함된다. 6개 reset 조합 모두 canonical 상태로 복원됐다.

### D5 — HandMarker scale

**PASS**

Canonical `(1,1,1)`이 scene/reset/snapshot/hash에서 일치한다.

### D6 — provenance 및 runtime 로그

**PASS**

Runtime tag count:

- provenance `1`
- course `3`
- task reset `17`
- GO `5`
- reset `17`
- runtime sample `3`
- runtime reset `6`
- runtime task state `4`
- probe complete `1`
- depth drift `0`
- provenance invalid `0`
- `result=FAIL` `0`
- exception `0`

Expected log chain:

```text
DU02_PROVENANCE ×1
→ DU02_COURSE ×3
→ SCENE_START reset
→ DU02_RUNTIME_SAMPLE ×3
→ lane/reset + DU02_RUNTIME_RESET ×6
→ DU02_RUNTIME_TASK_STATE ×4
→ DU02_RUNTIME_PROBE_COMPLETE result=PASS
```

### D7 — task-state success seam

**PASS**

- T1 contact+1초 hold: success
- T2 contact+1초 hold: success
- T3 start band only: reject
- T3 both bands+1초 hold: success
- Runtime 4/4 PASS, PlayMode 2/2 PASS

## 6. 테스트·빌드 결과

- Compile: **PASS**, C# error 0
- Scene build: **PASS**
- Windows player build: **PASS**
- EditMode: **12/12 PASS** (`testresults-20260731-223917.xml:2`)
- PlayMode: **2/2 PASS** (`testresults-20260731-223933.xml:2`)
- Standalone sampling: **3/3 PASS**
- Runtime reset D3-R1: **6/6 PASS**
- Runtime task-state: **4/4 PASS**
- Runtime fatal/acceptance failure signal: **0**

## 7. DU-03A 이후 범위 미선행

**PASS**

정적 검색 결과:

- 실제 StrokeSession state machine 없음
- Aim/Trajectory adapter 없음
- stroke capsule chain 없음; 검색되는 CapsuleCollider는 player collider뿐
- 실제 ink ledger 없음; AvailableInk는 reset 계약용 placeholder
- candidate backend mutation, pending/commit/delete 없음
- runtime sampling seam은 observation과 sequence만 기록

`ProbePerturbed`는 reset 검증 전용 scaffold phase로, gameplay StrokeSession 구현을 선행하지 않는다.

## 8. 발견 결함

**신규 또는 잔여 수용 차단 결함 없음.**

비차단 유지보수 항목:

- `Assets/DoodleUp/Scripts/Runtime/Du02GoalZone.cs:19`의 obsolete API 경고 `CS0618`은 후속 정리 권고. 현재 동작·수용에는 영향 없음.

## 9. 책임자·다음 체크포인트·차단요인

- 최종 QA 판정 책임자: `game-qa` — **PASS**
- 카드 승인 책임자: `project-manager`
- 기술 책임자: `game-tech-director`
- 차단요인: **없음**
- 다음 체크포인트: project-manager가 DU-02를 완료 승인하고 정본 의존성에 따라 DU-03A 착수 여부 결정
- 회귀 기준: DU-03A 이후 실제 stroke state 연결 시 현재 6개 atomic reset 및 4개 task-state seam을 필수 회귀로 유지

## 10. QA 사인오프

- 최종: **PASS**
- D1: PASS
- D2: PASS
- D3/D3-R1: PASS — 종료
- D4: PASS
- D5: PASS
- D6: PASS
- D7: PASS
- DU-03A 이후 범위 미선행: PASS
- 제품 코드 수정: 수행하지 않음
- 제품 문서 수정: 수행하지 않음
- QA 보고서 갱신: 수행
- git 작업: 수행하지 않음

## 11. QA 프로세스 노트

- `afterHash==baselineHash`만으로 승인하지 않고, before가 실제 non-canonical인지와 snapshot/hash가 해당 필드를 포함하는지 함께 확인했다.
- Raw CSV, report, runtime log, probe 코드, snapshot equality/hash, test XML을 서로 독립된 증거 축으로 대조했다.
- Standalone provenance의 scene SHA256은 source scene path가 없는 player 환경에서 0이지만 executable 및 runtime assembly SHA256이 유효하고 QA 독립 hash와 일치해 artifact identity를 충족한다.
- Sampling은 target FPS별 정확한 기대 프레임 수가 아니라 실제 frame/sample 1:1과 10초 이상 monotonic 관측을 기준으로 판정했다.
