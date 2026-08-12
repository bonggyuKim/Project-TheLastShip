# LAST SHIFT ModularKit 하위 오브젝트 매니페스트

감사 기준: `Assets/DoodleUp/Prefabs/LastShiftModularKit/*.prefab` 직렬화 계층.

아래의 `Visual`은 해당 프리팹의 실제 메시 컨테이너다. root와 `Visual`은 항상 한 프리팹 단위로 유지·교체하며, 어느 쪽도 독립 씬 에셋이 아니다.

| 프리팹 | 직렬화된 하위 이름 | map 역할 | 독립 삭제 |
|---|---|---|---|
| LPK_Airlock_Exterior_4m | Visual, LPK_Airlock_Exterior_4m | 구형 외부 airlock 외피 | 불가 |
| LPK_Ceiling_Curve45 | Visual, LPK_Ceiling_Curve45 | 곡선 천장 전환 | 불가 |
| LPK_Ceiling_Straight_4m | Visual, LPK_Ceiling_Straight_4m | 직선 천장 구조 | 불가 |
| LPK_CentralLift_4m | LPK_CentralLift_4m, Visual | 중앙 EVA 리프트 판 | 불가 |
| LPK_Cockpit_ControlConsole | LPK_Cockpit_ControlConsole, Visual | 조종실 실콘솔 | 불가 |
| LPK_Cockpit_ViewWindow_4m | Visual, LPK_Cockpit_ViewWindow_4m | 조종실 선수 창 | 불가 |
| LPK_Cockpit_WallMirror_1m | FrameLeft, FrameRight, FrameTop, FrameBottom, ReflectiveSurface, LPK_Cockpit_WallMirror_1m | 거울 프레임·반사면 | 불가 |
| LPK_Connector_Neck_2m | LPK_Connector_Neck_2m, Visual | 모듈 연결부 | 불가 |
| LPK_Cooling_Exchanger | Visual, LPK_Cooling_Exchanger | 냉각실 주 설비 | 불가 |
| LPK_Corner_Inner_90 | LPK_Corner_Inner_90, Visual | 내측 코너 구조 | 불가 |
| LPK_Corner_Outer_90 | LPK_Corner_Outer_90, Visual | 외측 코너 구조 | 불가 |
| LPK_DamagedPipe_2m | Visual, LPK_DamagedPipe_2m | 생명유지 손상 목표물 | 불가 |
| LPK_DeckHatch_2m | LPK_DeckHatch_2m, Visual | 구형 deck hatch | 불가 |
| LPK_Door_Airlock_2m | Visual, LPK_Door_Airlock_2m | 방 경계 압력문 visual | 불가 |
| LPK_EVA_ConningTower_3m | LPK_EVA_ConningTower_3m, Visual | EVA 타워 | 불가 |
| LPK_EVA_TopHatch_1p6m | LPK_EVA_TopHatch_1p6m, Visual | EVA 상단 해치 | 불가 |
| LPK_Floor_Curve_45 | LPK_Floor_Curve_45, Visual | 곡선 갑판 전환 | 불가 |
| LPK_Floor_Square_2m | Visual, LPK_Floor_Square_2m | 주 갑판/underlay/천장 패널 | 불가 |
| LPK_Floor_Transition_2m | LPK_Floor_Transition_2m, Visual | 갑판 전환 | 불가 |
| LPK_Hull_Exterior_Curve45 | Visual, LPK_Hull_Exterior_Curve45 | 곡선 선체 외피 | 불가 |
| LPK_Hull_Exterior_Curve90 | Visual, LPK_Hull_Exterior_Curve90 | 90도 선체 외피 | 불가 |
| LPK_Hull_Exterior_Panel_4m | Visual, LPK_Hull_Exterior_Panel_4m | 32분할 선체 외피 | 불가 |
| LPK_Hull_WindowBay_4m | Visual, LPK_Hull_WindowBay_4m | 외피 창 베이 | 불가 |
| LPK_LifeSupport_Scrubber | Visual, LPK_LifeSupport_Scrubber | 생명유지 주 설비 | 불가 |
| LPK_OxygenLeakPipe_2m | Visual, LPK_OxygenLeakPipe_2m | 산소 누출 목표물 | 불가 |
| LPK_Power_Switchgear | Visual, LPK_Power_Switchgear | 전력실 주 설비 | 불가 |
| LPK_Quarters_Bunk | LPK_Quarters_Bunk, Visual | 숙소 침상 | 불가 |
| LPK_RepairConsole_1m | Visual, LPK_RepairConsole_1m | 수리 목표물 | 불가 |
| LPK_SalvagePad_4m | Visual, LPK_SalvagePad_4m | 구형 salvage pad | 불가 |
| LPK_Support_Pillar | Visual, LPK_Support_Pillar | 구조 지지대 | 불가 |
| LPK_TetherRack_2m | Visual, LPK_TetherRack_2m | tether 보관/표식 | 불가 |
| LPK_Wall_Curve_45 | LPK_Wall_Curve_45, Visual | 곡선 벽 | 불가 |
| LPK_Wall_Straight_2m | LPK_Wall_Straight_2m, Visual | 2m 직선 벽 | 불가 |
| LPK_Wall_Straight_4m | LPK_Wall_Straight_4m, Visual | 4m 직선 벽 | 불가 |
| LPK_Wall_Window_4m | Visual, LPK_Wall_Window_4m | 창 벽 | 불가 |

## 별도 런타임 자식

| 부모 | 자식 | 역할 | 독립 삭제 |
|---|---|---|---|
| ZoneDoor_B* | ZoneDoor_B*_Blocker | 닫힌 문 통행 차단 | 불가 |
| EVA top hatch | EvaTopHatch_Blocker | 닫힌 해치 통행 차단 | 불가 |
| Panel_* | Panel_*_Readout_0/1/2 | 벽 패널 발광 readout | 불가 |
| CoolingValve | Body/Lever/Spoke/Hub | C-3 조작 visual | 불가 |
| CrateStack_Fore | Visual | LP_CargoCrate 실물 메시 | 불가 |

`LPK_Ceiling_Straight_4m_Beam`은 이 prefab의 직렬화 child에는 없다. Unity Inspector에서 보이는 경우 FBX 내부 mesh/submesh 이름이며, 독립 map asset이나 독립 삭제 대상이 아니다.
