using System;
using System.IO;
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

        private readonly struct View
        {
            public View(string name, Vector3 position, Vector3 target)
            {
                Name = name;
                Position = position;
                Target = target;
            }

            public string Name { get; }
            public Vector3 Position { get; }
            public Vector3 Target { get; }
        }
    }
}
