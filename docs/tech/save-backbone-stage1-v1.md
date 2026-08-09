# 세이브 백본 1단계 구현 노트 (v1)

`docs/tech/save-backbone-feasibility-v1.md` §1.3 이 "첫 조각" 으로 지목한 것 —
**스냅샷 필드 확장 + 주입/권위이관 분리** — 을 냈다. 파일 포맷과 씬 복원은 이 카드 밖이다.

---

## 1. 남은 자리 (다음 카드가 부를 것)

| 하는 일 | 부를 것 |
|---|---|
| B층 상태를 값으로 접기 | `LastShiftSandboxController.CaptureRuntimeSnapshot()` |
| 히스테리시스 위상 접기 | `LastShiftSandboxController.CaptureSituationLatches()` |
| 되살리기 (권위는 이쪽) | `ApplyNetworkSnapshot(snapshot, LastShiftStateAuthority.Local, latches)` |
| 되살리기 (서버가 계속 계산) | `ApplyNetworkSnapshot(snapshot)` — 종전 그대로 |

`CaptureRuntimeSnapshot` 이 **캡처의 유일한 정본**이다. `LastShiftNetworkSandbox.PublishSnapshot`
도 이제 이 함수를 부르므로, 필드를 늘릴 때 한쪽만 늘어나는 자리가 없다.
파일 층은 `NetworkBehaviour` 없이도 캡처할 수 있다.

## 2. 주입과 권위 이관을 갈랐다 (§1.3-가)

종전 주입은 마지막에 `usesReplicatedState = true` 를 세워 파생값을 "받은 값" 으로 고정했다.
복원은 반대여야 한다 — 값만 받고 다음 tick 부터는 호스트가 직접 계산해야 한다.

- `Replicated` (기본, 멀티플레이 클라이언트): 종전과 동일. `UncontainedSystemMask` 는 받은 값.
- `Local` (세이브 복원): 결과가 아니라 **입력**(`DamagedSystemMask` · 수리 장부 전체)을 받고
  마스크는 직접 계산한다. 충격 연출을 재생하지 않고, 판정 시각은 절대 시각 대신 경과로 되돌린다.

## 3. 스냅샷 필드 `36 → 59`

새로 나르는 것: 수리 장부 3계통 전체(채널 잔여·우회 만료·완료 플래그·모드) + 이력 카운터 2,
손상 마스크, 조종 홀드 3, 조향 지연·대기 입력 4, 열 보호 누적, 죽은 구역 2, 도킹 진입 엣지,
적용된 운석 4, 밸브 홀더 슬롯 마스크, 판정 후 경과.

값 타입만 유지했다(`GameObject`·`Transform` 참조 0). 이 성질이 "저장하면서 계속 플레이"
(`S-10`)를 공짜로 만든다 — 캡처가 곧 복사라 이후 플레이가 이미 뜬 스냅샷을 못 건드린다.

### 안 넣은 것과 이유

- **상황 래치 체류 시간** — 소비자가 파일 하나뿐인데 `65`칸을 `0.25`초마다 전원에게 보낼 이유가
  없다. 평평한 `float[]`(음수=비활성)로 빼서 `CaptureSituationLatches` / 주입 인자로 다닌다.
- **런 요약** — 저장하지 않는다. 전부 위 필드의 파생이라 복원 후 다시 접으면 같은 값이 나온다.
  `SacrificeCount` 도 같은 이유로 장부 엔트리에서 다시 센다.
- **아이템·승무원 좌표** — `Transform`/`Rigidbody` 읽기라 값 타입 규약 밖이다. 파일 층이 캡처
  단계 안에서 따로 읽는다(§1.4-라). 주입 훅은 이미 있다
  (`LastShiftGrabbable` 물리 상태 · `LastShiftCrewOxygen.ApplyReplicated`).

## 4. 실측 — §3.2 의 추정 하나를 숫자로 바꿨다

```
[LAST_SHIFT_SAVE_PROBE] stage=b-layer-injection samples=200
                        total=0.425ms per_injection=0.0021ms budget=10.0ms result=PASS
```

**B층 주입 한 번 `0.0021`ms.** 문서가 "마이크로초 단위" 로 추정한 값이 맞았고, 이어하기
`10`초 예산의 `0.00002`% 다. 예산을 위협하는 것은 여전히 배치물 재조립 하나다.

(가) 배치물 재조립과 (다) 물리 정지 배치는 **아직 못 잰다** — 씬 복원 경로 자체가 없다(§3.1).
그 둘은 복원 경로를 내는 카드가 같은 로그 태그로 잰다.

## 5. 검증

- EditMode `568/568`, PlayMode `33/33` (Unity `6000.4.0f1`, cold `unity test`).
- 왕복 합격선(§2.2 "저장→로드 후 B층 전 필드 동일")은
  `RestoringWithLocalAuthorityReproducesEveryCapturedField` 가 전 필드 동등성으로 지킨다.
  `Vector3` 비교는 `==`(1e-5 근사) 대신 `Equals`(성분별 정확)를 쓴다.
- `EveryNewSnapshotFieldParticipatesInEquality` 가 새 필드마다 `Equals` 누락을 막는다 —
  빠지면 `NetworkVariable` 이 변경을 못 보고 값이 영영 전송되지 않는다.
