using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text;
using DoodleUp.Runtime;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 래그돌 프로토타입의 증거를 헤드리스로 뽑는다 — 연속 캡처(콘택트 시트)와 정지 시각 CSV.
    ///
    /// <b>왜 에디터에서 물리를 손으로 돌리나.</b> 플레이 모드로 띄우면 캡처마다 도메인 리로드와
    /// 창 띄우기를 물게 되고, 무엇보다 <b>같은 초에 같은 그림</b>이 안 나온다. 여기서는
    /// <c>Physics.simulationMode</c> 를 스크립트로 돌려 1/60 씩 직접 밟기 때문에 다섯 시나리오가
    /// 전부 같은 시간축 위에 놓인다 — 중력 비교(화성 vs 지구)와 튜닝 비교(목표 vs 대조군)는
    /// 시간축이 같아야만 비교가 된다.
    ///
    /// 물리 스텝은 <see cref="LastShiftRagdoll.StepPhysics"/> 를 그대로 부른다. 플레이에서
    /// <c>FixedUpdate</c> 가 부르는 것과 같은 함수라, 캡처가 플레이와 다른 물리를 도는 일이 없다.
    /// </summary>
    public static class LastShiftRagdollCapture
    {
        private const string OutputDirectory = "docs/tech/evidence/last-shift-ragdoll-prototype-2026-08-14";

        private const float StepSeconds = 1f / 60f;

        /// <summary>정지 판정을 기다려 주는 상한. 대조군이 여기까지 안 멈추면 "안 멈춘다"가 확정된다.</summary>
        private const float MeasureSeconds = 8f;

        /// <summary>콘택트 시트에 담는 프레임 간격과 장수. 0.3초 × 10 = 첫 2.7초.</summary>
        private const float FrameInterval = 0.25f;
        private const int FrameCount = 10;
        private const int SheetColumns = 5;

        private const int TileWidth = 480;
        private const int TileHeight = 270;
        private const int HeroWidth = 960;
        private const int HeroHeight = 540;

        /// <summary>대표 한 장을 뽑는 시각. 첫 튕김이 가장 크게 벌어져 있는 무렵이다.</summary>
        private const float HeroSeconds = 0.6f;

        public static void CaptureForAutomation()
        {
            Directory.CreateDirectory(OutputDirectory);

            var scenarios = new[]
            {
                new Scenario("A_bodycheck_mars_comic", Impulse.BodyCheck, false, false,
                    "R-1 문 앞 충돌 · 선내 저중력 · 목표 튜닝"),
                new Scenario("B_headflick_mars_comic", Impulse.HeadFlick, false, false,
                    "머리만 튕기기 · 목 관절이 얼마나 덜렁거리는가"),
                new Scenario("C_blast_mars_comic", Impulse.Blast, false, false,
                    "R-3 운석 충격 · 승무원도 날아가는가"),
                new Scenario("D_bodycheck_earth_comic", Impulse.BodyCheck, true, false,
                    "같은 충돌 · 지구 중력 대조군"),
                new Scenario("E_bodycheck_mars_wizard", Impulse.BodyCheck, false, true,
                    "같은 충돌 · Wizard 기본 튜닝(정지 판정 없음) 대조군")
            };

            var csv = new StringBuilder();
            csv.AppendLine("scenario,time_s,com_mps,max_linear_mps,max_angular_radps,pelvis_y,head_y,settled");

            var summary = new List<string>();
            var previousMode = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;

            try
            {
                foreach (var scenario in scenarios)
                    summary.Add(Run(scenario, csv));
            }
            finally
            {
                UnityEngine.Physics.simulationMode = previousMode;
            }

            File.WriteAllText(Path.Combine(OutputDirectory, "ragdoll-settle.csv"), csv.ToString());

            foreach (var line in summary) Debug.Log($"[LAST_SHIFT_RAGDOLL_CAPTURE] {line}");
            Debug.Log($"[LAST_SHIFT_RAGDOLL_CAPTURE] scenarios={scenarios.Length} output={OutputDirectory} result=PASS");
        }

        private static string Run(Scenario scenario, StringBuilder csv)
        {
            LastShiftRagdollLabScene.Build();

            var subject = GameObject.Find("RagdollSubject");
            if (subject == null) throw new InvalidOperationException("테스트맵에 RagdollSubject 가 없다.");

            var lab = subject.GetComponent<LastShiftRagdollLab>();
            if (lab != null) UnityEngine.Object.DestroyImmediate(lab); // 에디터에서는 입력 루프가 안 돈다.

            var ragdoll = subject.GetComponent<LastShiftRagdoll>();
            var tuning = scenario.WizardTuning
                ? LastShiftRagdollTuning.WizardDefault()
                : LastShiftRagdollTuning.Comic();
            if (scenario.EarthGravity) tuning = tuning.WithEarthGravity();

            ragdoll.Build(tuning);

            // 첫 스텝 전에 자세를 한 번 굳혀 둔다 — 빌드 직후 콜라이더가 서로 밀어내는 프레임이
            // 캡처 첫 장에 들어가면 "충돌 때문에 흐트러진 것"과 구분이 안 된다.
            UnityEngine.Physics.Simulate(StepSeconds);
            ragdoll.ResetToRestPose();

            ApplyScenarioImpulse(ragdoll, subject.transform, scenario.Impulse, tuning);

            var cameraObject = new GameObject("RagdollCaptureCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 60f;
            camera.allowHDR = false;

            var tiles = new List<Texture2D>();
            var pelvis = ragdoll.Bodies[LastShiftRagdollPart.Pelvis].transform;
            var head = ragdoll.Bodies[LastShiftRagdollPart.Head].transform;

            var elapsed = 0f;
            var nextFrame = 0f;
            var nextSample = 0f;
            var heroTaken = false;
            var totalSteps = Mathf.CeilToInt(MeasureSeconds / StepSeconds);

            // 정지 판정을 끈 대조군도 같은 자로 재야 비교가 된다. 튜닝의 SettleEnabled 와 무관하게
            // 목표 튜닝의 임계로 "실제로 조용해진 시각"을 따로 잰다 —
            // 대조군이 안 멈춘 게 물리 때문인지 판정 로직이 없어서인지를 여기서 가른다.
            var referee = LastShiftRagdollTuning.Comic();
            var passive = new LastShiftRagdollSettle();
            var passiveQuietAt = -1f;

            var startPelvisY = pelvis.position.y;
            var peakPelvisY = startPelvisY;
            var airborneUntil = 0f;
            var distinctFrames = 0;
            var inFrame = false;

            try
            {
                for (var step = 0; step <= totalSteps; step++)
                {
                    // 카메라를 매 프레임 골반에 맞춘다. 안 하면 밀려 떠간 승무원이 화면 끝에서
                    // 점이 돼 정작 봐야 할 부위별 반응이 안 보인다.
                    LastShiftRagdollLab.FrameSubject(camera, pelvis.position);

                    if (tiles.Count < FrameCount && elapsed >= nextFrame - 0.0001f)
                    {
                        tiles.Add(Render(camera, TileWidth, TileHeight));
                        nextFrame += FrameInterval;
                    }

                    if (!heroTaken && elapsed >= HeroSeconds - 0.0001f)
                    {
                        var hero = Render(camera, HeroWidth, HeroHeight);
                        File.WriteAllBytes(Path.Combine(OutputDirectory, scenario.Name + "_hero.png"), hero.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(hero);
                        heroTaken = true;
                    }

                    if (elapsed >= nextSample - 0.0001f)
                    {
                        csv.AppendLine(string.Join(",",
                            scenario.Name,
                            elapsed.ToString("F3", CultureInfo.InvariantCulture),
                            ragdoll.CenterOfMassSpeed.ToString("F4", CultureInfo.InvariantCulture),
                            ragdoll.MaxLinearSpeed.ToString("F4", CultureInfo.InvariantCulture),
                            ragdoll.MaxAngularSpeed.ToString("F4", CultureInfo.InvariantCulture),
                            pelvis.position.y.ToString("F4", CultureInfo.InvariantCulture),
                            head.position.y.ToString("F4", CultureInfo.InvariantCulture),
                            ragdoll.IsSettled ? "1" : "0"));
                        nextSample += 0.1f;
                    }

                    ragdoll.StepPhysics(StepSeconds);

                    if (passiveQuietAt < 0f
                        && passive.Step(ragdoll.MaxLinearSpeed, ragdoll.MaxAngularSpeed, StepSeconds, referee))
                        passiveQuietAt = elapsed;

                    peakPelvisY = Mathf.Max(peakPelvisY, pelvis.position.y);
                    if (pelvis.position.y > startPelvisY + 0.05f) airborneUntil = elapsed;

                    UnityEngine.Physics.Simulate(StepSeconds);
                    elapsed += StepSeconds;
                }

                WriteContactSheet(scenario, tiles);

                // 프레임이 전부 같은 그림이면 캡처는 성공한 척하면서 아무것도 안 담은 것이다.
                // 실제로 한 번 그렇게 나왔고(다섯 시나리오의 PNG 가 바이트 단위로 동일했다),
                // 파일이 생겼다는 사실만으로는 안 걸린다 — 그래서 여기서 세어 둔다.
                distinctFrames = tiles.Select(Checksum).Distinct().Count();
                var headScreen = camera.WorldToViewportPoint(head.position);
                inFrame = headScreen.z > 0f
                          && headScreen.x > 0f && headScreen.x < 1f
                          && headScreen.y > 0f && headScreen.y < 1f;
            }
            finally
            {
                foreach (var tile in tiles) UnityEngine.Object.DestroyImmediate(tile);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }

            var settled = ragdoll.SettledAtSeconds >= 0f
                ? $"settle={ragdoll.SettledAtSeconds:F2}s"
                : $"settle=NONE(>{MeasureSeconds:F0}s)";
            var quiet = passiveQuietAt >= 0f
                ? $"quietAt={passiveQuietAt:F2}s"
                : $"quietAt=NONE(>{MeasureSeconds:F0}s)";

            return $"scenario={scenario.Name} height={ragdoll.StandingHeight:F2}m " +
                   $"gravity={tuning.GravityY:F2} angularDamping={tuning.AngularDamping:F2} " +
                   $"settleLogic={(tuning.SettleEnabled ? "on" : "off")} {settled} {quiet} " +
                   $"airborne={airborneUntil:F2}s peakRise={(peakPelvisY - startPelvisY):F2}m " +
                   $"frames={distinctFrames}/{tiles.Count} headInFrame={inFrame} " +
                   $"finalLinear={ragdoll.MaxLinearSpeed:F3} finalAngular={ragdoll.MaxAngularSpeed:F3} " +
                   $"— {scenario.Note}";
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
                case Impulse.Blast:
                    ragdoll.ApplyBlast(
                        subject.position + LastShiftRagdollLab.DefaultBlastOrigin,
                        tuning.BlastImpulse,
                        tuning.BlastRadius);
                    break;
            }
        }

        /// <summary>프레임이 실제로 달라지는지만 보면 되므로 픽셀 합으로 충분하다.</summary>
        private static int Checksum(Texture2D frame)
        {
            var pixels = frame.GetPixels32();
            var hash = 17;
            for (var i = 0; i < pixels.Length; i += 37)
                hash = hash * 31 + (pixels[i].r << 16 | pixels[i].g << 8 | pixels[i].b);
            return hash;
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

        /// <summary>
        /// 프레임 열 장을 한 장에 붙인다. 시간이 흐르는 걸 보려면 장을 여러 개 열어 비교해야 하는데,
        /// 한 장이면 <b>펼쳐 놓고 한눈에</b> 읽힌다 — 검수 비용이 그림 장수에 비례하기 때문이다.
        /// </summary>
        private static void WriteContactSheet(Scenario scenario, List<Texture2D> tiles)
        {
            if (tiles.Count == 0) return;

            var rows = Mathf.CeilToInt(tiles.Count / (float)SheetColumns);
            var sheet = new Texture2D(TileWidth * SheetColumns, TileHeight * rows, TextureFormat.RGB24, false);

            var blank = new Color[TileWidth * TileHeight];
            for (var i = 0; i < blank.Length; i++) blank[i] = Color.black;
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < SheetColumns; column++)
                sheet.SetPixels(column * TileWidth, row * TileHeight, TileWidth, TileHeight, blank);

            for (var i = 0; i < tiles.Count; i++)
            {
                var column = i % SheetColumns;
                var row = i / SheetColumns;
                // 텍스처 원점이 왼쪽 아래라, 시간이 위에서 아래로 흐르게 하려면 행을 뒤집어 놓는다.
                var y = (rows - 1 - row) * TileHeight;
                sheet.SetPixels(column * TileWidth, y, TileWidth, TileHeight, tiles[i].GetPixels());
            }

            sheet.Apply();
            File.WriteAllBytes(Path.Combine(OutputDirectory, scenario.Name + "_sheet.png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
        }

        private enum Impulse
        {
            BodyCheck,
            HeadFlick,
            Blast
        }

        private readonly struct Scenario
        {
            public Scenario(string name, Impulse impulse, bool earthGravity, bool wizardTuning, string note)
            {
                Name = name;
                Impulse = impulse;
                EarthGravity = earthGravity;
                WizardTuning = wizardTuning;
                Note = note;
            }

            public string Name { get; }
            public Impulse Impulse { get; }
            public bool EarthGravity { get; }
            public bool WizardTuning { get; }
            public string Note { get; }
        }
    }
}
