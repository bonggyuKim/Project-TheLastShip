# LAST SHIFT 라임 외계인 젤리 표면 실제 적용

## 적용 결과

정본 `LastShiftLimeAlien_UnityExport_LeftToeFixed.blend`의 위치, 92개 셰이프키,
리그와 웨이트를 유지한 채 본체 토폴로지를 제작용 젤리 표면으로 정리했다.

- 프리뷰에서 판정한 Taubin 완화 12회(`0.8/-0.82`)를 실제 Basis에 적용했다.
  5,675정점이 이동했고 최대 이동은 실루엣 대각선의 2.073%, 라플라시안
  거칠기는 46.47% 감소했다. 열린 입 경계 36정점은 고정했다.
- Basis와 같은 변위를 나머지 91개 셰이프키에 더해 표정 상대 변위를
  보존했다(최대 오차 `1.54e-8`).
- 완화 뒤 60° 이하 삼각형 쌍만 결합하고 UV, 심, 샤프, 버텍스 컬러,
  재질 경계를 넘지 않아 6,400면 중 4,982면(77.8%)을 쿼드로 전환했다.
- `Jelly_Surface_Subdivision_L1` Catmull-Clark 1단계를 제작용 blend에
  비파괴 모디파이어로 저장했다.
- 본체 제어면 6,231개, 런타임 베이크면 23,844개, 눈 표면 전체에 스무스
  셰이딩을 강제하고 flat 면이 0개인지 생성 단계에서 검증한다.
- 런타임 FBX는 L1을 실제 베이크하고 보간된 스킨 웨이트를 유지했다.

| 상태 | 정점 | 삼각형 | 쿼드 비율 |
| --- | ---: | ---: | ---: |
| 기존 본체 | 5,711 | 11,382 | 14.5% |
| 완화+쿼드 제어 메시 | 5,711 | 11,382 | 77.8% |
| 런타임 L1 베이크 | 24,220 | 48,364 | 100% |

베이크 메시 24,220정점은 전부 스킨 웨이트를 가지며 리그 232본과 본체 그룹
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
- 접지 그림자 렌더:
  `lime-alien-jelly-production-shadow.png`
- 팔꿈치·무릎 FK 스트레스 포즈 렌더:
  `lime-alien-jelly-production-joint-pose.png`
- 독립 회귀 검증기와 결과:
  `Tools/art/verify_lime_alien_jelly.py`,
  `docs/art/evidence/last-shift-lime-alien-jelly-production/regression-report.json`

## 비용과 육안 판정

L1 본체는 기존보다 4.25배 많은 삼각형을 사용한다. L2는 약 20만 tris 이상으로
증가하고 프리뷰에서 목·배 요철까지 드러났으므로 제작 경로에서 제외했다.

비교 렌더에서는 머리·목·배의 계단형 면이 줄고 손가락, 발가락, 눈 테두리와
입 구멍 판독성이 유지된다. 접지 그림자 렌더는 24° 사선 전신, 약간 내려다보는
카메라, 실제 수평 리시버를 사용해 발 접점과 왼쪽 투영 그림자를 한 프레임에서
확인한다. 별도 FK 스트레스 포즈는 오른 팔꿈치 76°와 왼 무릎 68° 굽힘에서
표면의 폴리곤 계단과 날카로운 관절 주름이 다시 생기지 않는지 확인한다.
Unity의 실제 플레이 카메라와 Idle/Walk, 목 20°, 래그돌 극단 자세는 최종
수동 확인이 필요하다.

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

회귀 재검증 명령:

```powershell
& 'D:\blender\blender.exe' --background `
  'ArtSource\Characters\LastShiftLimeAlien\LastShiftLimeAlien_UnityExport_Jelly.blend' `
  --python 'Tools\art\verify_lime_alien_jelly.py' -- `
  --production-report 'docs\art\evidence\last-shift-lime-alien-jelly-production\report.json' `
  --fbx 'Assets\DoodleUp\Art\Characters\LastShiftLimeAlien\LastShiftLimeAlien_RigifyDeform.fbx' `
  --fbx 'Assets\DoodleUp\Art\Characters\LastShiftLimeAlien\LastShiftLimeAlien_RigifySoft.fbx' `
  --output 'docs\art\evidence\last-shift-lime-alien-jelly-production\regression-report.json'
```
