# 자유 배치 — `Resolve()` 런타임 사슬 전환 작업량 추정 (v1)

> 상태: `game-tech-director` 추정 v1 · `2026-08-08` · 카드 `78b69855-82ad-44c8-a009-81583c09330a` (사용자 직접 지시)
> 상대 문서: `free-placement-expansion-feasibility-v1.md` §12-1 이 이 카드에 넘긴 항목이다 — "실시간 검증의 실제 작업 규모: `Resolve()` 사슬 전환, `LastShiftCompartments` 가변화, 파생처 영향 범위"
> 실측 대상: `LastShiftCompartments.cs`(358) · `LastShiftZonePressure.cs`(380) · `LastShiftSceneBuilder.cs`(2,240) · `LastShiftDressingSeed.cs`(719) · `LastShiftBypassDuct.cs` · `LastShiftSandboxController.cs` · asmdef 8개 · `Rg1GuardrailTests`(471) · `PlazaRg1Tests`(651). **전부 이번 추정에서 직접 읽었다.**
> **산출물은 작업량 추정이다. 코드를 한 줄도 안 바꿨다.**

---

## 0. 결론 먼저 — 숫자 넷

1. **`Resolve()` 전환은 이 일에서 가장 싼 조각이다. 수정 `~15줄`, 신규 `~120줄`, 1.5작업일.** 그리고 **똑같은 모양의 선례가 이미 매 tick 경로에서 돌고 있다** — `LastShiftSandboxController.IsZoneVacuum(Vector3)`(`:814`)이 `Resolve` 를 부르기 **전에** `LastShiftBypassDuct.IsUnpressurizedSpace` 로 점–AABB 루프를 먼저 본다. 타당성 검토 §2.2 가 제안한 구조를 새로 만드는 게 아니라 그 자리를 넓히는 일이다.

2. **씬 빌더 `2,240`줄은 관문 크기가 아니다. 자유 배치 경로에 실제로 필요한 것은 `368`줄이고, 그중 Editor 결박은 정확히 `2`줄이다.** `CreateCompartment`(`:1479`)~`CreateDressingProps`(`:1846`) 닫힘 안의 `UnityEditor` 호출은 `AssetDatabase.LoadAssetAtPath<LastShiftDressingSet>`(`:1768`)과 `PrefabUtility.InstantiatePrefab`(`:1827`) 둘뿐이다. 파일 전체 Editor API `26`곳 중 나머지 `24`곳은 프리팹 저장·선체 베이·아이템 프리팹·머티리얼 자산에 몰려 있고 **전부 자유 배치 경로 밖**이다.

3. **진짜 뿌리는 정적 배열이 아니라 `enum LastShiftCompartment` 다.** `Specs` 배열을 `List` 로 바꾸는 건 쉽다. 안 되는 것은 `Of(LastShiftCompartment)`(`37`회 호출)와 `NameOf` switch 다 — **확장 모듈에는 줄 enum 값이 없다.** `LastShiftUpperGallery`·`LastShiftObservationGallery`·`LastShiftObservatoryWindow`·`LastShiftDressingRules` 가 enum 값을 리터럴로 물고 있다. 그래서 §3 은 배열 교체가 아니라 **고정 `11` + append 영역 이중 표**를 권고한다.

4. **RG-1 계산은 싸지만, 런타임에 존재하지 않는다. 그리고 테스트 안에 두 벌 있다.** `EgressMeters`·`ChainToHull` 은 `LastShiftRg1GuardrailTests:438`·`LastShiftPlazaRg1Tests:519` 에만 있고, 그 어셈블리는 `"includePlatforms": ["Editor"]` 다. 타당성 검토가 "계산 자체는 관문이 아니다" 라고 한 것은 맞지만, **그 계산은 아직 옮겨야 할 곳에 없다.** 배치 판정기 신규가 이 일에서 가장 큰 단일 조각이다(`~260`줄, 3작업일).

**합계: 최소 세로줄 `12작업일`.** 배치 UI 를 넣으면 `15`, 절차적 생성을 고르면 `13.5`, 둘 다면 `16.5`.

