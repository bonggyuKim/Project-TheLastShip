# LAST SHIFT 라임 외계인 리그 v1

## 산출물

- Blender 원본: `Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_Rigged.blend`
- 교환 파일: `Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_Rigged.fbx`
- 중립 포즈 정면: `Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_Rig_NeutralPose.png`
- 중립 포즈 측면: `Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_Rig_NeutralPose_Side.png`
- 익스트림 포즈 증빙: `Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_Rig_StressPose.png`
- 부위별 웨이트 증빙: `Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_WeightFinal_{Head,Arm_L,Arm_R,Leg_L,Leg_R}.png`
- 팔 리빌드 중립 증빙: `Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_ArmRebuild_Neutral.png`
- 양팔 오버헤드 증빙: `Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_WeightFinal_Arms_Overhead.png`

## 제작 구조

- 현재 씬의 회전·배치가 반영된 결합 메시를 좌우 대칭의 편안한 A포즈로 정렬하고, 이를 새 레스트 포즈로 베이크했다. 중심 계층은 `root > pelvis > spine > chest > head`이다.
- 팔은 `upper_arm > forearm > hand`, 다리는 `thigh > shin > foot` 좌우 변형 체인을 사용한다.
- 양팔과 양다리에 2본 IK, 팔꿈치·무릎 폴 컨트롤을 배치했다. 발은 IK 컨트롤 회전을 복사한다.
- 제작 중간 메시와 트레이스는 뷰포트·렌더 모두 숨김 상태로 보존하고, 런타임 대상은 `LastShift_LimeAlien_Combined`, `Eye_Pupil_Sphere`, `LastShift_LimeAlien_Rig`로 구분했다.
- 결합 과정에서 독립 표면으로 남은 눈 흰자 1,490정점은 `head`에 100% 강체 웨이트를 부여했다. 전 정점은 최대 4개 이하 본 영향을 갖는다.
- 새 레스트 기준 자동 웨이트를 다시 계산하고 머리/가슴, 상완/전완, 허벅지/정강이 경계를 토폴로지 인접 기반으로 국소 스무딩했다.
- 기존 좌우 팔 토폴로지는 본 중심과 일치하지 않아 제거했다. 좌우 대칭 연속 팔 튜브, 대칭 손, 어깨 캡을 별도 런타임 메시로 재구성하고 팔 본을 실제 중심선에 맞췄다.
- 몸통에 남아 있던 팔 본 영향은 제거했으며, 제거 경계는 `spine/chest` 전용 측면 보강 메시로 덮었다.

## 검증

- 리그 및 런타임 메시의 위치/회전은 0, 스케일은 1로 정규화했다.
- 양팔·양다리 IK 타깃과 폴 타깃 연결을 확인했다.
- 5,772개 결합 메시 정점 전체의 웨이트 할당을 확인했다.
- 머리, 좌우 팔, 좌우 다리를 각각 단독 구동해 비대상 몸통과 반대쪽 팔다리가 고정되는지 확인했다.
- 양손 IK를 머리 위까지 이동해 양팔이 어깨부터 손목까지 연속적으로 펴지고 몸통이 고정되는지 확인했다.
- 중립 레스트 포즈를 정면·측면 직교 카메라로 확인했다.
- 별도 익스트림 포즈에서 몸통, 어깨, 팔꿈치, 골반, 무릎, 발목의 실루엣과 표면 연속성을 확인한 뒤 원본은 중립 레스트 포즈로 저장했다.

Unity FBX 임포트 설정, Avatar 매핑, Animator 연결은 테크니컬 아트 단계에서 확인한다.
