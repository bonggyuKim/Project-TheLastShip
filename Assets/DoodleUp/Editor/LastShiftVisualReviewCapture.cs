using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DoodleUp.Editor
{
    public static class LastShiftVisualReviewCapture
    {
        private const string OutputDirectory = "docs/art/evidence/last-shift-rebake-visual-review-2026-08-14";
        private const string CeilingOutputDirectory = "docs/art/evidence/last-shift-ceiling-fix-2026-08-14";
        private const string QuartersOutputDirectory = "docs/art/evidence/last-shift-quarters-single-source-2026-08-14";
        private const string QuartersHeightOutputDirectory = "docs/art/evidence/last-shift-quarters-height-3m-2026-08-14";
        private const string PowerBusPanelOutputDirectory = "docs/art/evidence/last-shift-power-buspanel-2026-08-14";
        private const string QuartersBunkOrientationOutputDirectory = "docs/art/evidence/last-shift-quarters-bunk-orientation-2026-08-14";
        public const string EvidenceV2OutputDirectory = "docs/art/evidence/last-shift-whole-ship-review-v2";
        public const string EvidenceSchema = "last-shift.visual-review/v2";

        /// <summary>
        /// Whole-ship review evidence v2. Every review area has a context and a diagnostic angle,
        /// and manifest.json records the exact camera and source state used for the images.
        /// Existing dated evidence remains untouched so before/after sets cannot be mixed silently.
        /// </summary>
        [MenuItem("Last Shift/Review/Capture Whole Ship Evidence v2")]
        public static void CaptureWholeShipEvidenceV2ForAutomation()
        {
            var views = EvidenceV2Views();
            Run(EvidenceV2OutputDirectory, views, "LAST_SHIFT_VISUAL_REVIEW_V2", false);
            WriteEvidenceManifest(EvidenceV2OutputDirectory, views);
        }

        public static View[] EvidenceV2Views() => new[]
        {
            new View("plaza", "context", "01_plaza_context", new Vector3(5.4f, 1.65f, -4.8f), new Vector3(-0.5f, 1.25f, 0f)),
            new View("plaza", "diagnostic", "02_plaza_core_clearance", new Vector3(-5.4f, 1.65f, 4.8f), new Vector3(0.5f, 1.25f, 0f)),
            new View("cockpit", "context", "03_cockpit_entry", new Vector3(-7.2f, 1.65f, 0f), new Vector3(-15.8f, 1.35f, 0f)),
            new View("cockpit", "diagnostic", "04_cockpit_seat_grounding", new Vector3(-14.2f, 1.35f, 2.8f), new Vector3(-11.4f, 0.45f, 0f)),
            new View("power", "context", "05_power_context", new Vector3(0f, 1.65f, -7.2f), new Vector3(0f, 1.25f, -13f)),
            new View("power", "diagnostic", "06_power_cabinet_grounding", new Vector3(-2.8f, 1.15f, -11f), new Vector3(2.8f, 0.35f, -12.6f)),
            new View("cooling", "context", "07_cooling_context", new Vector3(0f, 1.65f, 7.2f), new Vector3(0f, 1.25f, 13f)),
            new View("cooling", "diagnostic", "08_cooling_box_grounding", new Vector3(3f, 1.1f, 10f), new Vector3(-2.8f, 0.3f, 12f)),
            new View("life_support", "context", "09_life_support_context", new Vector3(7.2f, 1.65f, 0f), new Vector3(15f, 1.25f, 0f)),
            new View("life_support", "diagnostic", "10_life_support_tank_grounding", new Vector3(11f, 1.1f, 3f), new Vector3(14f, 0.35f, -1f)),
            new View("quarters", "context", "11_quarters_entry", new Vector3(5.2f, 1.65f, 7f), new Vector3(10.8f, 1.3f, 10.8f)),
            new View("quarters", "diagnostic", "12_quarters_bunk_profile", new Vector3(5.4f, 1.1f, 10.4f), new Vector3(9.6f, 0.45f, 11.6f)),
            new View("cargo_props", "context", "13_cargo_group", new Vector3(3.2f, 1.65f, 12.2f), new Vector3(-2.5f, 0.9f, 9.5f)),
            new View("cargo_props", "diagnostic", "14_cargo_grounding", new Vector3(-3.5f, 1.05f, 8.2f), new Vector3(1f, 0.25f, 11f)),
            new View("eva", "context", "15_eva_exterior", new Vector3(4.8f, 7.8f, 4.8f), new Vector3(0f, 6.2f, 0f)),
            new View("eva", "diagnostic", "16_eva_lift_clearance", new Vector3(-1.2f, 1.65f, -5.2f), new Vector3(0f, 2.2f, 0f))
        };

        private static void WriteEvidenceManifest(string outputDirectory, View[] views)
        {
            var manifest = new EvidenceManifest
            {
                schema = EvidenceSchema,
                evidenceVersion = 2,
                scene = LastShiftSceneBuilder.ScenePath,
                unityVersion = Application.unityVersion,
                sourceRevision = ResolveSourceRevision(),
                sourceDirty = ResolveSourceDirty(),
                capturedUtc = DateTime.UtcNow.ToString("O"),
                width = 1280,
                height = 720,
                verticalFov = 68f,
                nearClip = 0.05f,
                farClip = 80f,
                reviewState = new ReviewState
                {
                    rebakeApplied = true,
                    ceilingShellApplied = true,
                    quartersSingleSourceApplied = true,
                    quartersHeightMeters = 3f,
                    powerBusPanelSeparated = true,
                    quartersBunksUpright = true
                },
                views = views.Select(view => new EvidenceView
                {
                    area = view.Area,
                    purpose = view.Purpose,
                    file = view.Name + ".png",
                    position = VectorValues(view.Position),
                    target = VectorValues(view.Target)
                }).ToArray()
            };
            File.WriteAllText(Path.Combine(outputDirectory, "manifest.json"), JsonUtility.ToJson(manifest, true), Encoding.UTF8);
        }

        private static float[] VectorValues(Vector3 value) => new[] { value.x, value.y, value.z };

        private static string ResolveSourceRevision() => RunGit("rev-parse --verify HEAD", "unknown");
        private static bool ResolveSourceDirty() => !string.IsNullOrEmpty(RunGit("status --porcelain --untracked-files=no", string.Empty));

        private static string RunGit(string arguments, string fallback)
        {
            try
            {
                var start = new System.Diagnostics.ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var process = System.Diagnostics.Process.Start(start))
                {
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(5000);
                    return process.ExitCode == 0 ? output : fallback;
                }
            }
            catch
            {
                return fallback;
            }
        }

        /// <summary>
        /// 천장 검수 시점. 앞서의 검수 시점은 전부 <b>수평</b>이라 천장이 없어도 화면에
        /// 안 잡혔다 — 별 배경이 프레임 위쪽에 걸쳐 보인 것으로 겨우 드러났다. 그래서
        /// 여기서는 방마다 <b>올려다보는</b> 시점 하나를 따로 잡고, 마지막에 배 전체를
        /// 위에서 한 장 찍는다(지붕이 뚫려 있으면 그 한 장에 전부 보인다).
        /// </summary>
        public static void CaptureCeilingsForAutomation()
        {
            var views = new[]
            {
                new View("01_plaza_up", new Vector3(4.2f, 1.65f, 4.2f), new Vector3(1.6f, 3.3f, 1.6f)),
                new View("02_cockpit_up", new Vector3(-11f, 1.65f, 0f), new Vector3(-13.4f, 3.3f, 0f)),
                new View("03_power_up", new Vector3(0f, 1.65f, -10f), new Vector3(0f, 3.3f, -12.4f)),
                new View("04_cooling_up", new Vector3(0f, 1.65f, 10f), new Vector3(0f, 3.3f, 12.4f)),
                new View("05_life_support_up", new Vector3(11f, 1.65f, 0f), new Vector3(13.4f, 3.3f, 0f)),
                new View("06_quarters_up", new Vector3(8f, 1.65f, 9f), new Vector3(10.4f, 3.1f, 9f)),
                new View("07_cargo_props_up", new Vector3(2f, 1.65f, 11.6f), new Vector3(0f, 3.3f, 9.6f)),
                new View("08_eva_lift_up", new Vector3(0f, 1.65f, 0f), new Vector3(0.01f, 6.1f, 0f)),
                new View("09_eva_trunk_exterior", new Vector3(6.4f, 8.2f, 6.4f), new Vector3(0f, 5f, 0f)),
                new View("10_ship_top_down", new Vector3(0f, 30f, 0.01f), new Vector3(0f, 0f, 0f))
            };
            Run(CeilingOutputDirectory, views, "LAST_SHIFT_CEILING_REVIEW", false);
        }

        /// <summary>
        /// 같은 시점을 <b>하늘만 자홍</b>으로 다시 찍는다. 별 배경은 어둡고 점이 작아 화면에서
        /// 새는 틈을 눈으로 세기 어렵다 — 배경을 단색으로 바꾸면 그 틈이 픽셀 단위로 세진다.
        /// 검수용 그림이 아니라 <b>측정용</b>이라 결과물은 <c>tmp/</c> 로 뺀다.
        /// </summary>
        public static void CaptureCeilingLeakMasksForAutomation()
        {
            var views = new[]
            {
                new View("01_plaza_up", new Vector3(4.2f, 1.65f, 4.2f), new Vector3(1.6f, 3.3f, 1.6f)),
                new View("02_cockpit_up", new Vector3(-11f, 1.65f, 0f), new Vector3(-13.4f, 3.3f, 0f)),
                new View("03_power_up", new Vector3(0f, 1.65f, -10f), new Vector3(0f, 3.3f, -12.4f)),
                new View("04_cooling_up", new Vector3(0f, 1.65f, 10f), new Vector3(0f, 3.3f, 12.4f)),
                new View("05_life_support_up", new Vector3(11f, 1.65f, 0f), new Vector3(13.4f, 3.3f, 0f)),
                new View("06_quarters_up", new Vector3(8f, 1.65f, 9f), new Vector3(10.4f, 3.1f, 9f)),
                new View("07_cargo_props_up", new Vector3(2f, 1.65f, 11.6f), new Vector3(0f, 3.3f, 9.6f)),
                new View("08_eva_lift_up", new Vector3(0f, 1.65f, 0f), new Vector3(0.01f, 6.1f, 0f))
            };
            Run("tmp/ceiling-leak-mask", views, "LAST_SHIFT_CEILING_LEAK_MASK", true);
        }

        /// <summary>
        /// 숙소 <b>한 방만</b> 여섯 시점. 고정 구획 큐브를 걷어내고 그 방을 정본 지도 하나가
        /// 세우게 한 변경(2026-08-14)의 검수용이다 — 벽 넷·바닥·천장이 각각 <b>한 벌씩</b>
        /// 서 있는지를 봐야 하므로 방 전체를 도는 시점이 필요하다. 천장 검수 세트의
        /// <c>06_quarters_up</c> 한 장으로는 벽과 바닥이 프레임에 안 들어온다.
        ///
        /// 좌표는 정본 지도의 <c>quarters.bounds</c>(<c>x 4..12</c>, <c>z 6..12</c>)에서 잡았고,
        /// 눈높이는 다른 검수 세트와 같은 <c>1.65</c> 다.
        /// </summary>
        public static void CaptureQuartersForAutomation() => Run(QuartersOutputDirectory, QuartersViews(), "LAST_SHIFT_QUARTERS_REVIEW", false);

        /// <summary>같은 시점을 자홍 배경으로. 새는 자리를 화소로 세는 측정용이라 <c>tmp/</c> 다.</summary>
        public static void CaptureQuartersLeakMasksForAutomation() => Run("tmp/quarters-leak-mask", QuartersViews(), "LAST_SHIFT_QUARTERS_LEAK_MASK", true);

        private static View[] QuartersViews() => new[]
        {
            // 천장. 판이 한 겹인지, 3.2 로 올라갔는지가 보 높이와의 간격으로 읽힌다.
            new View("01_quarters_ceiling", new Vector3(8f, 1.65f, 9f), new Vector3(9.2f, 3.4f, 9f)),
            // 바닥. 걷어낸 큐브 바닥과 지도 바닥 타일이 겹쳐 z-파이팅이 나던 자리다.
            new View("02_quarters_floor", new Vector3(8f, 2.4f, 9f), new Vector3(8.6f, 0f, 9f)),
            // 문 벽(좌현, x=4). 문 구멍이 하나인지 — 큐브 벽이 살아 있으면 판이 문 앞을 덮는다.
            new View("03_quarters_door_wall", new Vector3(10.5f, 1.65f, 9f), new Vector3(4f, 1.4f, 7f)),
            // 끝벽(우현, x=12).
            new View("04_quarters_end_wall", new Vector3(5.5f, 1.65f, 9f), new Vector3(12f, 1.4f, 9f)),
            // 선미 벽(z=6)과 선수 벽(z=12). 두 벌이 서면 여기서 두께가 두 배로 보인다.
            new View("05_quarters_aft_wall", new Vector3(8f, 1.65f, 11f), new Vector3(8f, 1.4f, 6f)),
            new View("06_quarters_fore_wall", new Vector3(8f, 1.65f, 7f), new Vector3(8f, 1.4f, 12f))
        };

        /// <summary>
        /// 부속(숙소) 실내고를 <c>3.0</c> 으로 낮춘 변경(2026-08-14)의 검수. <b>대비를 찍는
        /// 것이라 한 방만 보면 안 된다</b> — 같은 눈높이·같은 올려보기 각도로 본선 방을 한 장
        /// 같이 찍어야 <c>0.2m</c> 차이가 프레임에서 읽힌다.
        ///
        /// 문지방 시점이 첫 장인 이유. 이 연출이 실제로 걸리는 곳은 <b>문을 지나는 순간</b>이고,
        /// 거기서 광장 판(<c>3.2</c>)과 숙소 판(<c>3.0</c>)이 단차로 만난다.
        /// </summary>
        public static void CaptureQuartersHeightForAutomation()
        {
            var views = new[]
            {
                // 숙소 안에서 문(x 4.8, z 6) 쪽 천장. 광장 판과 숙소 판의 단차가 프레임 위쪽에 걸린다.
                new View("01_threshold_step", new Vector3(7.5f, 1.65f, 9f), new Vector3(4.9f, 2.9f, 6.4f)),
                // 숙소 천장. 앞선 검수 세트와 같은 시점이라 3.2 때의 장과 겹쳐 볼 수 있다.
                new View("02_quarters_ceiling", new Vector3(8f, 1.65f, 9f), new Vector3(9.2f, 3.4f, 9f)),
                // 본선(냉각실) 천장 — 같은 각도의 대조군.
                new View("03_main_ceiling_cooling", new Vector3(0f, 1.65f, 10f), new Vector3(1.2f, 3.4f, 10f)),
                // 방 전체. 낮춘 천장이 침상·소품과 어떻게 앉는지는 넓은 장이 있어야 판단된다.
                new View("04_quarters_wide", new Vector3(11.4f, 1.65f, 11.2f), new Vector3(5.2f, 1.6f, 7f))
            };
            Run(QuartersHeightOutputDirectory, views, "LAST_SHIFT_QUARTERS_HEIGHT", false);
        }

        /// <summary>
        /// 전력실 배전반이 방 설비에서 빠져나왔는지의 검수(2026-08-14).
        ///
        /// <b>겹침은 한 장으로 못 본다.</b> 파고든 쪽에서는 두 물건이 그냥 하나로 보이므로,
        /// 뒷벽을 정면에서 한 장(설비만 남았는가)과 우현 벽을 옆에서 한 장(배전반이 거기 섰는가),
        /// 그리고 둘이 같이 들어오는 넓은 장이 있어야 "갈라졌다" 가 프레임에서 읽힌다.
        ///
        /// 좌표는 정본 지도의 <c>power.bounds</c>(<c>x -4..4</c>, <c>z -14..-6</c>)에서 잡았고
        /// 눈높이는 다른 검수 세트와 같은 <c>1.65</c> 다.
        /// </summary>
        public static void CapturePowerBusPanelForAutomation()
        {
            var views = new[]
            {
                // 문(z=-6)으로 들어와 뒷벽 정면. 여기에 설비 하나만 서 있어야 한다.
                new View("01_power_back_wall", new Vector3(0f, 1.65f, -7.2f), new Vector3(0f, 1.25f, -13.6f)),
                // 우현 벽(x=4). 배전반이 옮겨 간 자리다 — 벽에 붙었는지·떠 있는지가 옆에서 읽힌다.
                new View("02_power_starboard_cabinet", new Vector3(-1.4f, 1.65f, -9.2f), new Vector3(3.9f, 1.2f, -11.5f)),
                // 둘이 한 프레임에. 갈라진 거리가 이 장에서 판단된다.
                new View("03_power_wide", new Vector3(-3.2f, 1.9f, -7.0f), new Vector3(2.4f, 1.1f, -12.6f)),
                // 뒷벽을 비스듬히. 설비 안에 남은 것이 있으면 이 각도에서 튀어나온다.
                new View("04_power_feature_oblique", new Vector3(2.8f, 1.65f, -10.8f), new Vector3(0f, 1.2f, -13.7f))
            };
            Run(PowerBusPanelOutputDirectory, views, "LAST_SHIFT_POWER_BUSPANEL", false);
        }

        /// <summary>
        /// 숙소 침상이 눕지 않고 섰는지의 검수(2026-08-14).
        ///
        /// <b>누운 침상은 정면에서 안 드러난다.</b> 옆으로 누우면 폭(x)은 그대로라
        /// 끝벽을 정면으로 찍은 장에서는 여전히 "긴 물건" 으로 보인다. 드러나는 것은
        /// 옆면이다 — 프레임·매트리스·베개·난간이 <b>위로</b> 쌓였는가, 아니면 바닥에
        /// 눌려 한 장으로 붙었는가. 그래서 정면 한 장과 <b>측면</b> 한 장을 같이 찍는다.
        ///
        /// 좌표는 정본 지도의 <c>quarters.bounds</c>(<c>x 4..12</c>, <c>z 6..12</c>)와
        /// 실제 침상 자리에서 잡았다 — 끝벽 쪽 <c>z ~ 11.5</c> 에 두 조가
        /// <c>x 7..9</c>, <c>x 9.25..11.25</c> 로 선다. 눈높이는 다른 검수 세트와 같은
        /// <c>1.65</c> 를 쓰되, 침상이 낮으므로 내려다보는 장은 목표점을 갑판 가까이 둔다.
        /// </summary>
        public static void CaptureQuartersBunkOrientationForAutomation()
        {
            var views = new[]
            {
                // 끝벽 정면. 침상 두 조가 나란히 프레임에 들어온다.
                new View("01_bunk_end_wall_front", new Vector3(9.1f, 1.55f, 8.4f), new Vector3(9.1f, 0.5f, 11.9f)),
                // 측면. 누웠는지 섰는지가 실제로 판정되는 장이다 — 침구가 위로 쌓여야 한다.
                new View("02_bunk_profile_side", new Vector3(5.4f, 1.1f, 10.4f), new Vector3(9.6f, 0.45f, 11.6f)),
                // 좌현 조 근접. 프레임/매트리스/베개가 따로 읽히는 거리다.
                new View("03_bunk_port_closeup", new Vector3(8.0f, 1.25f, 9.9f), new Vector3(8.0f, 0.35f, 11.5f)),
                // 비스듬히 내려다보기. 갑판에 박혔으면 이 각도에서 바닥선이 침상을 먹는다.
                new View("04_bunk_oblique_down", new Vector3(11.3f, 1.8f, 9.2f), new Vector3(8.6f, 0.3f, 11.6f))
            };
            Run(QuartersBunkOrientationOutputDirectory, views, "LAST_SHIFT_QUARTERS_BUNK_ORIENTATION", false);
        }

        public static void CaptureForAutomation()
        {
            var views = new[]
            {
                new View("01_plaza_bow_corners", new Vector3(5.4f, 1.65f, -4.8f), new Vector3(-0.5f, 1.25f, 0f)),
                new View("02_plaza_stern_corners", new Vector3(-5.4f, 1.65f, 4.8f), new Vector3(0.5f, 1.25f, 0f)),
                new View("03_cockpit_nose_windows", new Vector3(-7.2f, 1.65f, 0f), new Vector3(-15.8f, 1.35f, 0f)),
                new View("04_power", new Vector3(0f, 1.65f, -7.2f), new Vector3(0f, 1.25f, -13f)),
                new View("05_cooling", new Vector3(0f, 1.65f, 7.2f), new Vector3(0f, 1.25f, 13f)),
                new View("06_oxygen_life_support", new Vector3(7.2f, 1.65f, 0f), new Vector3(15f, 1.25f, 0f)),
                new View("07_quarters_bunks_curtains", new Vector3(5.2f, 1.65f, 7f), new Vector3(10.8f, 1.3f, 10.8f)),
                new View("08_quarters_reverse", new Vector3(11.4f, 1.65f, 11.2f), new Vector3(6f, 1.3f, 7f)),
                new View("09_cargo_props", new Vector3(3.2f, 1.65f, 12.2f), new Vector3(-2.5f, 0.9f, 9.5f)),
                new View("10_eva_hatch_exterior", new Vector3(4.8f, 7.8f, 4.8f), new Vector3(0f, 6.2f, 0f)),
                new View("11_eva_lift_interior", new Vector3(-1.2f, 1.65f, -5.2f), new Vector3(0f, 2.2f, 0f))
            };
            Run(OutputDirectory, views, "LAST_SHIFT_VISUAL_REVIEW", false);
        }

        private static void Run(string outputDirectory, View[] views, string tag, bool solidBackground)
        {
            EditorSceneManager.OpenScene(LastShiftSceneBuilder.ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(outputDirectory);

            var cameraObject = new GameObject("VisualReviewCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 68f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            camera.clearFlags = solidBackground ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
            camera.backgroundColor = Color.magenta;
            camera.allowHDR = false;

            try
            {
                foreach (var view in views)
                    Capture(camera, outputDirectory, view);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }

            Debug.Log($"[{tag}] views={views.Length} output={outputDirectory} result=PASS");
        }

        private static void Capture(Camera camera, string outputDirectory, View view)
        {
            camera.transform.position = view.Position;
            camera.transform.rotation = Quaternion.LookRotation(view.Target - view.Position, Vector3.up);

            var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;

            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(outputDirectory, view.Name + ".png"), image.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(image);
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }

        public readonly struct View
        {
            public View(string name, Vector3 position, Vector3 target)
                : this("legacy", "legacy", name, position, target)
            {
            }

            public View(string area, string purpose, string name, Vector3 position, Vector3 target)
            {
                Area = area;
                Purpose = purpose;
                Name = name;
                Position = position;
                Target = target;
            }

            public string Area { get; }
            public string Purpose { get; }
            public string Name { get; }
            public Vector3 Position { get; }
            public Vector3 Target { get; }
        }

        [Serializable] private sealed class EvidenceManifest
        {
            public string schema;
            public int evidenceVersion;
            public string scene;
            public string unityVersion;
            public string sourceRevision;
            public bool sourceDirty;
            public string capturedUtc;
            public int width;
            public int height;
            public float verticalFov;
            public float nearClip;
            public float farClip;
            public ReviewState reviewState;
            public EvidenceView[] views;
        }

        [Serializable] private sealed class ReviewState
        {
            public bool rebakeApplied;
            public bool ceilingShellApplied;
            public bool quartersSingleSourceApplied;
            public float quartersHeightMeters;
            public bool powerBusPanelSeparated;
            public bool quartersBunksUpright;
        }

        [Serializable] private sealed class EvidenceView
        {
            public string area;
            public string purpose;
            public string file;
            public float[] position;
            public float[] target;
        }
    }
}
