# LAST SHIFT rebake 이후 전체 씬 시각 검수 — 2026-08-14

## 검수 방식

이 기록은 좋다/나쁘다 판정을 하지 않고 `LastShiftShipGraybox` rebake 이후 화면에서 보이는 현상만 적는다.

검수 조건은 `LAST_SHIFT_SOLO` 씬, Unity 6000.4.0f1, 1280×720, 동일 카메라 FOV 68°와 씬 기본 조명이다. 캡처는 `DoodleUp.Editor.LastShiftVisualReviewCapture.CaptureForAutomation`으로 재생성한다.

## 공간별 관찰

| 범위 | 화면에서 보이는 현상 |
|---|---|
| 광장/모서리 | 양 대각 시점 모두 중앙 4×4m 구조물이 화면 중앙을 크게 가린다. 네 면은 거의 단색 암면이다. 상부에는 보·레일·조명·EVA 타워 지지대가 여러 높이와 각도로 교차한다. 천장 면 없이 별 배경이 보인다. 한쪽 모서리 벽은 주황빛을 받고 반대쪽은 거의 검정이다. |
| 조종석 노즈캡/창문 | 정면의 베이지 패널 두 장과 중앙 청록 구조 사이로 외부 별 배경이 보인다. 좌우 벽의 큰 검정 면과 정면 베이지 면의 명도 차가 크다. 중앙 검정 좌석/기둥이 정면 구조를 가린다. 천장 조명과 길쭉한 보가 별 배경 앞에 떠 있는 형태로 보인다. 좌우 좌석과 콘솔 크기가 정면 패널에 비해 작다. |
| 전력실 | 중앙 검정 설비가 화면 폭의 약 1/3을 차지하고 녹색 점 세 개만 분리된다. 좌측 근경 주황 상자는 화면 밖으로 잘리고, 우측 바닥 소품은 벽 아래 붉은 라인과 겹친다. 천장 면 없이 별 배경이 보이며 조명과 보가 허공에 놓인 형태다. |
| 냉각실 | 전면 캐니스터가 중앙 설비와 같은 축에 겹친다. 좌측 근경의 큰 직육면체가 화면 높이 대부분을 차지하고 프레임 밖으로 잘린다. 중앙 벽 설비는 주변 벽보다 밝은 회색이다. 천장 면 없이 별 배경이 보인다. |
| 산소실/생명유지실 | 좌우에 키 큰 탱크/기둥이 반복되고 중앙에도 원통이 있어 깊이 방향 시야가 여러 번 끊긴다. 우측 갈색 직육면체가 화면 아래 우측 절반가량을 가린다. 민트 발광선과 시안 소품은 보이지만, 천장 면 없이 별 배경과 조명 레일이 노출된다. |
| 숙소 침상 2조/커튼 | 첫 시점에서 긴 수평판 세 개와 중앙 테이블이 높이와 색이 비슷하게 겹친다. 침상 가장자리와 바닥 타일 사이에 밝은 회색 직사각형 두 장이 보인다. 커튼은 벽 쪽의 얇은 갈색 판으로 보이고 침상과 색이 유사하다. 역방향에서는 좌측 사물함/벽이 거의 검정이며, 우측 근경 가구가 프레임에 잘린다. 이 공간은 천장 면이 보인다. |
| 화물 소품 시점 | 좌측 검정 대형 프롭, 중앙 갈색 프롭, 우측 밝은 회색 프롭이 카메라에 매우 가깝게 놓여 각각 화면 가장자리에 잘린다. 세 프롭 사이 통로만 좁게 보이며 바닥 접촉점은 가려진다. 별도 `CargoBay` 방은 현행 modular map에 없어 냉각실 쪽 적재 소품 군을 촬영했다. 천장 면 없이 별 배경이 보인다. |
| EVA 상부 해치 | 외부 상단 시점에서 타워 지지대, 긴 보, 천장 레일이 서로 교차한다. 해치 중심은 주변 검정 구조와 같은 명도라 경계가 약하며, 시안 점광원 네 개가 모서리에 보인다. 우주선 내부의 조종석과 방들이 상부에서 그대로 내려다보인다. |
| EVA 리프트 내부 | 광장 접근 시 중앙 암색 벽이 화면 대부분을 차지한다. 상단 지지대와 보 사이로 별 배경이 보이고, 좌우 방 입구는 중앙 구조물 양옆의 좁은 영역으로만 보인다. |

## 증거

- `docs/art/evidence/last-shift-rebake-visual-review-2026-08-14/01_plaza_bow_corners.png`
- `docs/art/evidence/last-shift-rebake-visual-review-2026-08-14/02_plaza_stern_corners.png`
- `docs/art/evidence/last-shift-rebake-visual-review-2026-08-14/03_cockpit_nose_windows.png`
- `docs/art/evidence/last-shift-rebake-visual-review-2026-08-14/04_power.png`
- `docs/art/evidence/last-shift-rebake-visual-review-2026-08-14/05_cooling.png`
- `docs/art/evidence/last-shift-rebake-visual-review-2026-08-14/06_oxygen_life_support.png`
- `docs/art/evidence/last-shift-rebake-visual-review-2026-08-14/07_quarters_bunks_curtains.png`
- `docs/art/evidence/last-shift-rebake-visual-review-2026-08-14/08_quarters_reverse.png`
- `docs/art/evidence/last-shift-rebake-visual-review-2026-08-14/09_cargo_props.png`
- `docs/art/evidence/last-shift-rebake-visual-review-2026-08-14/10_eva_hatch_exterior.png`
- `docs/art/evidence/last-shift-rebake-visual-review-2026-08-14/11_eva_lift_interior.png`

## 수동 확인 필요

이 캡처는 정지 상태의 기본 씬 조명과 지정 좌표 카메라를 사용했다. 실제 플레이 카메라의 높이·후처리·캐릭터 점유에 따라 가림 비율은 달라질 수 있다. 부유/관통 여부는 가려진 바닥 접촉점과 동적 해치 상태를 에디터 Scene View에서 근접 확인해야 한다.
