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

## 4.1 뒤이어 잡은 것 — 간헐적 미표시 (카드 `988de472`, 2026-08-22)

사용자가 같은 랩에서 Play 테스트를 이어 하다 "캐릭터가 한 번씩 화면에서 안 보인다" 고 보고했다.
**렌더러도 머티리얼도 레이어도 멀쩡했다. 컬링이었다.**

실행 중인 에디터에 붙어 안 보이는 그 순간을 잡은 값:

| | 값 |
|---|---|
| `SkinnedMeshRenderer.isVisible` | **False** (`enabled=True`) |
| `updateWhenOffscreen` | **False** |
| 월드 바운드 중심 | `(0.01, 12.30, -0.11)` |
| 골반(물리 바디) | `(0.12, 0.26, -0.07)` |
| 바운드↔몸 거리 | **12.04 m** |
| `boundsInFrustum` | False |

`updateWhenOffscreen` 이 꺼져 있으면 스킨드 메시의 바운드는 임포트된 바인드 포즈 바운드를
**루트 본에 얹어** 계산하고 뼈가 실제로 간 자리를 안 본다. 래그돌은 루트를 안 움직이고 뼈만
물리로 옮기므로 둘이 갈라진다 — 보고 시점 랩 씬의 루트는 `y=11.57`, 몸은 바닥이었다
(높은 데서 떨어뜨려 보던 중). 카메라가 몸을 보고 있어도 바운드가 12m 위에 있으니 컬링된다.

**고친 것:** 랩 프리팹의 `LastShift_LimeAlien_Body` 에 `updateWhenOffscreen = 1` 오버라이드.
켜자마자 같은 인스턴스에서 바운드 중심이 `(0.01, 12.30, -0.11)` → `(-0.25, 0.33, -0.57)` 로
내려오고 `isVisible` 이 `True` 가 됐다.

**임포터가 아니라 프리팹에 건 이유:** 모델 임포터에 걸면 이 모델을 쓰는 모든 인스턴스에
매 프레임 바운드 재계산이 붙는다. 문제를 겪는 것은 뼈를 루트에서 멀리 옮기는 래그돌뿐이라
그 프리팹으로 좁힌다.

검사 `SkinnedMeshBoundsFollowTheBonesNotTheRoot` 가 8m 위에서 떨어뜨려 루트와 몸을 일부러
갈라 놓고(실측 `fromRoot=7.97m`) 바운드가 몸 쪽에 붙는지 본다(`fromBody=0.65m`).
플래그를 되돌리면 실패한다 — 확인했다.

> **남은 위험:** 같은 함정은 래그돌로 구동되는 다른 스킨드 캐릭터에도 그대로 있다.
> 게임 씬 승무원이 래그돌로 전환되는 시점에 같은 증상이 날 수 있으니, 그때는 이 문서를 먼저 본다.

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
