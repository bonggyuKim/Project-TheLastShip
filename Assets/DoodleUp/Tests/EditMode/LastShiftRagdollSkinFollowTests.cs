using DoodleUp.Editor;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 물리를 안 받는 변형본이 실제로 물리 뼈를 따라가는지 고정한다.
    ///
    /// <b>이 파일이 막는 사고.</b> 뼈 여섯(<c>DEF-shoulder</c>·<c>DEF-breast</c>·<c>DEF-pelvis</c>)은
    /// 부모가 제어본이라 래그돌이 돌아도 안 움직인다. 안 움직이면 화면에서 어깨·가슴·골반이
    /// 제자리에 남고 나머지가 끌려가 메시가 찢어지는데, 그 순간까지 콘솔에는 아무 말도 안 나온다.
    /// 이름이 바뀌어 매핑이 끊기는 것도 같은 증상으로만 드러나므로 여기서 이름까지 확인한다.
    ///
    /// 손으로 잡아 둔 부위별 콜라이더를 살리는 씬용이라, 여기서는 <see cref="LastShiftRagdoll"/>
    /// 빌더를 안 돌린다 — 빌더 없이도 이 조각만으로 찢어짐이 막히는지가 계약이다.
    /// </summary>
    public sealed class LastShiftRagdollSkinFollowTests
    {
        private GameObject _instance;

        [TearDown]
        public void TearDown()
        {
            if (_instance != null) Object.DestroyImmediate(_instance);
            _instance = null;
        }

        [Test]
        public void EveryUndrivenWeightedBoneGetsLinked()
        {
            var follow = InstantiateCrew().AddComponent<LastShiftRagdollSkinFollow>();

            follow.Capture();

            Assert.That(follow.LinkCount, Is.EqualTo(LastShiftRagdollRig.HelperAttachments.Length),
                "변형본 매핑이 끊겼다 — FBX 를 다시 뽑으면서 뼈 이름이 바뀌었을 가능성이 크다. "
                + "이대로 두면 어깨·가슴·골반 정점이 바인드 포즈에 박혀 메시가 찢어진다.");
        }

        [Test]
        public void LinkedBoneRidesTheSourceBoneRigidly()
        {
            var subject = InstantiateCrew();
            var follow = subject.AddComponent<LastShiftRagdollSkinFollow>();
            follow.Capture();

            var source = FindBone(subject, LastShiftRagdollRig.ChestBoneName);
            var bone = FindBone(subject, "DEF-shoulder.L");
            var restOffset = Quaternion.Inverse(source.rotation) * (bone.position - source.position);

            // 래그돌이 몸통을 채 간 상황을 흉내 낸다 — 위치와 회전을 같이 옮겨야 델타 계산이 실제로 걸린다.
            source.SetPositionAndRotation(
                source.position + new Vector3(2.5f, 1.25f, -0.75f),
                source.rotation * Quaternion.Euler(35f, -50f, 20f));

            follow.Apply();

            var movedOffset = Quaternion.Inverse(source.rotation) * (bone.position - source.position);
            Assert.That(Vector3.Distance(movedOffset, restOffset), Is.LessThan(0.0005f),
                "변형본이 물리 뼈 기준 자리에서 벗어났다 — 그만큼이 스킨에서 늘어난 삼각형이 된다.");
        }

        [Test]
        public void CaptureRunsBeforePhysicsSoTheBoneStaysPutAtRest()
        {
            var subject = InstantiateCrew();
            var follow = subject.AddComponent<LastShiftRagdollSkinFollow>();
            follow.Capture();

            var bone = FindBone(subject, "DEF-pelvis.R");
            var rest = bone.position;

            follow.Apply();

            Assert.That(Vector3.Distance(bone.position, rest), Is.LessThan(0.0005f),
                "물리가 안 돌았는데 변형본이 움직였다 — 정지 포즈 기준이 어긋나 있다.");
        }

        [Test]
        public void SkinWeightsAreBlendedAcrossAtLeastFourBones()
        {
            // <b>이 한 줄이 래그돌 찢어짐의 진짜 원인이었다(2026-08-19).</b> 품질 레벨 DU02 의
            // Skin Weights 가 1 Bone 이면 정점이 제일 무거운 뼈 하나에 통째로 붙어, 관절마다
            // 웨이트 블렌딩이 사라진다 — 목·엉덩이·무릎에서 삼각형이 갈라졌다(실측: 늘어난
            // 삼각형 415개 → 4 Bones 로 바꾸자 102개).
            //
            // <b>렌더러 쪽 quality 로는 못 막는다.</b> SkinnedMeshRenderer.quality 를 Bone4 로
            // 둬도 전역이 OneBone 이면 그대로 1개만 쓴다(실측: 둘 다 torn=81 로 동일).
            // 전역 설정이 상한이라 여기서만 잡을 수 있다.
            Assert.That((int)QualitySettings.skinWeights, Is.GreaterThanOrEqualTo((int)SkinWeights.FourBones),
                $"품질 레벨 '{QualitySettings.names[QualitySettings.GetQualityLevel()]}' 의 Skin Weights 가 "
                + $"{QualitySettings.skinWeights} 다 — 스킨 캐릭터가 관절마다 찢어진다.");
        }

        [Test]
        public void BendIsSharedByTheBonesBetweenTwoBodies()
        {
            // 래그돌은 목에 바디를 하나(머리)만 준다. 그러면 가슴↔머리 사이의 각이 전부
            // 고리 하나에 몰려 찢어진다 — 실측으로 20도에서 32개가 늘어났고, 등분해 주면 16개다.
            var subject = InstantiateCrew();
            var chest = FindBone(subject, LastShiftRagdollRig.ChestBoneName);
            var head = FindBone(subject, LastShiftRagdollRig.HeadBoneName);
            MakeBody(chest);
            MakeBody(head);

            var follow = subject.AddComponent<LastShiftRagdollSkinFollow>();
            follow.Capture();

            var neck = FindBone(subject, "DEF-spine.004");
            var lower = FindBone(subject, "DEF-spine.005");
            var restNeck = neck.rotation;
            var restLower = lower.rotation;

            const float bend = 45f;
            head.rotation = Quaternion.AngleAxis(bend, chest.right) * head.rotation;
            var posedHead = head.rotation;

            follow.Apply();

            // 등분이 아니라 아트 실측값이다 — DEF-spine.004 는 0%, DEF-spine.005 는 50%
            // (2026-08-19, head 로컬 Y +30도에서 0.000도 / 14.994도). 등분(33%/67%)은
            // .004 를 33% 과회전시키고 있었다.
            var expectedNeck = bend * LastShiftRagdollRig.BendShareOf("DEF-spine.004", 0, 2);
            var expectedLower = bend * LastShiftRagdollRig.BendShareOf("DEF-spine.005", 1, 2);
            Assert.That(expectedNeck, Is.EqualTo(0f).Within(0.01f), "실측 표에서 .004 몫이 0 이 아니다.");
            Assert.That(expectedLower, Is.EqualTo(bend * 0.5f).Within(0.01f), "실측 표에서 .005 몫이 50% 가 아니다.");
            Assert.That(Quaternion.Angle(restNeck, neck.rotation), Is.EqualTo(expectedNeck).Within(1f),
                "가슴에 가까운 목뼈가 실측 몫과 다르게 돌았다.");
            Assert.That(Quaternion.Angle(restLower, lower.rotation), Is.EqualTo(expectedLower).Within(1f),
                "머리에 가까운 목뼈가 실측 몫과 다르게 돌았다.");
            Assert.That(Quaternion.Angle(posedHead, head.rotation), Is.LessThan(0.1f),
                "중간 뼈를 돌리면서 물리가 정한 머리 포즈를 밀어 버렸다 — 되돌리는 순서가 깨졌다.");
        }

        [Test]
        public void SegmentsSkipPartsThatOnlyAJointConnects()
        {
            // 팔은 계층상 제어본(ORG-shoulder) 밑에 달려 있어 위로 올라가도 바디를 못 만난다.
            // 그런 부위에 억지로 구간을 만들면 엉뚱한 제어본을 돌리게 된다.
            var subject = InstantiateCrew();
            MakeBody(FindBone(subject, LastShiftRagdollRig.ChestBoneName));
            MakeBody(FindBone(subject, LastShiftRagdollRig.LeftShoulderBoneName));

            var follow = subject.AddComponent<LastShiftRagdollSkinFollow>();
            follow.Capture();

            Assert.That(follow.SegmentCount, Is.EqualTo(0),
                "계층으로 안 이어진 부위에 구간이 생겼다 — 제어본을 돌리게 된다.");
        }

        private static void MakeBody(Transform bone)
        {
            var body = bone.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;   // EditMode 라 물리는 안 돈다. 구간 탐색이 보는 것은 존재 여부뿐이다.
        }

        [Test]
        public void SelfCollisionKeepsPairsThatDoNotOverlapAtRest()
        {
            // 전부 끄면 몸이 서로를 통과하고, 전부 켜면 차렷 자세에서 이미 겹친 쌍이 솔버 예산을
            // 통째로 먹어 정작 막아야 할 접촉이 굶는다(실측: 비인접 관통 13쌍·최대 13cm).
            // 그래서 "겹친 것만 끄고 나머지는 살린다" 가 계약이다 — 살린 쪽이 0 이면 정책이 뒤집힌 것이다.
            var subject = InstantiateCrew();
            var chest = FindBone(subject, LastShiftRagdollRig.ChestBoneName);
            var hand = FindBone(subject, "DEF-hand.L");
            var foot = FindBone(subject, "DEF-foot.L");
            AddOverlappingBody(chest, 0.5f);   // 몸 전체를 삼킬 만큼 큰 구
            AddOverlappingBody(hand, 0.02f);
            AddOverlappingBody(foot, 0.02f);

            var policy = subject.AddComponent<LastShiftRagdollSelfCollision>();
            policy.Apply();

            Assert.That(policy.IgnoredPairs, Is.GreaterThan(0),
                "차렷 자세에서 겹친 쌍을 하나도 안 껐다 — 그 쌍들이 솔버를 먹는다.");
            Assert.That(policy.KeptPairs, Is.GreaterThan(0),
                "쌍을 전부 꺼 버렸다 — 이러면 몸이 서로를 통과한다.");
        }

        private static void AddOverlappingBody(Transform bone, float radius)
        {
            bone.gameObject.AddComponent<Rigidbody>().isKinematic = true;
            var holder = new GameObject(bone.name + "_Col");
            holder.transform.SetParent(bone, false);
            holder.AddComponent<SphereCollider>().radius = radius;
        }

        private static Transform FindBone(GameObject subject, string boneName)
        {
            foreach (var t in subject.GetComponentsInChildren<Transform>(true))
                if (t.name == boneName) return t;

            Assert.Fail($"뼈를 못 찾았다: {boneName}");
            return null;
        }

        private GameObject InstantiateCrew()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LastShiftRagdollLabScene.CharacterPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"승무원 프리팹이 없다: {LastShiftRagdollLabScene.CharacterPrefabPath}");

            _instance = Object.Instantiate(prefab);
            _instance.name = "RagdollSubject";
            return _instance;
        }
    }
}
