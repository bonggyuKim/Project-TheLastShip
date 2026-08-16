using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>
    /// <b>실내에서 껍질 밖 맨 하늘이 보이는가</b>를 화소로 센다.
    ///
    /// 아트 정본 <c>docs/art/last-shift-bow-window-glass-v1.md</c> §7-1 이 남긴 수동 확인
    /// ("유리 띠 위아래로 맨 별이 보인다")의 기계화다. 그 문장은 눈으로 보고 판단하라는
    /// 것이었고, 그래서 껍질이 바뀔 때마다 사람이 다시 봐야 했다 — 실제로 그 사이에
    /// 원반 테두리가 통째로 지워졌는데(<c>dc63f9b</c>) 아무 검사도 그 사실을 안 말했다.
    ///
    /// <b>왜 스크린샷이 아니라 숫자인가.</b> 별 배경은 어둡고 점이 작아서 그림에서는 새는
    /// 틈이 안 세어진다 — <see cref="LastShiftVisualReviewCapture"/> 의 자홍 마스크가 같은
    /// 이유로 있다. 여기서는 한 걸음 더 가서 마스크를 사람에게 넘기지 않고 화소를 직접
    /// 세고, 방마다 최악 시점만 <c>PNG</c> 로 남긴다.
    ///
    /// <b>시점은 시야 <b>전구</b>다.</b> 창 앞에서 정면만 보면 이 카드가 말한 결함
    /// (띠 <b>위아래</b>로 새는 것)이 프레임 밖으로 빠진다. 그래서 화각 <c>90°</c> 정사각
    /// 여섯 면으로 큐브를 만들어 한 지점의 사방을 빠짐 없이 덮는다.
    ///
    /// <b>EVA 승강구는 새는 것이 아니다.</b> 광장 코어 위로 뚫린 자리는 밖으로 나가는 길이라
    /// 하늘이 보이는 것이 맞다 — <see cref="EvaExemptSpaces"/> 가 그 방을 보고에서 가른다.
    /// </summary>
    public static class LastShiftInteriorSkyLeakAudit
    {
        /// <summary>
        /// 결과물 자리. <b>측정용인데도 <c>tmp/</c> 가 아니다</b> — 자홍 마스크를 <c>tmp/</c> 로
        /// 뺀 선례(<see cref="LastShiftVisualReviewCapture"/>)는 그림이 한 번 보고 버리는
        /// 것이었지만, 여기 최악 시점 여섯 장은 "이 시점에서 하늘이 안 보였다" 는 주장의
        /// 근거라서 다음 사람이 같은 자리를 다시 찍어 대조할 수 있어야 한다.
        /// </summary>
        private const string OutputDirectory = "docs/art/evidence/last-shift-interior-sky-leak";

        /// <summary>승무원 눈높이. 검수 세트가 쓰는 값과 같다.</summary>
        private const float EyeHeight = 1.65f;

        /// <summary>웅크린 높이. 창 아래로 새는 자리는 눈높이보다 여기서 크게 보인다.</summary>
        private const float CrouchHeight = 0.6f;

        /// <summary>표본 지점을 벽에서 띄우는 거리. 벽 안에 카메라가 들어가는 것을 막는다.</summary>
        private const float WallInset = 1.0f;

        /// <summary>
        /// 큐브 한 면의 변. 측정만 하면 <c>256</c> 으로도 충분하지만 최악 시점 여섯 장이 사람이
        /// 볼 근거라서 <c>512</c> 로 찍는다 — 새는 자리는 화소 몇 개짜리 실선이라 낮은 해상도로
        /// 남기면 그림에서 사라진다.
        /// </summary>
        private const int FaceResolution = 512;

        /// <summary>
        /// 하늘 화소 비율의 상한. <b>최악 한 면</b>의 비율에 건다 — 구 전체로 평균 내면 한 면이
        /// 통째로 뚫린 것과 여섯 면에 조금씩 새는 것이 같은 값이 되고, 사람이 보는 것은 언제나
        /// 한 면이다.
        ///
        /// <c>0</c> 이 아닌 이유는 문틀·판 이음매의 화소 한두 개까지 결함으로 세면 검사가
        /// 렌더러 정밀도에 매달리기 때문이다. 한 면 <c>512²</c>(<c>262144</c> 화소) 기준
        /// <c>0.0005</c> 는 약 <c>131</c> 화소 — 원반 테두리가 지워졌을 때 아트가 본 것
        /// ("유리 띠 위아래로 맨 별") 은 그 수백 배다.
        /// </summary>
        public const float LeakRatioTolerance = 0.0005f;

        /// <summary>하늘이 보이는 것이 맞는 방. EVA 승강구가 천장을 뚫고 나간다.</summary>
        private static readonly string[] EvaExemptSpaces = { "plaza" };

        /// <summary>
        /// 대조군 시점. 배 <b>위 한참 밖</b>에서 하늘만 본다.
        ///
        /// <b>이것이 없으면 이 감사는 아무것도 안 말한다.</b> "하늘 화소가 없다" 는 배가
        /// 막혔을 때도 나오지만 카메라가 아무것도 안 그렸을 때도 똑같이 나온다 — 배치
        /// 모드에서 렌더가 죽거나, <c>clearFlags</c> 가 안 먹거나, <c>ReadPixels</c> 가 빈
        /// 텍스처를 읽으면 전부 <c>0</c> 이고 전부 <c>PASS</c> 다. 통과가 뜻을 가지려면 같은
        /// 경로가 하늘을 <b>볼 줄 안다</b>는 것을 한 번 보여야 한다.
        /// </summary>
        private static readonly Vector3 ControlPoint = new(0f, 80f, 0f);

        /// <summary>대조군이 넘어야 하는 하늘 비율. 위를 보는 한 면이라 거의 <c>1</c> 이어야 한다.</summary>
        public const float ControlRatioFloor = 0.99f;

        [MenuItem("Last Shift/Review/Audit Interior Sky Leak")]
        public static void AuditForAutomation()
        {
            var report = Audit();
            File.WriteAllText(Path.Combine(OutputDirectory, "report.txt"), report.Text, Encoding.UTF8);
            var passed = report.ControlRatio >= ControlRatioFloor && report.WorstRatio <= LeakRatioTolerance;
            Debug.Log($"[LAST_SHIFT_INTERIOR_SKY_LEAK] samples={report.SampleCount} " +
                      $"control_ratio={report.ControlRatio:0.000000} " +
                      $"worst_space={report.WorstSpace} worst_ratio={report.WorstRatio:0.000000} " +
                      $"tolerance={LeakRatioTolerance:0.000000} output={OutputDirectory} " +
                      $"result={(passed ? "PASS" : "FAIL")}");
            foreach (var line in report.Lines) Debug.Log($"[LAST_SHIFT_INTERIOR_SKY_LEAK_SPACE] {line}");
        }

        /// <summary>
        /// 방마다 표본 지점을 훑어 최악 하늘 비율을 낸다. 씬을 여는 것까지 포함한다 —
        /// 부르는 쪽이 씬 상태를 맞춰 두게 하면 검사가 씬에 따라 다른 답을 준다.
        /// </summary>
        public static AuditReport Audit()
        {
            EditorSceneManager.OpenScene(LastShiftSceneBuilder.ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(OutputDirectory);

            var cameraObject = new GameObject("InteriorSkyLeakCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 90f;
            camera.aspect = 1f;
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 200f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.magenta;
            camera.allowHDR = false;

            var lines = new List<string>();
            var text = new StringBuilder();
            text.AppendLine("# LAST SHIFT 실내 하늘 노출 감사");
            text.AppendLine($"scene={LastShiftSceneBuilder.ScenePath}");
            text.AppendLine($"tolerance={LeakRatioTolerance.ToString("0.000000", CultureInfo.InvariantCulture)}");
            text.AppendLine();

            var sampleCount = 0;
            var worstRatio = 0f;
            var worstSpace = "none";
            var controlRatio = 0f;

            try
            {
                // 대조군 먼저. 여기가 0 이면 뒤의 0 들은 "막혔다" 가 아니라 "안 그렸다" 다.
                controlRatio = FaceSkyRatio(camera, ControlPoint, 4);
                CaptureFace(camera, ControlPoint, 4, Path.Combine(OutputDirectory, "control_open_sky.png"));
                text.AppendLine(
                    $"control\t({ControlPoint.x:0.00}, {ControlPoint.y:0.00}, {ControlPoint.z:0.00})\t" +
                    $"face=+y\tratio={controlRatio.ToString("0.000000", CultureInfo.InvariantCulture)}\t" +
                    $"floor={ControlRatioFloor.ToString("0.000000", CultureInfo.InvariantCulture)}");
                text.AppendLine();

                foreach (var space in Spaces())
                {
                    // 아무 데도 안 새면 최악 시점이 없다. 그때 찍을 자리를 미리 잡아 둔다 —
                    // 방 가운데 눈높이에서 <b>배 바깥쪽</b>을 본 면이다. 하늘이 샌다면 그
                    // 방향이라 통과 그림도 같은 각을 봐야 대조가 된다.
                    var spaceWorst = 0f;
                    var spaceWorstPoint = new Vector3(space.CenterX, EyeHeight, space.CenterZ);
                    var spaceWorstFace = OutwardFace(space);

                    foreach (var point in SamplePoints(space))
                    {
                        sampleCount++;
                        var ratio = CubeSkyRatio(camera, point, out var worstFace, out var faceRatio);
                        if (faceRatio > spaceWorst)
                        {
                            spaceWorst = faceRatio;
                            spaceWorstPoint = point;
                            spaceWorstFace = worstFace;
                        }

                        text.AppendLine(
                            $"{space.Id}\t({point.x:0.00}, {point.y:0.00}, {point.z:0.00})\t" +
                            $"sphere={ratio.ToString("0.000000", CultureInfo.InvariantCulture)}\t" +
                            $"worst_face={FaceName(worstFace)}\t" +
                            $"face={faceRatio.ToString("0.000000", CultureInfo.InvariantCulture)}");
                    }

                    var exempt = Array.IndexOf(EvaExemptSpaces, space.Id) >= 0;
                    // 새는 자리가 <b>없어도</b> 찍는다. 결함이 있을 때만 그림을 남기면
                    // "통과" 는 근거 없는 한 줄이 되고, 이 카드처럼 결함이 이미 사라진 뒤에
                    // 그것을 확인하러 오는 사람이 볼 것이 없다.
                    CaptureFace(camera, spaceWorstPoint, spaceWorstFace,
                        Path.Combine(OutputDirectory, $"{space.Id}_worst.png"));

                    lines.Add($"space={space.Id} worst_face_ratio={spaceWorst:0.000000} " +
                              $"eva_exempt={(exempt ? "yes" : "no")}");

                    if (!exempt && spaceWorst > worstRatio)
                    {
                        worstRatio = spaceWorst;
                        worstSpace = space.Id;
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }

            text.AppendLine();
            foreach (var line in lines) text.AppendLine(line);

            return new AuditReport(sampleCount, worstSpace, worstRatio, controlRatio, lines.ToArray(), text.ToString());
        }

        /// <summary>
        /// 한 지점의 사방 하늘 비율. 큐브 여섯 면을 전부 세되, 최악 <b>한 면</b>도 같이
        /// 돌려준다 — 구 전체로 평균 내면 한 면이 통째로 뚫린 것과 여섯 면에 조금씩 새는 것이
        /// 같은 값이 되고, 사람이 보는 것은 언제나 한 면이다.
        /// </summary>
        private static float CubeSkyRatio(Camera camera, Vector3 point, out int worstFace, out float worstFaceRatio)
        {
            var total = 0f;
            worstFace = 0;
            worstFaceRatio = 0f;
            for (var face = 0; face < 6; face++)
            {
                var ratio = FaceSkyRatio(camera, point, face);
                total += ratio;
                if (ratio > worstFaceRatio)
                {
                    worstFaceRatio = ratio;
                    worstFace = face;
                }
            }
            return total / 6f;
        }

        private static float FaceSkyRatio(Camera camera, Vector3 point, int face)
        {
            var image = RenderFace(camera, point, face);
            try
            {
                var pixels = image.GetPixels32();
                var sky = 0;
                foreach (var pixel in pixels)
                    if (IsSky(pixel)) sky++;
                return (float)sky / pixels.Length;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static void CaptureFace(Camera camera, Vector3 point, int face, string path)
        {
            var image = RenderFace(camera, point, face);
            try
            {
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static Texture2D RenderFace(Camera camera, Vector3 point, int face)
        {
            camera.transform.position = point;
            camera.transform.rotation = Quaternion.LookRotation(FaceForward(face), FaceUp(face));

            var target = RenderTexture.GetTemporary(FaceResolution, FaceResolution, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;

            var image = new Texture2D(FaceResolution, FaceResolution, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, FaceResolution, FaceResolution), 0, 0);
            image.Apply();

            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            return image;
        }

        /// <summary>
        /// 자홍인가. 정확히 같은 값만 세면 안 된다 — 반투명 유리가 배경 위에 얹히면 값이
        /// 살짝 밀리는데, 그 뒤는 여전히 맨 하늘이고 이 카드가 말하는 것은 <b>유리 밖</b>이다.
        /// 초록이 낮고 적·청이 높은 것만 자홍이라 배 안의 회색·청록 어느 것도 안 걸린다.
        /// </summary>
        private static bool IsSky(Color32 pixel) =>
            pixel.r > 150 && pixel.b > 150 && pixel.g < 90;

        private static Vector3 FaceForward(int face) => face switch
        {
            0 => Vector3.forward,
            1 => Vector3.back,
            2 => Vector3.left,
            3 => Vector3.right,
            4 => Vector3.up,
            _ => Vector3.down
        };

        private static Vector3 FaceUp(int face) => face is 4 or 5 ? Vector3.forward : Vector3.up;

        /// <summary>
        /// 방 가운데에서 <b>배 바깥</b>을 보는 면. 광장은 원점에 걸터앉아 바깥 방향이 없으므로
        /// 위(<c>+y</c>)를 본다 — 거기가 EVA 승강구라 광장에서 하늘이 날 수 있는 유일한 자리다.
        /// </summary>
        private static int OutwardFace(SpaceBounds space)
        {
            if (Mathf.Abs(space.CenterX) < 1f && Mathf.Abs(space.CenterZ) < 1f) return 4;
            if (Mathf.Abs(space.CenterX) >= Mathf.Abs(space.CenterZ)) return space.CenterX >= 0f ? 3 : 2;
            return space.CenterZ >= 0f ? 0 : 1;
        }

        private static string FaceName(int face) => face switch
        {
            0 => "+z",
            1 => "-z",
            2 => "-x",
            3 => "+x",
            4 => "+y",
            _ => "-y"
        };

        /// <summary>
        /// 방 안 표본 지점. 네 모서리와 가운데를 눈높이·웅크린 높이 둘로 본다 — 창 아래로
        /// 새는 자리는 눈높이에서 프레임에 안 들어오고 웅크리면 벌어진다(아트 정본 §7-2).
        /// </summary>
        private static IEnumerable<Vector3> SamplePoints(SpaceBounds space)
        {
            var minX = space.MinX + WallInset;
            var maxX = space.MaxX - WallInset;
            var minZ = space.MinZ + WallInset;
            var maxZ = space.MaxZ - WallInset;
            var midX = (minX + maxX) * 0.5f;
            var midZ = (minZ + maxZ) * 0.5f;

            var flats = new[]
            {
                new Vector2(minX, minZ), new Vector2(maxX, minZ),
                new Vector2(minX, maxZ), new Vector2(maxX, maxZ),
                new Vector2(midX, midZ)
            };

            foreach (var height in new[] { EyeHeight, CrouchHeight })
            foreach (var flat in flats)
                yield return new Vector3(flat.x, height, flat.y);
        }

        /// <summary>
        /// 감사 대상 방. <b>정본 지도에서 읽는다</b> — 여기 좌표를 리터럴로 적으면 방이
        /// 옮겨간 뒤에도 검사는 옛 자리에서 하늘을 안 보고 통과한다.
        /// </summary>
        private static IEnumerable<SpaceBounds> Spaces()
        {
            foreach (var footprint in LastShiftModularKitImporter.MapSpaceFootprints())
                yield return new SpaceBounds(footprint.Key, footprint.Value);
        }

        private readonly struct SpaceBounds
        {
            public SpaceBounds(string id, float[] bounds)
            {
                Id = id;
                MinX = bounds[0];
                MaxX = bounds[1];
                MinZ = bounds[2];
                MaxZ = bounds[3];
            }

            public string Id { get; }
            public float MinX { get; }
            public float MaxX { get; }
            public float MinZ { get; }
            public float MaxZ { get; }

            public float CenterX => (MinX + MaxX) * 0.5f;
            public float CenterZ => (MinZ + MaxZ) * 0.5f;
        }

        public readonly struct AuditReport
        {
            public AuditReport(int sampleCount, string worstSpace, float worstRatio, float controlRatio,
                string[] lines, string text)
            {
                SampleCount = sampleCount;
                WorstSpace = worstSpace;
                WorstRatio = worstRatio;
                ControlRatio = controlRatio;
                Lines = lines;
                Text = text;
            }

            public int SampleCount { get; }
            public string WorstSpace { get; }
            public float WorstRatio { get; }

            /// <summary>대조군 하늘 비율. <see cref="ControlRatioFloor"/> 밑이면 감사 자체가 무효다.</summary>
            public float ControlRatio { get; }
            public string[] Lines { get; }
            public string Text { get; }
        }
    }
}
