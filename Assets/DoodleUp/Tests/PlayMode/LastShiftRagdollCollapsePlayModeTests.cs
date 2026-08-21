using System.Collections;
using System.Collections.Generic;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleUp.Tests.PlayMode
{
    /// <summary>
    /// 승무원을 <b>진짜 플레이 루프에서</b> 떨어뜨려 몸이 공처럼 뭉치는지 본다.
    ///
    /// <b>왜 EditMode 로는 부족한가.</b> 이 래그돌의 검사는 지금까지 전부 EditMode 에서
    /// <c>Physics.Simulate</c> 를 손으로 밟았다. 그 경로에는 <c>Awake</c> 도 <c>LateUpdate</c> 도
    /// 없다 — <see cref="LastShiftRagdollSelfCollision"/>·<see cref="LastShiftRagdollBodySetup"/> 는
    /// 검사가 손으로 불러 줘야 돌고, <see cref="LastShiftRagdollSkinFollow"/> 는 물리 스텝당 정확히
    /// 한 번 불린다. 플레이에서는 셋 다 엔진이 부르고, 스킨은 <b>렌더 프레임마다</b> 불린다.
    /// 사용자가 "씬 열고 Play 눌렀더니 깨진다" 고 말할 때 도는 것은 이쪽이고, 이 카드가 두 번
    /// 재오픈되는 동안 <b>그 경로는 한 번도 안 돌았다.</b>
    ///
    /// <b>씬을 안 연다.</b> 랩 씬(<c>LAST_SHIFT_RAGDOLL_LAB</c>)은 빌드 세팅에 없어서
    /// <c>SceneManager.LoadSceneAsync</c> 로 못 열고, 넣으면 프로토타입 씬이 실제 빌드에 실린다.
    /// 프리팹을 그대로 인스턴스화하고 바닥을 깔면 <b>씬이 하는 일과 같고</b>(씬의 승무원도 이
    /// 프리팹의 인스턴스다) 빌드 세팅을 안 건드린다.
    ///
    /// <b>무엇을 재는가.</b> 관절 각만 보면 한계 안에서 여럿이 같이 접힌 경우를 못 잡는다 —
    /// 사용자가 본 "배가 공처럼 뭉쳐 다리가 안 보인다" 가 정확히 그것이다. 그래서
    /// 자유도별 초과(<see cref="LastShiftRagdollJointLimitTracker"/>)와 <b>실루엣</b>
    /// (골반→머리 길이, 골반→손발 뻗음)을 같이 본다.
    /// </summary>
    public sealed class LastShiftRagdollCollapsePlayModeTests
    {
        private const string PrefabResourcePath = "Assets/DoodleUp/Prefabs/LastShiftCrewRagdollSoft.prefab";

        /// <summary>낙하 뒤 관찰하는 시간(초). 착지와 정착이 둘 다 들어가야 한다.</summary>
        private const float ObservedSeconds = 4f;

        /// <summary>떨어뜨리는 높이(m). 헤드리스 `drop` 시나리오와 같은 값이다.</summary>
        private const float DropRise = 1.2f;

        /// <summary>
        /// 어느 자유도도 이보다 더 새면 안 된다(도). EditMode 실측 최악이 <c>22.1~24.6</c> 도라
        /// 여유를 뒀다 — 고치기 전은 <c>56.9</c> 도였다. 잡으려는 것은 미세한 초과가 아니라
        /// <b>한계가 통째로 무의미해진</b> 상태다.
        /// </summary>
        private const float MaxExcessDegrees = 35f;

        /// <summary>
        /// 골반→머리 길이가 정지 대비 이 아래로 내려가면 <b>몸통이 실제로 눌린</b> 것이다.
        /// 실측에서는 세 시나리오 모두 <c>0.97</c> 이하로 안 갔다 — 척추는 안 눌린다.
        /// 여기가 깨지면 이번과는 <b>다른 버그</b>이므로 그렇게 읽히게 따로 잰다.
        /// </summary>
        private const float MinTorsoSpan = 0.85f;

        /// <summary>
        /// 정착 시 손발이 골반에서 최소한 이만큼은 떨어져 있어야 한다(정지 대비).
        /// 사지가 골반 안으로 말려 들어가면 실루엣에서 다리가 사라진다.
        /// </summary>
        private const float MinSettledLimbSpan = 0.10f;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var spawned in _spawned)
                if (spawned != null) Object.Destroy(spawned);
            _spawned.Clear();
        }

        [UnityTest]
        public IEnumerator DroppedCrewKeepsItsSilhouetteAndJointLimits()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _spawned.Add(floor);
            floor.name = "TestFloor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);

            var subject = Spawn();

            // Awake 가 이미 돌았는지를 여기서 확인한다. 안 돌았으면 아래 측정은 씬과 다른 물리를
            // 재는 것이 되므로, 값이 좋게 나와도 검사가 아니다.
            var bodySetup = subject.GetComponent<LastShiftRagdollBodySetup>();
            Assert.That(bodySetup, Is.Not.Null, "프리팹에 LastShiftRagdollBodySetup 이 없다.");

            // 한 프레임 돌려 Awake/OnEnable 이 끝난 상태에서 정지 포즈를 잡는다.
            yield return null;

            Assert.That(bodySetup.ConfiguredBodies, Is.GreaterThan(0),
                "플레이인데도 바디 설정이 안 돌았다 — Awake 가 안 불렸다는 뜻이다.");
            Assert.That(bodySetup.BoostedBodies, Is.EqualTo(4),
                "경첩 바디(무릎·팔꿈치 넷)의 반복이 안 올라갔다.");

            var bodies = new List<Rigidbody>(subject.GetComponentsInChildren<Rigidbody>(true));
            var pelvis = Find(bodies, LastShiftRagdollRig.PelvisBoneName);
            var head = Find(bodies, LastShiftRagdollRig.HeadBoneName);
            Assert.That(pelvis, Is.Not.Null, "골반 바디를 못 찾았다.");
            Assert.That(head, Is.Not.Null, "머리 바디를 못 찾았다.");

            var torsoRest = Vector3.Distance(pelvis.transform.position, head.transform.position);
            var limbs = new List<(string Name, Rigidbody Body, float RestSpan)>();
            foreach (var name in new[] { "DEF-hand.L", "DEF-hand.R", "DEF-foot.L", "DEF-foot.R" })
            {
                var limb = Find(bodies, name);
                if (limb == null) continue;
                limbs.Add((name, limb, Vector3.Distance(limb.transform.position, pelvis.transform.position)));
            }

            Assert.That(limbs, Has.Count.EqualTo(4), "손발 넷을 다 못 찾았다 — 뼈 이름이 바뀌었다.");

            var trackers = new List<LastShiftRagdollJointLimitTracker>();
            foreach (var joint in subject.GetComponentsInChildren<Joint>(true))
            {
                if (joint.connectedBody == null) continue;
                trackers.Add(new LastShiftRagdollJointLimitTracker(joint));
            }

            // <b>여기서 트랜스폼을 옮기면 안 된다.</b> 프로젝트는 `autoSyncTransforms = 0` 이라
            // 물리가 한 번 돈 뒤의 트랜스폼 대입을 PhysX 가 안 읽고, 다음 FixedUpdate 에 제 포즈를
            // 도로 덮어쓴다. 들어 올리는 것은 생성 시점에 이미 끝냈다(Spawn). 속도만 준다.
            var shove = new Vector3(0.6f, 0.25f, 0.76f).normalized * 2f;
            foreach (var body in bodies)
            {
                if (body == null || body.isKinematic) continue;
                body.linearVelocity = shove;
            }

            var minTorso = 1f;
            var steps = Mathf.CeilToInt(ObservedSeconds / Time.fixedDeltaTime);
            for (var s = 0; s < steps; s++)
            {
                yield return new WaitForFixedUpdate();

                foreach (var tracker in trackers) tracker.Sample(out _, out _, out _);

                var torso = Vector3.Distance(pelvis.transform.position, head.transform.position) / torsoRest;
                if (torso < minTorso) minTorso = torso;
            }

            var worst = trackers[0];
            foreach (var tracker in trackers)
                if (tracker.WorstExcess > worst.WorstExcess) worst = tracker;

            var settledLimb = 1f;
            var settledName = "-";
            foreach (var (name, body, restSpan) in limbs)
            {
                var ratio = Vector3.Distance(body.transform.position, pelvis.transform.position) / restSpan;
                if (ratio >= settledLimb) continue;
                settledLimb = ratio;
                settledName = name;
            }

            Debug.Log($"[LAST_SHIFT_RAGDOLL_PLAYMODE] worst={worst.Name}.{worst.WorstAxis} "
                      + $"{worst.WorstDegrees:F1}/{worst.WorstLimit:F1} (+{worst.WorstExcess:F1}deg) "
                      + $"minTorsoSpan={minTorso:F3} settledLimb={settledLimb:F3}({settledName})");

            Assert.That(minTorso, Is.GreaterThan(MinTorsoSpan),
                $"골반→머리 길이가 정지 대비 {minTorso:F2} 까지 줄었다 — 몸통이 실제로 눌렸다. "
                + "이번 카드의 원인(팔다리 접힘)과 다른 버그이므로 스킨·척추 쪽을 따로 봐야 한다.");

            Assert.That(worst.WorstExcess, Is.LessThan(MaxExcessDegrees),
                $"'{worst.Name}.{worst.WorstAxis}' 가 {worst.WorstDegrees:F0}도까지 갔다"
                + $"(한계 {worst.WorstLimit:F0}도, {worst.WorstExcess:F0}도 초과). "
                + "한계를 못 잡으면 화면에서는 팔다리가 몸통에 접힌 덩어리로 보인다.");

            Assert.That(settledLimb, Is.GreaterThan(MinSettledLimbSpan),
                $"'{settledName}' 가 정착 시 골반까지 거리의 {settledLimb:F2} 밖에 안 된다 — "
                + "사지가 몸통 안으로 말려 실루엣에서 사라진다.");
        }

        /// <summary>
        /// 프리팹을 애셋 경로로 읽는다. <b>이 픽스처는 에디터 전용이다</b> — 랩 프리팹은
        /// <c>Resources</c> 밖에 있고, 프로토타입 애셋을 빌드에 실으려고 옮길 이유가 없다.
        /// 플레이어에서 돌면 조용히 통과하는 대신 건너뛴다.
        /// </summary>
        private GameObject Spawn()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabResourcePath);
#else
            GameObject prefab = null;
            Assert.Ignore("에디터 밖에서는 랩 프리팹을 못 읽는다 — 이 검사는 에디터 전용이다.");
#endif
            Assert.That(prefab, Is.Not.Null, $"래그돌 프리팹이 없다: {PrefabResourcePath}");

            var instance = Object.Instantiate(prefab);
            instance.name = "RagdollSubject";
            // 씬의 배치 높이 + 낙하 충격용 들어 올림. 물리가 돌기 전이라 이 대입은 PhysX 에 그대로 반영된다.
            instance.transform.position = new Vector3(0f, 0.304f + DropRise, 0f);
            _spawned.Add(instance);
            return instance;
        }

        private static Rigidbody Find(List<Rigidbody> bodies, string name)
        {
            foreach (var body in bodies)
                if (body != null && body.name == name) return body;
            return null;
        }
    }
}
