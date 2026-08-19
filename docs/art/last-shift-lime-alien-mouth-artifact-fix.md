# LAST SHIFT 라임 외계인 입 내부 고립 삼각형 수정

## 원인

`LastShift_LimeAlien_RigifyMesh`의 입 하단 로컬 좌표
`(-0.014, -0.211, 0.577)` 부근에 외피와 연결되지 않은 삼각형 한 장이
남아 있었다. 이 면은 라임 외피 머티리얼을 사용하므로 Unity의 밝은 환경광에서
입 안쪽으로 노출될 때 발광하는 틈처럼 보였다.

직전 목/왼 무릎 수정 전 커밋 `b3f7ed4`와 수정 후 커밋 `cba371c`를 비교했다.
고립 삼각형과 입 경계 13정점은 양쪽 모두 존재했고, 해당 경계 정점의 웨이트도
동일했다. 따라서 목 링 웨이트 수정의 회귀가 아니라 기존 메시 잔여물이다.

## 변경

- Blender 정본:
  `ArtSource/Characters/LastShiftLimeAlien/LastShiftLimeAlien_Rigify_Test.blend`
- Unity 교환 파일:
  `Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_Rigify_Test.fbx`
- 고립 삼각형 `1면 / 3엣지 / 3정점`만 제거했다.
- 연결된 외피, 입 실루엣, 머티리얼 슬롯, 리그 및 나머지 정점 웨이트는 변경하지
  않았다.

메시 수치는 두 파일 모두 `5,772 / 15,772 / 10,005`에서
`5,769 / 15,769 / 10,004`(정점/엣지/면)로 바뀌었다.

## 증빙 및 재검증

- 수정 전 근접 렌더:
  `docs/art/evidence/last-shift-lime-alien-mouth-fix/mouth-before.png`
- 수정 후 근접 렌더:
  `docs/art/evidence/last-shift-lime-alien-mouth-fix/mouth-after.png`
- 수정 도구:
  `Tools/art/fix_lime_alien_mouth_artifact.py`
- 동일 구도 렌더:
  `Tools/art/render_lime_alien_mouth_closeup.py`

검증은 Blender 정본과 재임포트한 FBX 양쪽에서 `--validate-only`로 실행하며,
대상 위치에 고립 삼각형이 다시 생기면 실패한다.

`origin/main`의 FBX와 수정 FBX를 의미 단위로 비교한 결과, 대상 삼각형을 제외한
정점 웨이트·좌표 digest, 폴리곤 digest, 머티리얼 슬롯, 버텍스 그룹 및 222본의
이름/부모 계층이 동일했다. FBX 재내보내기에서 생긴 본 위치 최대 차이는
`0.00001m`였다. 30° head 굽힘 배분 검사와 왼 무릎 전이 검사도 통과했다.
