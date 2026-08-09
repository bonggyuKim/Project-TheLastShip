# LAST SHIFT 라임 외계인 리그 v1

## 산출물

- Blender 원본: `Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_Rigged.blend`
- 교환 파일: `Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_Rigged.fbx`
- 중립 포즈 정면: `Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_Rig_NeutralPose.png`
- 중립 포즈 측면: `Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_Rig_NeutralPose_Side.png`
- 익스트림 포즈 증빙: `Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_Rig_StressPose.png`

## 제작 구조

- 현재 씬의 회전·배치가 반영된 결합 메시를 좌우 대칭의 편안한 A포즈로 정렬하고, 이를 새 레스트 포즈로 베이크했다. 중심 계층은 `root > pelvis > spine > chest > head`이다.
- 팔은 `upper_arm > forearm > hand`, 다리는 `thigh > shin > foot` 좌우 변형 체인을 사용한다.
- 양팔과 양다리에 2본 IK, 팔꿈치·무릎 폴 컨트롤을 배치했다. 발은 IK 컨트롤 회전을 복사한다.
- 제작 중간 메시와 트레이스는 뷰포트·렌더 모두 숨김 상태로 보존하고, 런타임 대상은 `LastShift_LimeAlien_Combined`, `Eye_Pupil_Sphere`, `LastShift_LimeAlien_Rig`로 구분했다.
- 결합 과정에서 독립 표면으로 남은 눈 흰자 1,490정점은 `head`에 100% 강체 웨이트를 부여했다. 전 정점은 최대 4개 이하 본 영향을 갖는다.

## 검증

- 리그 및 런타임 메시의 위치/회전은 0, 스케일은 1로 정규화했다.
- 양팔·양다리 IK 타깃과 폴 타깃 연결을 확인했다.
- 5,772개 결합 메시 정점 전체의 웨이트 할당을 확인했다.
- 중립 레스트 포즈를 정면·측면 직교 카메라로 확인했다.
- 별도 익스트림 포즈에서 몸통, 어깨, 팔꿈치, 골반, 무릎, 발목의 실루엣과 표면 연속성을 확인한 뒤 원본은 중립 레스트 포즈로 저장했다.

Unity FBX 임포트 설정, Avatar 매핑, Animator 연결은 테크니컬 아트 단계에서 확인한다.
