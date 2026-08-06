# LAST SHIFT 드레싱 에셋 정본 — 조명 수치 · 프리팹 · 머티리얼 (v1, `game-art`)

`docs/scene-dressing-brief-v1.md`(planning)가 정한 **무엇을·왜**를 실제 에셋으로 옮긴 것이다.
브리프 §9 미결 `1`("각 방의 정확한 조명 수치(색온도/럭스)·에셋 목록 확정 — `game-art`")에 대한 답이다.

배치(어디에)는 `game-tech-director` 의 `Assets/DoodleUp/Dressing/LastShiftDressingSet.asset` 이 정한다.
이 문서는 그 에셋의 `prefab` 칸에 무엇을 넣으면 되는지까지 적는다(§4).

## 1. 결론 먼저

- **프리팹 `58`개** — `Assets/DoodleUp/Prefabs/Dressing/LSDress_*.prefab`
- **머티리얼 `21`개** — `Assets/DoodleUp/Materials/Dressing/LSD_*.mat`
- **조명 `21`등** — 공간마다 색온도(K)와 목표 조도(lx)를 정하고, 그 값에서 Unity `intensity`/`range` 를 역산해 프리팹에 박았다(§3)
- **지난 회차(`last-shift-dressing-props-v1.md` §4.2)의 조명 권고는 틀렸다. 여기서 정정한다** — §3.2
- 설비 회색·경고 황·서버 LED·생장등은 새로 만들지 않고 씬 빌더가 이미 들고 있는 `LS_Fixture`/`LS_Hazard`/`LS_ServerIndicator`/`LS_GrowLight` 를 참조한다

## 2. 프리팹 규약 (배치 훅 연동)

`docs/scene-dressing-authoring.md`(tech)의 `prefab` 칸에 그대로 드래그하면 된다. 세 가지만 지켰다.

1. **루트 = 밑면.** 훅의 `bottomY` 가 밑면 기준이라 프리팹 원점을 바닥(또는 등기구 렌즈 아래)에 뒀다. 천장등은 `bottomY = 3.04`(천장 `3.2` − 등기구 두께 `0.16`)로 넣으면 천장면에 딱 붙는다
2. **정면 = `+z`.** 벽에 붙는 것(`ToolRack`, `StarChart`, `EvaSuitRack`, `BusPanel`, `EmergencyStrobe`)은 `+z` 가 방 안쪽을 보게 만들었다. `eulerAngles` 로 벽 법선에 맞춘다
3. **스케일 `1`.** 훅이 `size` 를 프리팹에 곱하지 않으므로(가이드 명시) 전부 실측 치수로 만들었다. `size` 칸에는 §5 표의 바깥 치수를 그대로 적으면 경계 검사가 맞는다. **인스턴스 스케일을 바꾸지 말 것** — 배관 두께·난간 굵기가 같이 늘어 손 크기 기준이 무너진다

콜라이더는 **부피가 있어 통과하면 즉시 가짜로 보이는 것**에만 붙였다(§5 표의 `C` 열). 얇은 판·띠·발광체·등기구에는 없다 — `#4d3bbdd9` 가 세운 "드레싱이 통행 폭을 `1cm` 도 줄이면 안 된다"를 그대로 따른다.

## 3. 조명

### 3.1 럭스를 어떻게 계산했나

빌트인 RP 는 측광 단위가 없다. 그래서 **지금 씬을 기준점으로 삼아 환산 상수를 하나 잡고, 그 위에서 모든 값을 역산했다.**

- 감쇠 모델: `atten = 1 / (1 + 25·d² / r²)`, `d ≥ r` 이면 `0` (하드 클립)
- 기준점: 현재 구역 등(`intensity 2.5` / `range 7` / 높이 `2.85`)의 바닥 조도를 **`300 lx`** 로 둔다
- 그러면 `1 illum unit = 617.3 lx`. 복도(등 간격 `5.5`, `r 7`, 등 높이 `3.04`)에서는 **`intensity 1` 당 바닥 평균 `142.4 lx`**

