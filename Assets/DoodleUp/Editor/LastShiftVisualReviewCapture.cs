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
