using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 압력문(<c>LPK_Door_Airlock_2m</c>)을 <b>단문</b>으로 매단다. 배에 서 있는 문 다섯이 전부
    /// 이 프리팹 하나를 쓰므로, 여기 한 번이 다섯 곳이다(방위·용도 브리프 §6 #11).
    ///
    /// <b>왜 킷 임포터가 아니라 여기인가.</b> 배에 실제로 서는 문은 킷 FBX 가 아니라 양산 프롭
    /// <c>LP_AirlockDoor</c> 다 — 프리팹이 그것을 <c>ProductionVisual</c> 로 품고 있고, 킷 쪽
    /// 계층(<c>DoorLeafPivot</c>)은 그 안에 없다. 그래서 킷 임포터가 굽던 클립은 <b>있지도 않은
    /// 마디</b>를 가리키고 있었고, 문 다섯이 통째로 안 움직였다. 이 루틴은 프리팹이 실제로 품은
    /// 계층을 읽어서 굽는다.
    ///
    /// <b>양문에서 단문으로.</b> 양산 프롭은 <c>Airlock_Left_Pivot</c>/<c>Airlock_Right_Pivot</c>
    /// 두 짝이 좌우로 갈라지는 미닫이였다. 두 짝을 <b>같은 경첩·같은 운동</b>에 묶어 한 짝처럼
    /// 젖힌다 — 맞물릴 상대가 사라지므로 좌우 오프셋을 맞출 일도 같이 사라진다. 나중에 아트가
    /// 두 판을 한 장으로 합쳐도 이 계산은 그대로다(판 수를 세어 쓰기 때문이다).
    /// </summary>
    public static class LastShiftAirlockDoorRig
    {
        private const string PrefabPath = "Assets/DoodleUp/Prefabs/LastShiftModularKit/LPK_Door_Airlock_2m.prefab";
        private const string ControllerPath = "Assets/DoodleUp/Prefabs/LastShiftModularKit/Animators/LPK_Door_Airlock_2m.controller";
        private const string ClipPath = "Assets/DoodleUp/Prefabs/LastShiftModularKit/Animators/LP_AirlockDoor_OpenClose.anim";

        /// <summary>
        /// <see cref="DoodleUp.Runtime.LastShiftZoneDoor"/> 가 이름으로 긁는 상태다. 클립 이름이
        /// 바뀌어도 이 상태 이름은 못 바꾼다 — 문 쪽 코드가 해시로 들고 있다.
        /// </summary>
        private const string StateName = "LP_Door_OpenClose";

        /// <summary>문짝이 젖혀지는 각. 벌크헤드 면에 붙을 만큼만 연다.</summary>
        private const float SwingDegrees = 90f;

        [MenuItem("Last Shift/SP-02A/Rig Airlock Door as Single Leaf")]
        public static void Rig()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ClipPath) ?? string.Empty);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var pivots = PanelPivots(root.transform);
                var space = pivots[0].parent;

                // 구멍은 문설주 <b>안쪽</b> 면 사이다. 인방까지 한 벌로 재면 바깥 폭이 나와
                // 문짝이 문설주를 덮고 서게 된다.
                var portJamb = LastShiftHingeAuthoring.LocalBounds(space,
                    LastShiftHingeAuthoring.Require(root.transform, "Airlock_FrameL", "LP_AirlockDoor"));
                var starboardJamb = LastShiftHingeAuthoring.LocalBounds(space,
                    LastShiftHingeAuthoring.Require(root.transform, "Airlock_FrameR", "LP_AirlockDoor"));
                var openingMin = Mathf.Min(portJamb.max.x, starboardJamb.max.x);
                var openingMax = Mathf.Max(portJamb.min.x, starboardJamb.min.x);

                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath)
                           ?? new AnimationClip { name = Path.GetFileNameWithoutExtension(ClipPath) };
                clip.ClearCurves();

                // 판들이 구멍을 나눠 덮는다. 한 장이면 그대로 전폭이고, 두 장이면 반씩이다 —
                // 아트가 합치든 쪼개든 세어서 나누므로 이 줄은 안 바뀐다.
                var width = (openingMax - openingMin) / pivots.Length;
                for (var i = 0; i < pivots.Length; i++)
                {
                    var closed = pivots[i].localPosition;
                    closed.x = openingMin + width * (i + 0.5f);
                    // 경첩은 판마다가 아니라 <b>구멍 한쪽 모서리에 하나</b>다. 두 판이 같은 점을
                    // 돌기 때문에 한 짝처럼 붙어서 젖혀진다.
                    var hinge = new Vector3(openingMin, closed.y, closed.z);
                    var leaf = new LastShiftHatchLeaf(hinge, closed, Quaternion.identity,
                        LastShiftHatchLeaf.KitUp, SwingDegrees);

                    pivots[i].localPosition = leaf.ClosedPosition;
                    pivots[i].localRotation = leaf.ClosedRotation;
                    LastShiftHingeAuthoring.WriteSwing(clip,
                        AnimationUtility.CalculateTransformPath(pivots[i], root.transform), leaf);
                }

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                if (AssetDatabase.GetAssetPath(clip) == string.Empty) AssetDatabase.CreateAsset(clip, ClipPath);
                EditorUtility.SetDirty(clip);

                BindController(root, clip);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[LAST_SHIFT_AIRLOCK_DOOR] leaves={pivots.Length} opening=" +
                          $"{openingMax - openingMin:F2} swing={SwingDegrees:F0} result=PASS");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>문짝 마디들을 폭 순서로. 순서가 흔들리면 판이 서로 자리를 바꿔 겹친다.</summary>
        private static Transform[] PanelPivots(Transform root)
        {
            var pivots = LastShiftHingeAuthoring.PartsNamed(root, "Airlock_");
            var leaves = new System.Collections.Generic.List<Transform>();
            foreach (var pivot in pivots)
                if (pivot.name.EndsWith("_Pivot", StringComparison.Ordinal)) leaves.Add(pivot);
            if (leaves.Count == 0) throw new InvalidOperationException("LP_AirlockDoor has no *_Pivot leaf");
            leaves.Sort((a, b) => a.localPosition.x.CompareTo(b.localPosition.x));
            return leaves.ToArray();
        }

        private static void BindController(GameObject root, AnimationClip clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                             ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var stateMachine = controller.layers[0].stateMachine;
            foreach (var existing in stateMachine.states) stateMachine.RemoveState(existing.state);
            var state = stateMachine.AddState(StateName);
            state.motion = clip;
            stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);

            var animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
        }
    }
}