**`617.3` 은 물리값이 아니라 "지금 씬이 이 정도 밝기다"라는 합의점이다.** 톤매핑이나 노출이 들어오면 이 상수를 다시 잡아야 하고, 그때 아래 표의 `intensity` 는 전부 같은 비율로 움직인다. 럭스 칸은 그대로 두면 된다 — 그게 이 표를 럭스로 적은 이유다.

### 3.2 정정 — 지난 회차 권고는 틀렸다

`last-shift-dressing-props-v1.md` §4.2 는 "등 사이에 어두운 골을 만들려면 `range 7 → 5.0`, `intensity 2.7~2.9`" 라고 적었다. **계산해 보니 둘 다 틀렸다.**

| `range` | `intensity` | 등 바로 아래 | 등 사이 중간 |
|---|---|---|---|
| `7.0` | `2.5` (현재) | `416 lx` | `322 lx` |
| `5.0` | `5.6` | `338 lx` | **`388 lx`** |
| `4.0` | `8.5` | `340 lx` | **`0 lx`** |

1. **`range` 를 줄여도 골이 안 생긴다.** 천장이 `3.04m` 인데 등 간격이 `5.5m` 라, 중간 지점은 등에서 수평 `2.75m` → 실거리 `4.10m` 밖에 안 된다. 수직 `3.04m` 와 차이가 작아서 **중간에서 등 둘을 받는 쪽이 바로 아래에서 하나를 받는 쪽보다 오히려 밝다.** `r ≤ 6` 에서는 비율이 뒤집힌다
2. **`r ≤ 4.0` 이면 중간이 `0 lx` 가 된다.** 하드 클립이라 부드러운 골이 아니라 바닥에 원이 그려진다
3. **`intensity 2.7~2.9` 는 `range 5` 에서 오히려 어둡다** — 약 `170 lx` 로, 지금(`300 lx`)의 절반이다. "평균 밝기는 유지"라고 쓴 것이 틀렸다

**즉 천장 `3.2m` 짜리 복도에서는 점광원의 간격·반경으로 명암 리듬을 만들 수 없다.** 대신 두 가지로 만든다.

- **구역·방마다 조도를 다르게 한다.** 관측실 `25 lx` → 복도 `300 lx` → 정비창 `450 lx` 를 통과하면 눈이 적응하며 길이감이 생긴다. 이게 §3.3 표의 조도가 방마다 크게 다른 이유다
- **국소 스팟(`LSDress_TaskSpot`)** — 스팟 `46°` 는 바닥에 지름 약 `3.8m` 짜리 경계가 분명한 풀을 만든다. 점광원이 못 만드는 대비를 이것이 만든다

**구역 등은 `range 7` 을 유지한다.** 다만 `intensity` 는 방마다 다시 잡았다(§3.3).

### 3.3 조명표 — 공간별 색온도 / 목표 조도 / Unity 값

`shadow`: `0`=없음 `1`=Hard `2`=Soft. 목표 조도는 복도가 span 평균, 방이 중심 바닥이다.

