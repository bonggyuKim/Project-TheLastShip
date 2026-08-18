using System.Collections.Generic;
using DoodleUp.Editor;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 국소 눌림 표현층(<c>docs/last-shift-body-deformation-runtime-v1.md</c>).
    ///
    /// 이 파일이 고정하는 것은 넷이다 — <b>부피가 보존되는가</b>, <b>탄성으로 돌아오는가</b>,
    /// <b>슬롯이 제대로 배분되는가</b>, <b>접촉이 맞은 부위로 가는가</b>.
    ///
    /// 화면은 안 본다. 3단계 셰이더는 같은 커널 식을 쓰지만 그 식이 옳은지는 여기서 CPU 로
    /// 판정하고, 셰이더 쪽은 사본이 갈리지 않았는지만 사람이 캡처 한 장으로 확인한다.
    /// </summary>
    public sealed class LastShiftBodyDeformTests
    {
        private GameObject _instance;

        [TearDown]
        public void TearDown()
        {
            if (_instance != null) Object.DestroyImmediate(_instance);
            _instance = null;
        }

        /// <summary>
        /// 불룩함 이득 10 은 <b>유도값</b>이다. 평평한 판에서 반경 방향으로 적분해 0 이 되게
        /// 두면 <c>∫W·t = 1/6</c>, <c>∫B·t = 1/60</c> 이라 <c>k = 10</c> 이 나온다.
        /// 누가 "좀 더 불룩하게" 하려고 이 상수를 만지면 부피가 새므로 여기서 못박는다.
        /// </summary>
        [Test]
        public void BulgeGainIsTheValueThatCancelsTheDent()
        {
            const int steps = 20000;
            var dent = 0.0;
            var bulge = 0.0;
            for (var i = 0; i < steps; i++)
            {
                var t = (i + 0.5) / steps;
                dent += LastShiftBodyDeformKernel.Weight((float)t) * t;
                bulge += LastShiftBodyDeformKernel.Bulge((float)t) * t;
            }

            Assert.That(dent / steps, Is.EqualTo(1.0 / 6.0).Within(0.001),
                "눌림 적분이 1/6 이 아니다 — W(t) 를 바꿨으면 이득도 다시 풀어야 한다.");
            Assert.That(bulge / steps, Is.EqualTo(1.0 / 60.0).Within(0.001),
                "불룩함 적분이 1/60 이 아니다 — B(t) 를 바꿨으면 이득도 다시 풀어야 한다.");
            Assert.That(LastShiftBodyDeformKernel.BulgeGain,
                Is.EqualTo(dent / bulge).Within(0.02),
                "이득이 두 적분의 비와 다르다 — 이 값이 어긋난 만큼 부피가 샌다.");
        }

        /// <summary>
        /// 실제 메시에서 부피가 보존되는가. 커널은 평평한 판에서 유도했으므로 곡면에서는
        /// 근사고, 그 근사가 얼마나 벌어지는지를 여기서 숫자로 잡아 둔다.
        /// </summary>
        [Test]
        public void DentingASphereKeepsItsVolume()
        {
            var mesh = BuildUvSphere(0.5f, 96, 48);
            try
            {
                var vertices = mesh.vertices;
                var normals = mesh.normals;
                var triangles = mesh.triangles;
                var before = SignedVolume(vertices, triangles);

                var contact = new Vector3(0f, 0.5f, 0f);
                var contactNormal = Vector3.up;
                const float radius = 0.2f;
                const float depth = 0.03f;

                var moved = new Vector3[vertices.Length];
                for (var i = 0; i < vertices.Length; i++)
                    moved[i] = vertices[i] + LastShiftBodyDeformKernel.Displace(
                        vertices[i], normals[i], contact, contactNormal, radius, depth);

                var after = SignedVolume(moved, triangles);
                var drift = Mathf.Abs(after - before) / before;

                // 실제로 눌리기는 했는지부터 본다. 변위가 0 이면 부피는 당연히 보존된다.
                var deepest = 0f;
                for (var i = 0; i < vertices.Length; i++)
                    deepest = Mathf.Max(deepest, (moved[i] - vertices[i]).magnitude);
                Assert.That(deepest, Is.GreaterThan(depth * 0.5f),
                    "정점이 거의 안 움직였다 — 커널이 도달 못 하는 반경이면 이 검사는 무의미하다.");

                Assert.That(drift, Is.LessThan(0.02f),
                    $"부피가 {drift * 100f:F2}% 변했다 — 눌린 만큼 둘레로 안 밀려났다.");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        /// <summary>
        /// 눌린 자국이 <b>스스로</b> 돌아오는가. 접촉이 끊기면 목표가 0 으로 가고 스프링이
        /// 되돌린다 — 물리가 잠들어도 이건 따로 돈다는 것이 두 층 분리의 요점이다.
        /// </summary>
        [Test]
        public void ADentSpringsBackWithinFourTenthsOfASecond()
        {
            var deform = CreateDeform(out var anchor);
            deform.AddContact(anchor, anchor.position, Vector3.up, 0.05f, 0.2f);

            const float step = 1f / 60f;
            var peak = 0f;
            var slot = FindSlot(deform, anchor);

            // 접촉이 살아 있는 동안 눌린다.
            for (var i = 0; i < 12; i++)
            {
                deform.Step(step);
                peak = Mathf.Max(peak, Mathf.Abs(deform.DepthOf(slot)));
            }
            Assert.That(peak, Is.GreaterThan(0.01f), "접촉을 먹였는데 눌리지 않았다.");

            // 접촉을 놓으면 돌아온다.
            var elapsed = 0f;
            while (elapsed < 0.4f)
            {
                deform.Step(step);
                elapsed += step;
            }

            Assert.That(Mathf.Abs(deform.DepthOf(slot)), Is.LessThan(peak * 0.05f),
                "0.4초가 지나도 자국이 남아 있다 — 맞은 자리가 영구 함몰로 보인다.");
        }

        /// <summary>
        /// 감쇠비가 1 미만이라 한 번은 넘어가야 한다. <b>안 넘어가면 살이 아니라 점토다.</b>
        /// 동시에 여러 번 출렁이면 젤리로 보이므로 상한도 같이 건다.
        /// </summary>
        [Test]
        public void TheDentOvershootsOnceAndNotMore()
        {
            var deform = CreateDeform(out var anchor);
            deform.AddContact(anchor, anchor.position, Vector3.up, 0.05f, 0.2f);

            const float step = 1f / 120f;
            var samples = new List<float>();
            for (var i = 0; i < 120; i++)
            {
                deform.Step(step);
                samples.Add(deform.DepthOf(FindSlotOrZero(deform, anchor)));
            }

            // <b>눈에 보이는 출렁임만 센다.</b> 극값을 그냥 세면 0 근처에서 잦아드는 잔진동까지
            // 잡혀 실제로는 한 번 넘어가는 움직임이 네 번으로 읽힌다. 자국이 반대로(바깥으로)
            // 넘어간 것 중 피크의 5% 를 넘는 것만 한 번으로 친다.
            var peak = 0f;
            foreach (var sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
            Assert.That(peak, Is.GreaterThan(0.01f), "접촉을 먹였는데 눌리지 않았다.");

            var threshold = peak * 0.05f;
            var overshoots = 0;
            var sign = 1;
            foreach (var sample in samples)
            {
                if (Mathf.Abs(sample) < threshold) continue;
                var current = sample > 0f ? 1 : -1;
                if (current != sign) overshoots++;
                sign = current;
            }

            Assert.That(overshoots, Is.GreaterThan(0),
                "한 번도 안 넘어간다 — 감쇠비가 1 이상이면 눌렸다 서서히 펴지는 점토가 된다.");
            Assert.That(overshoots, Is.LessThanOrEqualTo(2),
                $"출렁임이 {overshoots}번이다 — 살이 아니라 젤리로 보인다.");
        }

        /// <summary>
        /// 같은 자리를 계속 맞으면 슬롯 하나를 재사용하고, 다른 부위는 각자 슬롯을 받는다.
        /// 이게 안 되면 한 번 부딪히는 동안 슬롯 여덟 개가 다 차서 다음 충돌이 안 보인다.
        /// </summary>
        [Test]
        public void TheSameSpotReusesOneSlotAndOtherPartsGetTheirOwn()
        {
            var deform = CreateDeform(out var anchor);
            var other = new GameObject("other").transform;
            other.SetParent(anchor.parent, false);
            other.localPosition = new Vector3(1f, 0f, 0f);

            for (var i = 0; i < 5; i++)
                deform.AddContact(anchor, anchor.position, Vector3.up, 0.05f, 0.2f);
            Assert.That(deform.ActiveSlots, Is.EqualTo(1),
                "같은 자리를 다섯 번 맞았는데 슬롯을 다섯 개 썼다.");

            deform.AddContact(other, other.position, Vector3.up, 0.05f, 0.2f);
            Assert.That(deform.ActiveSlots, Is.EqualTo(2),
                "다른 부위를 맞았는데 앞의 자국을 덮어썼다.");
        }

        /// <summary>
        /// 래그돌 부위 열둘이 <b>각자</b> 릴레이를 갖는가. 하나라도 빠지면 그 부위만 안 눌린다.
        /// </summary>
        [Test]
        public void EveryRagdollPartGetsItsOwnContactRelay()
        {
            var ragdoll = BuildRagdollWithDeform(out _);
            var relays = ragdoll.Relays;

            Assert.That(relays.Count, Is.EqualTo(LastShiftRagdollRig.Bones.Length),
                "릴레이 수가 부위 수와 다르다.");

            var seen = new HashSet<LastShiftRagdollPart>();
            foreach (var relay in relays)
            {
                Assert.That(seen.Add(relay.Part), Is.True, $"{relay.Part} 릴레이가 둘이다.");
                Assert.That(relay.Radius, Is.GreaterThan(0f), $"{relay.Part} 눌림 반경이 0 이다.");
                Assert.That(relay.GetComponent<Rigidbody>(), Is.Not.Null,
                    $"{relay.Part} 릴레이가 Rigidbody 없는 오브젝트에 붙었다 — 충돌 콜백이 안 온다.");
            }
        }

        /// <summary>
        /// 릴레이가 받은 접촉이 <b>그 부위 뼈에 매달려</b> 표현층으로 가는가.
        /// 오브젝트 공간에 그대로 박히면 래그돌이 굴러갈 때 자국만 공중에 남는다.
        /// </summary>
        [Test]
        public void AContactLandsOnTheBoneThatWasHit()
        {
            var ragdoll = BuildRagdollWithDeform(out var deform);
            var head = FindRelay(ragdoll, LastShiftRagdollPart.Head);

            head.ReportContact(head.transform.position, Vector3.forward, 20f);

            Assert.That(deform.ActiveSlots, Is.EqualTo(1), "접촉이 표현층에 안 닿았다.");
            var slot = FindSlot(deform, head.transform);
            Assert.That(deform.AnchorOf(slot), Is.SameAs(head.transform),
                "자국이 맞은 부위가 아닌 곳에 매달렸다.");
        }

        /// <summary>
        /// 래그돌을 다시 지으면 낡은 릴레이가 안 남는가. 남으면 낡은 반경과 낡은 표현층을
        /// 물고 있는 릴레이가 되살아나 같은 충돌을 두 번 그린다.
        /// </summary>
        [Test]
        public void RebuildingDoesNotLeaveStaleRelays()
        {
            var ragdoll = BuildRagdollWithDeform(out _);
            ragdoll.Build(LastShiftRagdollTuning.Comic());

            Assert.That(ragdoll.Relays.Count, Is.EqualTo(LastShiftRagdollRig.Bones.Length));
            var live = ragdoll.GetComponentsInChildren<LastShiftRagdollContactRelay>(true);
            Assert.That(live.Length, Is.EqualTo(LastShiftRagdollRig.Bones.Length),
                "재빌드 뒤 릴레이가 늘었다 — Clear 가 안 지운 것이 남았다.");
        }

        private LastShiftRagdoll BuildRagdollWithDeform(out LastShiftBodyDeform deform)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LastShiftRagdollLabScene.CharacterPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"승무원 프리팹이 없다: {LastShiftRagdollLabScene.CharacterPrefabPath}");

            _instance = Object.Instantiate(prefab);
            _instance.name = "DeformSubject";

            // 표현층이 먼저 붙어 있어야 빌드가 릴레이에 물려 준다 — 없으면 릴레이는 붙되
            // 아무 데도 안 보낸다는 것이 설계상 허용된 상태다.
            deform = _instance.AddComponent<LastShiftBodyDeform>();
            deform.CollectRenderers();

            var ragdoll = _instance.AddComponent<LastShiftRagdoll>();
            ragdoll.Build(LastShiftRagdollTuning.Comic());
            return ragdoll;
        }

        private static LastShiftRagdollContactRelay FindRelay(LastShiftRagdoll ragdoll, LastShiftRagdollPart part)
        {
            foreach (var relay in ragdoll.Relays)
                if (relay.Part == part) return relay;
            Assert.Fail($"{part} 릴레이가 없다.");
            return null;
        }

        private LastShiftBodyDeform CreateDeform(out Transform anchor)
        {
            _instance = new GameObject("DeformHost");
            var deform = _instance.AddComponent<LastShiftBodyDeform>();
            anchor = new GameObject("anchor").transform;
            anchor.SetParent(_instance.transform, false);
            return deform;
        }

        private static int FindSlot(LastShiftBodyDeform deform, Transform anchor)
        {
            for (var i = 0; i < LastShiftBodyDeform.SlotCount; i++)
                if (deform.AnchorOf(i) == anchor) return i;
            Assert.Fail("슬롯을 못 찾았다 — 접촉이 안 들어갔다.");
            return -1;
        }

        private static int FindSlotOrZero(LastShiftBodyDeform deform, Transform anchor)
        {
            for (var i = 0; i < LastShiftBodyDeform.SlotCount; i++)
                if (deform.AnchorOf(i) == anchor) return i;
            return 0;
        }

        private static float SignedVolume(Vector3[] vertices, int[] triangles)
        {
            var volume = 0f;
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                volume += Vector3.Dot(a, Vector3.Cross(b, c));
            }
            return Mathf.Abs(volume) / 6f;
        }

        /// <summary>
        /// 조밀한 UV 구. Unity 기본 구(약 515 정점)는 커널 반경 안에 정점이 몇 개 안 들어와
        /// 이산화 오차가 부피 오차로 둔갑한다 — 커널을 재려면 메시가 충분히 촘촘해야 한다.
        /// </summary>
        private static Mesh BuildUvSphere(float radius, int segments, int rings)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            for (var y = 0; y <= rings; y++)
            {
                var v = (float)y / rings;
                var polar = v * Mathf.PI;
                for (var x = 0; x <= segments; x++)
                {
                    var u = (float)x / segments;
                    var azimuth = u * Mathf.PI * 2f;
                    var normal = new Vector3(
                        Mathf.Sin(polar) * Mathf.Cos(azimuth),
                        Mathf.Cos(polar),
                        Mathf.Sin(polar) * Mathf.Sin(azimuth));
                    normals.Add(normal);
                    vertices.Add(normal * radius);
                }
            }

            var stride = segments + 1;
            for (var y = 0; y < rings; y++)
            {
                for (var x = 0; x < segments; x++)
                {
                    var a = y * stride + x;
                    var b = a + stride;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(a + 1);
                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(b + 1);
                }
            }

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }
    }
}
