# LAST SHIFT 왼 사타구니·윗입술 형상 리워크 — 강도 상향

## 적용 결과

웨이트 경계 진단에서 확인한 REST 형상 문제를 정본
`LastShiftLimeAlien_UnityExport_LeftToeFixed.blend`에 직접 수정했다.

- 왼다리는 12회 `0.8/-0.82`, 윗입술은 6회 `0.38/-0.40`으로 분리했다. 왼
  사타구니–허벅지 전이의 3링, 40정점만 움직이며 바깥 실루엣과 패치 경계는
  고정했다. 최대 이동은 `0.01028`, 국소 라플라시안 거칠기는 60.46%,
  삼각형 최장/최단 변 평균은 `1.895 → 1.528`로 감소했다.
- 열린 입구 8정점을 고정하고 윗입술 4링 중 21정점의 방사 간격과 깊이를
  균등화했다. 최대 이동은 `0.00500`, 입구 경계 이동은 `0`이다. 거칠기는
  34.14%, 삼각형 최장/최단 변 평균은 `4.238 → 3.053`으로 감소했다.
- Basis와 동일한 변위를 나머지 91개 셰이프키에 적용해 상대 변위 최대 오차는
  `7.46e-9` 이하다. 토폴로지와 모든 스킨 웨이트의 digest도 바뀌지 않았다.

같은 왼다리 FK 스트레스 자세(`-34° / 68°`)에서 국소 거칠기는
`0.006519 → 0.003133`, 최대 에지 신장은 `1.542 → 1.453`으로 개선됐다.
포즈에서만 남는 추가 접힘이 악화되지 않아 `DEF-thigh.L`/골반 웨이트는
재분배하지 않았다.

## 산출물과 증거

- 재현 도구: `Tools/art/rework_last_shift_hip_mouth_geometry.py`
- 수치 보고서: `docs/art/evidence/last-shift-hip-mouth-geometry-rework/report.json`
- 방해 오브젝트 없는 동일 카메라 비교:
  `hip-rest-before/after.png`, `hip-fk-before/after.png`,
  `mouth-before/after.png`
- 전신 섀도우 증거에서는 리그를 따르지 않던 눈 복사본을 제외하고, 실제 곡면을
  주름처럼 과장하던 Workbench cavity를 껐다. 게임용 눈과 FBX는 그대로다.
- 재생성 제작본:
  `ArtSource/Characters/LastShiftLimeAlien/LastShiftLimeAlien_UnityExport_Jelly.blend`
- 재수출 런타임 자산:
  `LastShiftLimeAlien_RigifyDeform.fbx`, `LastShiftLimeAlien_RigifySoft.fbx`

최신 제작본 회귀에서 본체는 24,034정점/47,992삼각형, 92개 셰이프키, 232본을
유지했다. 두 FBX 모두 미가중 정점·flat 면·내부 비매니폴드·느슨한 에지가
0이며 재임포트 검증을 통과했다.

## 재현

```powershell
& 'D:\blender\blender.exe' --background `
  'ArtSource\Characters\LastShiftLimeAlien\LastShiftLimeAlien_UnityExport_LeftToeFixed.blend' `
  --python 'Tools\art\rework_last_shift_hip_mouth_geometry.py' -- `
  --output 'ArtSource\Characters\LastShiftLimeAlien\LastShiftLimeAlien_UnityExport_LeftToeFixed.blend' `
  --evidence-dir 'docs\art\evidence\last-shift-hip-mouth-geometry-rework' `
  --report 'docs\art\evidence\last-shift-hip-mouth-geometry-rework\report.json'
```

도구는 정본에 `ADK_HipMouthGeometryRework_v3_leg_strengthened` 마커를 기록한다.
이전 v1/v2 또는 현재 v3 마커가 있으면 누적 완화하지 않고 원본 정본에서 다시
생성하도록 중단한다. 이후 제작본과 FBX는
`Tools/art/apply_lime_alien_jelly.py`로 정본에서 다시 생성한다.