| 프리팹 | 공간 | CCT | 목표 조도 | `intensity` | `range` | `shadow` | 톤 근거(브리프) |
|---|---|---|---|---|---|---|---|
| `LSDress_Lamp_Cockpit` | 조종석 | `6500K` | `350 lx` | `2.46` | `7.0` | `2` | 침착한 청록/파랑. 배에서 가장 밝다 (§2.1) |
| `LSDress_Lamp_Power` | 전력실 | `3000K` | `200 lx` | `1.40` | `7.0` | `2` | 따뜻한 주황. 브리프가 "낮은 조도" 명시 (§2.2) |
| `LSDress_Lamp_Cooling` | 냉각실 | `7500K` | `300 lx` | `2.11` | `7.0` | `2` | 차가운 흰색/시안. 전력실과 색·밝기 둘 다 대비 (§2.3) |
| `LSDress_Lamp_LifeSupport` | 산소실 | `5000K` | `300 lx` | `2.11` | `7.0` | `2` | 초록기 있는 중성. 청결·안전 (§2.4) |
| `LSDress_Lamp_Observatory` | 관측실 | `2800K` | `25 lx` | `0.57` | `4.2` | `0` | 창밖 별을 죽이지 않게 최대한 낮춘다 (§6.1) |
| `LSDress_Lamp_Workshop` | 정비창 | `5000K` | `450 lx` | `7.74` | `4.9` | `1` | 제작하는 방. 배에서 가장 밝은 작업광 (§5.2) |
| `LSDress_Lamp_CargoBay` | 화물칸 | `3800K` | `90 lx` | `0.87` | `6.8` | `1` | 어두운 창고. 전반은 일부러 낮다 (§5.1) |
| `LSDress_Lamp_Hangar` | 격납고 | `3000K` | `200 lx` | `1.65` | `7.5` | `1` | 산업 노랑. `8×10` 공간감을 대비로 (§6.3) |
| `LSDress_Lamp_ServerRoom` | 서버/통신실 | `8000K` | `90 lx` | `1.49` | `5.0` | `0` | 차가운 블루. 화면을 크게 안 띄운다 (§6.2) |
| `LSDress_Lamp_Lavatory` | 화장실 | `4000K` | `180 lx` | `3.48` | `4.6` | `0` | 실용적 흰색 (§3) |
| `LSDress_Lamp_Quarters` | 숙소 | `3000K` | `120 lx` | `1.99` | `5.0` | `0` | 전반은 낮게. 취침등이 따로 있다 (§3) |
| `LSDress_Lamp_Lounge` | 휴게실 | `3500K` | `250 lx` | `4.15` | `5.0` | `1` | 밝고 사교적. 테이블 위가 중심 (§3) |
| `LSDress_Lamp_Hydroponics` | 수경재배 | `4500K` | `80 lx` | `1.12` | `5.5` | `0` | 통행용 최소. 방의 빛은 생장등이 만든다 (§5.3) |
| `LSDress_Lamp_MedBay` | 의무실 | `5500K` | `450 lx` | `7.74` | `4.9` | `1` | 청결한 처치광 (§4.2) |
| `LSDress_Lamp_EscapePod` | 구명정 | `3200K` | `150 lx` | `3.14` | `4.4` | `0` | 차분한 대기 상태 (§4.3) |
| `LSDress_Lamp_Airlock` | 에어록 | `2700K` | `200 lx` | `4.57` | `4.2` | `1` | 경고 톤. 감압 시 붉은 비상등으로 전환 (§4.1) |
| `LSDress_Lamp_Duct` | 우회 통로 | `3000K` | `15 lx` | `0.60` | `2.4` | `0` | **제약 4.** §3.4 |
| `LSDress_TaskSpot` | 화물칸·정비창 | `3800K` | `300 lx` | `4.20` | `4.5` | `2` | 스팟 `46°`. 국소 대비 전용 (§5.1) |
| `LSDress_BerthLight` | 숙소 침상 | `2400K` | `45 lx` | `0.35` | `1.6` | `0` | 개인 취침등. 통로로 안 새게 `range 1.6` (§3) |
| `LSDress_GrowLightBar` | 수경재배 | CCT 없음 | — | `1.10` | `2.0` | `0` | 마젠타 직접색 `(0.86, 0.42, 0.76)` (§5.3) |
| `LSDress_EmergencyStrobe` | 에어록·사고 | CCT 없음 | — | `1.10` | `3.2` | `0` | 적색 직접색. **상시 점등 아님** (§4.1) |

