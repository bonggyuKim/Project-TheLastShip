using DoodleUp.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 단문 경첩 규격(<see cref="LastShiftHatchLeaf"/>)과, 그 규격으로 구워 나간 클립을 본다.
    ///
    /// 클립까지 보는 이유는 규격이 맞아도 <b>구운 결과가 안 맞을 수 있어서</b>다. 옛 클립은
    /// 사원수 네 성분 중 <c>y</c> 하나만 적어서 닫힘 키가 길이 <c>0</c> 짜리 값이었고, 그건
    /// 코드를 읽어서는 안 보이고 에셋을 열어야 보인다.
    /// </summary>
    public sealed class LastShiftHatchLeafTests
    {
        private const string AnimatorFolder = "Assets/DoodleUp/Prefabs/LastShiftModularKit/Animators";
        private const float Tolerance = 1e-3f;

        /// <summary>상부 해치를 흉내 낸 판. 실제 킷과 같은 배치(누운 원판 + 테두리 경첩)다.</summary>
        private static LastShiftHatchLeaf Hatch(float radius = 0.64f, float openingHeight = 0.08f) =>
            LastShiftHatchLeaf.AtOpening(
                LastShiftHatchLeaf.KitUp * openingHeight,
                LastShiftHatchLeaf.KitUp * 0.055f,
                -LastShiftHatchLeaf.KitDepth * radius,
                LastShiftHatchLeaf.KitWidth, 105f);

        [Test]
        public void ClosedLeafSitsOverTheOpening()
        {
            var leaf = Hatch();
            leaf.Pose(0f, out var position, out var rotation);

            // 닫힘에서 판 중심(= 마디 + 아트가 준 오프셋)이 구멍 한가운데에 온다. 이 한 줄이
            // 양문 시절 좌우 오프셋을 맞추려고 매번 손보던 자리다.
            Assert.That(Vector3.Distance(position + rotation * (LastShiftHatchLeaf.KitUp * 0.055f),
                    LastShiftHatchLeaf.KitUp * 0.08f), Is.LessThan(Tolerance),
                "닫힌 판이 구멍 한가운데를 안 덮는다");
            Assert.That(Quaternion.Angle(rotation, Quaternion.identity), Is.LessThan(Tolerance),
                "닫힌 판은 문틀 면과 평평해야 한다");
        }

        [Test]
        public void HingeStaysPutThroughTheSwing()
        {
            var leaf = Hatch();
            // 경첩을 판 로컬로 옮겨 두고, 젖히는 내내 그 점이 제자리인지 본다. 이것이 "돈다" 와
            // "미끄러진다" 를 가르는 유일한 조건이다.
            var hingeOnLeaf = Quaternion.Inverse(leaf.ClosedRotation) * (leaf.Hinge - leaf.ClosedPosition);

            for (var step = 0; step <= 10; step++)
            {
                leaf.Pose(step / 10f, out var position, out var rotation);
                Assert.That(Vector3.Distance(position + rotation * hingeOnLeaf, leaf.Hinge),
                    Is.LessThan(Tolerance), $"openAmount={step / 10f} 에서 경첩이 흘렀다");
                Assert.That(new Vector4(rotation.x, rotation.y, rotation.z, rotation.w).magnitude,
                    Is.EqualTo(1f).Within(Tolerance), "자세가 단위 사원수가 아니다");
            }
        }

        [Test]
        public void OpenLeafClearsTheOpening()
        {
            const float radius = 0.64f;
            var leaf = Hatch(radius);
            leaf.Pose(1f, out var position, out var rotation);
            var centre = position + rotation * (LastShiftHatchLeaf.KitUp * 0.055f);

            // 열림에서 판이 구멍 위를 벗어나 있어야 한다. 판이 문틀 안쪽 어딘가에 남으면
            // 그림상 열려 있는데 지나갈 자리가 없다.
            var acrossOpening = new Vector2(Vector3.Dot(centre, LastShiftHatchLeaf.KitWidth),
                Vector3.Dot(centre, LastShiftHatchLeaf.KitDepth));
            Assert.That(acrossOpening.magnitude, Is.GreaterThan(radius),
                "젖힌 판이 아직 구멍 위를 덮고 있다");
            Assert.That(Vector3.Dot(centre, LastShiftHatchLeaf.KitUp), Is.GreaterThan(0.08f),
                "젖힌 판이 문틀 면 위로 안 올라왔다");
        }

        [Test]
        public void SwingKeepsEnoughKeysToStayOnTheArc()
        {
            // 호를 직선으로 이으므로 키가 성기면 판이 안쪽으로 잘린다. 규격이 각에 따라
            // 키를 늘리는지 본다 — 105°짜리 해치가 90°짜리 문보다 촘촘해야 한다.
            Assert.That(Hatch().KeyCount, Is.GreaterThanOrEqualTo(12));
            Assert.That(new LastShiftHatchLeaf(Vector3.zero, Vector3.right, Quaternion.identity,
                LastShiftHatchLeaf.KitUp, 90f).KeyCount, Is.GreaterThanOrEqualTo(10));
        }

        [TestCase("LP_EVA_Hatch_OpenClose")]
        [TestCase("LP_AirlockDoor_OpenClose")]
        public void ShippedClipDrivesTheWholePose(string clipName)
        {
            var clip = LoadClip(clipName);
            var bindings = AnimationUtility.GetCurveBindings(clip);

            // 자리와 자세를 통째로 적어야 경첩이 성립한다. 회전 네 성분 중 하나라도 빠지면
            // 나머지가 0 으로 남아 닫힘 키가 자세가 아닌 값이 된다.
            foreach (var property in new[]
                     {
                         "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
                         "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w"
                     })
                Assert.That(System.Array.Exists(bindings, b => b.propertyName == property), Is.True,
                    $"{clipName} 에 {property} 곡선이 없다");
        }

        [TestCase("LP_EVA_Hatch_OpenClose")]
        [TestCase("LP_AirlockDoor_OpenClose")]
        public void ShippedClipNeverKeysAValuelessRotation(string clipName)
        {
            var clip = LoadClip(clipName);
            foreach (var path in LeafPaths(clip))
            for (var step = 0; step <= 20; step++)
            {
                var time = step / 20f * clip.length;
                var rotation = RotationAt(clip, time, path);
                var length = new Vector4(rotation.x, rotation.y, rotation.z, rotation.w).magnitude;
                Assert.That(length, Is.EqualTo(1f).Within(0.02f),
                    $"{clipName}/{path} t={time:F2} 의 회전 길이가 {length:F3} 이다 — 자세가 아니다");
            }
        }

        [Test]
        public void ShippedDoorPanelsMoveAsOneLeaf()
        {
            // 단문의 조건은 "판이 하나" 가 아니라 <b>판들이 안 갈라진다</b> 이다. 양문 시절에는
            // 두 짝이 서로 반대로 미끄러져서 좌우 오프셋을 영원히 맞춰 놔야 했다. 지금은 같은
            // 경첩을 도니까 둘 사이 거리가 여닫는 내내 변하지 않는다 — 맞출 것이 없다.
            var clip = LoadClip("LP_AirlockDoor_OpenClose");
            var paths = LeafPaths(clip);
            Assert.That(paths.Length, Is.GreaterThanOrEqualTo(2),
                "문짝 마디를 둘 이상 못 찾았다 — 이 검사가 볼 것이 없다");

            var shut = LeafGap(clip, paths, 0f);
            for (var step = 1; step <= 10; step++)
                Assert.That(LeafGap(clip, paths, step / 10f * clip.length), Is.EqualTo(shut).Within(0.01f),
                    "여닫는 도중 문짝 둘이 갈라진다 — 아직 양문이다");

            // 같은 경첩을 도므로 자세도 내내 같다. 하나라도 어긋나면 판이 서로 어긋나 보인다.
            // 키 사이 값은 성분별 보간이라 길이가 1 에 살짝 못 미친다 — 정규화하고 비교하지
            // 않으면 <see cref="Quaternion.Angle"/> 가 그 부족분을 각도로 읽어 버린다.
            for (var step = 0; step <= 10; step++)
            {
                var time = step / 10f * clip.length;
                Assert.That(Quaternion.Angle(Quaternion.Normalize(RotationAt(clip, time, paths[0])),
                        Quaternion.Normalize(RotationAt(clip, time, paths[1]))),
                    Is.LessThan(0.5f), "문짝 둘의 자세가 갈린다");
            }
        }

        private static string[] LeafPaths(AnimationClip clip)
        {
            var paths = new System.Collections.Generic.List<string>();
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                if (binding.propertyName == "m_LocalPosition.x" && !paths.Contains(binding.path))
                    paths.Add(binding.path);
            paths.Sort(System.StringComparer.Ordinal);
            return paths.ToArray();
        }

        private static float LeafGap(AnimationClip clip, string[] paths, float time) =>
            Vector3.Distance(PositionAt(clip, time, paths[0]), PositionAt(clip, time, paths[1]));

        [Test]
        public void ShippedHatchClipOpensFarEnoughToWalkThrough()
        {
            var clip = LoadClip("LP_EVA_Hatch_OpenClose");
            var closed = RotationAt(clip, 0f);
            var open = RotationAt(clip, clip.length);

            Assert.That(Quaternion.Angle(closed, Quaternion.identity), Is.LessThan(1f),
                "닫힌 뚜껑이 문틀 면과 안 평평하다");
            Assert.That(Quaternion.Angle(closed, open), Is.GreaterThan(90f),
                "뚜껑이 수직도 못 넘게 열린다 — 올라오는 승무원 앞을 막는다");
        }

        [Test]
        public void ShippedHatchLidCoversTheOpeningShutAndLeavesItOpen()
        {
            // 규격과 클립이 맞아도 <b>구운 프리팹</b>이 어긋날 수 있다. 여기서는 실제 킷을 세워
            // 놓고 월드 좌표로 잰다 — 킷 안쪽 단위가 미터가 아니라, 로컬 숫자만 봐서는
            // 뚜껑이 문틀을 덮는지 알 수 없다.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/DoodleUp/Prefabs/LastShiftModularKit/LPK_EVA_TopHatch_1p6m.prefab");
            Assert.That(prefab, Is.Not.Null, "상부 해치 프리팹이 없다");

            var instance = Object.Instantiate(prefab);
            try
            {
                var clip = LoadClip("LP_EVA_Hatch_OpenClose");
                var pivot = Deep(instance.transform, "EVAHatchLidPivot");
                var lid = Deep(instance.transform, "EVAHatchLid").GetComponent<Renderer>();
                var frame = FrameBounds(instance.transform);

                Scrub(clip, pivot, 0f);
                var shut = lid.bounds;
                Assert.That(new Vector2(shut.center.x - frame.center.x, shut.center.z - frame.center.z).magnitude,
                    Is.LessThan(0.02f), "닫힌 뚜껑이 문틀 한가운데를 안 덮는다");
                Assert.That(shut.center.y, Is.EqualTo(frame.center.y).Within(0.05f),
                    "닫힌 뚜껑이 문틀 면에 안 앉는다");
                Assert.That(Mathf.Max(shut.size.x, shut.size.z), Is.LessThan(frame.size.x),
                    "뚜껑이 문틀보다 넓다 — 닫아도 얹히기만 한다");

                // 젖힌 뚜껑의 <b>발자국</b>을 본다. 경첩이 문틀 면에 있으므로 판의 아래 끝은
                // 열려 있어도 그 높이에 남는다 — 높이로 재면 영원히 "덮고 있다" 가 나온다.
                // 구멍 한가운데에 서서 위를 봤을 때 판이 없으면 그것이 열린 것이다.
                Scrub(clip, pivot, clip.length);
                var open = lid.bounds;
                var overhead = new Bounds(new Vector3(open.center.x, frame.center.y, open.center.z),
                    new Vector3(open.size.x, 0f, open.size.z));
                Assert.That(overhead.Contains(frame.center), Is.False,
                    "젖힌 뚜껑이 아직 구멍 위를 덮고 있다");
                Assert.That(open.max.y, Is.GreaterThan(frame.max.y),
                    "젖힌 뚜껑이 문틀 위로 안 일어섰다");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ShippedDoorLeafFillsTheOpeningWithoutFavouringASide()
        {
            // 카드의 수용기준이 이것이다 — "좌우 대칭 문제없이". 양쪽 문설주까지 남는 틈이 같으면
            // 어느 쪽으로도 안 치우친 것이고, 단문이 되면서 맞출 상대가 사라진 결과다.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/DoodleUp/Prefabs/LastShiftModularKit/LPK_Door_Airlock_2m.prefab");
            Assert.That(prefab, Is.Not.Null, "압력문 프리팹이 없다");

            var instance = Object.Instantiate(prefab);
            try
            {
                var clip = LoadClip("LP_AirlockDoor_OpenClose");
                var paths = LeafPaths(clip);
                var pivots = new Transform[paths.Length];
                for (var i = 0; i < paths.Length; i++)
                    pivots[i] = Deep(instance.transform, System.IO.Path.GetFileName(paths[i]));

                var portJamb = Deep(instance.transform, "Airlock_FrameL").GetComponent<Renderer>().bounds;
                var starboardJamb = Deep(instance.transform, "Airlock_FrameR").GetComponent<Renderer>().bounds;
                var across = (starboardJamb.center - portJamb.center).normalized;

                ScrubAll(clip, pivots, paths, 0f);
                var shut = PanelBounds(instance.transform);
                var port = Span(shut, across).min - Span(portJamb, across).max;
                var starboard = Span(starboardJamb, across).min - Span(shut, across).max;
                Assert.That(port, Is.EqualTo(starboard).Within(0.02f),
                    $"닫힌 문이 한쪽으로 치우쳤다 — 좌현 틈 {port:F3}, 우현 틈 {starboard:F3}");
                Assert.That(Mathf.Min(port, starboard), Is.GreaterThan(-0.02f), "문짝이 문설주를 파고든다");

                ScrubAll(clip, pivots, paths, clip.length);
                var open = Span(PanelBounds(instance.transform), across);
                var middle = Vector3.Dot((portJamb.center + starboardJamb.center) * 0.5f, across);
                Assert.That(middle, Is.Not.InRange(open.min, open.max), "젖힌 문이 아직 구멍을 가로막는다");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void ScrubAll(AnimationClip clip, Transform[] pivots, string[] paths, float time)
        {
            for (var i = 0; i < pivots.Length; i++)
            {
                pivots[i].localPosition = PositionAt(clip, time, paths[i]);
                pivots[i].localRotation = Quaternion.Normalize(RotationAt(clip, time, paths[i]));
            }
        }

        private static Bounds PanelBounds(Transform root)
        {
            var bounds = new Bounds();
            var measured = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.name.EndsWith("_Panel", System.StringComparison.Ordinal)) continue;
                if (measured) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; measured = true; }
            }
            Assert.That(measured, Is.True, "문짝 조각을 못 찾았다");
            return bounds;
        }

        /// <summary>상자를 한 방향으로 눌러 잰 구간. 문틀이 어느 월드 축에 서 있든 성립한다.</summary>
        private static (float min, float max) Span(Bounds bounds, Vector3 direction)
        {
            var centre = Vector3.Dot(bounds.center, direction);
            var reach = Mathf.Abs(bounds.extents.x * direction.x) + Mathf.Abs(bounds.extents.y * direction.y) +
                        Mathf.Abs(bounds.extents.z * direction.z);
            return (centre - reach, centre + reach);
        }

        /// <summary>클립이 그 시각에 적어 둔 자세를 마디에 그대로 얹는다. 애니메이터를 안 돌린다 —
        /// 확인하려는 것이 재생 경로가 아니라 <b>구워 둔 값</b>이다.</summary>
        private static void Scrub(AnimationClip clip, Transform pivot, float time)
        {
            var path = LeafPaths(clip)[0];
            pivot.localPosition = PositionAt(clip, time, path);
            pivot.localRotation = Quaternion.Normalize(RotationAt(clip, time, path));
        }

        private static Bounds FrameBounds(Transform root)
        {
            var bounds = new Bounds();
            var measured = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.name.StartsWith("Hatch_Frame_", System.StringComparison.Ordinal)) continue;
                if (measured) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; measured = true; }
            }
            Assert.That(measured, Is.True, "문틀 조각을 못 찾았다");
            return bounds;
        }

        private static Transform Deep(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            Assert.Fail($"{name} 마디가 없다");
            return null;
        }

        private static Quaternion RotationAt(AnimationClip clip, float time, string path = null) => new(
            Curve(clip, "m_LocalRotation.x", path).Evaluate(time), Curve(clip, "m_LocalRotation.y", path).Evaluate(time),
            Curve(clip, "m_LocalRotation.z", path).Evaluate(time), Curve(clip, "m_LocalRotation.w", path).Evaluate(time));

        private static Vector3 PositionAt(AnimationClip clip, float time, string path = null) => new(
            Curve(clip, "m_LocalPosition.x", path).Evaluate(time), Curve(clip, "m_LocalPosition.y", path).Evaluate(time),
            Curve(clip, "m_LocalPosition.z", path).Evaluate(time));

        private static AnimationClip LoadClip(string clipName)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimatorFolder}/{clipName}.anim");
            Assert.That(clip, Is.Not.Null, $"{clipName}.anim 이 없다 — 킷 임포터를 돌려야 한다");
            return clip;
        }

        /// <summary>마디를 안 지정하면 클립의 첫 마디를 본다. 판이 하나뿐인 해치용이다.</summary>
        private static AnimationCurve Curve(AnimationClip clip, string property, string path = null)
        {
            path ??= LeafPaths(clip)[0];
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                if (binding.propertyName == property && binding.path == path)
                    return AnimationUtility.GetEditorCurve(clip, binding);
            Assert.Fail($"{clip.name}/{path} 에 {property} 곡선이 없다");
            return null;
        }
    }
}