---

## 1. 작업일의 정의

"1작업일" = 구현 + 그 조각을 고정하는 EditMode 테스트 + 기존 테스트 회귀 통과까지다. Play 검증과 멀티 검증은 §7 에 따로 뗀다. **줄 수를 같이 적는 이유는 작업일이 틀렸을 때 어디가 틀렸는지 대조할 수 있게 하려는 것이다** — 줄 수는 실측이고 작업일은 환산이다.

---

## 2. 축 A — `Resolve()` 오버레이

### 2.1 실측: 호출부 `12`곳 중 거동이 바뀌는 것은 `4`곳이다

런타임 호출부 전수(테스트 `23`곳 제외):

| 호출부 | 넘기는 좌표 | 자유 배치에서 |
|---|---|---|
| `SandboxController:817` `IsZoneVacuum(Vector3)` | **플레이어 발밑** | **바뀐다 · 매 tick** |
| `SandboxController:793` 사망 구역 기록 | **플레이어 위치** | **바뀐다** |
| `SandboxController:1791` `TryResolveLocalZone` (HUD) | **플레이어 위치** | **바뀐다 · 매 프레임 GUI** |
| `SandboxController:1757` 배터리 라벨 | **휴대물 위치** | **바뀐다** |
| `SandboxController:251` `beyondX` | 고정 x | 안 바뀐다 |
| `SandboxController:329·332·396` 아이템/계통 명목 | `NominalPosition` | 안 바뀐다 |
| `SandboxController:382` 승강구 목 | 고정 | 안 바뀐다 |
| `BypassDuct:120` | 덕트 내부 후보 | 안 바뀐다 |
| `CoolingValve:79` · `DeckHatch:119` | 고정 명목 | 안 바뀐다 |
| `ImpactFeedback:57` | 선체 피격점 | 안 바뀐다 |
| `DressingSeed:463` (Editor) | 통로 중심 x | 안 바뀐다 |

**`Resolve` 를 고치면 `12`곳을 손보는 게 아니다. 함수 하나를 고치고, `4`곳의 거동이 따라 바뀐다.** 나머지 `8`곳은 고정 명목좌표를 넘기므로 오버레이에 안 걸린다 — 확장 모듈이 그 좌표를 덮지 않는 한, 그리고 모듈은 원반 밖이므로 안 덮는다.

### 2.2 선례가 이미 매 tick 경로에 있다

```csharp
// LastShiftSandboxController.cs:814
public bool IsZoneVacuum(Vector3 position)
{
    if (LastShiftBypassDuct.IsUnpressurizedSpace(position)) return true;   // ← 점-AABB 루프
    return IsZoneVacuum(LastShiftZoneAtlas.Resolve(position));
}
```

`IsUnpressurizedSpace` 가 도는 `ShaftContains`(`BypassDuct:204`)는 `ShaftCount` 개에 대한 `Mathf.Abs` 두 번짜리 점–AABB 루프다. **§2.2 가 제안한 모듈 오버레이와 같은 모양이고, 같은 매 tick 경로에 이미 들어가 있고, 테스트도 붙어 있다.** 그 파일 주석이 "진공 판정을 매 tick 도는 자리에 씬 조회를 들이지 않는 것이 이 선택의 이유다" 라고 적어 둔 그 규약을 그대로 따르면 된다 — **오버레이는 씬을 안 보고 값 배열만 본다.**

### 2.3 들어갈 것

```csharp
// 신규 LastShiftPlacedModules (Runtime)
//   - 배치 확정된 모듈의 (AABB, 캐시된 LastShiftZone) 목록
//   - TryResolve(Vector3, out LastShiftZone) : 점-AABB 선형 훑기
//   - 조항 F-1: 구역은 배치 시점 사슬 뿌리로 정하고 이후 재계산 없음

// 수정 LastShiftZoneAtlas.Resolve  (+3줄)
public static LastShiftZone Resolve(Vector3 position)
{
    if (LastShiftPlacedModules.TryResolve(position, out var moduleZone)) return moduleZone;
    for (var boundary = 0; boundary < BoundaryCount; boundary++)
        if (position.x <= BoundaryPlanes[boundary]) return (LastShiftZone)boundary;
    return (LastShiftZone)(ZoneCount - 1);
}
```

