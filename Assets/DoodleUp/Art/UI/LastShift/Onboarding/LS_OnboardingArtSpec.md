# LAST SHIFT — AI 온보딩 아트 규격

## 설명문 패널

- 위치: 화면 하단 중앙, 안전 영역 안쪽. 패널 폭은 화면의 70% 이내.
- 화자 라벨: `선내 관리 시스템`, 좌상단 고정. 본문보다 작고 자간을 넓힌다.
- 본문: 좌정렬, 최대 2줄. 한 줄 40자 기준에서 16:9 1080p 본문 최소 38px.
- Unity 파일: `UGUI/LS_UI_NarrationPanel_256x128.png`, Sprite FullRect / 100 PPU / 9-slice border `16px`.
- 색: 아이보리 프레임 `#F2E7D4`, 차콜 화면 `#182630`, 정상 `#4FD8A0`, 주의 `#FF9433`, 위기 `#FF5A4D`.
- 기존 화자 없는 튜토리얼 띠와 다르게 코너 노드·화자 라벨·로그 번호를 반드시 유지한다.
- 타이핑: 첫 글자 전 CHIME 재생, 글자당 25ms를 기본으로 한다. nudge 라인은 타이핑 없이 즉시 교체한다.

## 신호음

| 태그 | 파일 | 용도 |
|---|---|---|
| CHIME_LONG | `LS_CHIME_LONG.wav` | 단계 전환 첫 줄만. 2연 상승 신호. |
| CHIME_SHORT | `LS_CHIME_SHORT.wav` | 일반 안내. 1연 상승 신호. |
| CHIME_ALERT | `LS_CHIME_ALERT.wav` | 산소 임계·강제 회수. 하강 신호. |

재촉(nudge) 라인에는 신호음을 재생하지 않는다.

## 산소 경고 동기화

| 조건 | 문안 | 화면 | 소리 |
|---|---|---|---|
| `SuitOxygenWarningThreshold` 진입 | `산소 {threshold}%. 하강과 재가압 시간까지 계산할 것.` | 패널 상단선이 주의색으로 1회 300ms 점멸. 지속 HUD 색상은 유지. | `CHIME_ALERT` 1회 |
| `SuitOxygenCriticalThreshold` 진입 | `산소 {threshold}%. 복귀 외 행동 권장하지 않음.` | 패널 프레임과 산소 HUD가 같은 프레임에 위기색 밝기 pulse 2회. | `CHIME_ALERT` 1회 |
| 산소 0% 자동 회수 | `산소 고갈. 강제 회수됨.` | 위기색을 쓰지 않는다. 주의 주황(`#FF9433`)으로 2회 빠르게 점멸한 뒤 청록 정상 상태로 복귀. 사망 암전·지속 적색·반복 경보 금지. | `CHIME_ALERT` 1회 |

- 임계 숫자는 문자열 상수가 아니라 `LastShiftRecoveryTuning`의 활성 threshold를 `{threshold}`로 표시한다. 현재 활성값은 Warning `45%`(단발)·Critical `30%`(crisis pulse)이며, Warning/Critical은 배타 상태가 아니므로 Critical에서도 Warning 시각 상태를 끄지 않는다.
- CHIME·문안 노출·첫 점멸 프레임은 같은 이벤트에서 시작한다. crisis는 `LastShiftUiTheme.PulseCrisis(Time.unscaledTime)`의 1.5Hz 밝기 pulse를 사용하며, 알파 점멸이나 반복 경고음은 사용하지 않는다.