**`range` 하한 `4.2` 는 규칙이다.** 방 대각선 끝까지 빛이 닿게 `√((대각선/2)² + 3.04²) × 1.06` 으로 잡고 `4.2` 를 하한으로 뒀다. 그 아래로 내리면 모서리가 하드 클립으로 새까매진다(§3.2-2). 이 규칙 때문에 관측실(`3×4`)과 에어록(`3×3`)은 방이 작아도 `4.2` 다.

**색온도는 `m_UseColorTemperature: 1` 로 켜 뒀다.** `m_Color` 는 흰색이고 CCT 가 색을 만든다 — 나중에 톤을 조정할 때 켈빈 하나만 만지면 된다. 등기구 렌즈 발광색(`LSD_LensWarm/Neutral/Cool/Cold`)도 같은 CCT 대역에 맞춰 뒀다. 라이트만 바꾸고 렌즈를 안 바꾸면 등이 자기 빛과 다른 색으로 빛난다.

**앰비언트·Directional 은 그대로 둔다.** `ambientLight (0.10, 0.11, 0.14)` 는 이미 형태를 잃지 않을 최소값이고, `Directional 0.25` 는 천장에 막혀 개구부·창에서 새는 방향감만 담당한다.

### 3.4 우회 통로 — 제약 4 를 조명으로 건다

브리프 §7 이 "쾌적하면 안 된다"고 못 박았고, tech 규칙은 `LightSource` 밝기 합 `≤ 2.0` 이다.

`LSDress_Lamp_Duct` 는 `intensity 0.6` 이라 **셋까지 걸 수 있다(합 `1.8`).** `range 2.4` 는 웅크림 높이(`0.9m`) 단면에서 발밑만 겨우 비추는 값이다 — 앞이 안 보이고 지금 밟는 자리만 보인다. 바닥 조도로는 약 `15 lx`, 복도의 `1/20` 이다.

**`Comfort` 플래그가 붙는 것(핸드레일·넓은 발판)은 여기에 아예 안 만들었다.** 우회 통로용 소품은 등 하나뿐이다.

## 4. 배치 훅 연동 — `id` → 프리팹 매핑

`LastShiftDressingSet.asset` 의 `90`개 항목 중 프리팹으로 갈아 끼울 것들이다. **나머지(`FloorBand`, `Frost_*`, `Scorch_*`, `DuctLane_*`, `AirlockStripe_*`, `LaunchMark_*`)는 재질 판 그대로 두는 것이 맞다** — 전부 바닥/벽에 그려진 띠·얼룩이라 부피를 주면 발에 걸린다.

| `id` (패턴) | 프리팹 | 비고 |
|---|---|---|
| `SightingConsole` | `LSDress_SightingConsole` | |
| `ObserverSeat`, `PodSeat_*` | `LSDress_Seat`, `LSDress_PodSeat` | 구명정은 하네스가 달린 별도 좌석 |
| `InstrumentColumn` | `LSDress_InstrumentColumn` | |
| `StarChart` | `LSDress_StarChart` | 벽. 정면 `+z` |
| `Bench_*` (정비창) | `LSDress_Workbench` | 작업대다 — 휴게실 `Bench_*` 와 다른 프리팹 |
| `Bench_*` (휴게실) | `LSDress_Bench` | |
| `ToolRack` | `LSDress_ToolRack` | 벽 |
| `PartsPallet` | `LSDress_PartsPallet` | |
| `Crate_0..3` | `LSDress_Crate` | `eulerAngles.y` 를 항목마다 조금씩 달리 주면 쌓인 티가 난다 |
| `LashRail_*` | `LSDress_LashRail` | |
| `Cradle_*` | `LSDress_Cradle` | |
| `HangarRack` | `LSDress_HangarRack` | |
| `Gantry` | `LSDress_Gantry` | 높이 `2.84` — 격납고 천장 `3.0` 기준으로 확인 필요 |
| `Rack_0..3` | `LSDress_ServerRack` | |
| `RackIndicator_0..3` | `LSDress_RackIndicator` | `RoomSystemReadout` 유지 |
| `Basin` | `LSDress_Basin` | |
| `Stall_*` | `LSDress_Stall` | |
| `Bunk_*` | `LSDress_Bunk` | 상단 침상은 `bottomY` 만 올린다 |
| `Lockers` | `LSDress_LockerBank` | `4`연. `2`연이 필요하면 `LSDress_WallLocker` |
| `Table` | `LSDress_Table` | |
| `GalleyCounter` | `LSDress_GalleyCounter` | |
| `Tray_*` | `LSDress_GrowTray` | |
| `Growth_*` | `LSDress_GrowthHealthy` | 열화 표현은 `LSDress_GrowthWilted` 로 **교체**한다 |
| `GrowLight_*` | `LSDress_GrowLightBar` | `LightSource` 추가 필요 |
| `MedBed`, `ScannerArch`, `MedCabinet` | 동명 `LSDress_*` | |
| `PodConsole` | `LSDress_PodConsole` | 상태등 적/녹 포함. `RoomSystemReadout` |
| `HatchRing` | `LSDress_HatchRing` | |

