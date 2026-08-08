# 자유 배치 축 B — 구획표 이중화 (v1)

> 상태: `game-tech-director` 구현 v1 · `2026-08-08` · 카드 `a97c6969`
> 상대 문서: `free-placement-runtime-chain-estimate-v1.md` §3 이 설계, 이 문서가 실제로 들어간 것
> 코드: `LastShiftCompartments.cs` · 테스트 `LastShiftCompartmentTableTests.cs`

---

## 1. 무엇이 갈렸는가

`Count`(const `11`) 하나였던 것이 둘이 됐다.

| | 뜻 | 형태 |
|---|---|---|
| `FixedCount` | enum 이 덮는 영역. `11` | `const int` |
| `Count` | 지금 표 길이 = `FixedCount + ModuleCount` | 런타임 프로퍼티 |
| `FixedSpecs` | 배와 함께 태어난 열하나 | 안 변하는 배열 |
| `Specs` | 고정 + 배치된 모듈 | 등록·해제 때 갈아 끼우는 배열 |

`Of(enum)` 은 **고정 표에서만 찾는다.** 그것이 이 구조를 고른 이유 전부다 — `Of` 호출 `37`
자리와 enum 값을 리터럴로 물고 있는 넷(`UpperGallery`·`ObservationGallery`·`ObservatoryWindow`·
`DressingRules`)이 한 줄도 안 바뀌었다. `Specs` 를 `List` 로 바꾸고 정수 id 로 여는 정공법은
저 넷을 전부 다시 짜게 만든다(추정 §3.2).

`LastShiftCompartmentSpec` 은 `Index` 를 들고 `(int)Compartment == Index` 를 두 종류 모두에서
유지한다. 모듈은 범위 밖 값으로 캐스팅해 담으므로 `Of` 에 넣으면 **터진다** — 조용히 관측실로
읽히는 것보다 낫다. `NameOf` 의 `_ => "Compartment_EscapePod"` 도 같은 이유로 지웠다. 그대로
뒀으면 배치한 모듈이 전부 구명정 이름을 달고 씬에 선다.

---

## 2. 훑는 자리마다 "고정만" 인지 "전부" 인지를 정했다

추정 §3.2 가 "축 B 작업량의 실체" 라고 한 것이 이 표다. `foreach (var spec in Specs)` 가
모듈까지 자동으로 먹는 것이 맞는 곳과 틀린 곳이 갈린다.

| 자리 | 고른 것 | 왜 |
|---|---|---|
| `HullFrames.IsFree` | **고정만** | 골조는 씬을 세울 때 한 번 구워진다. 나중에 붙은 모듈을 세면 답만 바뀌고 씬은 안 바뀐다 |
| `Doorways.BuildAll` | **고정만** | 정적 생성자가 한 번 짓는 표다. 그 시점에 모듈은 하나도 없으므로 `Specs` 를 훑어도 모양만 갖춘다 |
| `CompartmentLabels.DoorwaysOnLabelWall` | **전부** | 이 면에 자식이 붙으면 구멍이 있어야 한다. 모듈이 붙은 것도 자식이다 |
| `DressingSeed` (`2`곳) | **고정만** | 드레싱은 `LastShiftDressingSpace.Of(enum)` 으로 키를 잡는다. 모듈에는 줄 키가 없다 |
| `SceneBuilder.CreateEndWall` · `CreateCompartments` · `ChildrenOn` | **전부** | 실제로 방을 세우는 자리다(축 C 가 여기로 들어온다) |
| `PlacementRules.TableOf(Specs)` | **전부** | 판정기는 지금 서 있는 모든 것과 겹침·사슬을 재야 한다 |

정본 좌표를 지키는 테스트 넷(`CompartmentLayout`·`CompartmentLabel`·`DiscHull`·`ObservatoryWindow`)은
`FixedSpecs`/`FixedCount` 로 옮겼다. 그 넷이 재는 것은 "배가 무엇을 들고 태어나는가" 이므로,
공유 픽스처가 모듈을 하나 등록하는 순간 조용히 깨지면 안 된다.

---

## 3. 등록은 판정을 건너뛸 수 없다

```
TryRegister(candidate, out index, out verdict)
  → Judge(candidate)            // = PlacementRules.Evaluate(TableOf(Specs), ...)
  → 통과하면 표에 append
  → 같은 호출에서 LastShiftPlacedModules.Register(발자국, verdict.Zone)
```

**표와 구역 오버레이를 한 호출에서 같이 잡는 것이 이 카드에서 축 A 와 붙는 자리다.** 따로
등록할 수 있게 두면 발자국은 있는데 압력이 선체 밴드에서 나오는 방이 생긴다 — 문을 닫아도
격리가 안 되는 배가 그것이다(타당성 검토 §11-1). 넘기는 구역은 후보 자기 좌표가 아니라
`verdict.Zone`, 즉 사슬 뿌리의 선체 문이 정한 값이다(조항 F-1).

