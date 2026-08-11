# LAST SHIFT 맵 에셋 카탈로그

정본: `Assets/DoodleUp/Data/LastShiftModularMap.json`.

이 문서는 `LAST_SHIFT_SP02A_NETWORK`와 SOLO 씬의 구조·기능 에셋 목록이다. 씬에 새 에셋을 추가하려면 아래에 용도와 배치를 먼저 기록한다. 목록에 없는 정적 씬 에셋은 제거 후보다.

## 구조와 이동

| 에셋 | 배치 | 용도 | 상태 |
|---|---|---|---|
| `LPK_Floor_Square_2m` | plaza, cockpit, lifeSupport, power, cooling, quarters | 주 갑판·underlay·천장 패널 | 유지 |
| `LPK_Wall_Straight_4m` | 여섯 공간 외곽 | 방 구획·압력 경계 | 유지 |
| `LPK_Ceiling_Straight_4m` | 여섯 공간 | 실내 천장·폐쇄감 | 유지 |
| `LPK_Hull_Exterior_Panel_4m` | 선체 외피 32분할 | 외부 실루엣·선체 경계 | 유지 |
| `LPK_Door_Airlock_2m` + blocker | 광장↔방 5곳 | 개폐·압력 차단·통행 판정 | 유지 |
| `LPK_CentralLift_4m` | plaza `(0,0,0)` | EVA 승강 플랫폼 | 유지 |
| `LPK_EVA_ConningTower_3m` | plaza 상부 `(0,3.2,0)` | EVA 상승 샤프트 | 유지 |
| `LPK_EVA_TopHatch_1p6m` | tower 상단 `(0,6.2,0)` | EVA 출입·폐쇄 blocker | 유지 |

## 방별 기능 에셋

| 공간 | 에셋 | 용도 | 상태 |
|---|---|---|---|
| 조종실 | `LPK_Cockpit_ControlConsole` | 조종실 정체성·콘솔 충돌면 | 유지 |
| 조종실 | `LPK_Cockpit_ViewWindow_4m` | 선수 방향·외부 시야 | 유지 |
| 조종실 | `LPK_Cockpit_WallMirror_1m` | 실내 시각 확장·반사 | 유지 |
| 생명유지실 | `LPK_LifeSupport_Scrubber` | 산소 설비 | 유지 |
| 전력실 | `LPK_Power_Switchgear` | 전력 설비 | 유지 |
| 냉각실 | `LPK_Cooling_Exchanger` | 냉각 설비 | 유지 |
| 숙소 | `LPK_Quarters_Bunk` | 휴식·개인 공간 정체성 | 유지 |

## 목표물과 상호작용 표식

| 에셋 | 위치 | 용도 | 상태 |
|---|---|---|---|
| `LPK_TetherRack_2m` | cockpit/plaza 경계 부근 | 테더 상호작용 표식 | 유지 |
| `LPK_OxygenLeakPipe_2m` | lifeSupport | 산소 누출 목표물 | 유지 |
| `LPK_RepairConsole_1m` | lifeSupport | 수리 목표물 | 유지 |
| `LPK_DamagedPipe_2m` | lifeSupport | 손상 상태 목표물 | 유지 |
| `CoolingValve` | cooling | C-3 누르고 유지하는 조작 동사 | 유지; 실프롭 교체 가능 |

## 구역 가독성

| 유형 | 배치 | 용도 | 상태 |
|---|---|---|---|
| 구역 라벨 5종 | plaza 인접 각 방과 숙소 | 공간 방향 인지 | 유지 |
| 상태 조명 9종 | plaza 4 + 방 5 | 구역색·시선 유도 | 유지 |
| `LS_SpaceSky` | 외부 | 우주 배경 | 유지 |

## 조건부 효과 자리

| 데이터 ID | 공간 | 용도 | 현재 상태 |
|---|---|---|---|
| `Frost_Deck`, `Frost_StarboardWall`, `Frost_Conduit` | cooling | 냉각 실패 VFX 앵커 | 좌표 데이터만 유지; 정적 씬 인스턴스 금지 |
| `Scorch_Deck`, `Scorch_PortWall`, `Scorch_Conduit` | power | 전력 실패 VFX 앵커 | 좌표 데이터만 유지; 정적 씬 인스턴스 금지 |

## 제거 기준

| 항목 | 판정 | 이유 |
|---|---|---|
| `CockpitConsole` native cube | 제거 | 실콘솔과 중복되는 레거시 graybox |
| `CoolingStack` + `CoolingStack_Fin_*` native cubes | 제거 | `LPK_Cooling_Exchanger`와 중복되는 레거시 graybox |
| legacy BypassDuct, DiscHull, AirlockHall | 금지 | 현 EVA/32분할 선체 정본에 없음 |
| `StarField`, `SpaceVoid`, `NebulaCard` | 금지 | `LS_SpaceSky`만 외부 배경으로 유지 |

## 변경 규칙

1. 동적 상태 효과는 정적 메시로 미리 세우지 않는다.
2. 구조·문·목표물·충돌면을 없앨 때는 대체 에셋과 게임플레이 참조를 먼저 확인한다.
3. 새 드레싱은 `lastshift-dressing-asset-catalog.md`에 먼저 등록하고, 실제 renderer bounds와 문 여유를 검증한다.