**이 매핑은 이미 `LastShiftDressingSet.asset` 에 넣어 뒀다.** `90`개 중 `65`개에 `prefab` 을 물렸고, `size` 는 프리팹 실측 바깥 치수로 갈아 끼웠다(경계 검사가 실제 부피를 보게 하려고). 좌표(`anchor`·`bottomY`·`eulerAngles`)는 tech 가 정한 값을 하나도 안 건드렸다.

`GrowLight_*` 여섯 항목만 `semantics` 도 손댔다 — `LightSource`(`1<<6`)를 더하고 `lightIntensity: 1.1` 을 적었다. 발광체인데 안 적으면 `C4`(우회 통로 밝기 합) 와 같은 기준으로 집계되지 않는다.

**씬은 다시 굽지 않았다.** `Rebuild Network Sandbox` 는 씬과 그레이박스 프리팹을 통째로 재직렬화해 `fileID` 가 전부 갈리므로, 빌드 시점은 tech 가 잡는 게 맞다.

### 4.1 새로 추가하기를 권하는 항목

기존 `90`개에 없지만 브리프가 요구하는 것들이다. 넣을지는 tech 판단이고, 넣는다면 프리팹은 준비돼 있다.

| 프리팹 | 공간 | `semantics` | `bottomY` | 왜 |
|---|---|---|---|---|
| `LSDress_Lamp_*` (`16`종) | 각 공간 | `LightSource` | `3.04` | **지금 빛의 출처가 안 보인다.** 라이트는 점광원 오브젝트뿐이다 |
| `LSDress_Lamp_Duct` ×`3` | 우회 통로 | `LightSource` | 관 천장 −`0.09` | §3.4 |
| `LSDress_TaskSpot` | 화물칸·정비창 | `LightSource` | `3.04` | §3.2 — 명암 대비를 만드는 유일한 수단 |
| `LSDress_BerthLight` ×`4` | 숙소 | `LightSource` | 침상 위 `0.6` | 브리프 §3 "사물함마다 다른 개인화" |
| `LSDress_BusPanel` | 전력실 | 없음 | `0` | 브리프 §2.2 배전반·`Battery` 슬롯·케이블 다발 |
| `LSDress_CoolingRack` | 냉각실 | 없음 | `0` | 브리프 §2.3 `CoolingCanister` 거치대·배관 |
| `LSDress_EvaSuitRack` | 에어록 | 없음 | `0` | 브리프 §4.1 EVA 우주복 거치대 |
| `LSDress_ConduitRun` | 통로 벽 상부 | 없음 | `2.60` | `4m` 간격. 눈높이 `0.6~2.1` 침범 금지(좌현이 창이다) |
| `LSDress_EmergencyStrobe` | 에어록 | 없음 | `2.00` | 상시 점등 아님 — 사고 상태에 물린다 |