`ZoneCount = 4` 를 안 건드리므로 §2.3 이 나열한 다섯(`LastShiftZonePressures` 배열 · `SIMUL_ZONES` · `RG-4` 조합 · HUD 칸 · 네트워크 스냅샷)이 전부 안 열린다. **이게 `Resolve` 전환이 싼 이유의 전부다.**

| | |
|---|---|
| 신규 | `LastShiftPlacedModules` `~120`줄 |
| 수정 | `LastShiftZoneAtlas.Resolve` `+3`줄 · 호출부 `0`줄 · 초기화 훅 `~12`줄 |
| 테스트 | `LastShiftZoneTopologyTests`(현재 `ZoneAtlas` 참조 `40`회)에 오버레이 케이스 `~80`줄 |
| **추정** | **1.5작업일** |

**여기의 위험은 규모가 아니라 자리다.** 타당성 검토 §11-1 이 옳다 — 틀리면 문을 닫아도 격리가 안 되는 배가 나온다. 그래서 §7 순서에서 이 조각은 판정기(축 D) **뒤에** 온다. 판정기가 먼저 있어야 "모듈 구역 = 사슬 뿌리 구역" 이 테스트로 고정된다.

---

## 3. 축 B — `LastShiftCompartments` 가변화

### 3.1 배열은 문제가 아니다. enum 이 문제다

```csharp
// LastShiftCompartments.cs:167
public static LastShiftCompartmentSpec Of(LastShiftCompartment compartment) => specs[(int)compartment];
```

`Of(enum)` 호출 `37`회, `NameOf` 는 `11`갈래 switch(`:170`), 정적 생성자(`:352`)가 `specs.Length == Enum.GetValues(...).Length` 를 강제한다. **확장 모듈은 컴파일 타임에 enum 값을 가질 수 없다.** 그리고 enum 값을 리터럴로 물고 있는 런타임 코드가 넷이다 — `LastShiftUpperGallery`(`Of` `11`회), `LastShiftObservationGallery`(`3`), `LastShiftObservatoryWindow`, `LastShiftDressingRules`(`NameOf` `4`).

### 3.2 그래서 이중 표를 권고한다

배열을 `List` 로 바꾸고 `Of` 를 정수 id 로 여는 "정공법"은 저 넷을 전부 다시 짜게 만든다. 대신:

- `Count`(현재 `const int = 11`) → **`FixedCount`(const `11`, enum 대응 영역)** + **`Count`(런타임 프로퍼티, `FixedCount + 배치 수`)** 로 가른다.
- `Specs` 는 `[0, FixedCount)` 가 enum 인덱스 그대로, `[FixedCount, Count)` 가 append 영역이다.
- `Of(enum)` 은 **그대로 살아 있다.** 저 넷을 안 건드린다.
- 확장 모듈은 `NameOf` 대신 `Compartment_Module_{index}` 규칙 이름을 쓰고, `LastShiftCompartmentSpec` 에 `DisplayName` 을 붙이지 않는다 — 붙이면 `readonly struct` 가 문자열을 들어 배치 판정 루프가 GC 를 만든다.
- 정적 생성자 검사는 `FixedCount` 기준으로 좁힌다.

**대가:** `foreach (var spec in Specs)` 로 도는 자리가 확장 모듈까지 자동으로 먹는다. 그게 맞는 곳(씬 빌더 `:840`, 배치 판정)과 틀린 곳(선체 프레임 `HullFrames`, 문틀 `Doorways`)이 갈리므로 **호출부마다 "고정만" 인지 "전부" 인지를 한 번씩 정해야 한다.** 이게 축 B 작업량의 실체다.

### 3.3 파생처 실측

`LastShiftCompartment*` 를 참조하는 파일 `24`개:

