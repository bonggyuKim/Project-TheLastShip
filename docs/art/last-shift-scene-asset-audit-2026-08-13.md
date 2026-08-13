# LAST SHIFT 씬/에셋 전수 감사 — 2026-08-13

이 파일은 `Tools/Audit-LastShiftSceneAssets.ps1`로 재생성한다. 범위는 두 LAST SHIFT 씬, `LastShiftShipGraybox`, 재귀 중첩 프리팹 최하위, 드레싱 102개 슬롯, modular map 카탈로그다.

## 집계

- 씬: 2개
- 재귀 참조 에셋/프리팹: 65개
- 중첩 프리팹 고유 오브젝트 이름(최하위 포함): 312개
- Ship prefab 직렬화 GameObject: 100개
- 드레싱 슬롯: 106개
- map 사용 modular asset ID: 18개
- 폴더 내 modular prefab: 35개
- map 미사용 modular prefab: 17개 (프로젝트 라이브러리 보관, 씬 배치 없음)
- 금지/레거시 씬 오브젝트 발견: 0개
- gameplay blocker 문 루트: 3개 (ZoneDoor_B1, ZoneDoor_B2, ZoneDoor_B0)
- map 문 슬롯 / 정상 문 visual 인스턴스: 5개 / 5개
- 비단위 Transform 후보: 59개 (생성된 치수형 primitive 포함; Unity 검증과 함께 판정)

## map 카탈로그 대조

| asset ID | scene/map 상태 |
|---|---|
| `LPK_Ceiling_Straight_4m` | 사용 |
| `LPK_CentralLift_4m` | 사용 |
| `LPK_Cockpit_ControlConsole` | 사용 |
| `LPK_Cockpit_ViewWindow_4m` | 사용 |
| `LPK_Cockpit_WallMirror_1m` | 사용 |
| `LPK_Cooling_Exchanger` | 사용 |
| `LPK_DamagedPipe_2m` | 사용 |
| `LPK_EVA_ConningTower_3m` | 사용 |
| `LPK_EVA_TopHatch_1p6m` | 사용 |
| `LPK_Floor_Square_2m` | 사용 |
| `LPK_Hull_Exterior_Panel_4m` | 사용 |
| `LPK_LifeSupport_Scrubber` | 사용 |
| `LPK_OxygenLeakPipe_2m` | 사용 |
| `LPK_Power_Switchgear` | 사용 |
| `LPK_Quarters_Bunk` | 사용 |
| `LPK_RepairConsole_1m` | 사용 |
| `LPK_TetherRack_2m` | 사용 |
| `LPK_Wall_Straight_4m` | 사용 |

## 폴더에는 있으나 map에 배치되지 않은 modular prefab

- `LPK_Airlock_Exterior_4m` — 씬 인스턴스 없음; 다른 제작/검증 용도 가능성이 있어 원본 파일은 보존
- `LPK_Ceiling_Curve45` — 씬 인스턴스 없음; 다른 제작/검증 용도 가능성이 있어 원본 파일은 보존
- `LPK_Connector_Neck_2m` — 씬 인스턴스 없음; 다른 제작/검증 용도 가능성이 있어 원본 파일은 보존
- `LPK_Corner_Inner_90` — 씬 인스턴스 없음; 다른 제작/검증 용도 가능성이 있어 원본 파일은 보존
- `LPK_Corner_Outer_90` — 씬 인스턴스 없음; 다른 제작/검증 용도 가능성이 있어 원본 파일은 보존
- `LPK_DeckHatch_2m` — 씬 인스턴스 없음; 다른 제작/검증 용도 가능성이 있어 원본 파일은 보존
- `LPK_Door_Airlock_2m` — 씬 인스턴스 없음; 다른 제작/검증 용도 가능성이 있어 원본 파일은 보존
- `LPK_Floor_Curve_45` — 씬 인스턴스 없음; 다른 제작/검증 용도 가능성이 있어 원본 파일은 보존
- `LPK_Floor_Transition_2m` — 씬 인스턴스 없음; 다른 제작/검증 용도 가능성이 있어 원본 파일은 보존
- `LPK_Hull_Exterior_Curve45` — 씬 인스턴스 없음; 다른 제작/검증 용도 가능성이 있어 원본 파일은 보존
- `LPK_Hull_Exterior_Curve90` — 씬 인스턴스 없음; 다른 제작/검증 용도 가능성이 있어 원본 파일은 보존
- `LPK_Hull_WindowBay_4m` — 씬 인스턴스 없음; 다른 제작/검증 용도 가능성이 있어 원본 파일은 보존
- `LPK_SalvagePad_4m` — 씬 인스턴스 없음; 다른 제작/검증 용도 가능성이 있어 원본 파일은 보존
- `LPK_Support_Pillar` — 씬 인스턴스 없음; 다른 제작/검증 용도 가능성이 있어 원본 파일은 보존
- `LPK_Wall_Curve_45` — 씬 인스턴스 없음; 다른 제작/검증 용도 가능성이 있어 원본 파일은 보존
- `LPK_Wall_Straight_2m` — 씬 인스턴스 없음; 다른 제작/검증 용도 가능성이 있어 원본 파일은 보존
- `LPK_Wall_Window_4m` — 씬 인스턴스 없음; 다른 제작/검증 용도 가능성이 있어 원본 파일은 보존

