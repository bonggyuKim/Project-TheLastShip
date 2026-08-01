# DU-03B/C 에디터 플레이 검증

상태: DU-03BC-IMPL-1 PM 축소 범위 (2026-08-01)

## 완료 기준

1. `D:/Project-DoodleUp` Unity compile 오류 0
2. 에디터 Play에서 `Tab`으로 Aim/Trajectory 전환 및 실제 입력 플레이 가능
3. 양 mode가 `Du03BCInputEdgeLatch`와 `Du03AStrokeDriver`/`Du03AStrokeSession`을 공유하고 DU-03A backend 무수정
4. EditMode에서 Aim ray-plane intersection과 Trajectory same-tick HandMarker mapping 오차 `<=1e-5u`
5. PlayMode에서 latch→driver LateUpdate 1회 소비, Drawing frame candidate 1개, release `CANDIDATE>RELEASE`
6. 에디터 Play에서 T1/T2/T3를 mode별 1회 통과 확인
7. Drawing cyan preview, Pending amber preview, reach/ink invalid red 구간, Confirm 후 opaque cyan capsule visual과 동일 collider를 확인
8. `PRETEST_FIRST_PERSON_V2` 어깨너머 시점에서 파란 body와 단일 authoritative 주황 hand가 보이고 Drawing 중 hand 중심 reach `1.25u` circle이 표시되는지 확인
9. DU-03A 및 DU-02 EditMode 회귀 유지

## QA 로그

| Tag | 확인 지점 |
|---|---|
| `[DU03BC_INPUT]` | 증거 수집용 `verboseInputLogging`을 켰을 때 LMB/E/RMB/Esc의 실제 Input System edge와 sequence (기본 off) |
| `[DU03BC_SAMPLE]` | 증거 수집용 `verboseMappingLogging`을 켰을 때 active mode, `phase=LATE_UPDATE`, `sampleIndex=1` (기본 off) |
| `[DU03BC_MAPPING]` | 증거 수집용 `verboseMappingLogging`을 켰을 때 source, candidate, independent expected, error (기본 off) |
| `[DU03BC_ROUTE]` | active adapter route |
| `[DU03BC_PLAY_MODE]` | `Tab` 전환 route, `sessionReset=True` |
| `[DU03BC_RESET]` | mode별 plane/edge reset |
| `[DU03BC_INPUT_RESET]` | R canonical trial reset |
| `[DU03A_LATE_UPDATE]` | candidate cardinality와 event order |

## 에디터 플레이 조작 및 직접 확인

Unity에서 `Assets/Scenes/DU02_SoloCourse.unity`를 열고 Play한다.

- 이동: `A` / `D`
- 점프: `Space`
- lane 선택: `1` / `2` / `3`
- 기본 Aim Draw: `LMB` drag (mouse ray candidate)
- Trajectory Draw: `Tab` 전환 후 `LMB` hold와 함께 `A` / `D` 이동 또는 `Space` 점프로 HandMarker candidate 이동. 마우스 drag만으로는 움직이지 않는다.
- Confirm: `E`, Cancel: `RMB` / `Esc`, reset: `R`
- adapter 전환: `Tab` (`Aim ↔ Trajectory`)
- 비증거 에디터 pretest 1인칭 visual yaw: `LeftArrow` / `RightArrow` hold (`PRETEST_FIRST_PERSON_V2`, `60°/s`, spawn 기준 `-30°~+30°`, Idle에서만; R/lane reset 시 `0°`)

각 mode에서 lane `1`, `2`, `3`을 선택해 T1/T2/T3를 한 번씩 직접 통과한다. 카메라가 player root 기준 `(0,+1.20,-1.25)`, pitch `-10°`이고 단일 authoritative HandMarker `(0,+0.980,0)`의 주황 hand가 화면 중앙에 보여야 한다. 어깨너머 시점의 파란 BodyVisual은 허용하되 visual hand proxy는 없어야 한다. Drawing 중 cyan 궤적과 hand 중심 reach circle, 사거리/잉크 초과 구간의 red 표시, LMB release 후 Pending amber 궤적, `E` Confirm 후 opaque cyan capsule chain이 gameplay depth `0`에 남고 그 위에 올라갈 수 있는지 확인한다. Idle에서 Left/Right를 1초 누르면 visual yaw가 정확히 `±30°` clamp되고, Drawing/Pending 동안 freeze되며, R/lane reset 후 `0°`로 복원되는지 확인한다. Player root와 gameplay `n0=(0,0,1)`은 변하지 않고 HandMarker local pose/identity rotation/unit scale과 depth `0±0.001`을 유지해야 한다. Console의 scene-start profile은 `PRETEST_FIRST_PERSON_V2`, route는 Aim이어야 한다. mapping source의 상세 확인은 기본 off인 `verboseMappingLogging`을 증거 수집 때만 켜고 Aim=`MOUSE_RAY`, Trajectory=`HAND_MARKER`인지 확인한다.

## 자동 검증

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 compile '' EditMode D:/Project-DoodleUp
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 method DoodleUp.Editor.Du02SceneBuilder.RebuildSoloCourse EditMode D:/Project-DoodleUp
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 test '' EditMode D:/Project-DoodleUp
powershell -NoProfile -ExecutionPolicy Bypass -File D:/.adk/scripts/unity-cli.ps1 test '' PlayMode D:/Project-DoodleUp
```

EditMode 관찰점:

- Reset/Cancel binding 분리와 latch exactly-once
- Aim plane snapshot, independent ray intersection, frozen plane, invalid ray
- Trajectory current HandMarker equality

PlayMode 관찰점:

- press frame `PRESS>CANDIDATE`, candidate count 1
- Drawing hold frame `CANDIDATE`, candidate count 1
- release frame `CANDIDATE>RELEASE`, candidate count 1
- reset 후 session Idle, ledger 5.00, held edge false

## 2026-08-01 결과

> 아래 자동 결과는 최종 `PRETEST_FIRST_PERSON_V2` camera `(0,+1.20,-1.25)`, pitch `-10°`, HandMarker `(0,+0.980,0)` 반영 기준이다. 실제 화면·입력·보행 검증은 사용자 재플레이 대기 상태다.

- compile: C# 오류 0 (`compile-20260801-140850.log`)
- EditMode 전체: 40/40 PASS (`test-20260801-141218.log`)
- DU-03BC mapping EditMode: 11/11 PASS
- DU-03A EditMode: 14/14 PASS
- DU-02 course/reset/sampling EditMode: 12/12 PASS
- DU-03BC playability visual EditMode: 1/1 PASS
- PlayMode 전체: 7/7 PASS (`test-20260801-140915.log`)
- latch→Driver/cardinality/release/reset/route 전환 관련 DU-03BC PlayMode: 5/5 PASS
- 실제 Input System 관찰: Editor.log에서 `DRAW_PRESS`와 Aim의 `DRAW_RELEASE_PENDING` 확인. 이는 입력→latch→router→driver 경로가 실제 플레이에서 동작했음을 보여준다.
- 에디터 직접 T1/T2/T3 mode별 1회 통과 및 Confirm 발판 보행: **미달** — 배치 CLI와 로그 판독은 사용자의 화면상 시각 확인·수동 조작·보행을 대체할 수 없어 사용자 플레이 체크포인트로 남김

## 이관·제외

RAW CSV, independent aggregator, standalone, hash, 영상, manifest와 독립 QA 판정은 이 카드 완료 조건이 아니며 DU-06A-A Gate A 증거팩으로 이관한다. X Delete, tester mouse provenance, mode 우열 판단도 제외한다.
