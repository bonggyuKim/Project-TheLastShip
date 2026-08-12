# Last Shift HUD — Icon-only v1

## 목표

상시 HUD는 화면 우측 상단의 **채움 아이콘 3개**만 사용한다. 숫자·퍼센트·이름·가로 막대·임계 눈금·배경 패널은 표시하지 않는다.

내레이션 패널과 상호작용 프롬프트는 맥락 안내이므로 이 규격의 상시 HUD가 아니며 유지한다.

## 배치

- 기준: 1920×1080, 우측 상단 `x=1816`, `y=28`
- 아이콘: `56×56 px`, 세로 간격 `12 px`
- 순서: 산소 → 전력 → 열
- 세 아이콘의 외곽선은 아이보리, 내부 fill만 상태를 표시한다.

## 아이콘과 fill

| ID | 소스 | fill 의미 | 정상 | 경고/위기 |
|---|---|---|---|---|
| `oxygen` | `icon_gauge_oxygen_{base,fill}.png` | 예비 산소 잔량. 아래→위 | teal `#4FD8A0` | Warning `#FF9433`; Critical `#FF5A4D` + 기존 `PulseCrisis` 1.5Hz 밝기 pulse |
| `power` | `icon_gauge_power_{base,fill}.png` | 버스 전력 잔량. 아래→위 | teal `#4FD8A0` | 낮은 전력에서 orange→red |
| `heat` | `icon_gauge_heat_{base,fill}.png` | 열 축적량. 아래→위. 차오를수록 나쁨 | 저열은 teal | 고열은 orange→red |

- fill은 기존 UGUI `Image.Type.Filled`, Vertical, Bottom origin을 사용한다.
- 아이콘은 상태를 읽는 유일한 상시 정보이므로 0일 때도 외곽선은 남긴다.
- 위기 pulse는 알파가 아니라 밝기만 바꾼다.
- 숫자, `%`, `SetName`, `SetValueLabel`, `SetThresholds`, `SetMovingMarker`는 이 HUD 경로에서 호출하지 않는다.

## 구현 연결

기존 `LastShiftGaugeView`의 base/fill 두 Image는 재사용한다. 아이콘 전용 모드 또는 전용 `LastShiftHudIconView`로 아래를 보장한다.

1. `56×56` 사각형에 base/fill을 겹친다.
2. Text, marker, threshold 오브젝트를 생성하지 않거나 비활성화한다.
3. `LastShiftHudLayout`의 상시 HUD는 우측 상단 세 슬롯만 반환한다.
4. 기존 좌상단 HUD panel, 목표/도킹/자원/구역압력/진단 텍스트는 상시 HUD 경로에서 그리지 않는다.
5. 산소는 crew `SuitOxygen`, 전력은 `BusPower`, 열은 `EngineHeat`를 같은 프레임에 읽는다.

## 범위 밖

- 온보딩 내레이션·상호작용 프롬프트·기상 도입부는 유지한다.
- 새 PNG 제작 없음: 현재 아이콘 6장을 정본으로 사용한다.
