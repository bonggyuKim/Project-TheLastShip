# LAST SHIFT 승무원 모델 — 정본 계보

작성 2026-08-20. 계보가 넷으로 갈라져 아트 작업이 반복해서 헛돌았기 때문에 한 장으로 못박는다.

## 정본은 하나다

| 역할 | 경로 |
| --- | --- |
| **작업용 blend (정본)** | `ArtSource/Characters/LastShiftLimeAlien/LastShiftLimeAlien_UnityExport_LeftToeFixed.blend` |
| **Unity 네트워크 플레이어 FBX** | `Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_RigifyDeform.fbx` |
| **Unity 래그돌 랩 FBX** | `Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_RigifySoft.fbx` |

두 FBX는 모두 위 blend에서 뽑는다. `RigifyDeform`은 실제 네트워크 플레이어와
`LastShiftCrewRagdoll.prefab`, `RigifySoft`는 래그돌 랩과
`LastShiftCrewRagdollSoft.prefab`이 기존 GUID로 참조하므로 경로만 둘로 유지한다.
2026-08-20 재수출 뒤 둘의 임포트 메시·본·웨이트 digest는 동일하다. 서로 다른
모델 계보로 다시 갈라 작업하지 않는다.

**다른 파일에 작업하지 말 것.** 아래 셋은 과거 계보이고 Unity 는 안 쓴다.

| 파일 | 상태 |
| --- | --- |
| `LastShiftLimeAlien_FinalRig_Weighted.blend` | 정본의 조상. 섬 3개·이름이 정리 전 |
| `LastShiftLimeAlien_UnityDeformRig_Clean.blend` | 소프트 변형본이 **없는** 계보 |
| `LastShiftLimeAlien_Rigify_Test*.blend` | 별도 테스트 계보 |

## 정본 blend 의 상태

`FinalRig_Weighted` 에서 다음을 거쳐 만들었다.

1. 떠 있는 섬 3개(42+16+3 정점) 제거 → 5,711 정점
2. 오브젝트 이름을 Unity 계약에 맞춤 — 메시 `LastShift_LimeAlien_Body`,
   리그 `LastShift_LimeAlien_Rig`, 눈 `LastShift_LimeAlien_Eyes`
3. `Tools/art/fix_lime_alien_left_toe_weights.py` 로 왼발 발가락 웨이트 수정
4. `Tools/art/rework_last_shift_left_knee_weights.py` 로 왼 무릎 전이 웨이트 수정
5. `Tools/art/rework_last_shift_hip_mouth_geometry.py` 로 왼 사타구니 3링과
   윗입술 4링의 강화된 REST 형상 완화(왼다리 12회 `0.8/-0.82`, 윗입술
   6회 `0.38/-0.40`, 입구 경계·웨이트·상대 셰이프키 고정)
6. `Tools/art/rework_lime_alien_mouth_circle.py` 로 실제 닫힌 입 함몰의 안쪽
   20정점 링은 정원으로, 피부 경계의 바깥 입구 24정점 링은 1.08:1의 약한 타원으로 재배치하고
   얼굴 쪽 2링을 국소 완화한 뒤 주변 3링을 감쇠 연결

뼈 232개(소프트 변형본 `DEF-head.soft.*` · `DEF-belly.soft.*` 열 개 포함). 아마추어는 REST 포즈로 저장돼 있다.

**주의**: REST 로 저장돼 있어서 `head` 컨트롤을 돌려도 아무것도 안 움직인다.
포즈 시험을 하려면 먼저 `arm.data.pose_position = 'POSE'` 를 줘야 한다.

실제 게임 표시용 젤리 표면은 이 정본을 입력으로 만든
`LastShiftLimeAlien_UnityExport_Jelly.blend`에 저장한다. 2026-08-20 최종본은
제어 정점 완화, 입 안쪽·입구의 1:1 정원 복원, 국소 쿼드화(80.5%), Catmull-Clark L1을
통합했다. `RigifyDeform` / `RigifySoft` FBX가 현재
런타임 계보다. 본체·눈은 전면 스무스 셰이딩이며 접지·FK 관절 포즈 렌더와
FBX 재임포트 회귀 결과는 `last-shift-lime-alien-jelly-production` 증거 폴더에 둔다.
위치·셰이프키·웨이트 정본은 계속 `UnityExport_LeftToeFixed`다.

## 좌우 웨이트 현황 (정본 기준, Unity 분할 정점)

