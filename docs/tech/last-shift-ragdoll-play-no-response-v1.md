# Play 에서 래그돌이 반응 안 하고 콜라이더가 안 보이던 것 (v1)

> 상태: `game-tech-director` · `2026-08-22` · 카드 `3a3953a7` (`c0672ad5` 후속)
> 증상: (1) `LAST_SHIFT_RAGDOLL_LAB` 을 Play 하고 승무원을 넘어뜨려도 아무 반응이 없다.
> (2) 래그돌 파츠의 콜라이더 설정이 안 보인다.
>
> 같은 계열 앞선 문서: [축 정렬](last-shift-ragdoll-joint-frame-collapse-v1.md) ·
> [스킨 늘어남](last-shift-ragdoll-fall-mesh-collapse-v1.md) ·
> [한계 재도출](last-shift-ragdoll-limit-frame-mismatch-v1.md)

---

## 0. 결론 먼저

1. **콜라이더는 안 지워졌다.** 실행 중인 에디터의 씬을 그대로 조회해 리지드바디 15 · 콜라이더 15
   (전부 `enabled`) · 조인트 14 를 확인했다. 안 보인 이유는 콜라이더가 `DEF-` 뼈가 아니라
   그 밑의 `*_Col` 자식(`DEF-spine_Col`, `DEF-thigh.L_Col` …)에 달려 있어서다 —
   **뼈를 클릭하면 인스펙터가 비어 보인다.** 카드가 의심한
   `LastShiftRagdollBodySetup` 은 `Rigidbody` 의 솔버·감쇠 값만 만지고 `Collider` 를 일절 안 건드린다.
2. **물리도 안 죽어 있었다. 밀 수단이 없었다.** 랩 씬의 MonoBehaviour 수는 **0** 이었다.
   조작을 담당하는 `LastShiftRagdollLab`(Space/H/B/R)은 `[RequireComponent(typeof(LastShiftRagdoll))]`
   이라 이 프리팹에 못 붙는다 — `LastShiftRagdoll.Build()` 는 프록시 바디·조인트·콜라이더를
   **제 손으로 다시 만들어서**, 얹는 순간 손으로 잡아 둔 콜라이더 모양과 `c0672ad5` 에서
   재도출한 관절 한계가 통째로 날아간다.
3. 씬 뷰에서 끄는 것도 안 통한다. 프로젝트가 `autoSyncTransforms = 0` 이라 그 트랜스폼 대입이
   PhysX 로 안 넘어가고 다음 스텝에 제 포즈로 덮인다.
4. **슬립 임계는 원인이 아니었다** — 가설은 세웠고 실측으로 죽였다(§2).

## 1. 무엇을 넣었나

`LastShiftRagdollSoftLab` (신규, 프리팹 루트에 얹음). **리지드바디·조인트·콜라이더는 하나도 안 건드린다.**

| 조작 | 하는 일 |
|---|---|
| 좌클릭 드래그 | 레이캐스트로 맞은 부위를 스프링으로 잡아끈다 |
| `Space` | 온몸에 `2 m/s` 속도 변화 + 가슴에 임펄스 |
| `H` | 머리에만 임펄스 |
| `B` | 폭심에서 온몸을 민다 |
| `R` | 정지 포즈 복귀(`Physics.SyncTransforms` 포함) |

- **밀기가 두 겹인 이유.** 가슴만 때리면 이미 누워 있는 승무원은 상체만 움찔한다 —
  실측으로 골반이 `0.075m` 밖에 안 움직였다. 전신 속도 변화를 같이 주면 `0.659m` 다.
  세기는 새로 만든 값이 아니라 `LastShiftRagdollCollapseProbe` 의 `shove` 시나리오와 같은 `2 m/s` 다.
- **카메라를 따라가게 했다.** 고정 카메라로는 밀린 몸을 못 본다 — 증거 촬영 1차에서 바디체크
  한 번에 승무원이 뷰포트 `-5.76` 으로 나갔다. 프레이밍은 `LastShiftRagdollLab.FrameSubject`
  를 그대로 쓴다(두 랩이 다른 구도로 찍히면 비교가 안 된다).
- **선택하면 콜라이더 열다섯 개를 다 그린다**(`OnDrawGizmosSelected`). 증상 (2) 를 다시 겪지 않게
  루트만 선택해도 배치가 보이게 둔다.

