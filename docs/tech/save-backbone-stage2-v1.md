# 세이브 백본 2단계 구현 노트 (v1)

`docs/tech/save-backbone-feasibility-v1.md` 의 남은 넷 — **파일 포맷(§2) · 씬 복원(§3) ·
재진입 가드(§1.4-마) · 저장 RPC(§7.1)** — 을 냈다. 1단계가 낸 스냅샷 확장과 권위 분리
(`save-backbone-stage1-v1.md`) 위에 얹는다.

---

## 1. 층이 셋이고 각자 모르는 것이 있다

| 층 | 파일 | 아는 것 | 모르는 것 |
|---|---|---|---|
| 포맷 | `LastShiftSaveFile.cs` | `JSON` 문자열 · 스키마 판정 | 게임 상태 · 디스크 |
| 담기/되세우기 | `LastShiftSaveCapture.cs` | 게임 상태 · 씬 | 파일 · 소켓 |
| 서비스 | `LastShiftSaveService.cs` | 시점 · 스레드 · 재진입 | 게임 상태 |

**포맷이 디스크를 모르는 것이 요점이다.** §2.2 합격선("저장→로드 후 B층 전 필드 비트 동일")을
문자열 왕복만으로 재므로, 그 합격선이 디스크 상태에 매달리지 않는다 — `EditMode` 에서 씬도 파일도
없이 선다.

## 2. A층을 새로 담지 않았다 — 복제가 이미 담고 있었다

이 카드에서 가장 크게 줄어든 자리다. `LastShiftPlacementReplication` 이 이미
"표 · 구역 오버레이 · 원장 · 항해를 값으로 담고 값에서 되세우는" **순수 함수**를 갖고 있고,
그게 정확히 파일이 필요로 하는 것이다.

```
Capture(records) / CaptureLedger()   →  파일의 campaign
Apply(records)   / ApplyLedger()     →  campaign 에서 배를 되세운다
```

새 `DTO` 를 한 벌 더 만들지 않은 것이 §4.3 불변식을 공짜로 지키게 한다 — `CaptureLedger` 가 읽는
래치 수가 **판정 순간에 접힌** `LastShiftVoyage.LastLatchCount` 이고, 두 번째 캡처 경로가 없으므로
"구간 중에 다시 세어 넣는" 자리가 애초에 생기지 않는다. `S-11` 이 처음으로 실제 위험이 된다고
적은 그 자리다(§7.6).

`CursorHolder` 하나만 저장 시점에 `NoHolder` 로 눕힌다. 세션 안에서만 뜻이 있는 값이라
파일에 실으면 접속하지도 않은 클라이언트가 커서를 든 채로 판이 시작한다.

## 3. 포맷 — `JsonUtility` 한 벌, 그리고 정밀도는 검사로 못 박았다

`{ SchemaA, SchemaB, HasSegment, Campaign, Segment }`. §4.4 권고 형태 그대로다.

- **`schemaB` 불일치** → `Segment` 를 버리고 A만 싣는다(`SegmentDropped`).
- **`schemaA` 불일치** → `Failed`. 조용한 부분 로드를 하지 않는다.
- **구간 키 부재** → 오류가 아니라 정상 경로. 기항 세이브가 이 모양이고, **쓰기도 키 자체를 뺀다.**

`HasSegment` 를 값으로 든 것은 `JsonUtility` 가 `null` 중첩 객체를 표현하지 못하기 때문이다 —
없음을 값으로 적지 않으면 기항 세이브와 "전부 `0` 인 구간" 이 구분되지 않는다. 스키마 기본값을
`0` 으로 둔 것도 같은 이유다: 헤더가 통째로 없는 파일이 현재 스키마로 조용히 통과하면 안 된다.

**§2.2 가 "권고" 로 남긴 왕복 보존을 검사로 바꿨다.** `SavedSegmentRoundTripsEveryFieldBitExact`
가 `0.1+0.2`, `1/3`, `float.MaxValue/3`, `0.90000004` 같은 모양을 통과시킨 뒤
`BitConverter.SingleToInt32Bits` 로 비교한다. **`Unity 6000.4` 의 `JsonUtility` 는 `float` 를
비트 그대로 왕복한다** — 문서가 추정으로 남긴 것을 실측으로 닫았고, 그래서 나중에 "정밀도 때문일
리 없다" 를 다시 증명할 일이 없다.

## 4. 복원 순서 — 표 → 조립 → 주입 → 포즈

```
LastShiftPlacementReplication.Apply(records) / ApplyLedger()   (가)
LastShiftModuleAssembler.Rebuild(yard, palette)                (가)
ApplyNetworkSnapshot(snapshot, Local, latches)                 (나)
아이템·승무원 정지 배치                                          (다)
```

순서가 규약이다. 표가 서기 전에 조립하면 지난 판의 방을 세우고, 조립 전에 포즈를 앉히면 방 안에
있어야 할 물건이 허공에 선다.

**B층 폴백은 두 줄이다**(§4.2) — `EnterSegment(n)` 로 회차만 옮기고 `ResetPreset` 으로 구간 시작을
만든다. 구간 시작은 저장해서 얻는 상태가 아니라 만들어 낼 수 있는 상태라서 그렇고, 원장은
한 글자도 안 건드린다.

