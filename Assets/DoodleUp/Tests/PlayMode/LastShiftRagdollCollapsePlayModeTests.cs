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

        /// <summary>미는 검사에서 완전히 정착할 때까지 기다리는 시간(초). 잠들고도 남는 길이다.</summary>
        private const float SettleSeconds = 4f;

        /// <summary>민 뒤 반응을 보는 시간(초).</summary>
        private const float ShoveObservedSeconds = 1.5f;

        /// <summary>
        /// 밀었을 때 골반이 최소한 이만큼은 움직여야 한다(m).
        ///
        /// <b>화면에서 알아볼 수 있는 크기여야 한다.</b> 승무원 키가 1m 남짓이니 골반이 25cm 움직이면
        /// 몸 하나의 4분의 1이다. 전신 밀침(<c>2 m/s</c>)의 실측 이동은 이보다 훨씬 크고,
        /// 가슴만 때렸을 때는 <c>0.075m</c> 였다 — 그 둘을 가르는 자리에 둔다.
        /// </summary>
        private const float MinShoveTravel = 0.25f;

        /// <summary>손으로 얹은 바디 수. 부위가 통째로 빠지면 여기서 걸린다.</summary>
        private const int ExpectedBodies = 15;

        /// <summary>
        /// 바운드 검사에서 승무원을 들어 올리는 높이(m). 사용자가 실제로 그렇게 테스트한다 —
        /// 2026-08-22 보고 시점의 랩 씬 루트가 <c>y=11.57</c> 이었고 바디는 바닥에 있었다.
        /// </summary>
        private const float HighDropRise = 8f;

        /// <summary>
        /// 렌더러 바운드 중심이 골반에서 이보다 멀면 <b>몸을 안 감싸고 있는</b> 것이다(m).
        /// 승무원 키가 1m 남짓이라 1m 는 몸 하나 크기다.
        /// </summary>
        private const float MaxBoundsOffsetFromBody = 1f;

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
        /// <b>정착한 뒤에도 밀면 눈에 보이게 움직이는가.</b>
        ///
        /// 2026-08-22 사용자 보고 "래그돌인데 넘어뜨려도 아무 변화가 없다" 를 그대로 검사로 옮긴 것이다.
        /// 원인은 둘이었고 둘 다 여기서 물린다.
        /// <list type="number">
        /// <item><see cref="LastShiftRagdollBodySetup"/> 가 저중력 프록시용 슬립 임계
        /// (<c>0.05</c>, Unity 기본의 열 배)를 엔진 중력으로 도는 이 프리팹에 그대로 얹어
        /// 정착 도중 잠가 버렸다.</item>
        /// <item>랩 씬에 조작 주체가 아예 없어서 <b>밀 수단이 없었다</b>
        /// (<see cref="LastShiftRagdollSoftLab"/> 이 그것을 채운다).</item>
        /// </list>
        ///
        /// <b>정착까지 기다린 뒤에 민다.</b> 떨어지는 중에 밀면 잠들 틈이 없어 이 버그를 못 잡는다 —
        /// 사용자가 겪은 것은 "다 넘어져 누운 다음" 이다.
        /// </summary>
        [UnityTest]
        public IEnumerator SettledCrewStillRespondsToAShove()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _spawned.Add(floor);
            floor.name = "TestFloor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);

            var subject = Spawn(0f);
            yield return null;

            var lab = subject.GetComponent<LastShiftRagdollSoftLab>();
            Assert.That(lab, Is.Not.Null,
                "프리팹에 LastShiftRagdollSoftLab 이 없다 — 플레이에서 승무원을 밀 수단이 없다는 뜻이다.");

            var bodies = new List<Rigidbody>(subject.GetComponentsInChildren<Rigidbody>(true));
            var pelvis = Find(bodies, LastShiftRagdollRig.PelvisBoneName);
            Assert.That(pelvis, Is.Not.Null, "골반 바디를 못 찾았다.");

            // 완전히 정착할 때까지 둔다. 여기서 잠들어도 된다 — 깨울 수 있어야 하는 것이 요점이다.
            var settleSteps = Mathf.CeilToInt(SettleSeconds / Time.fixedDeltaTime);
            for (var s = 0; s < settleSteps; s++) yield return new WaitForFixedUpdate();

            var sleepingBefore = lab.SleepingBodies();
            var before = pelvis.transform.position;

            lab.BodyCheck();

            var pushSteps = Mathf.CeilToInt(ShoveObservedSeconds / Time.fixedDeltaTime);
            var travelled = 0f;
            for (var s = 0; s < pushSteps; s++)
            {
                yield return new WaitForFixedUpdate();
                travelled = Mathf.Max(travelled, Vector3.Distance(pelvis.transform.position, before));
            }

            Debug.Log($"[LAST_SHIFT_RAGDOLL_SHOVE] sleepingBeforeShove={sleepingBefore}/{bodies.Count} "
                      + $"pelvisTravel={travelled:F3}m action={lab.LastAction}");

            Assert.That(travelled, Is.GreaterThan(MinShoveTravel),
                $"정착한 승무원을 밀었는데 골반이 {travelled:F3}m 밖에 안 움직였다"
                + $"(밀기 전 잠든 바디 {sleepingBefore}/{bodies.Count}). "
                + "화면에서는 '래그돌인데 아무 반응이 없다' 로 보인다.");

            Assert.That(sleepingBefore, Is.EqualTo(0),
                $"정착만 했을 뿐인데 바디 {sleepingBefore}/{bodies.Count} 개가 잠들어 있다. "
                + "잠든 바디는 씬 뷰에서 트랜스폼을 끌어도 안 깨어난다"
                + "(autoSyncTransforms = 0 이라 그 대입이 PhysX 로 안 넘어간다).");

            // 설정 자체도 못 박는다. 위 두 줄이 '왜' 깨졌는지를 이 줄이 말해 준다.
            foreach (var body in bodies)
            {
                Assert.That(body.sleepThreshold, Is.EqualTo(LastShiftRagdollBodySetup.SleepThreshold).Within(1e-4f),
                    $"'{body.name}' 의 슬립 임계가 {body.sleepThreshold:F4} 다. 저중력 프록시용 값"
                    + "(LastShiftRagdollTuning.SleepThreshold)을 엔진 중력 프리팹에 얹으면 정착 도중 잠긴다.");
            }
        }

        /// <summary>
        /// <b>부위마다 콜라이더가 붙어 있는가.</b>
        ///
        /// 사용자가 "콜라이더 설정이 안 보인다" 고 한 것은 콜라이더가 <c>DEF-</c> 뼈가 아니라
        /// 그 밑의 <c>*_Col</c> 자식에 달려 있어서였다 — 뼈를 클릭하면 인스펙터가 비어 보인다.
        /// 지워진 것이 아니라는 근거를 검사로 남긴다. <b>바디에 붙은 콜라이더</b>를 세므로
        /// 자식에 있든 같은 오브젝트에 있든 상관없이 물리적으로 맞는 것만 통과한다.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryRagdollBodyKeepsACollider()
        {
            var subject = Spawn(0f);
            yield return null;

            var bodies = new List<Rigidbody>(subject.GetComponentsInChildren<Rigidbody>(true));
            Assert.That(bodies, Has.Count.EqualTo(ExpectedBodies),
                $"래그돌 바디가 {bodies.Count} 개다 — {ExpectedBodies} 개여야 한다.");

            var naked = new List<string>();
            var total = 0;
            foreach (var body in bodies)
            {
                var mine = 0;
                foreach (var collider in body.GetComponentsInChildren<Collider>(true))
                {
                    // 자식 바디가 가져간 콜라이더는 이 바디 것이 아니다.
                    if (collider.attachedRigidbody != body) continue;
                    if (!collider.enabled) continue;
                    mine++;
                }

                total += mine;
                if (mine == 0) naked.Add(body.name);
            }

            Debug.Log($"[LAST_SHIFT_RAGDOLL_COLLIDERS] bodies={bodies.Count} colliders={total}");

            Assert.That(naked, Is.Empty,
                "콜라이더가 하나도 안 붙은 바디가 있다: " + string.Join(", ", naked));
            Assert.That(total, Is.GreaterThanOrEqualTo(ExpectedBodies),
                $"살아 있는 콜라이더가 {total} 개다 — 바디 수({ExpectedBodies})보다 적으면 부위 하나가 비어 있다.");
        }

        /// <summary>
        /// <b>스킨드 메시 바운드가 루트가 아니라 뼈를 따라가는가.</b>
        ///
        /// 2026-08-22 사용자 보고 "래그돌이 Play 중 한 번씩 화면에서 안 보인다" 를 옮긴 것이다.
        /// 원인은 컬링이었다 — <c>updateWhenOffscreen</c> 이 꺼져 있으면 스킨드 메시의 바운드는
        /// 임포트된 바인드 포즈 바운드를 <b>루트 본에 얹어</b> 계산하고, 뼈가 실제로 간 자리를 안 본다.
        /// 래그돌은 루트를 안 움직이고 뼈만 물리로 옮기므로 둘이 갈라진다. 보고 시점 실측:
        /// 루트 <c>y=11.57</c>, 골반 <c>y=0.26</c>, 바운드 중심이 몸에서 <c>12.04m</c> 떨어져
        /// <c>isVisible=False</c> 였다. 렌더러도 머티리얼도 레이어도 멀쩡했다.
        ///
        /// <b>플래그만 보지 않는다.</b> 값이 켜져 있어도 바운드가 실제로 몸을 감싸는지는 다른 문제라,
        /// 높은 곳에서 떨어뜨려 루트와 몸을 <b>일부러 갈라 놓고</b> 바운드가 어느 쪽에 붙는지 잰다.
        /// </summary>
        [UnityTest]
        public IEnumerator SkinnedMeshBoundsFollowTheBonesNotTheRoot()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _spawned.Add(floor);
            floor.name = "TestFloor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);

            var subject = Spawn(HighDropRise);
            yield return null;

            var skin = subject.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(skin, Is.Not.Null, "승무원에 SkinnedMeshRenderer 가 없다.");
            Assert.That(skin.updateWhenOffscreen, Is.True,
                "updateWhenOffscreen 이 꺼져 있다. 래그돌이 뼈를 루트에서 멀리 옮기면 "
                + "바운드가 루트에 남아 프러스텀 컬링으로 캐릭터가 통째로 안 보인다.");

            var bodies = new List<Rigidbody>(subject.GetComponentsInChildren<Rigidbody>(true));
            var pelvis = Find(bodies, LastShiftRagdollRig.PelvisBoneName);
            Assert.That(pelvis, Is.Not.Null, "골반 바디를 못 찾았다.");

            var steps = Mathf.CeilToInt(SettleSeconds / Time.fixedDeltaTime);
            for (var s = 0; s < steps; s++) yield return new WaitForFixedUpdate();

            // 렌더 프레임을 한 번 더 돌려야 바운드 재계산이 반영된다.
            yield return null;

            var fromBody = Vector3.Distance(skin.bounds.center, pelvis.transform.position);
            var fromRoot = Vector3.Distance(skin.bounds.center, subject.transform.position);

            Debug.Log($"[LAST_SHIFT_RAGDOLL_BOUNDS] root={subject.transform.position.y:F2} "
                      + $"pelvis={pelvis.transform.position.y:F2} boundsCenter={skin.bounds.center} "
                      + $"fromBody={fromBody:F2} fromRoot={fromRoot:F2}");

            // 루트와 몸이 실제로 갈라졌는지 먼저 본다 — 안 갈라졌으면 이 검사는 아무것도 안 잰 것이다.
            Assert.That(fromRoot, Is.GreaterThan(MaxBoundsOffsetFromBody * 2f),
                $"루트와 바운드가 {fromRoot:F2}m 밖에 안 떨어졌다 — 승무원이 안 떨어진 것이라 "
                + "이 검사가 컬링 조건을 재현하지 못했다.");

            Assert.That(fromBody, Is.LessThan(MaxBoundsOffsetFromBody),
                $"렌더러 바운드 중심이 골반에서 {fromBody:F2}m 떨어져 있다(루트에서는 {fromRoot:F2}m). "
                + "바운드가 몸이 아니라 루트를 따라가면 카메라가 몸을 보고 있어도 컬링돼 "
                + "'캐릭터가 한 번씩 안 보인다' 가 된다.");
        }

        /// <summary>
        /// 프리팹을 애셋 경로로 읽는다. <b>이 픽스처는 에디터 전용이다</b> — 랩 프리팹은
        /// <c>Resources</c> 밖에 있고, 프로토타입 애셋을 빌드에 실으려고 옮길 이유가 없다.
        /// 플레이어에서 돌면 조용히 통과하는 대신 건너뛴다.
        /// </summary>
        private GameObject Spawn()
        {
            return Spawn(DropRise);
        }

        private GameObject Spawn(float rise)
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
            instance.transform.position = new Vector3(0f, 0.304f + rise, 0f);
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
