# DU-03B/C 에디터 플레이 검증

상태: DU-03BC-PLAY-3 이동·LMB release commit 수정 검증 (2026-08-01)

## 완료 기준

1. `D:/Project-DoodleUp` Unity compile 오류 0
2. 에디터 Play에서 `Tab`으로 Aim/Trajectory 전환 및 실제 입력 플레이 가능
3. `A/D`가 player X를 이동시키고 `Space`가 grounded 상태에서 양의 Y 속도를 만든다.
4. 양 mode가 `Du03BCInputEdgeLatch`와 `Du03AStrokeDriver`/`Du03AStrokeSession`을 공유한다.
5. EditMode에서 Aim ray-plane intersection과 Trajectory same-tick HandMarker mapping 오차 `<=1e-5u`
6. PlayMode에서 latch→driver LateUpdate 1회 소비, Drawing frame candidate 1개, release `CANDIDATE>RELEASE>AUTO_COMMIT`
7. Drawing cyan preview, reach/ink invalid red 구간, LMB release 후 opaque cyan capsule visual과 동일 collider를 확인한다. 실제 UX에 amber Pending이나 `E` Confirm은 없다.
8. 생성 capsule 위 player 접지·보행이 가능해야 한다.
9. `PRETEST_FIRST_PERSON_V2` 어깨너머 시점에서 파란 body와 단일 authoritative 주황 hand가 보이고 Drawing 중 hand 중심 reach `1.25u` circle이 표시되는지 확인
10. DU-03A 및 DU-02 EditMode/PlayMode 회귀 유지

## QA 로그

| Tag | 확인 지점 |
|---|---|
| `[DU03BC_INPUT]` | 증거 수집용 `verboseInputLogging`을 켰을 때 LMB/RMB/Esc의 실제 Input System edge와 sequence (기본 off) |
| `[DU03BC_SAMPLE]` | 증거 수집용 `verboseMappingLogging`을 켰을 때 active mode, `phase=LATE_UPDATE`, `sampleIndex=1` (기본 off) |
| `[DU03BC_MAPPING]` | 증거 수집용 `verboseMappingLogging`을 켰을 때 source, candidate, independent expected, error (기본 off) |
| `[DU03BC_ROUTE]` | active adapter route |
| `[DU03BC_PLAY_MODE]` | `Tab` 전환 route, `sessionReset=True` |
| `[DU03BC_RESET]` | mode별 plane/edge reset |
| `[DU03BC_INPUT_RESET]` | R canonical trial reset |
| `[DU03A_LATE_UPDATE]` | candidate cardinality와 `CANDIDATE>RELEASE>AUTO_COMMIT` event order |
| `[DU02_GROUND]` | player offset capsule의 접지 상태 전이와 bounds-center probe provenance |

## 에디터 플레이 조작 및 직접 확인

Unity에서 `Assets/Scenes/DU02_SoloCourse.unity`를 열고 Play한다.

- 이동: `A` / `D`
- 점프: `Space`
- lane 선택: `1` / `2` / `3`
- 기본 Aim Draw: `LMB` drag (mouse ray candidate)
- Trajectory Draw: `Tab` 전환 후 `LMB` hold와 함께 `A` / `D` 이동 또는 `Space` 점프로 HandMarker candidate 이동. 마우스 drag만으로는 움직이지 않는다.
- Commit: `LMB` release, Cancel: Drawing 중 `RMB` / `Esc`, reset: `R`
- adapter 전환: `Tab` (`Aim ↔ Trajectory`)
- 비증거 에디터 pretest 1인칭 visual yaw: `LeftArrow` / `RightArrow` hold (`PRETEST_FIRST_PERSON_V2`, `60°/s`, spawn 기준 `-30°~+30°`, Idle에서만; R/lane reset 시 `0°`)

각 mode에서 lane `1`, `2`, `3`을 선택해 T1/T2/T3를 한 번씩 직접 통과한다. 카메라가 player root 기준 `(0,+1.20,-1.25)`, pitch `-10°`이고 단일 authoritative HandMarker `(0,+0.980,0)`의 주황 hand가 화면 중앙에 보여야 한다. 어깨너머 시점의 파란 BodyVisual은 허용하되 visual hand proxy는 없어야 한다. Drawing 중 cyan 궤적과 hand 중심 reach circle, 사거리/잉크 초과 구간의 red 표시, LMB release 직후 opaque cyan capsule chain이 gameplay depth `0`에 남고 그 위에 올라갈 수 있는지 확인한다. amber Pending과 `E` Confirm은 없어야 한다. Idle에서 Left/Right를 1초 누르면 visual yaw가 정확히 `±30°` clamp되고, Drawing 동안 freeze되며, R/lane reset 후 `0°`로 복원되는지 확인한다. Player root와 gameplay `n0=(0,0,1)`은 변하지 않고 HandMarker local pose/identity rotation/unit scale과 depth `0±0.001`을 유지해야 한다. Console의 scene-start profile은 `PRETEST_FIRST_PERSON_V2`, route는 Aim이어야 한다. mapping source의 상세 확인은 기본 off인 `verboseMappingLogging`을 증거 수집 때만 켜고 Aim=`MOUSE_RAY`, Trajectory=`HAND_MARKER`인지 확인한다.

## 비증거 샌드박스 직접 확인

`Assets/Scenes/DU_Sandbox.unity`는 profile `PRETEST_DEPTH_LOCOMOTION_V1`로 실행한다. 40×40u 평면과 Player만 있으며 Goal/lane/course 구조물과 evidence runner는 없다. `A/D`, `Space`, LMB release commit, RMB/Esc, R, Tab, Left/RightArrow는 기존과 같고, W는 `+n0`, S는 `-n0` depth 이동이다.