아이템은 **속도를 복원하지 않는다**(`S-7`). 그 성질이 저장 시점의 소유자 권위 지연(§7.3, 최대
`30`cm)을 판정에서 떼어 낸다 — 어긋난 값으로 다시 계산되는 것이 없다.

## 5. 실측 — §3.1 이 "지금은 못 잰다" 고 적은 것을 쟀다

```
[LAST_SHIFT_SAVE_PROBE] stage=restore modules=30
                        reassemble=11.30ms inject=0.9149ms pose=0.382ms
                        total=12.59ms budget=10000ms result=PASS
```

**배치물 `30`개 + B층 주입 전체 경로가 `12.59`ms 다. `10`초 예산의 `0.13`%.**

§3.2 는 "(가) 배치물 재조립이 예산을 다 쓰는 유일한 조각" 으로 봤다. 셋 중 가장 큰 조각인 것은
맞지만(`11.30` / `12.59` = `90`%), **예산을 다 쓰지는 않는다** — 셋을 합쳐도 예산의 `0.13`% 다.

> **이 수치는 하한이다.** 측정 칸에 선체 판이 없어서 `LastShiftBakedDoorways.Open` 이 자를 벽이
> 없고, 팔레트가 없어 그레이박스로 선다. 실제 씬은 문틀 `30`개 절단과 프리팹 인스턴스화가 더
> 붙는다. 그래도 남은 여유가 `790`배라 **세이브가 성계 구성을 압박하지 않는다**는 §3.2 의 결론은
> 그대로다.

## 6. 저장 시점과 재진입

- **캡처는 `LateUpdate`**(§7.4). `LastShiftSaveService` 가 `DefaultExecutionOrder(500)` 으로
  `LastShiftNetworkGrabbable`(기본 `0`) 뒤에 선다 — `Update` 에서 캡처하면 들린 아이템이 홀더보다
  한 프레임 뒤인 조합이 파일에 남는다.
- **`RequestSave()` 는 플래그만 세운다.** 입력이 언제 들어오든 담기는 것은 그 프레임의 확정된 포즈다.
- **재진입 가드는 플래그 하나다**(§1.4-마-1). 같은 프레임의 연타는 하나로 접히고, **쓰기 도중의
  요청은 버리지 않고 다음 프레임에 새로 캡처해서 내보낸다** — 버리면 누른 사람은 눌렀는데 아무
  일도 안 일어난 판을 본다.
- **"저장됨" 은 쓰기 완료에만 걸린다**(§1.4-마-2). 직렬화와 디스크 쓰기는 `Task.Run` 으로 나가고,
  완료 판정은 다음 `LateUpdate` 의 메인 스레드에서 한다. 임시 파일에 쓰고 갈아 끼우므로 쓰는 중에
  프로세스가 죽어도 기존 세이브가 반쪽으로 남지 않는다.
- **클라이언트가 누르면 `RequestSaveRpc`**(§7.1). 넘어가는 것은 요청 하나뿐이고 파일도 캡처도
  호스트 것이다(`S-2`). 클라이언트를 세우지 않는다 — 세우면 요구 (가)(체감 정지 `0`)를 스스로 깨고
  얻는 것은 판정에 안 쓰이는 `30`cm 다(§7.3).

## 7. 검증

- `EditMode` `578/578`, `PlayMode` `37/37` (Unity `6000.4.0f1`, cold `unity test`).
  1단계 대비 `+10` / `+4` 이고 기존 테스트는 하나도 안 건드렸다.
- 지키는 것들:
  - `SavedSegmentRoundTripsEveryFieldBitExact` — §2.2 합격선(비트 동일).
  - `SchemaAMismatchFailsWithoutLoadingAnything` / `SchemaBMismatchDropsTheSegmentAndKeepsTheCampaign`
    / `HeaderlessJsonFailsInsteadOfDefaultingToTheCurrentSchema` — §4.4 세 갈래.
  - `MidSegmentCaptureStoresTheFoldedLatchCountNotTheLiveOne` — §7.6 이 붙인 조건.
  - `DroppedSegmentRestartsTheSegmentAndLeavesTheLedgerAlone` — §4.2 폴백이 원장을 안 건드린다.
  - `ReentrantRequestsCoalesceAndAreNotDropped` / `SavedStatusWaitsForTheWriteNotTheCapture` — §1.4-마.
  - `ServiceRoundTripsThroughDisk` — 디스크를 지나는 경로가 실제로 닫힌다.

## 8. 남은 자리

- **씬 배선.** `LastShiftSaveService` 를 씬의 런타임 오브젝트에 붙이고 저장 입력을 잇는 것은
  안 했다 — 저장 버튼과 안내 문구는 §6.5 · `game-planning` 소관이고, 여기서 임의로 키를 잡으면
  그 결정을 코드가 먼저 내린다.
- **팔레트 있는 재조립 실측.** 위 `11.30`ms 는 그레이박스·문틀 절단 없는 하한이다. 실제 씬에서
  같은 로그 태그로 한 번 더 재면 (가)의 진짜 크기가 나온다.
- **로드 시 클라이언트 절차.** §7.8 이 명시적으로 밖에 둔 것 — "재접속인가 인-플레이스 재주입인가"
  는 연출·흐름 선택이라 기술 답이 아니다.
