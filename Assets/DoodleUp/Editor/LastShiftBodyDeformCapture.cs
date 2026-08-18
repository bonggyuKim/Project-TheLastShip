using System;
using System.Globalization;
using System.IO;
using DoodleUp.Runtime;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 공을 배에 맞혀 <b>눌림 → 둘레 불룩함 → 복원 → 래그돌 반응</b>을 한 컷에 담는다.
    ///
    /// <b>물리를 손으로 밟는다.</b> 에디터 Play 로 찍으면 프레임 간격이 실행 속도에 흔들려
    /// 영상만 보고 물리가 튀었는지 판단할 수 없다. <see cref="SimulationMode.Script"/> 로
    /// 60Hz 를 정확히 밟고 매 스텝 한 장을 담은 뒤 30fps 로 재생하면 <b>정확히 0.5배</b> 슬로모션이
    /// 된다 — 배속을 편집기에서 눈대중으로 맞추지 않는다.
    ///
    /// <b>표현층도 손으로 밟는다.</b> <c>LateUpdate</c> 는 에디터에서 안 도므로
    /// <see cref="LastShiftBodyDeform.Step"/> 과 <see cref="LastShiftBodyDeform.PushToRenderers"/> 를
    /// 물리와 같은 스텝으로 직접 부른다. 안 그러면 눌림이 화면에 한 번도 안 들어간다.
    /// </summary>
    public static class LastShiftBodyDeformCapture
    {
        public const string OutputDirectory =
            "docs/tech/evidence/last-shift-body-deform-2026-08-18";

        private const string FrontFolder = "frames_front";

        /// <summary>
        /// 측면. <b>정면만으로는 눌림도 찢김도 못 읽는다</b> — 공이 접촉면을 가리고, 어깨·골반이
        /// 제자리에 남는 종류의 찢김은 실루엣에서만 드러난다. 90도에서 보면 눌림 깊이와 둘레
        /// 불룩함이 실루엣으로 나오고 몸통이 통째로 밀리는지도 같이 보인다.
        /// </summary>
        private const string SideFolder = "frames_side";

        private const float StepSeconds = 1f / 60f;
        private const float DurationSeconds = 2.6f;

        /// <summary>공 반지름(m). 배 굵기보다 작아야 눌린 자국이 공 크기로 읽힌다.</summary>
        private const float BallRadius = 0.13f;

        private const float BallMass = 3.0f;
        private const float BallSpeed = 5.0f;
        private const float BallStartDistance = 1.15f;

        private const int FrameWidth = 1280;
        private const int FrameHeight = 720;

        [MenuItem("Last Shift/Prototype/Capture Body Deform Video")]
        public static void CaptureForAutomation()
        {
            // <b>변형을 끄고도 한 번 찍을 수 있어야 한다.</b> 메시가 찢어졌을 때 원인이 눌림
            // 셰이더인지 스키닝인지는, 눌림만 빼고 같은 물리를 돌려 같은 프레임을 비교해야 갈린다.
            var deformEnabled = CommandLineValue("-deform") != "off";
            var suffix = deformEnabled ? "" : "_nodeform";

            var frontDirectory = Path.Combine(OutputDirectory, FrontFolder + suffix);
            var sideDirectory = Path.Combine(OutputDirectory, SideFolder + suffix);
            foreach (var directory in new[] { frontDirectory, sideDirectory })
            {
                Directory.CreateDirectory(directory);
                foreach (var stale in Directory.GetFiles(directory, "frame_*.png")) File.Delete(stale);
            }

            LastShiftRagdollLabScene.Build();

            var subject = GameObject.Find("RagdollSubject");
            if (subject == null) throw new InvalidOperationException("테스트맵에 RagdollSubject 가 없다.");

            var lab = subject.GetComponent<LastShiftRagdollLab>();
            if (lab != null) UnityEngine.Object.DestroyImmediate(lab);

            var deform = subject.GetComponent<LastShiftBodyDeform>();
            if (deform == null) throw new InvalidOperationException("승무원에 표현층이 없다.");
            deform.CollectRenderers();

            var ragdoll = subject.GetComponent<LastShiftRagdoll>();
            var tuning = LastShiftRagdollTuning.Comic();
            ragdoll.Build(tuning);

            var previousMode = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;

            GameObject ball = null;
            GameObject cameraObject = null;
            GameObject sideObject = null;
            var pelvisStart = Vector3.zero;
            var pelvisEnd = Vector3.zero;
            var ballEnd = Vector3.zero;
            var closestApproach = float.MaxValue;
            var written = 0;
            var deepestDent = 0f;
            var slotsSeen = 0;
            var impactFrame = -1;
            var deepestFrame = -1;
            var timeline = new System.Text.StringBuilder("frame,depth" + Environment.NewLine);

            try
            {
                // 빌드 직후 콜라이더가 서로 밀어내는 한 프레임이 첫 장에 들어가면 충격과 구분이 안 된다.
                UnityEngine.Physics.Simulate(StepSeconds);
                ragdoll.ResetToRestPose();

                var belly = ragdoll.Bodies[LastShiftRagdollPart.Spine].transform;
                pelvisStart = ragdoll.Root.position;

                // 승무원은 +z 를 본다(눈이 +z 에 있다). 공은 그 정면에서 배로 곧장 온다.
                var facing = Vector3.forward;
                var target = belly.position;
                ball = BuildBall(target + facing * BallStartDistance, -facing * BallSpeed);
                var ballBody = ball.GetComponent<Rigidbody>();
                var ballCollider = ball.GetComponent<Collider>();

                cameraObject = new GameObject("BodyDeformCameraFront");
                var camera = MakeCamera(cameraObject);
                sideObject = new GameObject("BodyDeformCameraSide");
                var side = MakeCamera(sideObject);

                // <b>정면에서 30도만 튼다.</b> 완전 정면이면 공이 카메라 쪽으로 날아와 정작
                // 맞는 순간의 눌림을 공이 가린다 — 접촉점과 공이 같이 보이는 최소한의 각도다.
                // 프레이밍은 배가 아니라 <b>몸통 전체</b>를 잡는다. 배에 딱 붙여 찍었더니 머리가
                // 잘리고 공이 화면의 절반을 먹어, 정작 판단해야 할 눌림·불룩함·복원이 공 뒤에
                // 가려졌다. 상체가 다 들어와야 자국의 크기를 몸과 견줘 읽을 수 있다.
                var framing = Vector3.Lerp(belly.position, ragdoll.Bodies[LastShiftRagdollPart.Chest].position, 0.5f);
                var eye = framing
                          + Quaternion.AngleAxis(28f, Vector3.up) * facing * 1.95f
                          + Vector3.up * 0.30f;
                camera.transform.position = eye;
                camera.transform.rotation = Quaternion.LookRotation(framing - eye, Vector3.up);

                var sideEye = framing
                                 + Quaternion.AngleAxis(90f, Vector3.up) * facing * 1.70f
                                 + Vector3.up * 0.22f;
                side.transform.position = sideEye;
                side.transform.rotation = Quaternion.LookRotation(framing - sideEye, Vector3.up);

                var totalSteps = Mathf.CeilToInt(DurationSeconds / StepSeconds);
                for (var step = 0; step <= totalSteps; step++)
                {
                    var name = "frame_" + written.ToString("D4", CultureInfo.InvariantCulture) + ".png";
                    WriteFrame(camera, Path.Combine(frontDirectory, name));
                    WriteFrame(side, Path.Combine(sideDirectory, name));
                    written++;

                    var velocityBefore = ballBody.linearVelocity;
                    ragdoll.StepPhysics(StepSeconds);
                    UnityEngine.Physics.Simulate(StepSeconds);
                    if (deformEnabled) RelayBallContact(ragdoll, ballCollider, ballBody, velocityBefore);

                    // 표현층은 물리와 같은 스텝으로 민다. 슬롯 깊이를 같이 재 두는 이유는,
                    // 영상 파일이 만들어졌다는 것만으로는 "눌림이 화면에 들어갔는가" 가 안 걸리기 때문이다.
                    deform.Step(StepSeconds);
                    deform.PushToRenderers();

                    var active = deform.ActiveSlots;
                    if (active > slotsSeen) slotsSeen = active;
                    for (var i = 0; i < LastShiftBodyDeform.SlotCount; i++)
                        deepestDent = Mathf.Max(deepestDent, Mathf.Abs(deform.DepthOf(i)));
                    if (impactFrame < 0 && active > 0) impactFrame = written;

                    if (written == 20) LogRenderState(deform, subject);
                if (written == 1) LogSkeletonState(subject, "rest");
                if (written == 40) LogSkeletonState(subject, "afterImpact");

                    var frameDepth = 0f;
                    for (var i = 0; i < LastShiftBodyDeform.SlotCount; i++)
                        frameDepth = Mathf.Max(frameDepth, Mathf.Abs(deform.DepthOf(i)));
                    timeline.AppendLine($"{written},{frameDepth.ToString("F4", CultureInfo.InvariantCulture)}");
                    if (frameDepth > deepestDent - 0.0001f && frameDepth > 0.001f) deepestFrame = written;

                    closestApproach = Mathf.Min(closestApproach,
                        Vector3.Distance(ball.transform.position, belly.position));
                }

                pelvisEnd = ragdoll.Root.position;
                ballEnd = ball.transform.position;
            }
            finally
            {
                if (ball != null) UnityEngine.Object.DestroyImmediate(ball);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (sideObject != null) UnityEngine.Object.DestroyImmediate(sideObject);
                UnityEngine.Physics.simulationMode = previousMode;
            }

            File.WriteAllText(Path.Combine(OutputDirectory, "dent-depth.csv"), timeline.ToString());
            Debug.Log($"[LAST_SHIFT_DEFORM_VIDEO] deepestFrame={deepestFrame} " +
                      $"frames={written} front={frontDirectory} side={sideDirectory} " +
                      $"playbackFps=30 physicsFps=60 slowMotion=0.5x " +
                      $"impactFrame={impactFrame} slots={slotsSeen} " +
                      $"deepestDent={deepestDent.ToString("F4", CultureInfo.InvariantCulture)}m " +
                      $"closestApproach={closestApproach.ToString("F3", CultureInfo.InvariantCulture)}m " +
                      $"pelvisMoved={Vector3.Distance(pelvisStart, pelvisEnd).ToString("F3", CultureInfo.InvariantCulture)}m " +
                      $"ballEnd={ballEnd.ToString("F2")} " +
                      $"result={(slotsSeen > 0 && deepestDent > 0.001f ? "PASS" : "FAIL")}");
        }

        /// <summary>
        /// 공이 몸에 파고든 것을 <see cref="LastShiftRagdollContactRelay"/> 로 넣는다.
        ///
        /// <b>왜 충돌 콜백을 안 쓰나.</b> 에디터의 <c>Physics.Simulate</c> 는 물리는 정확히 돌리지만
        /// <c>OnCollisionEnter</c> 같은 MonoBehaviour 메시지를 <b>보내지 않는다</b> — 실측으로 이
        /// 장면에서 공이 튕겨 나가고 골반이 0.286m 밀렸는데도 표현층 슬롯은 0이었다.
        /// 그래서 캡처에서는 <b>메시지 전달만</b> 관통 조회로 대신한다. 접촉점·법선·충격량은
        /// 콜백이 줬을 값과 같은 것을 쓴다(충격량은 공의 운동량 변화 = <c>collision.impulse</c>).
        /// 릴레이부터 셰이더까지의 경로는 게임에서 도는 것과 같은 코드다.
        /// </summary>
        private static void RelayBallContact(
            LastShiftRagdoll ragdoll,
            Collider ballCollider,
            Rigidbody ballBody,
            Vector3 velocityBefore)
        {
            var impulse = BallMass * (velocityBefore - ballBody.linearVelocity).magnitude;
            if (impulse <= 0.0001f) return;

            for (var i = 0; i < ragdoll.Colliders.Count; i++)
            {
                var part = ragdoll.Colliders[i];
                if (part == null) continue;
                if (!UnityEngine.Physics.ComputePenetration(
                        ballCollider, ballCollider.transform.position, ballCollider.transform.rotation,
                        part, part.transform.position, part.transform.rotation,
                        out var direction, out var distance)) continue;

                var relay = part.GetComponentInParent<LastShiftRagdollContactRelay>();
                if (relay == null) continue;

                // direction 은 공을 밀어내야 할 방향, 곧 몸 표면의 바깥 법선이다.
                // 접촉점은 공 중심에서 그 반대로 반지름만큼 들어간 자리다.
                var point = ballCollider.transform.position - direction * (BallRadius - distance * 0.5f);
                relay.ReportContact(point, direction, impulse);
                return;
            }
        }

        /// <summary>
        /// 화면에 눌림이 안 나올 때 어디가 끊겼는지 찍는다. 셰이더가 맞는지, 프로퍼티 블록이
        /// 렌더러까지 갔는지, 값이 0 이 아닌지 — 셋 중 하나만 어긋나도 그림은 똑같이 멀쩡해 보인다.
        /// </summary>
        private static void LogRenderState(LastShiftBodyDeform deform, GameObject subject)
        {
            var block = new MaterialPropertyBlock();
            foreach (var skin in LastShiftCrewBody.Renderers(subject.transform))
            {
                skin.GetPropertyBlock(block);
                var positions = block.GetVectorArray("_LSDeformPosition");
                var normals = block.GetVectorArray("_LSDeformNormal");
                var shaders = string.Join("|", System.Array.ConvertAll(
                    skin.sharedMaterials, m => m == null ? "null" : m.shader.name));
                Debug.Log($"[LAST_SHIFT_DEFORM_STATE] renderer={skin.name} shaders={shaders} " +
                          $"blockEmpty={block.isEmpty} count={block.GetFloat("_LSDeformCount")} " +
                          $"slot0pos={(positions != null && positions.Length > 0 ? positions[0].ToString("F3") : "none")} " +
                          $"slot0nrm={(normals != null && normals.Length > 0 ? normals[0].ToString("F3") : "none")} " +
                          $"activeSlots={deform.ActiveSlots} rendererCount={deform.RendererCount}");
            }
        }

        /// <summary>
        /// 메시가 찢어졌는지 <b>숫자로</b> 남긴다. 스킨 바운즈가 충돌 뒤에 비정상적으로 커지면
        /// 일부 뼈만 제자리에 남아 메시가 늘어난 것이고, 주요 뼈의 월드 위치를 같이 찍으면
        /// 어느 뼈가 안 따라왔는지가 바로 보인다 — 화면만 보고는 "좀 이상하다" 까지밖에 못 간다.
        /// </summary>
        private static void LogSkeletonState(GameObject subject, string label)
        {
            foreach (var skin in LastShiftCrewBody.Renderers(subject.transform))
            {
                var bounds = skin.bounds;
                Debug.Log($"[LAST_SHIFT_DEFORM_SKELETON] phase={label} renderer={skin.name} " +
                          $"boundsCenter={bounds.center.ToString("F3")} boundsSize={bounds.size.ToString("F3")}");
            }

            foreach (var boneName in new[]
                     {
                         "DEF-spine", "DEF-spine.003", "DEF-spine.006",
                         "DEF-shoulder.L", "DEF-breast.L", "DEF-pelvis.L",
                         "DEF-upper_arm.L", "DEF-thigh.L"
                     })
            {
                var bone = System.Array.Find(
                    subject.GetComponentsInChildren<Transform>(true), t => t.name == boneName);
                if (bone == null) continue;
                Debug.Log($"[LAST_SHIFT_DEFORM_SKELETON] phase={label} bone={boneName} " +
                          $"pos={bone.position.ToString("F3")} " +
                          $"rot={bone.rotation.eulerAngles.ToString("F1")} " +
                          $"parent={(bone.parent != null ? bone.parent.name : "none")}");
            }
        }

        private static GameObject BuildBall(Vector3 position, Vector3 velocity)
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "ImpactBall";
            ball.transform.position = position;
            ball.transform.localScale = Vector3.one * (BallRadius * 2f);

            var material = AssetDatabase.LoadAssetAtPath<Material>(LastShiftRagdollLabScene.PropMaterialPath);
            if (material != null) ball.GetComponent<Renderer>().sharedMaterial = material;

            var body = ball.AddComponent<Rigidbody>();
            body.mass = BallMass;
            body.useGravity = false;
            body.linearDamping = 0f;
            body.angularDamping = 0.05f;
            // 빠른 작은 구는 한 스텝에 콜라이더를 통과한다. 통과하면 충돌 자체가 안 일어난다.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = velocity;
            return ball;
        }

        private static string CommandLineValue(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == key) return args[i + 1];
            return null;
        }

        private static Camera MakeCamera(GameObject host)
        {
            var camera = host.AddComponent<Camera>();
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 40f;
            camera.allowHDR = false;
            return camera;
        }

        private static void WriteFrame(Camera camera, string path)
        {
            var frame = Render(camera, FrameWidth, FrameHeight);
            File.WriteAllBytes(path, frame.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(frame);
        }

        private static Texture2D Render(Camera camera, int width, int height)
        {
            var target = RenderTexture.GetTemporary(width, height, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;

            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            return texture;
        }
    }
}
