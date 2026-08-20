# LAST SHIFT 왼 허벅지·입 위쪽 웨이트 경계 진단

## 결론

왼 허벅지–사타구니의 접합 주름과 입 위쪽 찌그러짐은 **웨이트 경계만의 문제가 아니다**.
두 결함 모두 리그를 `REST`로 둔 기존 정본/젤리 렌더에서 이미 보인다. `REST`에서는
본 변환이 스킨에 적용되지 않으므로, 이 상태의 주름과 찌그러짐은 제어 메시의 국소 형상과
삼각 분포가 만든 표면 문제다.

- 왼 허벅지는 `DEF-thigh.L` 계열과 골반 계열 사이에 실제 영향도 전이가 있어 포즈에서
  접힘이 더 강조될 수 있다. 다만 정지 상태의 날카로운 삼각 패치는 웨이트 수정으로
  없어지지 않는다.
- 입 위쪽 후보 영역은 `DEF-head.soft.eye`가 사실상 단일 지배 그룹이다. 같은 결함이
  `REST`에도 남고, 완화 반복 수와 쿼드 병합 각도를 바꾼 비교본에서도 위치가 유지됐다.
  따라서 이 부위에 골반/목 방식의 웨이트 스무딩을 적용하면 원인을 건드리지 못한 채
  표정·소프트 변형 범위만 흐릴 위험이 크다.

정본 측정값은 다음과 같다.

| 영역 | 후보 정점 | 내부 에지 | 최대 영향도 차 | 0.5 이상 단절 | 지배 그룹 |
| --- | ---: | ---: | ---: | ---: | --- |
| 왼 허벅지–사타구니 | 125 | 312 | 0.273 | 0 | `DEF-thigh.L` 계열과 골반 계열 혼합 |
| 입 위쪽 | 499 | 1,039 | 0.369 | 0 | 499개 중 498개가 `DEF-head.soft.eye` |

두 영역 모두 한 에지에서 영향도가 완전히 끊기는 0.5 이상 단절은 없다. 허벅지에는
완만한 실제 전이가 있지만, 입 위쪽 찌그러짐을 설명할 만한 지배 그룹 교체는 한 정점뿐이다.

## 재현과 증거

수치 진단은 다음처럼 정본과 제작본에 각각 실행한다.

```powershell
D:\blender\blender.exe -b ArtSource/Characters/LastShiftLimeAlien/LastShiftLimeAlien_UnityExport_LeftToeFixed.blend `
  -P Tools/art/diagnose_last_shift_hip_mouth_boundaries.py -- `
  --report docs/art/evidence/last-shift-hip-mouth-weight-boundary/canonical-report.json

D:\blender\blender.exe -b ArtSource/Characters/LastShiftLimeAlien/LastShiftLimeAlien_UnityExport_Jelly.blend `
  -P Tools/art/diagnose_last_shift_hip_mouth_boundaries.py -- `
  --report docs/art/evidence/last-shift-hip-mouth-weight-boundary/production-report.json
```

시각 증거는 기존 제작 렌더를 그대로 기준으로 삼는다.

- 정지 비교: `docs/art/evidence/last-shift-lime-alien-jelly-production/lime-alien-jelly-production-front.png`
- FK 스트레스 포즈: `docs/art/evidence/last-shift-lime-alien-jelly-production/lime-alien-jelly-production-joint-pose.png`
- 무완화에 가까운 베이스 비교: `docs/art/evidence/last-shift-lime-alien-subdivision-preview/lime-alien-subdivision-front.png`

## 다음 형상 리워크 범위

후속 수정은 웨이트를 먼저 흐리지 말고, 정본 제어 메시에서 두 국소 영역만 다룬다.

1. 왼 사타구니–허벅지: 패치의 긴 삼각형과 법선 전환을 정리하고, 실루엣을 바꾸지 않는
   범위에서 2~3링을 완화한다.
2. 입 위쪽: 입구 실루엣을 고정한 채 윗입술의 방사형 간격과 깊이만 균등화한다.
3. 그 뒤 동일 FK 포즈로 웨이트 전이가 접힘을 추가하는지 재측정한다. 포즈에서만 남는
   접힘에 한해 `DEF-thigh.L`↔골반의 합산 웨이트를 보존하며 좁게 재분배한다.

트레이드오프는 명확하다. 지금 바로 웨이트를 넓히면 포즈 접힘은 완화될 수 있지만,
정지 상태 패치는 그대로 남고 골반 볼륨이 물러진다. 형상 리워크를 먼저 하면 원인별
수정량을 분리할 수 있다.
