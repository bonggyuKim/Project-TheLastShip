using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DoodleUp.Runtime;
using UnityEngine;
using UnityEditor;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 격리 랩 캡처. <b>움직이는 것은 공뿐이다.</b>
    ///
    /// 캐릭터에는 물리가 없으므로 뼈 월드 행렬이 충돌 전후로 <b>같아야 한다</b>. 그것을 같이
    /// 재서 로그로 남긴다 — 화면만 보면 "안 움직인 것 같다" 까지밖에 못 가고, 그 애매함 때문에
    /// 앞선 판정이 여러 번 갈렸다.
    /// </summary>
    public static class LastShiftDeformOnlyCapture
    {
        public const string OutputDirectory = "docs/tech/evidence/last-shift-deform-only-2026-08-18";

        private const float StepSeconds = 1f / 60f;
        private const float DurationSeconds = 2.4f;

        private const float BallRadius = 0.12f;
        private const float BallMass = 3.0f;
        private const float BallSpeed = 5.0f;
        private const float BallStartDistance = 1.15f;

        private const int FrameWidth = 1280;
        private const int FrameHeight = 720;

        [MenuItem("Last Shift/Prototype/Capture Deform Only Video")]
        public static void CaptureForAutomation()
        {
            var deformEnabled = CommandLineValue("-deform") != "off";
            var suffix = deformEnabled ? "" : "_nodeform";
            var frontDirectory = Path.Combine(OutputDirectory, "frames_front" + suffix);
            var angledDirectory = Path.Combine(OutputDirectory, "frames_angled" + suffix);
            foreach (var directory in new[] { frontDirectory, angledDirectory })
            {
                Directory.CreateDirectory(directory);
                foreach (var stale in Directory.GetFiles(directory, "frame_*.png")) File.Delete(stale);
            }

            LastShiftDeformLabScene.Build(out var crew);
            var deform = crew.GetComponent<LastShiftBodyDeform>();
            deform.CollectRenderers();

            var belly = FindBone(crew, LastShiftRagdollRig.SpineBoneName);
            if (belly == null) throw new InvalidOperationException("배 뼈를 못 찾았다.");

            // 충돌 전 뼈 상태. 이 값이 끝까지 안 바뀌어야 "몸은 안 움직였다" 가 성립한다.
            var before = SnapshotBones(crew);
            var boundsBefore = LastShiftCrewBody.Renderers(crew.transform)[0].bounds;

            var previousMode = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;

            GameObject ball = null;
            GameObject frontObject = null;
            GameObject angledObject = null;
            var written = 0;
            var deepestDent = 0f;
            var deepestFrame = -1;
            var impactFrame = -1;
            var timeline = new System.Text.StringBuilder("frame,depth" + Environment.NewLine);

            try
            {
                var facing = Vector3.forward;
                var target = belly.position;
                ball = BuildBall(target + facing * BallStartDistance, -facing * BallSpeed);
                var ballBody = ball.GetComponent<Rigidbody>();
                var ballCollider = ball.GetComponent<Collider>();

                frontObject = new GameObject("DeformCameraFront");
                var front = MakeCamera(frontObject);
                angledObject = new GameObject("DeformCameraAngled");
                var angled = MakeCamera(angledObject);

                // 머리까지 들어와야 자국 크기를 몸과 견줘 읽을 수 있다. 배에 딱 붙이면
                // 화면이 공으로 차서 눌림이 얼마나 깊은지 감이 안 온다.
                var framing = target + Vector3.up * 0.18f;
                Aim(front, framing, Quaternion.AngleAxis(20f, Vector3.up) * facing * 1.75f + Vector3.up * 0.30f);
                Aim(angled, framing, Quaternion.AngleAxis(45f, Vector3.up) * facing * 1.75f + Vector3.up * 0.30f);

                var totalSteps = Mathf.CeilToInt(DurationSeconds / StepSeconds);
                for (var step = 0; step <= totalSteps; step++)
                {
                    var name = "frame_" + written.ToString("D4", CultureInfo.InvariantCulture) + ".png";
                    WriteFrame(front, Path.Combine(frontDirectory, name));
                    WriteFrame(angled, Path.Combine(angledDirectory, name));
                    written++;

                    var velocityBefore = ballBody.linearVelocity;
                    UnityEngine.Physics.Simulate(StepSeconds);
                    if (deformEnabled) RelayBallContact(crew, ballCollider, ballBody, velocityBefore);

                    deform.Step(StepSeconds);
                    deform.PushToRenderers();

                    if (impactFrame < 0 && deform.ActiveSlots > 0) impactFrame = written;
                    var frameDepth = 0f;
                    for (var i = 0; i < LastShiftBodyDeform.SlotCount; i++)
                        frameDepth = Mathf.Max(frameDepth, Mathf.Abs(deform.DepthOf(i)));
                    timeline.AppendLine($"{written},{frameDepth.ToString("F4", CultureInfo.InvariantCulture)}");
                    if (frameDepth > deepestDent) { deepestDent = frameDepth; deepestFrame = written; }
                }
            }
            finally
            {
                if (ball != null) UnityEngine.Object.DestroyImmediate(ball);
                if (frontObject != null) UnityEngine.Object.DestroyImmediate(frontObject);
                if (angledObject != null) UnityEngine.Object.DestroyImmediate(angledObject);
                UnityEngine.Physics.simulationMode = previousMode;
            }

            var moved = CompareBones(crew, before, out var worstBone, out var worstDrift);
            var boundsAfter = LastShiftCrewBody.Renderers(crew.transform)[0].bounds;
            var boundsGrowth = boundsAfter.size.magnitude / Mathf.Max(0.0001f, boundsBefore.size.magnitude);

            File.WriteAllText(Path.Combine(OutputDirectory, "dent-depth" + suffix + ".csv"), timeline.ToString());

            Debug.Log($"[LAST_SHIFT_DEFORM_ONLY] deform={(deformEnabled ? "on" : "off")} frames={written} " +
                      $"impactFrame={impactFrame} deepestFrame={deepestFrame} " +
                      $"deepestDent={deepestDent.ToString("F4", CultureInfo.InvariantCulture)}m " +
                      $"bonesMoved={moved} worstBone={worstBone} " +
                      $"worstDrift={worstDrift.ToString("F6", CultureInfo.InvariantCulture)}m " +
                      $"boundsBefore={boundsBefore.size.ToString("F3")} boundsAfter={boundsAfter.size.ToString("F3")} " +
                      $"boundsGrowth={boundsGrowth.ToString("F3", CultureInfo.InvariantCulture)} " +
                      $"front={frontDirectory} angled={angledDirectory} " +
                      $"result={(moved == 0 ? "PASS" : "FAIL")}");
        }

        /// <summary>
        /// 공이 파고든 것을 릴레이로 넣는다. 에디터의 <c>Physics.Simulate</c> 는 물리는 정확히
        /// 돌리지만 <c>OnCollisionEnter</c> 를 안 보내므로, <b>메시지 전달만</b> 관통 조회로
        /// 대신한다. 접촉점·법선·충격량은 콜백이 줬을 값과 같다.
        /// </summary>
        private static void RelayBallContact(
            GameObject crew, Collider ballCollider, Rigidbody ballBody, Vector3 velocityBefore)
        {
            var impulse = BallMass * (velocityBefore - ballBody.linearVelocity).magnitude;
            if (impulse <= 0.0001f) return;

            foreach (var relay in crew.GetComponentsInChildren<LastShiftRagdollContactRelay>(true))
            {
                var surface = relay.GetComponent<Collider>();
                if (surface == null) continue;
                if (!UnityEngine.Physics.ComputePenetration(
                        ballCollider, ballCollider.transform.position, ballCollider.transform.rotation,
                        surface, surface.transform.position, surface.transform.rotation,
                        out var direction, out var distance)) continue;

                var point = ballCollider.transform.position - direction * (BallRadius - distance * 0.5f);
                relay.ReportContact(point, direction, impulse);
                return;
            }
        }

        private static Dictionary<Transform, Matrix4x4> SnapshotBones(GameObject crew)
        {
            var snapshot = new Dictionary<Transform, Matrix4x4>();
            foreach (var bone in crew.GetComponentsInChildren<Transform>(true))
                snapshot[bone] = bone.localToWorldMatrix;
            return snapshot;
        }

        private static int CompareBones(
            GameObject crew, Dictionary<Transform, Matrix4x4> before, out string worstBone, out float worstDrift)
        {
            worstBone = "none";
            worstDrift = 0f;
            var moved = 0;

            foreach (var pair in before)
            {
                if (pair.Key == null) continue;
                var drift = (pair.Key.localToWorldMatrix.GetColumn(3) - pair.Value.GetColumn(3)).magnitude;
                if (drift > worstDrift) { worstDrift = drift; worstBone = pair.Key.name; }
                if (drift > 0.0005f) moved++;
            }
            return moved;
        }

        private static GameObject BuildBall(Vector3 position, Vector3 velocity)
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "ImpactBall";
            ball.transform.position = position;
            ball.transform.localScale = Vector3.one * (BallRadius * 2f);

            var material = AssetDatabase.LoadAssetAtPath<Material>(LastShiftDeformLabScene.PropMaterialPath);
            if (material != null) ball.GetComponent<Renderer>().sharedMaterial = material;

            var body = ball.AddComponent<Rigidbody>();
            body.mass = BallMass;
            body.useGravity = false;
            body.linearDamping = 0f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = velocity;
            return ball;
        }

        private static Transform FindBone(GameObject crew, string boneName) =>
            Array.Find(crew.GetComponentsInChildren<Transform>(true), bone => bone.name == boneName);

        private static void Aim(Camera camera, Vector3 target, Vector3 offset)
        {
            camera.transform.position = target + offset;
            camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, Vector3.up);
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
            var target = RenderTexture.GetTemporary(FrameWidth, FrameHeight, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;

            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            var texture = new Texture2D(FrameWidth, FrameHeight, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, FrameWidth, FrameHeight), 0, 0);
            texture.Apply();

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);

            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static string CommandLineValue(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == key) return args[i + 1];
            return null;
        }
    }
}
