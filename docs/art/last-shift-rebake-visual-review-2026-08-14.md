# LAST SHIFT rebake 이후 전체 씬 시각 검수 — 2026-08-14

## 판정

**수정 필요 (visual gate FAIL).** `LastShiftShipGraybox` rebake 결과는 방별 구조와 기능색은 읽히지만, 현재 상태를 최종 인게임 비주얼로 승인할 수 없다.

검수 조건은 `LAST_SHIFT_SOLO` 씬, Unity 6000.4.0f1, 1280×720, 동일 카메라 FOV 68°와 씬 기본 조명이다. 캡처는 `DoodleUp.Editor.LastShiftVisualReviewCapture.CaptureForAutomation`으로 재생성한다.

## 핵심 관찰

| 우선순위 | 범위 | 실제 화면 관찰 | 아트 판정 |
|---|---|---|---|
| P0 | 조종석·전력·냉각·생명유지실 | 상부 대부분에 별 배경이 직접 노출된다. 조명 기구와 덕트가 우주에 떠 있는 것처럼 보인다. | 실내 천장/헐 연속성이 깨져 플레이 공간이 미완성으로 읽힌다. 천장 렌더 누락 또는 면 방향/조립 상태를 먼저 수정해야 한다. |
| P0 | 중앙 광장 | 4×4m 코어가 양 접근 시점 모두 화면 중앙 대부분을 차지하며, 표면 신호가 거의 없는 검은 직육면체로 읽힌다. | 코어 점유 크기는 gameplay 고정이므로 줄이지 않는다. 네 면의 값 분할, 상태 스트립, 모서리 챔퍼로 큰 암면을 해체해야 한다. |
| P1 | 전력실 | 주황 소품과 녹색 판독점은 구분되지만 주 설비가 검은 덩어리로 합쳐진다. | 기능색은 유지. 전면 패널 테두리/상단 실루엣에 중간 명도 분리를 추가한다. |
| P1 | 냉각실 | 시안 계열은 남아 있으나 중앙 설비와 전면 캐니스터가 겹쳐 목적물이 한 덩어리로 보인다. | 플레이 카메라에서 캐니스터가 설비 윤곽을 가리지 않는지 수동 이동 검증이 필요하다. |
| P1 | 생명유지실 | 방별 민트 신호는 가장 잘 읽히지만 근경의 큰 갈색 프롭이 우측 시야를 과도하게 막는다. | 통행 폭을 유지하며 프롭 높이/회전을 낮춰 핵심 판독 패널 시야를 확보한다. |
| P2 | 숙소 | 천장이 닫혀 있고 웜 톤과 가구 실루엣이 안정적으로 읽힌다. 다만 바닥과 사물함 암부가 거의 검정이다. | 현재 구성은 유지하되 인게임 플레이 카메라에서 승무원 실루엣 분리만 재확인한다. |

## 유지할 방향

- 방별 색 구분: 조종석 청록, 전력 주황, 냉각 시안, 생명유지 민트가 서로 혼동되지 않는다.
- 무광 로우폴리 표면과 단순한 큰 형태는 프로젝트 스타일 가이드에 맞는다.
- 붉은 바닥 라인은 동선 경계로 꾸준히 보이지만, 현재는 모든 방에서 우세하므로 방 고유색보다 강해지지 않게 유지한다.
- 중앙 코어의 4×4m 점유는 `SIMUL_ZONES` 가독성 조건이므로 아트 수정으로 축소하지 않는다.

## 증거

- `docs/art/evidence/last-shift-rebake-visual-review-2026-08-14/01_plaza_bow.png`
- `docs/art/evidence/last-shift-rebake-visual-review-2026-08-14/02_plaza_stern.png`
- `docs/art/evidence/last-shift-rebake-visual-review-2026-08-14/03_cockpit.png`
- `docs/art/evidence/last-shift-rebake-visual-review-2026-08-14/04_power.png`
- `docs/art/evidence/last-shift-rebake-visual-review-2026-08-14/05_cooling.png`
- `docs/art/evidence/last-shift-rebake-visual-review-2026-08-14/06_life_support.png`
- `docs/art/evidence/last-shift-rebake-visual-review-2026-08-14/07_quarters.png`

## 수동 확인 필요

이 자동 캡처는 정지 상태의 기본 씬 조명만 검수한다. 수정 후 실제 플레이 카메라로 광장 양방향 접근, 문 통과, 캐니스터 운반, 4인 승무원 중첩 상황을 확인해야 한다. 특히 천장 수정 후에도 조명 기구가 천장 면에 묻히지 않는지와 숙소 암부에서 캐릭터가 분리되는지가 남는다.
