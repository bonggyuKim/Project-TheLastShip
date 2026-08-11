# LAST SHIFT 드레싱 에셋 카탈로그

정본: `Assets/DoodleUp/Dressing/LastShiftDressingSet.asset`.

드레싱은 공간의 용도를 읽히게 하는 비상호작용 소품이다. floor-level 소품의 기준 접지면은 `y=0.12`, 문 접근 여유는 최소 `0.10m`다. 이름이 같아도 공간이 다르면 별도 인스턴스다.

## 조종실

| 유형 | 용도 |
|---|---|
| HelmSeat, HelmThrottle | 조종석·승무원 조작 위치 |
| NavChartTable, PanelBank | 항법·상태 확인 공간 |
| Lockers, Basin, Bench, Table, Stall | 승무원 생활·준비 공간 |
| CableTray, ConduitJunction, DeckGrate, RibFrame, FloorBand, AirlockStripe | 선내 구조·정비 결 |

## 전력실

| 유형 | 용도 |
|---|---|
| BreakerCabinet | 전력 분배 설비 |
| PartsPallet, CrateStack_Aft/Fore | 부품·정비 자재 보관 |
| CableTray, ConduitJunction, DeckGrate, RibFrame, StowageNet | 전력 배선·구조 결 |
| Scorch 데이터 | 전력 실패 VFX 앵커; 정적 표시 금지 |

## 냉각실

| 유형 | 용도 |
|---|---|
| CoolingRack, HeatExchangerCoil | 냉각·열교환 설비 |
| LashRail, PartsPallet, CrateStack_Aft | 정비·고정·자재 보관 |
| CableTray, ConduitJunction, DeckGrate, RibFrame, StowageNet | 설비 배관·구조 결 |
| Frost 데이터 | 냉각 실패 VFX 앵커; 정적 표시 금지 |

## 생명유지실

| 유형 | 용도 |
|---|---|
| ScrubberStack, O2TankBank, WallLocker | 산소 처리·탱크·보급 |
| CrateStack_Aft/Fore/Mid, PartsPallet | 수리 자재·화물 |
| VentFan_Service, EmergencyBeacon_Service | 환기·비상 신호; source scale 수정 전 이동 금지 |
| CableTray, ConduitJunction, DeckGrate, PanelBank, RibFrame, Lamp | 설비·조명·구조 결 |

## 숙소

| 유형 | 용도 |
|---|---|
| Bunk_Port/Starboard Upper/Lower | 승무원 취침 |
| LockerBank | 개인 수납 |

## 실제 메시 기준 예외

| 에셋 | 실제 renderer bounds | 운영 규칙 |
|---|---|---|
| CargoCrate | `0.72 × 0.52 × 0.86m` | CrateStack 7곳의 정본 실프롭 |
| PartsPallet | `0.52 × 0.44 × 0.43m` | actual bounds로 clearance 계산 |
| O2Tank | `0.38 × 1.06 × 0.44m` | actual bounds로 clearance 계산 |
| VentFan_Service | 약 `4.67 × 4.88 × 6.48m` | 원본 축/스케일 수정 전 위치·size 동결 |
| EmergencyBeacon_Service | 약 `7.81 × 0.85 × 7.62m` | 원본 축/스케일 수정 전 위치·size 동결 |

## 개별 확인 항목

| 에셋 | 공간·배치 | 용도 | 삭제 기준 |
|---|---|---|---|
| `LashRail_Port` | 냉각실 좌현 바닥, 문 접근을 피해 배치 | 열교환기/화물을 묶는 고정 레일. 냉각실 정비 성격을 읽히게 함 | 냉각실 고정 장비를 전부 교체할 때만 함께 검토 |
| `RibFrame_Port` | 전력실·냉각실 벽면 | 선체 프레임 보강재. 방의 구조 깊이와 선체 방향을 읽히게 함 | 벽 킷이 동일한 리브를 내장할 때만 제거 |
| `StowageNet_Port` | 전력실·냉각실 상부 벽면 | 작은 정비 물품 보관망. 상부 빈 벽을 기능적으로 채움 | 실제 수납 프리팹으로 대체할 때만 제거 |
| `ConduitJunction_Starboard` | 조종실 천장/상부 벽, 방별 변형 존재 | 배선·배관이 갈라지는 junction. 설비가 실제로 연결된다는 신호 | 배관 킷이 junction을 포함할 때만 제거 |
| `NavChartTable` | 조종실 바닥 | 항법 보조 테이블. 현재는 비상호작용 드레싱 | 실제 항법 상호작용으로 승격하거나 조종실 밀도가 과할 때만 재검토 |

## 금지

- Tripo/생성형 3D 프롭을 Last Shift 씬에 다시 연결하지 않는다.
- 광장 중앙 리프트/EVA 동선에 드레싱을 놓지 않는다.
- 선언 size만 보고 프리팹 스케일을 곱하지 않는다. 실제 renderer bounds로 배치·여유를 검증한다.
