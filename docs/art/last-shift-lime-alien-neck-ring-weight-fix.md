# LAST SHIFT 라임 외계인 목 링 스킨 웨이트 재작업

## 결과

`LastShiftLimeAlien_Rigify_Test.blend`의 목 둘레에서 두 구간으로 갈라져 있던
`DEF-spine.004 -> .005 -> .006` hand-off를 하나의 연속 3본 곡선으로
재배분했다. 수정본은 다음 파일이다.

- `ArtSource/Characters/LastShiftLimeAlien/LastShiftLimeAlien_Rigify_Test_NeckRingFixed.blend`

가중치 증거 이미지에서 빨강/초록/파랑은 각각 `.004`/`.005`/`.006`을
뜻한다. 수정 전 측면의 좁고 톱니 모양이던 초록 전이대가 수정 후 넓고
연속적인 청록/황록 전이대로 바뀐다.

## 배분 원리

직전 실측의 head 굽힘 응답값 `.004 = 0`, `.005 = 0.5`, `.006 = 1`을
사용한다. 기존 가중치가 만드는 유효 굽힘 비율을 `t`라 두고 각 정점의
목 계열 총가중치를 보존한 채 다음 Bernstein basis로 다시 나눴다.

```text
DEF-spine.004 = (1 - t)^2
DEF-spine.005 = 2t(1 - t)
DEF-spine.006 = t^2
```

이 식은 `0*w004 + 0.5*w005 + 1*w006 = t`를 만족하므로 기존 굽힘 위치는
유지하면서, `.005`에서 기울기가 꺾이던 2단 hand-off만 연속 곡선으로
바꾼다.

## 검증

- 변경 정점: 360개
- 검증 엣지: 861개
- 메시 정점 위치: 완전 동일
- 대상 외 vertex-group 가중치: SHA-256 digest 동일
- 정점별 세 대상 본 총가중치 최대 오차: `3.17e-8`
- 유효 굽힘 응답 최대 오차: `1.11e-16`

head 컨트롤 60° 스트레스 결과:

| 축 | 최대 신장 전 -> 후 | 95% 절대 변형률 전 -> 후 | 평균 절대 변형률 전 -> 후 |
| --- | ---: | ---: | ---: |
| X | 4.6232 -> 3.3698 | 2.8027 -> 1.7076 | 0.7939 -> 0.5939 |
| Y | 2.2194 -> 2.2095 | 0.9622 -> 0.9471 | 0.3477 -> 0.3376 |
| Z | 5.1025 -> 3.7371 | 2.6999 -> 1.7121 | 0.7213 -> 0.5899 |

세 축 모두 최대 신장과 평균 절대 변형률이 감소했다. 수치 원본과 전후
Workbench 렌더는 `docs/art/evidence/last-shift-neck-ring-weight-fix/`에 있다.

## 재현

```powershell
& 'D:\blender\blender.exe' -b `
  'ArtSource\Characters\LastShiftLimeAlien\LastShiftLimeAlien_Rigify_Test.blend' `
  --python 'Tools\art\fix_lime_alien_neck_ring_weights.py' -- `
  --mode apply `
  --output 'ArtSource\Characters\LastShiftLimeAlien\LastShiftLimeAlien_Rigify_Test_NeckRingFixed.blend' `
  --report 'docs\art\evidence\last-shift-neck-ring-weight-fix\report.json' `
  --evidence-dir 'docs\art\evidence\last-shift-neck-ring-weight-fix'
```

Blender에서 최종 애니메이션 클립을 재생해 어깨가 프레임에 들어오는 실제
카메라 거리에서도 목 뒤 실루엣이 튀지 않는지 확인해야 한다. Unity FBX
재출력·임포트와 런타임 스키닝 검증은 테크니컬 아트 경계다.