**`BusPanel`·`CoolingRack` 에는 `StateResponsive` 를 붙이지 않는다.** 상태에 반응하지 않는 정적 설비라 노출 원뿔(`z ≤ +1.40`) 제한 대상이 아니다 — 브리프 §2.2/§2.3 과 `LastShiftDressing.StateCueSafeMaxZ` 주석이 같은 판단을 이미 적어 뒀다. 상태를 말하는 서리·그을음은 기존 `Frost_*`/`Scorch_*` 가 안전대 안에서 전담한다.

## 5. 프리팹 목록 (58)

`C` = 콜라이더 있음. 치수는 바깥 경계(m), 훅의 `size` 칸에 그대로 쓴다.

| 프리팹 | 치수 `x×y×z` | C | 프리팹 | 치수 `x×y×z` | C |
|---|---|---|---|---|---|
| `Lamp_*` (`16`종) | `1.6×0.16×0.38` | | `Lamp_Duct` | `0.30×0.09×0.20` | |
| `TaskSpot` | `0.32×0.34×0.32` | | `BerthLight` | `0.20×0.08×0.10` | |
| `GrowLightBar` | `1.16×0.09×0.24` | | `EmergencyStrobe` | `0.30×0.31×0.16` | |
| `SightingConsole` | `1.84×0.90×0.54` | ● | `InstrumentColumn` | `0.50×1.72×0.50` | ● |
| `StarChart` | `1.50×1.10×0.06` | | `Seat` | `0.50×0.90×0.50` | ● |
| `Workbench` | `2.02×1.08×0.72` | ● | `ToolRack` | `1.40×1.10×0.05` | |
| `PartsPallet` | `1.12×0.40×0.92` | ● | `Crate` | `0.92×0.72×0.77` | ● |
| `LashRail` | `2.40×0.20×0.12` | | `Cradle` | `2.20×0.80×1.45` | ● |
| `Gantry` | `0.30×2.84×4.10` | | `HangarRack` | `2.50×2.00×0.64` | ● |
| `ServerRack` | `0.76×2.00×0.87` | ● | `RackIndicator` | `0.56×0.09×0.03` | |
| `Basin` | `0.62×1.75×0.46` | ● | `Stall` | `1.20×2.00×0.24` | |
| `Bunk` | `2.02×0.45×0.92` | ● | `LockerBank` | `2.02×1.90×0.44` | ● |
| `WallLocker` | `1.02×1.90×0.44` | ● | `Table` | `1.82×0.78×0.92` | ● |
| `Bench` | `1.72×0.50×0.44` | ● | `GalleyCounter` | `1.66×2.02×0.64` | ● |
| `GrowTray` | `1.22×0.90×0.62` | ● | `GrowthHealthy` | `1.04×0.22×0.25` | |
| `GrowthWilted` | `1.04×0.12×0.25` | | `MedBed` | `2.02×1.04×0.78` | ● |
| `ScannerArch` | `0.36×1.60×1.46` | | `MedCabinet` | `0.92×1.82×0.38` | ● |
| `PodSeat` | `0.60×1.00×0.60` | ● | `PodConsole` | `1.02×1.17×0.42` | ● |
| `HatchRing` | `1.48×0.15×1.48` | | `EvaSuitRack` | `1.20×2.10×0.61` | ● |
| `BusPanel` | `1.62×2.24×0.47` | ● | `CoolingRack` | `1.52×1.87×0.52` | ● |
| `ConduitRun` | `4.00×0.55×0.22` | | `CrateStack` | `1.00×1.55×0.80` | ● |
| `ShaftLadder` | `0.47×1.60×0.05` | | | | |

## 6. 머티리얼 (21)

