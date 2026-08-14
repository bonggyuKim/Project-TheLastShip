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

        public static void CaptureForAutomation()
        {
            EditorSceneManager.OpenScene(LastShiftSceneBuilder.ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(OutputDirectory);

            var cameraObject = new GameObject("VisualReviewCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 68f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = false;

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

            try
            {
                foreach (var view in views)
                    Capture(camera, view);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }

            Debug.Log($"[LAST_SHIFT_VISUAL_REVIEW] views={views.Length} output={OutputDirectory} result=PASS");
        }

        private static void Capture(Camera camera, View view)
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
            File.WriteAllBytes(Path.Combine(OutputDirectory, view.Name + ".png"), image.EncodeToPNG());

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
