# LAST SHIFT 라임 외계인 젤리 표면 실제 적용

## 적용 결과

정본 `LastShiftLimeAlien_UnityExport_LeftToeFixed.blend`의 위치, 92개 셰이프키,
리그와 웨이트를 유지한 채 본체 토폴로지를 제작용 젤리 표면으로 정리했다.

- 60° 이하 삼각형 쌍만 결합하고 UV, 심, 샤프, 버텍스 컬러, 재질 경계를
  넘지 않아 6,231면 중 5,151면(82.7%)을 쿼드로 전환했다.
- `Jelly_Surface_Subdivision_L1` Catmull-Clark 1단계를 제작용 blend에
  비파괴 모디파이어로 저장했다.
- 모든 제어면과 베이크면에 스무스 셰이딩을 적용했다.
- 런타임 FBX는 L1을 실제 베이크하고 보간된 스킨 웨이트를 유지했다.

| 상태 | 정점 | 삼각형 | 쿼드 비율 |
| --- | ---: | ---: | ---: |
| 기존 본체 | 5,711 | 11,382 | 14.5% |
| 쿼드 제어 메시 | 5,711 | 11,382 | 82.7% |
| 런타임 L1 베이크 | 23,882 | 47,688 | 100% |

베이크 메시 23,882정점은 전부 스킨 웨이트를 가지며 리그 232본과 본체 그룹
45개를 FBX 재임포트에서도 유지한다. 기존 입의 열린 경계 36개는 L1에서
72개로 정상 분할되며 내부 비매니폴드와 느슨한 모서리는 새로 생기지 않았다.

## 산출물

- 제작용 blend:
  `ArtSource/Characters/LastShiftLimeAlien/LastShiftLimeAlien_UnityExport_Jelly.blend`
- 실제 소비 FBX:
  `Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_RigifyDeform.fbx`
  및 `LastShiftLimeAlien_RigifySoft.fbx`
- 재현 도구: `Tools/art/apply_lime_alien_jelly.py`
- 수치 보고서:
  `docs/art/evidence/last-shift-lime-alien-jelly-production/report.json`
- 정면·사선 비교:
  `lime-alien-jelly-production-front.png`,
  `lime-alien-jelly-production-oblique.png`

## 비용과 육안 판정

L1 본체는 기존보다 4.19배 많은 삼각형을 사용한다. L2는 약 20만 tris 이상으로
증가하고 프리뷰에서 목·배 요철까지 드러났으므로 제작 경로에서 제외했다.

비교 렌더에서는 머리·목·배의 계단형 면이 줄고 손가락, 발가락, 눈 테두리와
입 구멍 판독성이 유지된다. Unity의 실제 플레이 카메라와 Idle/Walk, 목 20°,
무릎·래그돌 극단 자세에서 실루엣과 스킨 변형은 최종 수동 확인이 필요하다.

재생성 명령:

```powershell
& 'D:\blender\blender.exe' --background `
  'ArtSource\Characters\LastShiftLimeAlien\LastShiftLimeAlien_UnityExport_LeftToeFixed.blend' `
  --python 'Tools\art\apply_lime_alien_jelly.py' -- `
  --output 'ArtSource\Characters\LastShiftLimeAlien\LastShiftLimeAlien_UnityExport_Jelly.blend' `
  --fbx-output 'Assets\DoodleUp\Art\Characters\LastShiftLimeAlien\LastShiftLimeAlien_RigifyDeform.fbx' `
  --fbx-output 'Assets\DoodleUp\Art\Characters\LastShiftLimeAlien\LastShiftLimeAlien_RigifySoft.fbx' `
  --evidence-dir 'docs\art\evidence\last-shift-lime-alien-jelly-production'
```
