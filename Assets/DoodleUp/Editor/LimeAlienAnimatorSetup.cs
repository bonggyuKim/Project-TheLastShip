#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoodleUp.Editor
{
    public static class LimeAlienAnimatorSetup
    {
        private const string Root = "Assets/DoodleUp/Art/Characters/LastShiftLimeAlien";
        private const string ModelPath = Root + "/LastShiftLimeAlien_Rigged.fbx";
        private const string ControllerPath = Root + "/LastShiftLimeAlien.controller";
        private const string MaskPath = Root + "/LastShiftLimeAlien_UpperBody.mask";
        private const string PrefabPath = Root + "/LastShiftLimeAlien_Animated.prefab";
        private const string ScenePath = Root + "/LastShiftLimeAlien_AnimationPreview.unity";

        private sealed class ClipSpec
        {
            public string State;
            public string File;
            public bool Loop;

            public ClipSpec(string state, string file, bool loop)
            {
                State = state;
                File = file;
                Loop = loop;
            }
        }

        private static readonly ClipSpec[] Clips =
        {
            new("Idle", "LastShiftLimeAlien_Idle_Loop.fbx", true),
            new("Walk", "LastShiftLimeAlien_Walk_Loop.fbx", true),
            new("Jump_Start", "LastShiftLimeAlien_Jump_Start.fbx", false),
            new("Jump_Loop", "LastShiftLimeAlien_Jump_Loop.fbx", true),
            new("Jump_Land", "LastShiftLimeAlien_Jump_Land.fbx", false),
            new("Grab", "LastShiftLimeAlien_Interact_Grab.fbx", false),
            new("Carry_Hold_Loop", "LastShiftLimeAlien_Carry_Hold_Loop.fbx", true),
            new("Drop", "LastShiftLimeAlien_Interact_Drop.fbx", false),
        };

        [MenuItem("DoodleUp/Characters/Build Lime Alien Animator")]
        public static void Build()
        {
            ConfigureModel(ModelPath, true, false);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid)
                throw new InvalidOperationException("Lime Alien Generic Avatar was not created or is invalid.");

            foreach (var spec in Clips)
                ConfigureModel(Root + "/" + spec.File, false, spec.Loop, avatar);

            var motions = Clips.ToDictionary(c => c.State, LoadMotion);
            var mask = BuildUpperBodyMask();
            var controller = BuildController(motions, mask);
            BuildPrefab(controller);
            BuildPreviewScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Validate();
            Debug.Log("LIME_ALIEN_SETUP_OK: Generic rig, clips, controller, upper-body override layer, prefab and preview scene created.");
        }

        public static void Validate()
        {
            var errors = new List<string>();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid) errors.Add("Generic avatar is missing/invalid");

            foreach (var spec in Clips)
            {
                var path = Root + "/" + spec.File;
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
                if (importer == null || importer.animationType != ModelImporterAnimationType.Generic)
                    errors.Add(spec.State + " is not Generic");
                if (clip == null) errors.Add(spec.State + " clip is missing");
                else if (clip.isLooping != spec.Loop) errors.Add(spec.State + " loop mismatch");
                else Debug.Log($"LIME_CLIP {spec.State}: {clip.length:F3}s {clip.frameRate:F1}fps loop={clip.isLooping}");
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) errors.Add("Animator Controller missing");
            else
            {
                if (controller.layers.Length != 2) errors.Add("Animator must have Base and Carry Override layers");
                var allStates = controller.layers.SelectMany(l => l.stateMachine.states).Select(s => s.state.name).ToHashSet();
                foreach (var spec in Clips)
                    if (!allStates.Contains(spec.State)) errors.Add("Animator state missing: " + spec.State);
            }

            if (!File.Exists(ScenePath)) errors.Add("Preview scene missing");
            if (errors.Count > 0) throw new InvalidOperationException("Lime Alien validation failed: " + string.Join("; ", errors));
            Debug.Log("LIME_ALIEN_VALIDATE_OK");
        }

        private static void ConfigureModel(string path, bool isMain, bool loop, Avatar avatar = null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("ModelImporter missing: " + path);

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = isMain ? ModelImporterAvatarSetup.CreateFromThisModel : ModelImporterAvatarSetup.CopyFromOther;
            if (!isMain) importer.sourceAvatar = avatar;
            importer.importAnimation = !isMain;
            importer.optimizeGameObjects = false;
            importer.importCameras = false;
            importer.importLights = false;

            if (!isMain)
            {
                var defaults = importer.defaultClipAnimations;
                if (defaults.Length == 0) throw new InvalidOperationException("No animation take found: " + path);
                var take = defaults[0];
                take.name = Path.GetFileNameWithoutExtension(path).Replace("LastShiftLimeAlien_", string.Empty);
                take.firstFrame = defaults.Min(c => c.firstFrame);
                take.lastFrame = defaults.Max(c => c.lastFrame);
                take.loopTime = loop;
                take.loopPose = loop;
                take.keepOriginalOrientation = true;
                take.keepOriginalPositionY = true;
                take.keepOriginalPositionXZ = true;
                take.lockRootRotation = true;
                take.lockRootHeightY = true;
                take.lockRootPositionXZ = true;
                importer.clipAnimations = new[] { take };
            }

            importer.SaveAndReimport();
        }

        private static AnimationClip LoadMotion(ClipSpec spec)
        {
            var path = Root + "/" + spec.File;
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (clip == null) throw new InvalidOperationException("Animation clip not found: " + path);
            return clip;
        }

        private static AvatarMask BuildUpperBodyMask()
        {
            var old = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (old != null) AssetDatabase.DeleteAsset(MaskPath);
            var mask = new AvatarMask { name = "LastShiftLimeAlien_UpperBody" };
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var instance = UnityEngine.Object.Instantiate(model);
            try
            {
                var root = instance.transform;
                mask.AddTransformPath(root, false);
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t == root) continue;
                    mask.AddTransformPath(t, false);
                }
                for (var i = 0; i < mask.transformCount; i++)
                {
                    var p = mask.GetTransformPath(i).ToLowerInvariant();
                    var upper = p.Contains("spine") || p.Contains("chest") || p.Contains("neck") ||
                                p.Contains("head") || p.Contains("shoulder") || p.Contains("arm") ||
                                p.Contains("hand") || p.Contains("clav") || p.Contains("eye");
                    mask.SetTransformActive(i, upper);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
            AssetDatabase.CreateAsset(mask, MaskPath);
            return mask;
        }

        private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips, AvatarMask mask)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Grab", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Drop", AnimatorControllerParameterType.Trigger);

            var baseSm = controller.layers[0].stateMachine;
            baseSm.name = "Locomotion & Jump";
            var idle = baseSm.AddState("Idle"); idle.motion = clips["Idle"];
            var walk = baseSm.AddState("Walk"); walk.motion = clips["Walk"];
            var jumpStart = baseSm.AddState("Jump_Start"); jumpStart.motion = clips["Jump_Start"];
            var jumpLoop = baseSm.AddState("Jump_Loop"); jumpLoop.motion = clips["Jump_Loop"];
            var jumpLand = baseSm.AddState("Jump_Land"); jumpLand.motion = clips["Jump_Land"];
            baseSm.defaultState = idle;
            AddCondition(idle.AddTransition(walk), AnimatorConditionMode.Greater, 0.1f, "Speed", false, 0.12f);
            AddCondition(walk.AddTransition(idle), AnimatorConditionMode.Less, 0.1f, "Speed", false, 0.12f);
            AddCondition(baseSm.AddAnyStateTransition(jumpStart), AnimatorConditionMode.If, 0, "Jump", false, 0.05f);
            AddCondition(jumpStart.AddTransition(jumpLoop), AnimatorConditionMode.IfNot, 0, "Grounded", true, 0.08f);
            AddCondition(jumpLoop.AddTransition(jumpLand), AnimatorConditionMode.If, 0, "Grounded", false, 0.08f);
            var landIdle = jumpLand.AddTransition(idle); landIdle.hasExitTime = true; landIdle.exitTime = 0.9f; landIdle.duration = 0.1f;
            landIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            var landWalk = jumpLand.AddTransition(walk); landWalk.hasExitTime = true; landWalk.exitTime = 0.9f; landWalk.duration = 0.1f;
            landWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var carrySm = new AnimatorStateMachine { name = "Carry Upper Body" };
            AssetDatabase.AddObjectToAsset(carrySm, ControllerPath);
            var empty = carrySm.AddState("Empty");
            var grab = carrySm.AddState("Grab"); grab.motion = clips["Grab"];
            var hold = carrySm.AddState("Carry_Hold_Loop"); hold.motion = clips["Carry_Hold_Loop"];
            var drop = carrySm.AddState("Drop"); drop.motion = clips["Drop"];
            carrySm.defaultState = empty;
            AddCondition(empty.AddTransition(grab), AnimatorConditionMode.If, 0, "Grab", false, 0.05f);
            var grabHold = grab.AddTransition(hold); grabHold.hasExitTime = true; grabHold.exitTime = 0.9f; grabHold.duration = 0.08f;
            AddCondition(hold.AddTransition(drop), AnimatorConditionMode.If, 0, "Drop", false, 0.08f);
            var dropEmpty = drop.AddTransition(empty); dropEmpty.hasExitTime = true; dropEmpty.exitTime = 0.9f; dropEmpty.duration = 0.08f;

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "Carry Override",
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 1f,
                stateMachine = carrySm
            });
            return controller;
        }

        private static void AddCondition(AnimatorStateTransition transition, AnimatorConditionMode mode,
            float threshold, string parameter, bool exitTime, float duration)
        {
            transition.hasExitTime = exitTime;
            transition.duration = duration;
            transition.AddCondition(mode, threshold, parameter);
        }

        private static void BuildPrefab(AnimatorController controller)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null) throw new InvalidOperationException("Cannot instantiate Lime Alien model");
            instance.name = "LastShiftLimeAlien_Animated";
            var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
        }

        private static void BuildPreviewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var character = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            character.transform.position = Vector3.zero;

            var cameraGo = new GameObject("Preview Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.1f, 0.14f);

            var lightGo = new GameObject("Key Light");
            var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            EditorSceneManager.SaveScene(scene, ScenePath);

            VerifyAnimatorSampling(character.GetComponent<Animator>());
            FrameCamera(camera, character);
            Capture(camera, Root + "/LastShiftLimeAlien_CarryPreview.png");
        }

        private static void FrameCamera(Camera camera, GameObject character)
        {
            var renderers = character.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Lime Alien has no renderers");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            var distance = Mathf.Max(size * 2.2f, 0.5f);
            camera.transform.position = bounds.center + new Vector3(0f, bounds.size.y * 0.05f, distance);
            camera.transform.rotation = Quaternion.LookRotation(bounds.center - camera.transform.position, Vector3.up);
            camera.nearClipPlane = Mathf.Max(distance * 0.01f, 0.01f);
            camera.farClipPlane = distance * 5f;
            Debug.Log($"LIME_ALIEN_BOUNDS center={bounds.center} size={bounds.size} cameraDistance={distance:F2}");
        }

        private static void VerifyAnimatorSampling(Animator animator)
        {
            animator.Rebind();
            animator.Update(0f);
            var checks = new[]
            {
                (0, "Idle"), (0, "Walk"), (0, "Jump_Start"), (0, "Jump_Loop"), (0, "Jump_Land"),
                (1, "Grab"), (1, "Carry_Hold_Loop"), (1, "Drop")
            };
            foreach (var check in checks)
            {
                var hash = Animator.StringToHash(check.Item2);
                if (!animator.HasState(check.Item1, hash))
                    throw new InvalidOperationException($"Animator state cannot be sampled: layer={check.Item1} state={check.Item2}");
                animator.Play(hash, check.Item1, 0.35f);
                animator.Update(1f / 30f);
            }
            animator.Play("Walk", 0, 0.25f);
            animator.Play("Carry_Hold_Loop", 1, 0.25f);
            animator.Update(1f / 30f);
            Debug.Log("LIME_ALIEN_STATE_SAMPLE_OK: Idle/Walk, Jump chain and Grab/Carry/Drop states sampled.");
        }

        private static void Capture(Camera camera, string assetPath)
        {
            var rt = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(768, 768, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 768, 768), 0, 0);
                tex.Apply();
                File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(tex);
            }
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("LIME_ALIEN_CAPTURE_OK: " + assetPath);
        }
    }
}
#endif
