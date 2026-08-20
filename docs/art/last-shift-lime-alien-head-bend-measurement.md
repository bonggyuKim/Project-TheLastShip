# LAST SHIFT 라임 외계인 head 목 굽힘 배분 실측

## 결론

`LastShiftLimeAlien_Rigify_Test.blend`의 `rig`에서 `head` 컨트롤을 로컬 Y축으로 30° 굽혔을 때 평가된 DEF 본의 rest-relative 회전량은 다음과 같다.

| DEF 본 | 회전량 | head 입력 대비 | 판정 |
| --- | ---: | ---: | --- |
| `DEF-spine.004` | 0.000000° | 0.0000% | head 굽힘 미배분 |
| `DEF-spine.005` | 14.993751° | 49.9792% | 설계값 50% |

따라서 head 컨트롤의 목 굽힘 배분값은 **`.004 : .005 = 0 : 0.5`**다. 수치상 `.005`의 `49.9792%`는 Blender 평가 행렬의 부동소수점 오차이며 회귀 판정은 `50% ±0.1%p`로 둔다.

## 측정 조건과 재현

- Blender: 4.5.3 LTS
- 원본: `ArtSource/Characters/LastShiftLimeAlien/LastShiftLimeAlien_Rigify_Test.blend`
- 컨트롤: `head`, quaternion 로컬 Y축 +30°
- 대상: `DEF-spine.004`, `DEF-spine.005`
- 산출: 중립 상태와 입력 상태의 evaluated pose matrix 회전 차이
- DEF 제약 경로: `.004 -> ORG-spine.004`, `.005 -> ORG-spine.005` (`Copy Transforms`, influence 1.0)

```powershell
& 'D:\blender\blender.exe' -b `
  'ArtSource\Characters\LastShiftLimeAlien\LastShiftLimeAlien_Rigify_Test.blend' `
  --python 'Tools\art\measure_lime_alien_head_bend.py'
```

스크립트는 JSON 한 줄을 출력하고, Y축 배분이 `.004 = 0`, `.005 = 0.5`에서 ±0.001 이상 벗어나면 실패한다. `LastShiftLimeAlien_Rigged.blend`는 단순 제작 리그라 해당 Rigify DEF 본이 없으므로 이 측정 대상이 아니다.

## 시각 확인 메모

이번 작업은 리그 수치 실측이며 원본 `.blend`의 포즈나 형상은 변경하지 않았다. Blender GUI가 열린 환경에서는 head를 로컬 Y축으로 굽혀 `.004`가 고정되고 `.005`에서 상부 목 곡률이 시작되는지 측면 실루엣을 추가 확인한다.