| 뼈 | 왼쪽 | 오른쪽 | 비 |
| --- | --- | --- | --- |
| `DEF-toe` | 176.1 | 178.3 | 1.01 |
| `DEF-foot` | 209.6 | 226.3 | 1.08 |
| `DEF-shin` | 75.7 | 110.2 | 1.46 |
| `DEF-shin.001` | 194.8 | 157.9 | 1.23 |
| `DEF-thigh` | 93.6 | 93.8 | 1.00 |
| `DEF-thigh.001` | 90.2 | 67.6 | 1.33 |

## 목 링 해결 기록

`DEF-spine.004` · `.005` ↔ `.006` 전이대가 좁아 목이 찢어지던 문제는
`Tools/art/rework_lime_alien_canonical.py`로 정본에서 해결했다.
축을 섞어 재면 목이 버티는 각이 **8도**다(허벅지·무릎·발·팔꿈치·손 60도, 어깨 40도, 가슴 15~20도).

가장 직접적인 증거 — 터진 자리 최악 모서리 양끝 정점, 정지 상태에서 0.93cm 거리:

```
A : DEF-spine.004=0.42  .005=0.26  .006=0.19  .003=0.13   <- 목뼈 넷에 골고루
B : DEF-spine.006=0.65  DEF-head.soft.eye=0.35            <- 목뼈 웨이트 0
```

한쪽은 섞여 있는데 바로 옆 정점은 `.004`·`.005` 웨이트가 0이다. 전이가 모서리 하나에서 끝난다.

### 이미 시도했고 안 되는 것 (다시 하지 말 것)

| 시도 | 결과 |
| --- | --- |
| 배분 비율을 실측값(0%/50%)으로 | 26 → 27~29개, 차이 없음 |
| 관절 한계를 8도까지 조임 | 29 → 24~26개 |
| 굽힘 분산 on/off | 무관 |
| `fix_lime_alien_neck_ring_weights.py` 를 이 계보에 적용 | **나빠짐** — 20도 32→34, 46도 45→50, 90도 80→87 |

마지막 항목이 중요하다. 그 스크립트는 `RESPONSE=(0, 0.5, 1.0)` 로 `.004` 의 웨이트를 빼서
`.005` 로 옮기는데, `Rigify_Test` 계보에서는 개선이었지만 이 계보에서는 찢어지는
`.004`↔`.006` 경계를 오히려 날카롭게 만든다. **계보가 다르면 같은 스크립트가 반대로 간다.**

### 판정 결과

블렌더에서 `DEF-spine.006`만 직접 돌려 Unity의 변형 경계를 재현했다. `X+20°`
늘어난 삼각형은 `32 → 0`, ±3축 20°는 전부 `0`, ±3축 46°도 전부 `0`이다.
90° 극단 자세도 모든 축에서 최악 신장과 찢김 수가 감소했다. 변경은 급격한
이동 영향도 경계에서 네 링 확장한 299정점에 한정했고, 비대상 deform 웨이트와
정점 위치는 그대로다. 수치와 근접 렌더는
`docs/art/evidence/last-shift-lime-alien-canonical-rework/`에 있다.

## 수출 절차

정본 blend 는 이미 정리·개명이 끝나 있으므로 수출만 하면 된다.
스케일 옵션은 반드시 `FBX_SCALE_ALL` — `NONE` 이면 리그 노드가 100배로 들어와
콜라이더가 전부 어긋난다.

```python
bpy.ops.export_scene.fbx(
    filepath=out, use_selection=True,
    apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', bake_space_transform=False,
    object_types={'ARMATURE', 'MESH'}, use_mesh_modifiers=True,
    add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
    armature_nodetype='NULL', bake_anim=False,
)
```

## 측정 도구

- `Assets/DoodleUp/Editor/LastShiftSkinToleranceProbe.cs` — 관절별로 스킨이 버티는 각을 잰다
- `Assets/DoodleUp/Editor/LastShiftRagdollSkinLimits.cs` — `min(원본, 스킨여유)` 로 관절 한계를 맞춘다

늘어남을 셀 때는 **몸 렌더러를 이름으로 지정**해야 한다. `GetComponentInChildren<SkinnedMeshRenderer>()`
는 눈을 집을 수 있고, 그러면 머리를 90도 꺾어도 찢어짐 0 이 나온다(실제로 겪음).
`BakeMesh` 는 렌더러의 로컬 스케일만 반영하므로 `rest` 와 비교할 때 1.5 로 나누면 안 된다.