`Assets/DoodleUp/Materials/Dressing/`. 새로 만든 것만이다 — 설비 회색(`LS_Fixture`), 경고 황(`LS_Hazard`), 서버 LED(`LS_ServerIndicator`), 생장등(`LS_GrowLight`), 어두운 패널(`LS_Panel`)은 씬 빌더가 정본이라 참조만 한다.

**규칙은 지난 회차와 같다 — 드레싱은 무채색, 채도는 게임플레이의 몫이다.** 배터리 `(0.95, 0.65, 0.12)`·냉각 캐니스터·테더가 이미 채도를 쓰고 있어서, 배경에 채도를 주면 집을 수 있는 것과 섞인다. 구분은 명도 사다리로 한다.

| 대역 | 머티리얼 | 뜻 |
|---|---|---|
| `0.12~0.16` | `LSD_ConduitBand` `LSD_Mat` | 틈·바탕. "사이"로 읽혀야 하는 면 |
| `0.18~0.22` | `LSD_LampHousing` | 벽/천장에 붙어 있는 것 |
| `0.24~0.30` | `LSD_Conduit` `LSD_Locker` | 선체에서 떨어진 설비 |
| `0.30~0.34` | `LSD_Crate` `LSD_CrateTrim` `LSD_Fabric` | 사람이 놓아 둔 것 |
| `0.52~0.74` | `LSD_Glass` `LSD_Ceramic` | 청결면(의무실·화장실). 이 배에서 유일하게 밝은 표면 |
| 발광 | `LSD_Screen` `LSD_ScreenAmber` `LSD_StatusRed` `LSD_StatusGreen` `LSD_StrobeLens` `LSD_Lens{Warm,Neutral,Cool,Cold}` | 화면·상태등·등기구 렌즈에만 |

**예외 둘.**

- `LSD_Plant (0.24, 0.48, 0.22)` / `LSD_PlantWilt (0.42, 0.39, 0.19)` — 수경재배실 식물. **색이 곧 상태**라 무채색 규칙 밖이다. 브리프 §5.3 이 열화 표현을 명시적으로 허용했고, `LastShiftDressing` 이 이걸 게이지가 아닌 "방 고유 시스템"으로 분류했다
- `LSD_Ceramic (0.72, 0.74, 0.76)` — 의무실·화장실의 청결감은 밝기로만 낼 수 있다. 다른 방에는 쓰지 않는다

**청결면을 밝게 쓴 것이 이 문서에서 유일하게 손대는 명도 규칙이다.** 지난 회차는 "손 닿는 것만 밝다(`LS_Fixture 0.39`)"였는데, 의무실이 `0.39` 로는 청결해 보이지 않는다. `0.72` 는 화이트가 아니라 밝은 회색이라 아이템(패치 판 `0.78~0.88`)보다는 여전히 어둡다.

## 7. 4대 제약이 에셋에 어떻게 걸려 있나

제약은 원래 **배치**에 걸리는 규칙이고 tech 검증기(`C1_ExposureCone` 등)가 판정한다. 에셋 쪽에서 미리 막아 둔 것만 적는다.

| 제약 | 에셋에서 한 것 |
|---|---|
| **1. 노출 원뿔** (냉각실·전력실 상태 단서 `z ≤ +1.40`) | 냉각실·전력실용 프리팹(`CoolingRack`·`BusPanel`)을 **상태 비반응 정적 설비로 만들었다** — 발광부·상태등을 아예 안 넣었으므로 `StateResponsive` 를 붙일 이유가 없고, 따라서 원뿔 안에 놓아도 새는 정보가 없다 |
| **2. 잠긴 구획 해치 표식 금지** | **해치 표식 프리팹을 만들지 않았다.** `LSDress_HatchRing` 은 구명정 좌석 옆 바닥 링이고 언락 신호가 아니다 — 구명정은 §15.4 로 처음부터 열려 있어 제약 대상 자체가 아니다 |
| **3. 압력존 미편입 구획 게이지·사이렌 금지** | 구획용 프리팹 어디에도 압력 게이지·경보등이 없다. 발광이 붙은 것은 `RackIndicator`(통신 상태)·`PodConsole`(발진 상태)·`GrowLightBar`(생장등) 셋뿐이고, 전부 브리프 §1.3 이 "방 고유 시스템"으로 분류한 것들이다 |
| **4. 우회 통로 = 불편함** | 통로용 소품은 등 하나(`LSDress_Lamp_Duct`)뿐이다. `Comfort` 로 분류될 핸드레일·발판을 **만들지 않았다.** 등도 `15 lx` / `range 2.4` 로 발밑만 비춘다(§3.4) |

