# LAST SHIFT 씬 계층 하위 오브젝트 카탈로그

정본: `LastShiftShipGraybox.prefab`의 생성 경로와 `LastShiftModularKit` prefab 내부 계층.

이 문서는 루트 에셋 안의 자식도 독립적으로 판단하기 위한 목록이다. `Visual`은 메시 컨테이너일 뿐 독립 배치·삭제 대상이 아니다.

## Ship root가 직접 생성하는 하위 그룹

| 부모 | 자식 | 용도 | 상태 |
|---|---|---|---|
| `PlazaCore` | `PlazaCore_Stern/Port/Starboard`, `BowJamb_Port/Starboard`, `BowLintel`, `BowGate` | 광장 중심 코어·EVA 리프트 개구부 | 유지 |
| `ZoneDoor_B0..B4` | `ZoneDoor_B*_Blocker` | 문 visual과 닫힘 통행 차단 | 유지 |
| `CockpitGlass` | `CockpitGlass_00..02` | 조종실 창의 분할 유리 | 유지 |
| `Panel_*` | `Panel_*_Readout_0..02` | 방별 wall panel의 발광 readout 줄 | 실제 wall panel kit 대체 전 유지 |
| `Zone_*` | `ZoneStrip` | 구역 바닥의 색 방향 띠 | 유지 |
| `ModularKitAssembly` | map JSON placement instances | 정본 모듈킷의 배치 root | 유지 |

## 덕트 하위 오브젝트

| 오브젝트 | 배치 | 용도 | 상태 |
|---|---|---|---|
| `Duct_Main_Fore` | 천장 아래 선수측 장축 | 조종실↔생명유지실을 잇는 주 설비 배관 | 실제 배관 킷 대체 전 유지 |
| `Duct_Main_Aft` | 천장 아래 선미측 장축 | 두 번째 주 설비 배관 | 실제 배관 킷 대체 전 유지 |
| `Duct_Riser_Cockpit_Fore/Aft` | 조종실 벽면 | 주 배관→조종실 분기 | 실제 배관 킷 대체 전 유지 |
| `Duct_Riser_Power_Fore/Aft` | 전력실 벽면 | 주 배관→전력실 분기 | 실제 배관 킷 대체 전 유지 |
| `Duct_Riser_Cooling_Fore/Aft` | 냉각실 벽면 | 주 배관→냉각실 분기 | 실제 배관 킷 대체 전 유지 |
| `Duct_Riser_LifeSupport_Fore/Aft` | 생명유지실 벽면 | 주 배관→생명유지실 분기 | 실제 배관 킷 대체 전 유지 |

## 냉각 조작 계층

| 부모 | 자식 | 용도 | 상태 |
|---|---|---|---|
| `CoolingValve` | `CoolingValve_Body`, `CoolingValve_Lever`, `CoolingValve_Spoke`, `CoolingValve_Hub` | C-3 유지 조작의 시각 표식 | 유지; 실프롭 교체 시 이름·조작 위치 보존 |
| `CoolingStack` | `CoolingStack_Fin_0..4` | 과거 열교환기 graybox 장식 | 제거 |

## 드레싱 계층

| 부모 | 자식 | 용도 | 상태 |
|---|---|---|---|
| `ZoneDressing` | `Cockpit`, `Power`, `Cooling`, `LifeSupport`, `Quarters` | DressingSet을 방별로 나누는 root | 유지 |
| 각 방 root | catalog ID instance, 필요 시 `Visual` | 실제 드레싱 프리팹/메시 컨테이너 | `lastshift-dressing-asset-catalog.md` 기준 |
| `CrateStack_Fore` | `Visual` | `LP_CargoCrate_0p7m` 실물 crate의 anchor/mesh | 유지; 이름만 legacy처럼 보임 |

## 모듈 프리팹 내부 자식

| 프리팹 | 하위 이름 | 용도 | 상태 |
|---|---|---|---|
| 대부분의 `LPK_*` | root + `Visual` | root는 배치 pivot, Visual은 실제 메시 컨테이너 | 둘을 따로 배치·삭제하지 않음 |
| `LPK_Cockpit_WallMirror_1m` | `FrameLeft`, `FrameRight`, `FrameTop`, `FrameBottom`, `ReflectiveSurface` | 거울 프레임과 반사면 | 프리팹 단위로 유지 |
| `LPK_Door_Airlock_2m` | root + `Visual` | 문 메시. blocker는 scene/runtime authority | 프리팹 단위로 유지 |
| `LPK_EVA_TopHatch_1p6m` | root + `Visual` | 해치 메시. 닫힘 blocker는 runtime authority | 프리팹 단위로 유지 |
| `LPK_CentralLift_4m` | root + `Visual` | 이동 승강판 시각물 | 프리팹 단위로 유지 |

### Ceiling beam 이름에 대한 규칙

현재 프로젝트의 `LPK_Ceiling_Straight_4m.prefab` 직렬화 계층은 `LPK_Ceiling_Straight_4m`과 `Visual`뿐이다. `LPK_Ceiling_Straight_4m_Beam`이 Unity에서 보이면 FBX 내부의 메시/서브메시 이름이며, map placement 또는 독립 게임플레이 에셋은 아니다. 천장 구조를 지우거나 바꿀 때는 Beam 하나가 아니라 `LPK_Ceiling_Straight_4m` 프리팹 전체를 기준으로 판단한다.

## 금지

- `Visual`, `Readout_*`, `Fin_*`, `Blocker`처럼 부모 에셋의 기능을 구성하는 자식을 독립 장식으로 추가하지 않는다.
- root와 Visual 중 한쪽만 삭제해 broken prefab instance를 만들지 않는다.
- 새 child가 생기면 이 문서에 부모·역할·삭제 기준을 먼저 추가한다.
