# LAST SHIFT 라임 외계인 정본 Unity 재정렬

## 결과

`LastShiftLimeAlien_UnityExport_LeftToeFixed.blend`의 5,711정점 몸 메시와
232본 리그를 유일한 정본으로 삼아 아래 두 Unity 경로를 다시 수출했다.

- `LastShiftLimeAlien_RigifyDeform.fbx` — 네트워크 플레이어와 기본 래그돌
- `LastShiftLimeAlien_RigifySoft.fbx` — 래그돌 랩의 소프트 비교 경로

두 경로는 소비 프리팹과 GUID만 다르다. Blender 재임포트 기준 정점 5,711,
면 9,942, 본 232, 메시·웨이트 digest가 동일하다. 기존 `RigifyDeform`은
5,772정점/56본인 2026-08-18 구계보였고 이번 재수출로 정본에 맞췄다.

## 목 전이

`DEF-spine.006`과 그 자식 soft 본의 이동 영향도가 인접 정점에서 약
`0.14 → 1.00`으로 끊기던 것이 찢어짐 원인이었다. 기존
`fix_lime_alien_neck_ring_weights.py`의 3본 Bernstein 재배분은 정본에서
악화되므로 사용하지 않았다.

새 도구 `Tools/art/rework_lime_alien_canonical.py`는 영향도 차가 0.45 이상인
경계를 찾고 네 링만 확장한 뒤, 20회 이웃 완화로 이동 영향도를 연속화한다.
299정점의 `DEF-spine.003/.004/.005/.006` 및 `DEF-head.soft.*`만 재배분하며
정점별 deform 총합과 비대상 웨이트를 보존한다.

| 직접 회전 | 찢어진 삼각형 전 → 후 | 최악 신장 전 → 후 |
| --- | ---: | ---: |
| X +20° | 32 → 0 | 8.13 → 1.79 |
| X -20° | 40 → 0 | 8.33 → 1.88 |
| Y ±20° | 36/37 → 0/0 | 7.02/7.29 → 1.58/1.57 |
| Z ±20° | 26/22 → 0/0 | 4.54/5.11 → 1.68/1.63 |
| ±3축 46° | 41~53 → 모두 0 | 9.97~18.51 → 2.35~2.98 |

90° 극단 자세도 여섯 방향 모두 찢김 수와 최악 신장이 감소했다. 실제 관절
상한은 `LastShiftRagdollSkinLimits`의 목 20°/10°/-10°..5°라 20° 결과가
런타임 판정 경계다.

## 입과 셰이딩

정본의 입 부위는 구계보 수정 전부터 고립 삼각형이 없다. 연결 성분은 몸
4,221정점과 눈 1,490정점뿐이며 입 창의 고립면 검사는 0개다. 몸 9,942면은
전부 스무스 셰이딩이다. 따라서 메시 삭제를 재적용하지 않고 상태를 검증·보존했다.

## 증거

- `docs/art/evidence/last-shift-lime-alien-canonical-rework/report.json`
- `neck-before-46deg.png` / `neck-after-46deg.png`
- `mouth-smooth-before.png` / `mouth-smooth-after.png`

FBX는 `FBX_SCALE_ALL`, `-Z/Y`, leaf bone 없음, 애니메이션 없음으로 수출한다.

Unity 6000.4.0f1에서 강제 임포트한 뒤 아래 EditMode 회귀 `53/53`이 통과했다.

- `LastShiftRagdollSkinFollowTests`, `LastShiftRagdollTests`,
  `LastShiftBodyDeformTests`: 40건
- `LastShiftGhostTests`, `LastShiftMapCanonTests`: 13건

최종 육안 판정은 전후 목 근접 렌더에서 턱 아래 돌출이 사라졌는지, 입 근접
렌더에서 라임색 고립면 없이 내부가 어둡게 유지되는지 확인한다.