`Judge` 는 따로 있다. 배치 커서는 매 프레임 재기만 하고, 재는 것이 등록이면 커서를 끄는
것만으로 표가 부풀어 오른다.

후보 제원의 `Index` 는 `NextModuleIndex` 와 같아야 하고, 아니면 `ArgumentException` 이다.
미리 재 본 후보와 실제로 들어가는 것이 같은 물건이라는 것을 그 검사가 건다.

---

## 4. 해제는 표를 당긴다 — 무덤을 안 남긴다

`TryRemove(index)` 는 **자식이 달린 모듈을 거부한다.** 빼면 그 자식이 표 밖을 가리키거나
당겨진 엉뚱한 부모에 붙는다. 잎부터 빼는 것은 부르는 쪽 몫이다.

뺀 뒤에는 뒤 칸을 당기고 그보다 큰 `ParentIndex` 를 하나씩 줄인다. 대가는 **모듈 인덱스가
안정적이지 않다**는 것이고, 그래서 `Revision` 이 같이 있다 — 표를 옮겨 담아 두는 쪽이 자기
사본이 낡았는지 묻는 자리다.

`LastShiftPlacedModules` 는 반대로 무덤(`Registered = false`)을 남기는 쪽을 골랐다. 두 쪽이
다른 것은 의도다. 오버레이의 핸들은 나눠 준 뒤 남이 들고 있으므로 당기면 남의 핸들이 다른
모듈을 가리키게 되고, 구획표는 `Specs` 를 훑는 자리 여섯 곳이 전부 "죽은 칸인가" 를 물어야
하는데 그 물음을 한 곳에서 빠뜨리면 씬에 부피 없는 방이 선다. **자기 배열을 누가 훑는지가
달라서 답이 갈렸다.**

마지막 모듈이 빠지면 `Specs` 는 `FixedSpecs` **그 자체**로 돌아간다. 길이만 같은 사본을
남기면 그 뒤로 고정 표를 두 벌 드는 셈이다.

---

## 5. 검증

`unity test --mode EditMode` cold 실행, **418개 전부 통과**(기존 `400` + 신규 `18`).
결과는 `CardB_EditMode.xml`.

신규 `18`개가 거는 것: 고정 영역이 enum 과 정확히 같은가 · 모듈이 없는 표가 고정 표 자체인가 ·
`Of` 가 append 인덱스를 거부하는가 · 모듈 이름이 구명정이 아닌가 · 사슬이 두 영역을 넘는가 ·
물린 후보가 표와 오버레이 어디에도 안 남는가 · 재는 것이 등록이 아닌가 · 등록이 발자국을 사슬
뿌리 구역으로 덮는가 · 해제가 그 자리를 선체에 돌려주는가 · 자식 달린 모듈이 안 빠지는가 ·
당겨진 뒤 부모가 따라오는가 · `Revision` 이 오르는가 · 고정만 훑는 자리가 안 흔들리는가.

**기존 `400`개가 하나도 안 움직인 것이 이 전환이 값 갈이가 아니라는 증거다** — 모듈이 없으면
`Count == FixedCount` 이고 `Specs`·`FixedSpecs` 가 같은 배열이므로, 이 파일들은 예전과 같은
경로를 돈다.

Play 검증은 안 했다. 이 카드에는 씬에 서는 것이 없다 — 모듈이 실제로 방으로 세워지는 것은
축 C 다.

---

## 6. 안 한 것

- **모듈 vs 선체 골조 겹침.** 판정기는 겹침(배치끼리)·선체 내부·사슬·이탈만 본다. 골조를 뚫는
  모듈은 지금 통과한다. `HullFrames.IsFree` 를 판정기가 부르게 하는 것이 자연스럽지만 그건
  축 D 를 여는 일이라 여기서 안 했다.
- ~~**모듈 문틀·드레싱.**~~ **닫힘** — 카드 `1363faf6`, `free-placement-baked-doorway-v1.md`.
  `LastShiftDoorways` 는 이제 `Revision` 으로 다시 짓고 모듈 문을 담는다. 드레싱 검사가 모듈
  문 앞도 같이 보게 됐고, 부모 쪽 구운 벽은 `LastShiftBakedDoorways` 가 뚫는다.
- **모듈 이름의 인덱스 의존.** `Compartment_Module_{index}` 는 앞 칸이 빠지면 당겨진다. 배치
  해제가 기항에서만 일어나고 그때 씬을 다시 세우므로 지금은 문제가 아니다. 판 안에서 해제가
  가능해지면 이름을 인덱스에서 떼야 한다.
- **네트워크 동기화.** 추정 §8 의 전제("배치는 기항에서만")를 그대로 받았다. 표가 네트워크
  상태가 되면 `Revision` 만으로는 안 되고 결정론적 id 가 필요하다.