| 구분 | 파일 수 | 그중 수정 필요 | 근거 |
|---|---|---|---|
| Runtime | `11` | **`5`** | `Compartments` · `Doorways` · `HullFrames` · `CompartmentLabels` · `DressingRules` — `Specs` 를 훑는다 |
| Editor | `2` | **`2`** | `SceneBuilder:840` 훑기 · `DressingSeed:Specs` `2`회 |
| Tests | `11` | **`6`** | `Count == 11` · `Specs.Length` 를 전제하는 것들 |
| — | | 나머지 `11` | `Of(enum)` 만 쓰므로 이중 표에서 **안 바뀐다** |

`LastShiftSceneVerifier`(354줄)는 구획을 **한 번도 참조하지 않는다.** 검증기는 이 일에서 빠진다.

| | |
|---|---|
| 신규 | 이중 표 접근자 · 배치 등록/해제 `~200`줄 |
| 수정 | 런타임 `5` · Editor `2` · 테스트 `6` = `~250`줄 |
| **추정** | **3작업일** |

---

## 4. 축 C — 런타임 조립 경로

### 4.1 `2,240`줄 중 `368`줄이다

`CreateCompartment`(`:1479`)부터 `CreateDressingProps`(`:1846`)까지가 구획 하나를 세우는 닫힘이다 — `IsOwnDoorFace` · `ChildrenOn` · `ChildDoorwaysOn` · `WindowsOn` · `CreateWallWithOpenings` · `CreateSlab` · `CreateCompartmentLabel` · `CreateCompartmentDressing` · `CreateDecorCube` · 드레싱 세트 로더 · `SameSpace` · `HasDressing` · `CreateDressingProps`. **`368`줄.**

### 4.2 그 안의 Editor 결박은 `2`줄이다

| 줄 | 호출 | 런타임 치환 |
|---|---|---|
| `:1768` | `AssetDatabase.LoadAssetAtPath<LastShiftDressingSet>` | 직렬화 참조(`ScriptableObject` 를 컴포넌트 필드로) |
| `:1827` | `PrefabUtility.InstantiatePrefab` | `Object.Instantiate` |

파일 전체 Editor API `26`곳 중 나머지 `24`곳의 분포: 배 프리팹 저장 `:125–129`, 선체 창 베이/기둥 프리팹 `:1118–1181`, 아이템 프리팹 `:1968–2001`, 머티리얼 자산 `:2167–2236`. **자유 배치 경로에 하나도 안 들어온다.**

예외 하나: `compartmentMaterial`(`:34`)이 `CreateMaterial`(`:2183`, `AssetDatabase.CreateAsset`) 산이다. 런타임은 **직렬화된 머티리얼 팔레트**를 물어야 한다(`~40`줄). `Shader.Find` 로 런타임 생성하면 빌드에서 셰이더가 스트립돼 분홍색이 된다.

### 4.3 이게 타당성 검토 §4 의 근거를 바꾼다

§4 는 **"런타임에 방을 세우는 경로가 이 프로젝트에 없다"** 를 근거로 절차적 생성을 기각하고 모듈 프리팹 조립을 권고했다. 측정하면 그 문장은 **자산 경로에 대해서는 맞고 지오메트리 경로에 대해서는 틀리다** — 지오메트리 경로는 `368`줄 이동 + `2`줄 치환 + 머티리얼 팔레트 `40`줄이다.

**권고 자체는 바꾸지 않는다. 근거만 바꾼다.** 프리팹 조립이 여전히 나을 수 있다 — 드레싱 프롭이 이미 프리팹이고(`:1827`), 아트가 손댈 표면이 프리팹이라야 `game-art` 가 tech 를 안 거치고 일한다. 하지만 그 선택의 이유는 "경로가 없어서" 가 아니라 **"아트 파이프라인이 그걸 원해서"** 이고, 그건 tech 판단이 아니라 §12-2 의 `game-art` 판단이다. **근거가 틀린 채로 굳으면, 나중에 절차적 생성이 필요해졌을 때 "불가능하다고 결론 났다" 로 잘못 읽힌다.**

