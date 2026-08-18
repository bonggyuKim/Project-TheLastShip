using System;
using System.Collections.Generic;
using DoodleUp.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 국소 눌림만 보는 격리 테스트맵.
    ///
    /// <b>캐릭터에 물리를 하나도 안 준다.</b> Rigidbody·Joint·<see cref="LastShiftRagdoll"/>·
    /// 포즈 복사가 전부 없고, 남는 것은 <see cref="SkinnedMeshRenderer"/> 와 충돌을 받을
    /// 정적 콜라이더뿐이다. 뼈도 루트도 안 움직인다 — 화면에서 움직이는 것은 <b>공</b>과
    /// <see cref="LastShiftBodyDeform"/> 이 미는 정점뿐이다.
    ///
    /// <b>왜 따로 만드나.</b> 래그돌 랩에서는 몸이 날아가는 동안 눌림이 같이 일어나서, 화면에
    /// 이상한 것이 보여도 그것이 물리 탓인지 표현 탓인지 못 가른다. 실제로 메시가 찢어졌을 때
    /// 원인을 좁히는 데만 여러 번을 썼다. 여기서는 변수를 하나로 줄인다.
    ///
    /// 래그돌 랩(<see cref="LastShiftRagdollLabScene"/>)은 그대로 둔다. 두 씬은 서로를 안 읽는다.
    /// </summary>
    public static class LastShiftDeformLabScene
    {
        public const string ScenePath = "Assets/Scenes/LAST_SHIFT_DEFORM_LAB.unity";

        /// <summary>
        /// <b>이 랩만 정리본을 쓴다.</b> 래그돌 랩과 네트워크 씬은 기존 FBX 그대로 둔다 —
        /// 두 테스트를 완전히 갈라 놓는 것이 이 씬을 따로 만든 이유이기 때문이다.
        ///
        /// 정리본은 본체에서 떨어져 있던 섬 셋(42/16/3 정점)을 뺀 것이다. 그 섬들은 뼈가 돌 때만
        /// 판 조각처럼 튀어나오고 정지 자세에서는 딱 붙어 있어 눈으로는 안 걸렸다.
        /// 임포트 직후 <c>Last Shift/Prototype/Dump Character Rig</c> 로 확인한다 —
        /// 정리본은 <c>islands count=2</c>(본체 4,221 + 눈 흰자 1,490)다.
        /// </summary>
        public const string CharacterPrefabPath =
            "Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_RigifyDeformClean.fbx";

        public const string FloorMaterialPath = "Assets/DoodleUp/Materials/LS_Floor.mat";
        public const string PropMaterialPath = "Assets/DoodleUp/Materials/LS_Hull.mat";

        /// <summary>게임과 같은 배율. 눌림 반경·깊이가 월드 미터라 크기가 다르면 감각이 달라진다.</summary>
        public const float CrewScale = 1.5f;

        public const string DeformMaterialFolder = "Assets/DoodleUp/Materials/Lab";
        public const string DeformShaderName = "LastShift/BodyDeform";

        [MenuItem("Last Shift/Prototype/Rebuild Deform Lab Scene")]
        public static void Rebuild()
        {
            var scene = Build(out _);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LAST_SHIFT_DEFORM_LAB_BUILD] scene={ScenePath} result=PASS");
        }

        /// <summary>씬을 세우고 저장은 안 한다. 캡처가 디스크를 안 건드리고 같은 씬을 쓰려는 것이다.</summary>
        public static Scene Build(out GameObject crew)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            var propMaterial = AssetDatabase.LoadAssetAtPath<Material>(PropMaterialPath);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            floor.isStatic = true;
            if (floorMaterial != null) floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

            // 뒷벽. 실루엣이 하늘에 묻히면 눌림 깊이를 눈으로 못 잰다.
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Backdrop";
            wall.transform.position = new Vector3(0f, 1.6f, -2.2f);
            wall.transform.localScale = new Vector3(6f, 3.2f, 0.3f);
            wall.isStatic = true;
            if (propMaterial != null) wall.GetComponent<Renderer>().sharedMaterial = propMaterial;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"승무원 프리팹을 못 찾았다: {CharacterPrefabPath}");

            crew = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            crew.name = "DeformSubject";
            crew.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            crew.transform.localScale = Vector3.one * CrewScale;

            // 애니메이터가 살아 있으면 매 프레임 뼈를 되돌려 "안 움직인다" 는 전제가 깨진다.
            foreach (var animator in crew.GetComponentsInChildren<Animator>(true)) animator.enabled = false;

            foreach (var skin in crew.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // 정점을 셰이더가 밀어내므로 원래 바운즈를 벗어날 수 있다. 안 켜면 컬링된다.
                skin.updateWhenOffscreen = true;
                skin.forceMatrixRecalculationPerRender = true;
            }

            crew.AddComponent<LastShiftBodyDeform>();
            ApplyDeformMaterials(crew);
            BuildStaticContactSurfaces(crew);

            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0.9f, 0.8f, 2.0f);
                camera.transform.rotation = Quaternion.LookRotation(
                    new Vector3(0f, 0.55f, 0f) - camera.transform.position, Vector3.up);
                camera.fieldOfView = 42f;
                camera.nearClipPlane = 0.03f;
                camera.farClipPlane = 40f;
            }

            var light = UnityEngine.Object.FindAnyObjectByType<Light>();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
                light.intensity = 0.9f;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return scene;
        }

        /// <summary>
        /// 충돌을 받을 <b>정적</b> 콜라이더를 뼈마다 얹는다.
        ///
        /// Rigidbody 를 안 붙인다. 몸이 안 움직이는 것이 이 테스트의 전제이고, Rigidbody 가
        /// 하나라도 있으면 그 전제가 조용히 깨진다. 정적 콜라이더도 충돌 메시지를 받으므로
        /// <see cref="LastShiftRagdollContactRelay"/> 는 그대로 쓸 수 있다.
        ///
        /// 치수는 래그돌과 같은 표(<see cref="LastShiftRagdollRig"/>)에서 뽑는다 — 두 랩에서
        /// 접촉 반경이 다르면 눌림 크기가 달라져 비교가 안 된다.
        /// </summary>
        private static void BuildStaticContactSurfaces(GameObject crew)
        {
            var bones = new Dictionary<string, Transform>();
            foreach (var bone in crew.GetComponentsInChildren<Transform>(true))
                if (!bones.ContainsKey(bone.name)) bones[bone.name] = bone;

            var hipSpan = Span(bones, LastShiftRagdollRig.LeftHipBoneName, LastShiftRagdollRig.RightHipBoneName);
            var shoulderSpan = Span(bones, LastShiftRagdollRig.LeftShoulderBoneName, LastShiftRagdollRig.RightShoulderBoneName);
            var crownRise = CrownRise(crew, bones);

            var deform = crew.GetComponent<LastShiftBodyDeform>();

            foreach (var spec in LastShiftRagdollRig.Bones)
            {
                if (!bones.TryGetValue(spec.BoneName, out var bone)) continue;

                var holder = new GameObject(spec.Part + "__DeformCollider");
                holder.transform.SetParent(bone, false);

                var lossy = holder.transform.lossyScale;
                var uniform = (lossy.x + lossy.y + lossy.z) / 3f;
                if (uniform <= 0.0001f) continue;

                float worldRadius;
                if (spec.Girth == LastShiftRagdollGirth.CrownRise)
                {
                    worldRadius = Mathf.Max(0.02f, crownRise * spec.GirthScale);
                    holder.transform.position = bone.position + Vector3.up * worldRadius;
                    holder.transform.rotation = Quaternion.identity;
                    holder.AddComponent<SphereCollider>().radius = worldRadius / uniform;
                }
                else
                {
                    if (spec.TipBoneName == null || !bones.TryGetValue(spec.TipBoneName, out var tip)) continue;
                    var delta = tip.position - bone.position;
                    var length = delta.magnitude;
                    if (length <= 0.0001f) continue;

                    var girth = spec.Girth switch
                    {
                        LastShiftRagdollGirth.HipSpan => hipSpan,
                        LastShiftRagdollGirth.ShoulderSpan => shoulderSpan,
                        _ => length
                    };
                    worldRadius = Mathf.Max(0.015f, girth * spec.GirthScale);

                    var direction = delta / length;
                    holder.transform.position = bone.position + direction * (length * 0.5f);
                    holder.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);

                    var capsule = holder.AddComponent<CapsuleCollider>();
                    capsule.direction = 1;
                    capsule.radius = worldRadius / uniform;
                    capsule.height = Mathf.Max(length, worldRadius * 2f) / uniform;
                }

                // 릴레이는 콜라이더 자신에게 붙인다 — Rigidbody 가 없으므로 충돌 메시지가
                // 이 오브젝트로 온다. 앵커는 뼈라서 자국이 부위에 매달린다.
                var relay = holder.AddComponent<LastShiftRagdollContactRelay>();
                relay.Configure(deform, spec.Part, worldRadius);
            }
        }

        private static float Span(IReadOnlyDictionary<string, Transform> bones, string a, string b)
        {
            if (!bones.TryGetValue(a, out var left) || !bones.TryGetValue(b, out var right)) return 0.2f;
            return Vector3.Distance(left.position, right.position);
        }

        private static float CrownRise(GameObject crew, IReadOnlyDictionary<string, Transform> bones)
        {
            if (!bones.TryGetValue(LastShiftRagdollRig.HeadBoneName, out var head)) return 0.2f;
            var top = float.MinValue;
            foreach (var renderer in crew.GetComponentsInChildren<Renderer>(true))
                top = Mathf.Max(top, renderer.bounds.max.y);
            var rise = top - head.position.y;
            return rise > 0.01f ? rise : 0.2f;
        }

        /// <summary>
        /// 몸 머티리얼을 눌림 셰이더 판으로 바꾼다. 래그돌 랩과 같은 자산을 쓴다 —
        /// 두 랩이 다른 머티리얼을 쓰면 룩 판정이 갈린다.
        /// </summary>
        private static void ApplyDeformMaterials(GameObject crew)
        {
            var shader = Shader.Find(DeformShaderName);
            if (shader == null) throw new InvalidOperationException($"눌림 셰이더를 못 찾았다: {DeformShaderName}");
            if (!AssetDatabase.IsValidFolder(DeformMaterialFolder))
                AssetDatabase.CreateFolder("Assets/DoodleUp/Materials", "Lab");

            var made = new Dictionary<string, Material>();
            foreach (var skin in LastShiftCrewBody.Renderers(crew.transform))
            {
                var sources = skin.sharedMaterials;
                var swapped = new Material[sources.Length];
                for (var i = 0; i < sources.Length; i++)
                {
                    var source = sources[i];
                    if (source == null) continue;
                    if (!made.TryGetValue(source.name, out var deform))
                    {
                        deform = LoadOrCreateDeformMaterial(source, shader);
                        made[source.name] = deform;
                    }
                    swapped[i] = deform;
                }
                skin.sharedMaterials = swapped;
            }
        }

        private static Material LoadOrCreateDeformMaterial(Material source, Shader shader)
        {
            var path = $"{DeformMaterialFolder}/LS_Deform_{source.name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            if (source.HasProperty("_Color")) material.SetColor("_Color", source.GetColor("_Color"));
            if (source.HasProperty("_MainTex")) material.SetTexture("_MainTex", source.GetTexture("_MainTex"));
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
