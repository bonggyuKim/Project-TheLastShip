using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DoodleUp.Runtime;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 래그돌 프로토타입을 <b>연속 프레임</b>으로 뽑는다. 영상으로 합쳐 보기 위한 것이다.
    ///
    /// <b>왜 <see cref="LastShiftRagdollCapture"/> 와 따로 두나.</b> 그쪽은 검수용이다 —
    /// 0.25초 간격 열 장을 한 판에 붙여 <b>펼쳐 놓고 비교</b>하는 것이 목적이라, 다섯 시나리오를
    /// 한 번에 돌고 CSV 까지 쓴다. 여기는 <b>움직임 자체</b>를 보는 것이 목적이라 간격이 촘촘하고
    /// (30fps) 시나리오가 적다. 둘의 프레임 간격과 장수를 한 상수로 묶으면 한쪽을 고칠 때마다
    /// 다른 쪽 산출물이 조용히 바뀐다.
    ///
    /// 물리는 그쪽과 같은 <see cref="LastShiftRagdoll.StepPhysics"/> 를
    /// <see cref="SimulationMode.Script"/> 로 직접 밟는다. 캡처가 플레이와 다른 물리를 도는
    /// 일이 없고, 프레임 간격이 실행 속도에 흔들리지도 않는다 — 영상은 시간축이 고르지 않으면
    /// 그 자체로 물리가 튄 것처럼 보인다.
    /// </summary>
    public static class LastShiftRagdollVideoCapture
    {
        private const string OutputDirectory =
            "docs/tech/evidence/last-shift-ragdoll-prototype-2026-08-14/video";

        private const float StepSeconds = 1f / 60f;

        /// <summary>담는 길이. 목표 튜닝의 정지 시각을 넘겨야 "멈추는 것"까지 영상에 들어간다.</summary>
        private const float DurationSeconds = 5f;

        /// <summary>물리 스텝 몇 번마다 한 장 담는가. 2 면 60Hz 물리에 30fps 영상이다.</summary>
        private const int StepsPerFrame = 2;

        private const int FrameWidth = 960;
        private const int FrameHeight = 540;

        public static void CaptureForAutomation()
        {
            var scenarios = new[]
            {
                new Scenario("B_headflick_mars_comic", Impulse.HeadFlick,
                    "머리만 튕기기 · 목 관절이 얼마나 덜렁거리는가"),
                new Scenario("A_bodycheck_mars_comic", Impulse.BodyCheck,
                    "R-1 문 앞 충돌 · 선내 저중력 · 목표 튜닝")
            };

            var summary = new List<string>();
            var previousMode = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;

            try
            {
                foreach (var scenario in scenarios)
                    summary.Add(Run(scenario));
            }
            finally
            {
                UnityEngine.Physics.simulationMode = previousMode;
            }

            foreach (var line in summary) Debug.Log($"[LAST_SHIFT_RAGDOLL_VIDEO] {line}");
            Debug.Log($"[LAST_SHIFT_RAGDOLL_VIDEO] scenarios={scenarios.Length} " +
                      $"fps={60 / StepsPerFrame} output={OutputDirectory} result=PASS");
        }

        private static string Run(Scenario scenario)
        {
            var frameDirectory = Path.Combine(OutputDirectory, scenario.Name);
            Directory.CreateDirectory(frameDirectory);

            // 같은 이름으로 다시 뽑을 때 이전 회차의 남은 프레임이 뒤에 붙으면, 영상 끝에
            // 지난번 움직임이 이어져 나온다. 파일이 덮이는 것만으로는 안 지워진다.
            foreach (var stale in Directory.GetFiles(frameDirectory, "frame_*.png")) File.Delete(stale);

            LastShiftRagdollLabScene.Build();

            var subject = GameObject.Find("RagdollSubject");
            if (subject == null) throw new InvalidOperationException("테스트맵에 RagdollSubject 가 없다.");

            var lab = subject.GetComponent<LastShiftRagdollLab>();
            if (lab != null) UnityEngine.Object.DestroyImmediate(lab); // 에디터에서는 입력 루프가 안 돈다.

            var ragdoll = subject.GetComponent<LastShiftRagdoll>();
            var tuning = LastShiftRagdollTuning.Comic();
            ragdoll.Build(tuning);

            // 빌드 직후 콜라이더가 서로 밀어내는 프레임이 첫 장에 들어가면 충격 반응과 구분이 안 된다.
            UnityEngine.Physics.Simulate(StepSeconds);
            ragdoll.ResetToRestPose();

            ApplyScenarioImpulse(ragdoll, subject.transform, scenario.Impulse, tuning);

            var cameraObject = new GameObject("RagdollVideoCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 60f;
            camera.allowHDR = false;

            var pelvis = ragdoll.Bodies[LastShiftRagdollPart.Pelvis].transform;
            var head = ragdoll.Bodies[LastShiftRagdollPart.Head].transform;

            var totalSteps = Mathf.CeilToInt(DurationSeconds / StepSeconds);
            var written = 0;
            var lostFrames = 0;
            var elapsed = 0f;

            try
            {
                for (var step = 0; step <= totalSteps; step++)
                {
                    // 카메라를 매 프레임 골반에 맞춘다. 안 하면 밀려 떠간 승무원이 화면 끝에서
                    // 점이 돼 정작 봐야 할 부위별 반응이 안 보인다.
                    LastShiftRagdollLab.FrameSubject(camera, pelvis.position);

                    if (step % StepsPerFrame == 0)
                    {
                        var frame = Render(camera, FrameWidth, FrameHeight);
                        var path = Path.Combine(frameDirectory,
                            "frame_" + written.ToString("D4", CultureInfo.InvariantCulture) + ".png");
                        File.WriteAllBytes(path, frame.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(frame);
                        written++;

                        // 머리가 화면 밖으로 나간 장이 몇이나 되는지 세어 둔다. 영상은 "찍히긴 했는데
                        // 볼 게 안 담긴" 실패가 파일 존재만으로는 안 걸린다.
                        var headScreen = camera.WorldToViewportPoint(head.position);
                        var inFrame = headScreen.z > 0f
                                      && headScreen.x > 0f && headScreen.x < 1f
                                      && headScreen.y > 0f && headScreen.y < 1f;
                        if (!inFrame) lostFrames++;
                    }

                    ragdoll.StepPhysics(StepSeconds);
                    UnityEngine.Physics.Simulate(StepSeconds);
                    elapsed += StepSeconds;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }

            var settled = ragdoll.SettledAtSeconds >= 0f
                ? $"settle={ragdoll.SettledAtSeconds:F2}s"
                : $"settle=NONE(>{DurationSeconds:F0}s)";

            return $"scenario={scenario.Name} frames={written} fps={60 / StepsPerFrame} " +
                   $"duration={elapsed:F2}s {settled} headOutOfFrame={lostFrames} " +
                   $"dir={frameDirectory} — {scenario.Note}";
        }

        private static void ApplyScenarioImpulse(
            LastShiftRagdoll ragdoll, Transform subject, Impulse impulse, LastShiftRagdollTuning tuning)
        {
            var direction = tuning.ImpactDirection(LastShiftRagdollLab.DefaultImpactHeading);
            switch (impulse)
            {
                case Impulse.BodyCheck:
                    ragdoll.ApplyVelocityChange(direction * tuning.BodyCheckSpeed);
                    ragdoll.ApplyImpulse(LastShiftRagdollPart.Chest, direction * tuning.BodyCheckSnapImpulse);
                    break;
                case Impulse.HeadFlick:
                    ragdoll.ApplyImpulse(LastShiftRagdollPart.Head, direction * tuning.HeadFlickImpulse);
                    break;
            }
        }

        private static Texture2D Render(Camera camera, int width, int height)
        {
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;

            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;

            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();

            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            return image;
        }

        private enum Impulse
        {
            BodyCheck,
            HeadFlick
        }

        private readonly struct Scenario
        {
            public Scenario(string name, Impulse impulse, string note)
            {
                Name = name;
                Impulse = impulse;
                Note = note;
            }

            public string Name { get; }
            public Impulse Impulse { get; }
            public string Note { get; }
        }
    }
}