| 선택지 | 신규 | 이동 | 추정 |
|---|---|---|---|
| **(나) 모듈 프리팹 조립** | `~180`줄(조립기·앵커·머티리얼 팔레트) | `0` | **2.5작업일** + 아트 |
| (가) 런타임 절차 생성 | `~220`줄 | `368`줄 | **4작업일** |

---

## 5. 축 D — 배치 판정기 (이 일의 가장 큰 조각)

### 5.1 RG-1 은 런타임에 없다. 그리고 두 벌이다

```
LastShiftRg1GuardrailTests.cs:438   private static float EgressMeters(LastShiftCompartmentSpec spec)
LastShiftPlazaRg1Tests.cs:519       private static (float Meters, Vector3 HullDoor) ChainToHull(...)
```

둘 다 `DoodleUp.Tests.EditMode` 안이고, 그 asmdef 는 `"includePlatforms": ["Editor"]` 에 `autoReferenced: false` 다. **런타임 어셈블리에서 RG-1 을 부를 방법이 지금 없다.** 게다가 이탈 거리 계산이 두 파일에 독립 사본으로 있다 — 자유 배치가 붙기 전에도 이미 갈라질 수 있는 상태다.

### 5.2 들어갈 것

신규 `LastShiftPlacementVerdict` (Runtime):

| 판정 | 내용 | 비용 |
|---|---|---|
| 겹침 | 신규 모듈 AABB vs 기존 `Count` 개 | `O(N)` |
| 선체 침범 | `OverlapsHullInterior` 재사용 | `O(1)` |
| 사슬 | `ParentIndex` 순환·깊이 (`DoorDepth` 재사용) | `O(깊이)` |
| `RG-1(1)` 이탈 | `EgressMeters` 를 런타임으로 승격 | `O(깊이)` |
| `W-1` | 구역 내 최장 쌍 | `O(N²)`, `N=20` 이면 `400` |
| 구역 귀속 | 사슬 뿌리 → 축 A 오버레이 등록 (조항 F-1) | `O(깊이)` |

`N = 20` 에서 전체 재판정이 산술 수백 회다. 타당성 검토 §0-2 의 "커서를 움직일 때마다 돌려도 부담이 없다" 는 그대로 성립한다 — **비용이 아니라 존재가 없었을 뿐이다.**

### 5.3 부수 이득과 선행 조건

두 테스트 파일이 같은 런타임 함수를 부르게 되면 중복 사본이 사라진다. 다만 **승격 전에 두 사본이 지금 같은 값을 내는지 대조해야 한다**(반나절). 값이 다르면 어느 쪽이 정본인지가 먼저 정해져야 하고, 그건 `rg1-1-measurement-definition-v1.md` 조문 대조 건이다.

| | |
|---|---|
| 신규 | `LastShiftPlacementVerdict` `~260`줄 |
| 수정 | `Rg1GuardrailTests` · `PlazaRg1Tests` 를 런타임 함수 호출로 `~120`줄 |
| **추정** | **3작업일** (사본 대조 0.5 포함) |

---

## 6. 축 E — 파생처 보수 · 배치 UI

| 항목 | 추정 | 비고 |
|---|---|---|
| 테스트 `40`개 중 구획 표 전제 `6`개 보수 | 2작업일 | §3.3 |
| 배치 커서 · `1m` 스냅 · `90°` 4단 회전 · 미리보기 | 3작업일 | **이 카드 밖.** §12-9(2인 이상에서 커서 소유권)이 미결이라 단일 클라이언트 전제로만 잡은 값이다 |

---

## 7. 합산과 순서

| 축 | 조각 | 작업일 |
|---|---|---|
| D | 배치 판정기 (RG-1/W-1 런타임 승격) | **3** |
| A | `Resolve()` 오버레이 | **1.5** |
| B | 구획 표 이중화 | **3** |
| C | 모듈 조립 (프리팹 안) | **2.5** |
| E | 테스트 보수 | **2** |
| | **세로줄 합계** | **12** |
| | + 배치 UI(§12-9 미결 전제) | 15 |
| | + 절차적 생성 선택 시 | +1.5 |