## 2. 기각된 원인 — 다시 훑지 말 것

### 2.1 슬립 임계가 래그돌을 얼린 것은 아니다

`LastShiftRagdollBodySetup` 이 `LastShiftRagdollTuning.SleepThreshold`(`0.05`, Unity 기본의 열 배)를
얹고 있었다. 그 값은 `LastShiftRagdoll` 이 **중력을 매 물리 스텝 손으로 넣어** 바디가 계속
깨어 있는 경로용이고(해당 필드 주석), 엔진 중력을 받는 이 프리팹에는 전제가 안 맞는다.
"정착하자마자 잠들어 안 깨어난다" 를 의심할 만했다. **실측은 그것을 기각했다.**

| 슬립 임계 | 4초 정착 뒤 잠든 바디 | 같은 바디체크에 골반 이동 |
|---|---|---|
| `0.05` (튜닝값) | **0 / 15** | 0.024 m |
| `0.005` (엔진 기본) | **0 / 15** | 0.075 m |

잠든 바디는 두 경우 모두 없었다. 다만 정착 경로가 달라져 **반응 크기가 3분의 1** 이 되므로,
전제가 안 맞는 값을 그대로 둘 이유도 없다 — 엔진 기본값으로 되돌렸다.
`WakeAll()` 은 그대로 남긴다(오래 놔두면 실제로 잠들고, 깨우는 비용은 없다).

### 2.2 콜라이더 소실

§0.1 그대로다. 프리팹 YAML 과 실행 중 씬 양쪽에서 15개를 셌고,
`BodySetup` 코드 경로에 `Collider` 참조가 없다. **다시 뒤질 필요 없다.**

## 3. 검사

`LastShiftRagdollCollapsePlayModeTests` 에 둘을 더했다.

- `SettledCrewStillRespondsToAShove` — 4초 정착시킨 뒤 밀어 **골반이 0.25m 넘게** 움직이는지.
  잠든 바디가 하나라도 있으면 따로 실패시키고, 슬립 임계도 못 박는다.
  가드가 무는지 확인했다: 밀기를 가슴 임펄스만으로 되돌리면 `0.024m` 로 **실패한다**.
- `EveryRagdollBodyKeepsACollider` — 바디 15개 각각에 `attachedRigidbody` 가 자기인 콜라이더가
  붙어 있는지. 자식에 있든 같은 오브젝트에 있든 물리적으로 맞는 것만 통과한다.

실행 결과(2026-08-22, 열린 에디터에 붙여 실행):

```
PlayMode  3/3   pelvisTravel=0.659m  sleepingBeforeShove=0/15  bodies=15 colliders=15
EditMode  4/4   (LastShiftRagdollCollapseTests)
```

붕괴 가드는 그대로다 — `minTorsoSpan=0.983`, 자유도별 최악 초과 `20.5도`(고치기 전 `56.9`).

## 4. 남은 것 — 이 카드 밖

- **렌더에서 다리가 실루엣으로 안 잡힌다.** 증거 사진(§5)에서 머리·팔·손은 또렷한데 다리는
  몸통에 묻힌다. 수치로는 정상이다(정착 시 골반→발이 정지 대비 `0.752`, 몸통 `0.983`) —
  즉 **물리가 접은 것이 아니라 스킨/프로포션 문제**다. 웨이트 건은
  [스킨 늘어남 문서](last-shift-ragdoll-fall-mesh-collapse-v1.md) §5 에서 이미 아트 소관으로 남아 있다.
  이번 카드의 두 증상(무반응·콜라이더)과는 별개 건이라 여기서 손대지 않았다.
- `shove` 가 무릎 꿇은 자세로 굳는 것, 스킨 여유표가 옛 축 기준인 것은
  [한계 재도출 문서](last-shift-ragdoll-limit-frame-mismatch-v1.md) §5 그대로 남아 있다.

## 5. 증거

`docs/tech/evidence/last-shift-ragdoll-play-response-20260822/`

| 파일 | 무엇 |
|---|---|
| `00_play_start.png` | Play 직후 |
| `01_settled.png` | 정착 |
| `02_shove_a.png` · `03_shove_b.png` | 바디체크 직후(카메라가 따라간다) |
| `04_shove_settled.png` | 밀린 뒤 정착 |
| `05_blast.png` | 블라스트 |
| `06_reset.png` | `R` 복귀 뒤 |
