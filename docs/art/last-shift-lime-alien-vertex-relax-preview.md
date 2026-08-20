# LAST SHIFT 라임 외계인 실제 정점 완화 프리뷰

## 방향

기존 젤리 제작 경로는 정본의 제어 정점 좌표를 바꾸지 않는 안전장치가 있어,
스무딩 결과가 Catmull-Clark 추가 정점과 노멀 처리에 의한 것인지 원본 밀도의
실제 정점 완화로도 얻을 수 있는지 분리해 볼 수 없었다.

이번 프리뷰는 정본과 소비 FBX를 그대로 둔 채 복제본에서만 안전장치를 완화한다.
92개 셰이프키가 없는 일회성 basis 복제본에 Taubin 완화를 적용하고, 열린 입 경계는
고정한다. 따라서 두 비교 대상 모두 5,711정점·11,382삼각형이며 차이는 실제 정점
위치뿐이다.

## 산출물

- 프리뷰 blend:
  `ArtSource/Characters/LastShiftLimeAlien/LastShiftLimeAlien_VertexRelaxPreview.blend`
- 생성 도구: `Tools/art/render_lime_alien_vertex_relax_preview.py`
- 수치 보고서와 정면·사선·와이어 비교:
  `docs/art/evidence/last-shift-lime-alien-vertex-relax-preview/`

보고서의 `moved_vertices`, `max_displacement_ratio`,
`laplacian_roughness_reduction`이 각각 실제 정점 이동, 실루엣 변형 상한,
국소 요철 감소를 증명한다.

강화한 기본 12회 프리뷰에서 입 경계 36정점은 고정된 채 나머지 5,675정점이
이동했고, 최대 이동은 캐릭터 대각선의 2.04%였다. 생성기는 최대 이동이
2.0~2.5% 범위에 들어오는지도 검사한다. 동일 토폴로지에서 Laplacian 요철
평균은 46.47% 감소했다.

## 트레이드오프와 판정

이 방식은 추가 삼각형 비용 없이 표면 요철을 줄이지만, 제작 FBX에 적용하면 92개
셰이프키와 스킨 극단 자세를 함께 보정해야 한다. 이번 산출물은 채택 전 육안 프리뷰로
한정하며 현재 L1 젤리 FBX를 교체하지 않는다.

정면에서는 머리·배 외곽과 입 구멍 판독성, 사선에서는 목·어깨·손가락 실루엣,
와이어에서는 완화가 단순 노멀 착시가 아닌지 확인한다. 채택 시에는 Idle/Walk,
목 20°, 무릎 및 래그돌 극단 자세를 별도로 확인해야 한다.

현재 정면·24° 사선 프리뷰에서는 머리와 배의 큰 실루엣, 눈 테두리, 열린 입,
손가락과 발가락 판독성이 유지된다. 0.45% 프리뷰보다 눈 주변·목·전완·허벅지의
작은 면 꺾임이 뚜렷하게 줄었다. 대신 입술과 손발 끝의 모서리도 더 둥글어졌고
다리 안쪽의 기존 삼각형 흐름은 남아 있어, 실제 제작 적용 전 스킨 자세 검증은
생략할 수 없다.

재생성 명령:

```powershell
& 'D:\blender\blender.exe' --background `
  'ArtSource\Characters\LastShiftLimeAlien\LastShiftLimeAlien_UnityExport_LeftToeFixed.blend' `
  --python 'Tools\art\render_lime_alien_vertex_relax_preview.py' -- `
  --output-dir 'docs\art\evidence\last-shift-lime-alien-vertex-relax-preview' `
  --blend-output 'ArtSource\Characters\LastShiftLimeAlien\LastShiftLimeAlien_VertexRelaxPreview.blend'
```
