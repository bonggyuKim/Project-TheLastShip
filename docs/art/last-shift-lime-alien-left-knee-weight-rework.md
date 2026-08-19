# LAST SHIFT 라임 외계인 왼 무릎 웨이트 재작업

## 변경

- 대상: `LastShift_LimeAlien_RigifyMesh`
- 본 경계: `DEF-thigh.L.001` ↔ `DEF-shin.L`
- 관절 중심 전후 50 mm 구간의 기존 웨이트 합을 유지하면서 C2 연속 곡선으로 재분배했다.
- 엉덩이·발목 및 다른 본의 웨이트는 수정하지 않았다.
- Blender 정본과 Unity 교환용 `LastShiftLimeAlien_Rigify_Test.fbx`에 동일한 전이를 적용했다.
- 목 링 3본 전이 수정과 같은 정본/FBX에 통합했으며, Unity asset GUID는 유지했다.

## 검증

- 대상 정점: 192개, 변경 정점: 191개
- 최대 단일 웨이트 변화: `0.1354687`
- `DEF-thigh.L.001 + DEF-shin.L` 합산 최대 오차: `1.1102230246251565e-16`
- 전이 역전: 0개
- 기존 최대 영향 수 유지: Blender 11개, FBX 10개
- 재실행 최대 오차: `7.75e-7` 이하
- IK 스트레스 포즈 `(foot_ik.L = 0, 0.12, 0.08)`에서 무릎 안쪽 접힘선의 연속성을 확인했다.

검토 렌더: `docs/art/last-shift-lime-alien-left-knee-weight-stress.png`

기존 Rigify 테스트 자산은 정점당 최대 영향 수가 이미 4개를 넘는다. 이번 변경은 해당 상한을 늘리지 않고 무릎 경계 두 그룹만 다룬다. Unity 6000.4.0f1 배치 임포트는 성공했으며, 플랫폼별 4-weight 제한의 런타임 실루엣은 테크니컬 아트 단계에서 별도로 확인한다.