## 드레싱 102개 슬롯 대조

| # | ID | 연결 에셋 |
|---:|---|---|
| 1 | `FloorBand` | `(data/material anchor)` |
| 2 | `Frost_Deck` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Decal_Frost_Deck.prefab` |
| 3 | `Frost_StarboardWall` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Decal_Frost_Wall.prefab` |
| 4 | `Frost_Conduit` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Decal_Frost_Conduit.prefab` |
| 5 | `Scorch_Deck` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Decal_Scorch_Deck.prefab` |
| 6 | `Scorch_PortWall` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Decal_Scorch_Wall.prefab` |
| 7 | `Scorch_Conduit` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Decal_Scorch_Conduit.prefab` |
| 8 | `DuctLane_Run` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Decal_DuctLane_Run.prefab` |
| 9 | `DuctLane_Leg` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Decal_DuctLane_Leg.prefab` |
| 10 | `AirlockStripe_0` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Decal_HazardPost.prefab` |
| 11 | `AirlockStripe_1` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Decal_HazardPost.prefab` |
| 12 | `AirlockStripe_2` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Decal_HazardPost.prefab` |
| 13 | `AirlockStripe_3` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Decal_HazardPost.prefab` |
| 14 | `Lamp` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Lamp_Cockpit.prefab` |
| 15 | `Lamp` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Lamp_Power.prefab` |
| 16 | `Lamp` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Lamp_Cooling.prefab` |
| 17 | `Lamp` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Lamp_LifeSupport.prefab` |
| 18 | `Lamp` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Lamp_Cockpit.prefab` |
| 19 | `Lamp` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Lamp_Airlock.prefab` |
| 20 | `Lamp` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Lamp_Duct.prefab` |
| 21 | `CableTray_Fore` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_CableTray.prefab` |
| 22 | `CableTray_Mid` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_CableTray.prefab` |
| 23 | `CableTray_Aft` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_CableTray.prefab` |
| 24 | `ConduitJunction_Port_Fore` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_ConduitJunction.prefab` |
| 25 | `ConduitJunction_Port_Aft` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_ConduitJunction.prefab` |
| 26 | `ConduitJunction_Starboard` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_ConduitJunction.prefab` |
| 27 | `PanelBank_Starboard` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_PanelBank.prefab` |
| 28 | `RibFrame_Port_Fore` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_RibFrame.prefab` |
| 29 | `RibFrame_Port_Aft` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_RibFrame.prefab` |
| 30 | `DeckGrate_Fore` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_DeckGrate.prefab` |
| 31 | `DeckGrate_Starboard` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_DeckGrate.prefab` |
| 32 | `NavChartTable` | `(data/material anchor)` |
| 33 | `HelmThrottle_Port` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_HelmThrottleStand.prefab` |
| 34 | `HelmThrottle_Starboard` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_HelmThrottleStand.prefab` |
| 35 | `HelmSeat_Port` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Seat.prefab` |
| 36 | `HelmSeat_Starboard` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Seat.prefab` |
| 37 | `CrateStack_Aft` | `Assets/DoodleUp/Prefabs/Dressing/RealProps/LP_CargoCrate_0p7m.prefab` |
| 38 | `CableTray_Fore` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_CableTray.prefab` |
| 39 | `CableTray_Aft` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_CableTray.prefab` |
| 40 | `ConduitJunction_Starboard_Fore` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_ConduitJunction.prefab` |
| 41 | `ConduitJunction_Starboard_Aft` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_ConduitJunction.prefab` |
| 42 | `ConduitJunction_Port` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_ConduitJunction.prefab` |
| 43 | `PanelBank_Starboard` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_PanelBank.prefab` |
| 44 | `RibFrame_Starboard` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_RibFrame.prefab` |
| 45 | `DeckGrate_Fore` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_DeckGrate.prefab` |
| 46 | `DeckGrate_Aft` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_DeckGrate.prefab` |
| 47 | `CableTray_Port` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_CableTray.prefab` |
| 48 | `CableTray_Starboard` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_CableTray.prefab` |
| 49 | `ConduitJunction_Port` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_ConduitJunction.prefab` |
| 50 | `RibFrame_Port` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_RibFrame.prefab` |
| 51 | `StowageNet_Port` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_StowageNet.prefab` |
| 52 | `DeckGrate_Port` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_DeckGrate.prefab` |
| 53 | `DeckGrate_Starboard` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_DeckGrate.prefab` |
| 54 | `BreakerCabinet` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_BreakerCabinet.prefab` |
| 55 | `PartsPallet` | `Assets/DoodleUp/Prefabs/Dressing/RealProps/LP_PortableBattery_0p5m.prefab` |
| 56 | `CrateStack_Fore` | `Assets/DoodleUp/Prefabs/Dressing/RealProps/LP_CargoCrate_0p7m.prefab` |
| 57 | `CrateStack_Aft` | `Assets/DoodleUp/Prefabs/Dressing/RealProps/LP_CargoCrate_0p7m.prefab` |
| 58 | `CableTray_Port` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_CableTray.prefab` |
| 59 | `CableTray_Starboard` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_CableTray.prefab` |
| 60 | `ConduitJunction_Port` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_ConduitJunction.prefab` |
| 61 | `RibFrame_Port` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_RibFrame.prefab` |
| 62 | `StowageNet_Port` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_StowageNet.prefab` |
| 63 | `DeckGrate_Fore` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_DeckGrate.prefab` |
| 64 | `DeckGrate_Aft` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_DeckGrate.prefab` |
| 65 | `HeatExchangerCoil` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_HeatExchangerCoil.prefab` |
| 66 | `CoolingRack` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_CoolingRack.prefab` |
| 67 | `LashRail_Port` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_LashRail.prefab` |
| 68 | `CrateStack_Aft` | `Assets/DoodleUp/Prefabs/Dressing/RealProps/LP_CargoCrate_0p7m.prefab` |
| 69 | `PartsPallet` | `Assets/DoodleUp/Prefabs/Dressing/RealProps/LP_PortableBattery_0p5m.prefab` |
| 70 | `CableTray_Fore` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_CableTray.prefab` |
| 71 | `CableTray_Mid` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_CableTray.prefab` |
| 72 | `CableTray_Aft` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_CableTray.prefab` |
| 73 | `ConduitJunction_Port_Fore` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_ConduitJunction.prefab` |
| 74 | `ConduitJunction_Port_Aft` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_ConduitJunction.prefab` |
| 75 | `ConduitJunction_Starboard` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_ConduitJunction.prefab` |
| 76 | `PanelBank_Starboard` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_PanelBank.prefab` |
| 77 | `RibFrame_Port_Fore` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_RibFrame.prefab` |
| 78 | `RibFrame_Port_Aft` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_RibFrame.prefab` |
| 79 | `DeckGrate_Fore` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_DeckGrate.prefab` |
| 80 | `DeckGrate_Aft` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Kit_DeckGrate.prefab` |
| 81 | `ScrubberStack` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_ScrubberStack.prefab` |
| 82 | `O2TankBank_Fore` | `Assets/DoodleUp/Prefabs/Dressing/RealProps/LP_OxygenTank_1m.prefab` |
| 83 | `O2TankBank_Aft` | `Assets/DoodleUp/Prefabs/Dressing/RealProps/LP_OxygenTank_1m.prefab` |
| 84 | `WallLocker_Fore` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_WallLocker.prefab` |
| 85 | `LockerBank_Aft` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_LockerBank.prefab` |
| 86 | `CrateStack_Fore` | `Assets/DoodleUp/Prefabs/Dressing/RealProps/LP_CargoCrate_0p7m.prefab` |
| 87 | `CrateStack_Mid` | `Assets/DoodleUp/Prefabs/Dressing/RealProps/LP_CargoCrate_0p7m.prefab` |
| 88 | `CrateStack_Aft` | `Assets/DoodleUp/Prefabs/Dressing/RealProps/LP_CargoCrate_0p7m.prefab` |
| 89 | `PartsPallet` | `Assets/DoodleUp/Prefabs/Dressing/RealProps/LP_PortableBattery_0p5m.prefab` |
| 90 | `Lamp` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Lamp_Observatory.prefab` |
| 91 | `BerthLight_Port_Lower` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_BerthLight.prefab` |
| 92 | `BerthLight_Port_Upper` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_BerthLight.prefab` |
| 93 | `BerthLight_Starboard_Lower` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_BerthLight.prefab` |
| 94 | `BerthLight_Starboard_Upper` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_BerthLight.prefab` |
| 95 | `Bunk_Port_Lower` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Bunk.prefab` |
| 96 | `Bunk_Port_Upper` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Bunk.prefab` |
| 97 | `Bunk_Starboard_Lower` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Bunk.prefab` |
| 98 | `Bunk_Starboard_Upper` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Bunk.prefab` |
| 99 | `Lockers` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_LockerBank.prefab` |
| 100 | `Basin` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Basin.prefab` |
| 101 | `Stall` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Stall.prefab` |
| 102 | `Table` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Table.prefab` |
| 103 | `Bench_Port` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Bench.prefab` |
| 104 | `Bench_Starboard` | `Assets/DoodleUp/Prefabs/Dressing/LSDress_Bench.prefab` |
| 105 | `VentFan_Service` | `Assets/DoodleUp/Prefabs/Dressing/RealProps/LP_VentFan.prefab` |
| 106 | `EmergencyBeacon_Service` | `Assets/DoodleUp/Prefabs/Dressing/RealProps/LP_EmergencyBeacon.prefab` |

## 판정

- 카탈로그에 없는 레거시/금지 오브젝트는 ship prefab에서 0개다.
- `Visual`, blocker, readout, mesh child는 부모 프리팹 구성요소이므로 독립 삭제하지 않는다.
- map 미사용 prefab은 씬 미배치 상태이며 프로젝트 파일 자체는 다른 제작 경로가 참조할 수 있어 삭제하지 않는다.
