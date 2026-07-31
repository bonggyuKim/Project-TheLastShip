# DU-03B/C 에디터 플레이 검증

상태: DU-03BC-IMPL-1 PM 축소 범위 (2026-08-01)

## 완료 기준

1. `D:/Project-DoodleUp` Unity compile 오류 0
2. 에디터 Play에서 `Tab`으로 Aim/Trajectory 전환 및 실제 입력 플레이 가능
3. 양 mode가 `Du03BCInputEdgeLatch`와 `Du03AStrokeDriver`/`Du03AStrokeSession`을 공유하고 DU-03A backend 무수정
4. EditMode에서 Aim ray-plane intersection과 Trajectory same-tick HandMarker mapping 오차 `<=1e-5u`
5. PlayMode에서 latch→driver LateUpdate 1회 소비, Drawing frame candidate 1개, release `CANDIDATE>RELEASE`
6. 에디터 Play에서 T1/T2/T3를 mode별 1회 통과 확인
7. DU-03A 및 DU-02 EditMode 회귀 유지

## QA 로그

| Tag | 확인 지점 |
|---|---|
| `[DU03BC_INPUT]` | LMB/E/RMB/Esc의 실제 Input System edge와 sequence |
| `[DU03BC_SAMPLE]` | active mode, `phase=LATE_UPDATE`, `sampleIndex=1` |
| `[DU03BC_MAPPING]` | source, candidate, independent expected, error |
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
- Draw: `LMB`, Confirm: `E`, Cancel: `RMB` / `Esc`, reset: `R`
- adapter 전환: `Tab` (`Trajectory ↔ Aim`)

각 mode에서 lane `1`, `2`, `3`을 선택해 T1/T2/T3를 한 번씩 직접 통과한다. 전환 직후 Console에서 `[DU03BC_PLAY_MODE] ... sessionReset=True result=PASS`를 확인하고 그리기 중 `[DU03BC_MAPPING]`의 source가 Aim=`MOUSE_RAY`, Trajectory=`HAND_MARKER`인지 확인한다.

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

- compile: C# 오류 0
- EditMode 전체: 37/37 PASS
- DU-03BC mapping EditMode: 11/11 PASS
- DU-03A EditMode: 14/14 PASS
- DU-02 course/reset/sampling EditMode: 12/12 PASS
- PlayMode 전체: 7/7 PASS
- latch→Driver/cardinality/release/reset/route 전환 관련 DU-03BC PlayMode: 5/5 PASS
- 에디터 직접 T1/T2/T3 mode별 1회 통과: **미달** — 배치 CLI는 실제 사용자의 수동 조작과 재미 체감을 대체할 수 없어 사용자 플레이 체크포인트로 남김

## 이관·제외

RAW CSV, independent aggregator, standalone, hash, 영상, manifest와 독립 QA 판정은 이 카드 완료 조건이 아니며 DU-06A-A Gate A 증거팩으로 이관한다. X Delete, tester mouse provenance, mode 우열 판단도 제외한다.