- Idle에서 W/S 지상 속도 `2.50u/s`, 공중 속도 `2.00u/s`.
- A/D+W/S 대각선 horizontal magnitude는 해당 상태 속도와 같아야 한다.
- Drawing/Pending 동안 W/S는 잠기고 A/D·Space는 유지된다. release auto-commit 또는 Cancel 후 W/S가 다시 허용된다.
- R 후 player root depth `0`, HandMarker canonical local pose, ink `5.00`, committed collider `0`인지 확인한다.
- Draw 시작 depth는 stroke별로 고정되며 root/hand drift `>0.001u`는 `[DU_SANDBOX_INVALID] reason=TECH_INVALID/DRAW_DEPTH_DRIFT`다.
- 샌드박스 결과는 Gate A 72행, qualified 판정, mode 선택에 사용하지 않는다.

## 자동 검증

`com.unity.pipeline` `0.4.0-exp.1`은 manifest에 설치되어 있다. Editor가 열려 있으면 공식 Unity CLI에서 `pipelineServer.isReachable=true`, Editor `state=ready`를 확인한 뒤 Pipeline 명령을 사용한다. PlayMode test는 domain reload로 동기 HTTP 연결이 끊어지므로 반드시 `--async_tests true`로 시작하고 `test_status`가 `completed`일 때까지 조회한다.

```powershell
unity status --format json
unity command recompile --project-path D:/Project-DoodleUp
unity command recompile_status --project-path D:/Project-DoodleUp
unity command run_tests --mode editor --async_tests true --project-path D:/Project-DoodleUp
unity command test_status --project-path D:/Project-DoodleUp
unity command run_tests --mode playmode --async_tests true --project-path D:/Project-DoodleUp
unity command test_status --project-path D:/Project-DoodleUp
unity command get_console_logs --severity error --limit 50 --project-path D:/Project-DoodleUp
```

Editor가 프로젝트를 점유하지 않는 cold 검증에는 공식 `unity run`/`unity test`를 사용한다. `D:/.adk/scripts/unity-cli.ps1`은 동일 명령을 호출하고 기존 로그 경로를 유지하는 호환 wrapper다.

PlayMode fixture에서 non-batch scene start를 재현할 때는 첫 frame을 먼저 진행해야 한다. `Du03BCAdapterRouter.Start()`가 실제 플레이 계약에 따라 route를 Aim으로 정규화하고 adapter edge를 reset한 뒤, 테스트가 대상 route를 설정하고 probe input을 넣는다. 첫 frame 전에 Trajectory edge를 넣으면 live Editor에서는 정상적인 scene-start reset이 edge를 지우므로 환경 의존 실패가 된다.

EditMode 관찰점:

- Reset/Cancel binding 분리와 latch exactly-once
- Aim plane snapshot, independent ray intersection, frozen plane, invalid ray
- Trajectory current HandMarker equality

PlayMode 관찰점:

- press frame `PRESS>CANDIDATE`, candidate count 1
- Drawing hold frame `CANDIDATE`, candidate count 1
- release frame `CANDIDATE>RELEASE>AUTO_COMMIT`, candidate count 1, 최종 session Idle/Committed, collider `>0`
- offset capsule ground probe 후 A/D 양방향 목표 X 속도와 Space jump
- reset 후 session Idle, ledger 5.00, held edge false

## 2026-08-01 결과

- 공식 CLI/Pipeline live Editor compile: `status=completed`, `failed=false`, C# 오류 0
- 공식 CLI/Pipeline live Editor EditMode 전체: **41/41 PASS**
- 공식 CLI/Pipeline live Editor PlayMode 전체: **9/9 PASS**
  - offset capsule bounds-center ground probe
  - frictionless player collider에서 A/D 양방향 목표 속도 `±2.50u/s`
  - grounded Space jump와 다음 physics tick airborne 전이
  - release `CANDIDATE>RELEASE>AUTO_COMMIT`, 최종 Idle/Committed와 collider 생성
- 실제 Play에서 T1/T2/T3를 각각 reset한 뒤 `rootY=0.10`, `capsuleBottom=0.10`, `ledgeTop=0.10`, `gap=0`, `grounded=True`를 확인했다. `SpawnOffset.y`는 기존 `0.60`에서 ledge half-height `0.10`으로 수정했다.
- 실제 Play 상태에서 `grounded=True` 확인 후 Input System D 이벤트가 player X를 증가시켰다. 700ms hold 후에는 start ledge를 넘어 추락한 상태였지만 X 위치가 `-0.20 → 1.8957`로 변해 입력·motor·physics 이동이 실제 frame에서 수행됨을 확인했다. 기본 collider 마찰이 목표 X 속도를 상쇄하던 문제도 runtime frictionless material로 제거했다.
- 실제 Aim Input System mouse press→drag→release 경로에서 `session=Idle`, committed collider `2`를 확인했다. 별도 E 입력 없이 release commit이 완료됐다.
- Game View 시각 확인: `Temp/DU03BC-live-lmb-release-commit.png`에서 opaque cyan 2-segment capsule chain이 표시됐다.
- Pipeline 자동 입력은 실제 latch/router/driver/physics 경로를 실행했지만, 사용자의 물리 키보드/마우스 재플레이와 T1/T2/T3 mode별 통과·생성 발판 보행은 최종 사람 플레이 체크포인트로 남는다.

## 이관·제외

RAW CSV, independent aggregator, standalone, hash, 영상, manifest와 독립 QA 판정은 이 카드 완료 조건이 아니며 DU-06A-A Gate A 증거팩으로 이관한다. X Delete, tester mouse provenance, mode 우열 판단도 제외한다.