`RackIndicator`·`PodConsole`·`GrowLightBar` 는 `RoomSystemReadout` 이 필요하고 `justification` 이 필수다. 문구는 브리프 §6.2 / §4.3 / §5.3 을 그대로 쓰면 된다.

## 8. 검증

- **Unity `6000.4.0f1` 콜드 임포트 통과** — 프리팹 `58` · 머티리얼 `21` 임포트 오류 `0`, `error CS` `0`
- **참조 검사 통과** — 외부 GUID 참조 `0` 끊김. 씬 빌더 정본 재질(`LS_Fixture` 등) 재사용분 포함
- **계층 무결** — 프리팹마다 루트 `Transform` `1`개, `GameObject` 수 = `Transform` 수
- **`Light` 컴포넌트 `21`개** — 표 §3.3 과 개수 일치
- **EditMode 테스트 `201`개 전부 통과** (`failed 0`, `5.7s`) — 에셋만 추가했으니 통과가 당연하고, 통과한다는 것이 이 층이 기존 제약·좌표 정본을 안 건드렸다는 증거다
- **tech 의 4대 제약 검증기 통과** — `[LAST_SHIFT_DRESSING_VALIDATION] props=90 violations=0 result=PASS`. 프리팹 `65`개를 물리고 `size` 를 실측치로 갈아 끼운 상태에서 `C1`~`C4` 전부 통과했다
- **물린 참조 `65`개 전부 해석됨** — 끊긴 프리팹 참조 `0`

### 8.1 남는 수동 확인

임포트가 통과했다는 것은 에셋이 열린다는 뜻이지 화면에서 옳다는 뜻이 아니다. 배치가 붙은 뒤에 봐야 한다.

1. **`617.3 lx/unit` 환산이 실제 화면과 맞는가** — §3.1 은 "현재 구역 등 = `300 lx`" 라는 가정 위에 서 있다. 눈으로 보고 어긋나면 상수만 고치고 표의 럭스는 그대로 둔다
2. **정비창·의무실 `450 lx`(`intensity 7.74`)가 날아가지 않는가** — 환산상 `0.73 illum unit` 이라 클리핑은 아니지만, 등 바로 아래가 흰 판으로 뭉개지면 `350 lx` 로 내린다
3. **`Gantry` 높이 `2.84m`** — 격납고 천장이 `3.0m` 라 여유가 `0.16m` 뿐이다. 천장 리브와 겹치면 빔 높이를 `2.6` 으로 낮춘다
4. **수경재배 마젠타(`LS_GrowLight`)가 옆 방으로 새는가** — `range 2.0` 으로 묶었지만 문이 열렸을 때 복도에서 보이면 구역색 넷과 경쟁한다
5. **의무실·화장실 청결면(`0.72`)이 아이템과 안 헷갈리는가** — 패치 판(`0.78~0.88`)과 명도가 가깝다. 헷갈리면 `0.62` 로 내린다
6. **우회 통로가 실제로 불편한가** — `15 lx` 가 "긴장되는 어둠"인지 "아무것도 안 보이는 불편"인지는 걸어 봐야 갈린다. 후자면 등을 `4`개로 늘리는 대신 `intensity 0.45` 로 낮춰 합 `1.8` 을 유지한다
