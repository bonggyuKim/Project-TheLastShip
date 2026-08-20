# ArtSource

DCC 원본 파일(`.blend` 등)을 두는 곳이다. `Assets/` 밖이므로 Unity 가 임포트하지 않는다.

`.blend` 를 `Assets/` 안에 두면 Unity 가 FBXImporter 로 열려고 하면서 Blender 실행 파일을 요구한다.
Blender 가 없는 머신(대부분의 개발자 PC, CI)에서는 클론할 때마다 콘솔에
`Blender could not be found.` 에러가 남고, 있어도 매 임포트마다 Blender 를 띄워 시간을 쓴다.
원본은 여기에 두고, Unity 에는 `Assets/DoodleUp/Art/` 로 내보낸 `.fbx` 만 커밋한다.

## 규칙

- 원본 `.blend` 는 `ArtSource/<카테고리>/<자산이름>/` 에 둔다.
- 내보낸 `.fbx` 는 `Assets/` 아래 대응 폴더에 두고 `.meta` 를 함께 커밋한다.
  Unity 를 거치지 않고 커밋하면 `.meta` 가 없어 클론마다 GUID 가 새로 생기고 참조가 끊긴다.
- 렌더 검증 이미지는 자산이 아니므로 `Assets/` 에 넣지 않는다.

## 현재 내용

| 원본 | 대응 Unity 자산 |
| --- | --- |
| `Characters/LastShiftLimeAlien/LastShiftLimeAlien_Rigged.blend` | `Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_Rigged.fbx` |
| `Characters/LastShiftLimeAlien/LastShiftLimeAlien_Rigify_Test.blend` | `Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_Rigify_Test.fbx` 및 애니메이션 FBX |
| `Characters/LastShiftLimeAlien/LastShiftLimeAlien_SubdivisionPreview.blend` | 프리뷰 전용 — Unity 수출 대상 아님 |
| `Characters/LastShiftLimeAlien/LastShiftLimeAlien_VertexRelaxPreview.blend` | 실제 제어 정점 완화 비교 프리뷰 — Unity 수출 대상 아님 |
| `Characters/LastShiftLimeAlien/LastShiftLimeAlien_UnityExport_Jelly.blend` | 정점 완화+Catmull-Clark L1 통합 제작본 — Unity FBX 수출 원본 |
| `LastShift/SystemHeroes/*/*.blend` | `Assets/DoodleUp/Art/Props/LastShiftSystemHeroes/*.fbx` |