### 순서를 `D → A → B → C → E` 로 권고한다

1. **`D` 가 먼저인 이유가 이 추정의 실질 권고다.** 판정기는 **자유 배치가 하나도 안 붙은 지금 상태에서 검증되는 유일한 조각**이다 — 기존 구획 `11`개를 넣고 기존 EditMode 테스트와 값이 같은지 대조하면 끝난다. 뒤로 미루면 `A`·`B` 를 고칠 때 정답을 들고 있는 게 없다.
2. `A` 가 `B` 보다 먼저다. `Resolve` 오버레이는 모듈 AABB 목록만 있으면 되고, 구획 표를 안 건드린다. 여기서 격리 거동을 먼저 고정해야 `B` 가 표를 흔들 때 회귀가 잡힌다.
3. `C` 는 마지막이다. 형상이 없어도 `D`+`A`+`B` 는 EditMode 로 전부 검증된다 — 실제로 방이 서는 건 Play 검증 한 번에 몰아도 된다.

---

## 8. 안 센 것 · 전제

- **`Equalize()` 체적 항**(§6-나, §12-5). 안 셌다. 압력 시뮬을 여는 유일한 항목이고 `game-balance` 값이 먼저다.
- **`RG-1(3)` 조문 개정**(§6). 안 셌다. 조문 작업이고 tech 작업이 아니다.
- **격납고·화물칸 제거 후 도안 §5 재실행**(§12-3). 별건이다. 이 추정의 어느 축에도 안 들어간다.
- **`SIMUL_ZONES ≤ 2` 재검**(§12 표). 위 `12작업일` 밖이다. 모듈이 원반 밖이고 게이지가 개구부 `1`·`3` 에 있어 위반 가능성이 낮다는 §12 판단에 동의하지만, 확인은 배치 UI 가 있어야 가능하다.
- **네트워크 동기화.** 배치가 기항(판 밖)에서만 일어난다는 §3.1 전제를 그대로 받았다. 판 안에서 배치가 가능해지면 이 추정 전체가 무효다 — 모듈 목록이 네트워크 상태가 되고 `Resolve` 오버레이가 클라이언트마다 갈릴 수 있다.
- **`45°` 회전.** §3.3 대로 기각된 것으로 두고 셌다. 허용되면 `WallAperture`·`CreateWallWithOpenings`(`:1623`)·`AABB` 겹침·`DressingRules` 가 통째로 다시 열린다. 그 경우 축 C·D 가 각각 2배 이상이다.

---

## 9. §12-1 에 대한 회신 요약

| 타당성 검토가 물은 것 | 실측 답 |
|---|---|
| `Resolve()` 사슬 전환 규모 | 수정 `~15`줄 · 신규 `~120`줄 · **1.5작업일**. 호출부 `12` 중 거동이 바뀌는 것은 `4`. 동형 선례(`BypassDuct` 오버레이)가 이미 매 tick 경로에 있다 |
| `LastShiftCompartments` 가변화 규모 | **3작업일**. 뿌리는 배열이 아니라 `enum` 이고, 이중 표로 `Of(enum)` 을 살리면 참조 파일 `24` 중 `13`만 손댄다 |
| 파생처 영향 범위 | Runtime `5` · Editor `2` · Tests `6`. `LastShiftSceneVerifier` 는 **무관하다**(구획 참조 `0`) |
| 씬 빌더 `2,240`줄이 관문인가 | **아니다.** 관련 `368`줄, Editor 결박 `2`줄. §4 의 기각 근거를 "경로가 없다" 에서 "아트 파이프라인 판단" 으로 고쳐 적어야 한다 |
| RG-1 계산이 싼가 | **싸다. 그러나 런타임에 없고 테스트에 두 벌 있다.** 승격이 이 일의 최대 단일 조각(`3작업일`)이고 **가장 먼저 해야 한다** |
