# 씬 드레싱 작성 가이드 (art 용)

`docs/scene-dressing-brief-v1.md`(planning)의 배치 의도를 실제 씬에 넣는 방법이다.
**코드를 고칠 일이 없다.** 에셋 하나를 Inspector 에서 채우면 씬 빌드가 그대로 세운다.

## 어디를 고치나

`Assets/DoodleUp/Dressing/LastShiftDressingSet.asset`

Props 리스트에 항목을 추가하면 소품 하나가 늘어난다. 지금 90개가 들어 있다.

## 항목 하나 채우기

| 칸 | 뜻 |
|---|---|
| `id` | 씬 하이어라키에 그대로 붙는 이름. 같은 공간 안에서만 안 겹치면 된다 |
| `space` | 어느 공간인가. `kind` 를 고르고 그에 맞는 칸만 채운다 (Zone→`zone`, Compartment→`compartment`, Passage→`passage`) |
| `size` | 미터 단위 크기 |
| `anchorMode` | `UnitOfSpace` = 방 크기 대비 비율, `MetersFromSpaceCenter` = 방 중심에서 미터 |
| `anchor` | (x, z) 자리. 단위좌표면 `-1` = 한쪽 벽, `0` = 가운데, `+1` = 반대 벽 |
| `bottomY` | 바닥에서 소품 밑면까지 높이 |
| `prefab` | 드래그. 넣으면 이 프리팹을 세운다 |
| `material` | 드래그. 프리팹이 없을 때 박스에 입힌다 |
| `semantics` | 이 소품이 **무엇으로 읽히는지**. 아래 참조 |
| `lightIntensity` | `LightSource` 일 때 밝기 |
| `justification` | 예외를 쓸 때 사유 |

**자리는 되도록 `UnitOfSpace` 로 적는다.** 방 치수가 바뀌어도 벽에 붙은 채로 따라간다.
미터로 적으면 치수 개정 때 소품만 벽을 뚫고 남는다.

`prefab` 을 넣으면 프리팹 스케일을 그대로 쓴다. `size` 는 경계 검사용 치수일 뿐
프리팹에 곱하지 않는다 — 그래야 넣은 에셋이 안 찌그러진다.

## semantics — 제약이 여기서 걸린다

상자가 게이지인지 아닌지는 좌표로 알 수 없어서, **적은 대로 판정한다.**
아무것도 안 적으면 제약도 안 걸린다.

| 플래그 | 뜻 | 걸리는 제약 |
|---|---|---|
| `StateResponsive` | 선체·구역 상태에 반응한다 (서리가 자란다, 아크가 튄다) | 냉각실·전력실에서 `z ≤ +1.40` |
| `PressureGauge` | 압력 게이지 | 구획 11개에 금지 |
| `SirenEffect` | 전선 사이렌·경보 | 구획 11개에 금지 |
| `HatchMarker` | 해치 표식·언락 신호 | 공간이 잠긴 구획에 금지 |
| `RoomSystemReadout` | 그 방 고유 시스템 표현 | 수경재배·서버통신실·의무실·구명정만, 사유 필수 |
| `Comfort` | 핸드레일·넓은 발판 같은 쾌적 설비 | 우회 통로에 금지 |
| `LightSource` | 발광체 | 우회 통로 밝기 합 ≤ 2.0 |

게이지 금지와 "그 방 고유 시스템" 의 구분은 브리프 §1.3 을 따른다.
수경재배 식물 열화, 서버 LED, 구명정 발진 상태등은 게이지가 아니다 —
대신 `RoomSystemReadout` 으로 적고 `justification` 에 왜 게이지가 아닌지 쓴다.

## 색을 고치고 싶을 때

`Assets/DoodleUp/Materials/LS_*.mat` 을 직접 고친다. **씬을 다시 구워도 안 덮인다.**
선체·바닥 같은 구조물 재질만 코드가 정본이라 매번 덮어쓴다.

## 확인하는 법

에셋만 검사한다(초 단위, 씬 안 굽는다):

```
unity run <project> -- -nographics -logFile validate.log \
  -executeMethod DoodleUp.Editor.LastShiftDressingValidation.ValidateForAutomation
```

에디터에서는 메뉴 `Last Shift/SP-02A/드레싱 데이터 검증`.

씬에 반영하려면 `Last Shift/SP-02A/Rebuild Network Sandbox`.
**위반이 있으면 씬이 안 구워진다** — 소품을 세우기 전에 멈추므로 위반본이 저장되는 일은 없다.
로그에 규칙 id(`C1_ExposureCone` 등)와 어느 소품인지가 같이 찍힌다.

## 에셋을 되살려야 할 때

`Last Shift/SP-02A/드레싱 에셋 부트스트랩 (덮어씀)` — 코드에 든 초기값 90개로 되돌린다.
**Inspector 편집분이 사라지므로** 에셋이 지워졌을 때만 쓴다.
