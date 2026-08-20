using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 래그돌 셋업을 <b>그대로 둔 채</b> 밑에 깔린 모델만 갈아 끼운다.
    ///
    /// <b>왜 필요했나.</b> Unity 에 들어간 FBX 는 소프트 변형본 열(<c>DEF-head.soft.*</c>,
    /// <c>DEF-belly.soft.*</c>)이 빠진 리그에서 뽑혀 있었다. 그 열을 살린 모델로 갈아 끼우면
    /// 나중에 눌림·출렁임을 걸 손잡이가 생긴다.
    ///
    /// <b>목 찢어짐은 이걸로 안 풀린다.</b> 소프트 본은 <c>DEF-spine.006</c> 의 뻣뻣한 자식이라
    /// 부모와 똑같이 움직인다 — 웨이트를 일곱으로 나눠도 변형 결과가 같다(실측: 20·46·90도에서
    /// 32·45·80개로 신구 동일). 같은 메시를 블렌더에서 <c>DEF-spine.006</c> 만 돌려도 29·40·79개로
    /// 똑같이 찢어진다. 남은 원인은 래그돌이 머리뼈 하나만 돌리는 것이다.
    ///
    /// <b>왜 다시 만들지 않고 옮기나.</b> 부위별 콜라이더 모양은 손으로 맞춰 둔 값이라 다시
    /// 계산하면 안 된다. 두 FBX 의 뼈 정지 포즈와 계층이 완전히 같은 것을 먼저 확인했으므로
    /// (실측: 최대 위치차 0.00000m, 회전차 0.000도, 계층 불일치 0건) 리지드바디·조인트·
    /// 콜라이더 홀더를 <b>값 그대로</b> 새 뼈에 옮겨 붙이면 자리가 어긋나지 않는다.
    /// </summary>
    public static class LastShiftRagdollModelSwap
    {
        /// <summary>소프트 변형본이 살아 있는 리그에서 뽑은 정본.</summary>
        public const string SoftModelPath =
            "Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_RigifySoft.fbx";

        [MenuItem("Last Shift/Prototype/Swap Ragdoll Model To Soft Rig")]
        private static void SwapInOpenScene()
        {
            var old = GameObject.Find("RagdollSubject");
            if (old == null)
            {
                Debug.LogError("RagdollSubject 를 못 찾았다 — 래그돌 랩 씬을 먼저 열어라.");
                return;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SoftModelPath);
            if (model == null)
            {
                Debug.LogError($"모델을 못 찾았다: {SoftModelPath}");
                return;
            }

            var report = new List<string>();
            var swapped = Swap(old, model, report);
            Undo.RegisterCreatedObjectUndo(swapped, "Swap ragdoll model");
            Debug.Log("모델 교체 완료:\n" + string.Join("\n", report));
        }

        /// <summary>
        /// <paramref name="old"/> 의 물리 셋업을 <paramref name="model"/> 인스턴스에 옮기고
        /// 새 오브젝트를 돌려준다. 옮기는 순서가 중요하다 — 조인트의 <c>connectedBody</c> 를
        /// 이름으로 다시 잇기 때문에 리지드바디가 <b>전부</b> 생긴 뒤에 조인트를 붙인다.
        /// </summary>
        public static GameObject Swap(GameObject old, GameObject model, List<string> report)
        {
            var fresh = (GameObject)PrefabUtility.InstantiatePrefab(model, old.scene);
            fresh.transform.SetParent(old.transform.parent, false);
            fresh.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
            fresh.transform.localScale = old.transform.localScale;

            var bones = fresh.GetComponentsInChildren<Transform>(true)
                .GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());

            Transform Bone(string name) => bones.TryGetValue(name, out var t) ? t : null;

            // 0) 씬에서 손으로 옮겨 둔 노드 오프셋을 옮긴다.
            //    두 FBX 의 뼈 정지 포즈는 같으므로(실측 0.00000m) 여기서 값이 다르게 나오는 것은
            //    <b>사람이 준 오버라이드</b>뿐이다 — 예: 프리팹에서 리그 노드를 Z 로 -0.072 민 것.
            //    차이가 나는 것만 옮기고 로그로 남긴다. 조용히 덮으면 정지 포즈가 어긋나도 모른다.
            foreach (var source in old.GetComponentsInChildren<Transform>(true))
            {
                if (source == old.transform) continue;
                if (source.GetComponent<Collider>() != null) continue;   // 콜라이더 홀더는 2)에서 통째로 복제한다
                // <b>렌더러는 건너뛴다.</b> 눈 메시처럼 두 FBX 에서 오브젝트 원점이 다른 것은
                // 사람이 준 오버라이드가 아니라 소스 데이터의 정당한 차이다. 덮으면 눈이 얼굴에서 빠진다.
                if (source.GetComponent<Renderer>() != null) continue;
                var target = Bone(source.name);
                if (target == null) continue;

                var moved = Vector3.Distance(source.localPosition, target.localPosition) > 0.00001f
                            || Quaternion.Angle(source.localRotation, target.localRotation) > 0.001f
                            || Vector3.Distance(source.localScale, target.localScale) > 0.00001f;
                if (!moved) continue;

                report.Add($"  노드 오프셋 이식 {source.name}: {target.localPosition:F4} -> {source.localPosition:F4}");
                target.localPosition = source.localPosition;
                target.localRotation = source.localRotation;
                target.localScale = source.localScale;
            }

            // 1) 리지드바디 — 조인트보다 먼저. 값은 통째로 복사한다.
            var bodies = old.GetComponentsInChildren<Rigidbody>(true);
            foreach (var rb in bodies)
            {
                var target = Bone(rb.name);
                if (target == null) { report.Add($"  [빠짐] 리지드바디 {rb.name}: 새 모델에 뼈가 없다"); continue; }
                ComponentUtility.CopyComponent(rb);
                ComponentUtility.PasteComponentAsNew(target.gameObject);
            }
            report.Add($"  리지드바디 {bodies.Length}개 이식");

            // 2) 콜라이더 홀더 — GameObject 째로 복제해야 콜라이더 종류·치수·로컬 트랜스폼이
            //    한 값도 안 바뀐다. 여기서 다시 계산하는 것은 <b>금지</b>다.
            var holders = old.GetComponentsInChildren<Transform>(true)
                .Where(t => t.GetComponent<Collider>() != null).ToArray();
            foreach (var holder in holders)
            {
                var parent = Bone(holder.parent.name);
                if (parent == null) { report.Add($"  [빠짐] 콜라이더 {holder.name}: 부모 뼈 {holder.parent.name} 없음"); continue; }
                var clone = Object.Instantiate(holder.gameObject, parent);
                clone.name = holder.name;
                clone.transform.localPosition = holder.localPosition;
                clone.transform.localRotation = holder.localRotation;
                clone.transform.localScale = holder.localScale;
            }
            report.Add($"  콜라이더 홀더 {holders.Length}개 이식 (값 그대로)");

            // 3) 조인트 — connectedBody 는 참조라 복사하면 옛 오브젝트를 가리킨다. 이름으로 다시 잇는다.
            var joints = old.GetComponentsInChildren<CharacterJoint>(true);
            foreach (var joint in joints)
            {
                var target = Bone(joint.name);
                if (target == null) { report.Add($"  [빠짐] 조인트 {joint.name}: 뼈 없음"); continue; }
                ComponentUtility.CopyComponent(joint);
                ComponentUtility.PasteComponentAsNew(target.gameObject);

                var pasted = target.GetComponent<CharacterJoint>();
                var connectedName = joint.connectedBody != null ? joint.connectedBody.name : null;
                pasted.connectedBody = connectedName != null && Bone(connectedName) != null
                    ? Bone(connectedName).GetComponent<Rigidbody>()
                    : null;
                if (pasted.connectedBody == null) report.Add($"  [경고] 조인트 {joint.name}: connectedBody 를 못 이었다");
            }
            report.Add($"  조인트 {joints.Length}개 이식");

            // 4) 루트에 붙어 있던 컴포넌트들.
            foreach (var component in old.GetComponents<Component>())
            {
                if (component is Transform) continue;
                // FBX 인스턴스가 이미 갖고 있는 것(대개 Animator)은 두 번 붙이지 않는다.
                if (fresh.GetComponent(component.GetType()) != null) continue;
                ComponentUtility.CopyComponent(component);
                ComponentUtility.PasteComponentAsNew(fresh);
                report.Add($"  루트 컴포넌트 {component.GetType().Name} 이식");
            }

            fresh.name = old.name;
            return fresh;
        }
    }
}
