using System;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>부위별 래그돌이 만드는 물리 바디 하나. 언리얼 Physics Asset 의 "본 바디"에 해당한다.</summary>
    public enum LastShiftRagdollPart
    {
        Pelvis,
        Spine,
        Chest,
        Head,
        UpperArmL,
        ForearmL,
        UpperArmR,
        ForearmR,
        ThighL,
        ShinL,
        ThighR,
        ShinR
    }

    /// <summary>콜라이더 반지름을 무엇에서 뽑을지. 뼈 길이만으로는 몸통이 실제 폭을 못 얻는다.</summary>
    public enum LastShiftRagdollGirth
    {
        /// <summary>자기 뼈 길이 × 배율. 팔·다리처럼 가늘고 긴 부위용.</summary>
        BoneLength,

        /// <summary>좌우 허벅지 뼈 간격 × 배율. 골반용.</summary>
        HipSpan,

        /// <summary>좌우 위팔 뼈 간격 × 배율. 척추·가슴용.</summary>
        ShoulderSpan,

        /// <summary>머리 뼈에서 메시 정수리까지 × 배율. 머리 구체용.</summary>
        CrownRise
    }

    /// <summary>부위 하나의 정본 스펙. 좌표·크기는 안 들어간다 — 전부 실제 뼈 배치에서 뽑는다.</summary>
    public readonly struct LastShiftRagdollBone
    {
        public LastShiftRagdollBone(
            LastShiftRagdollPart part,
            string boneName,
            string tipBoneName,
            LastShiftRagdollPart parent,
            bool isRoot,
            float massRatio,
            LastShiftRagdollGirth girth,
            float girthScale,
            float swing1Limit,
            float swing2Limit,
            float twistLimit,
            bool isHinge = false,
            bool hingeBendsForward = false)
        {
            IsHinge = isHinge;
            HingeBendsForward = hingeBendsForward;
            Part = part;
            BoneName = boneName;
            TipBoneName = tipBoneName;
            Parent = parent;
            IsRoot = isRoot;
            MassRatio = massRatio;
            Girth = girth;
            GirthScale = girthScale;
            Swing1Limit = swing1Limit;
            Swing2Limit = swing2Limit;
            TwistLimit = twistLimit;
        }

        public LastShiftRagdollPart Part { get; }

        /// <summary>리그 안의 뼈 이름. Generic 리그라 <c>HumanBodyBones</c> 가 없어 이름으로 직접 찾는다.</summary>
        public string BoneName { get; }

        /// <summary>
        /// 길이를 재는 상대 뼈. 캡슐 길이는 "이 뼈에서 저 뼈까지"로만 정해진다 —
        /// 모델 비율이 바뀌어도 스펙을 안 고쳐도 되게 하려는 것이다.
        /// 머리는 자식 뼈가 없어 <c>null</c> 이고 <see cref="LastShiftRagdollGirth.CrownRise"/> 로 대신 잰다.
        /// </summary>
        public string TipBoneName { get; }

        /// <summary>조인트가 붙을 부모 부위. <see cref="IsRoot"/> 면 무시된다.</summary>
        public LastShiftRagdollPart Parent { get; }

        /// <summary>조인트 없이 자유롭게 나는 바디(골반) 인지.</summary>
        public bool IsRoot { get; }

        /// <summary>질량 배분 비율. 합이 1 이 아니어도 되고 <see cref="LastShiftRagdollRig.MassOf"/> 가 정규화한다.</summary>
        public float MassRatio { get; }

        public LastShiftRagdollGirth Girth { get; }
        public float GirthScale { get; }

        /// <summary>주 스윙 한계(도). 트위스트 축에 수직인 첫 축.</summary>
        public float Swing1Limit { get; }

        /// <summary>보조 스윙 한계(도). 팔꿈치·무릎을 경첩처럼 만드는 값이 여기 들어간다.</summary>
        public float Swing2Limit { get; }

        /// <summary>비틀림 한계(도). 대칭으로 ±로 쓴다.</summary>
        public float TwistLimit { get; }

        /// <summary>
        /// 볼 조인트가 아니라 <b>경첩</b>인가. 무릎·팔꿈치가 여기 해당한다.
        ///
        /// 이 둘을 <c>CharacterJoint</c> 로 두면 <see cref="Swing1Limit"/> 85 · <see cref="Swing2Limit"/> 10
        /// 처럼 <b>극단적으로 찌그러진 스윙 콘</b>이 되는데, PhysX 는 이 비율에서 한계를 못 지킨다 —
        /// 실측으로 무릎이 한계 85° 인데 175° 까지 접혔고, 솔버 반복을 12/4 → 32/8 로 올려도
        /// 1.8배 초과가 남았다. 경첩은 애초에 1자유도라 나머지 두 축이 아예 잠긴다.
        /// </summary>
        public bool IsHinge { get; }

        /// <summary>
        /// 경첩이 접히는 쪽. 팔꿈치는 손이 <b>앞으로</b>(가슴 쪽), 무릎은 발이 <b>뒤로</b> 간다.
        /// 축의 부호는 리그마다 다르므로 값으로 박지 않고, 빌드 시점에 실제 뼈를 이 방향으로
        /// 돌려 보고 정한다 — 리그를 다시 뽑아도 안 틀어지게 하려는 것이다.
        /// </summary>
        public bool HingeBendsForward { get; }
    }

    /// <summary>
    /// <c>LastShiftLimeAlien_Rigged.fbx</c> 리그를 부위별 래그돌로 바꾸는 정본 표.
    ///
    /// <b>이 리그는 Generic 이다</b>(<c>animationType: 2</c>). 그래서 <c>Animator.GetBoneTransform</c>
    /// 이나 Unity Ragdoll Wizard 가 기대하는 <c>HumanBodyBones</c> 매핑을 못 쓴다 — 뼈를 이름으로
    /// 직접 찾는 수밖에 없고, 이름이 바뀌면 조용히 실패하므로 EditMode 테스트가 실제 FBX 를 열어
    /// 이 표의 이름이 전부 존재하는지 지킨다(<c>LastShiftRagdollTests</c>).
    ///
    /// 실제 뼈 계층(<c>Last Shift/Prototype/Dump Character Rig</c> 로 뽑았다):
    /// <code>
    /// root/DEF-spine/DEF-spine.001/…/DEF-spine.006          ← 척추·머리
    /// LastShift_LimeAlien_Rig/…/ORG-spine/DEF-thigh.L/DEF-thigh.L.001/DEF-shin.L/…/DEF-foot.L
    ///                         …/ORG-shoulder.L/DEF-upper_arm.L/…/DEF-forearm.L/…/DEF-hand.L
    /// </code>
    /// <b>척추와 팔다리가 다른 가지에 있다.</b> Rigify 가 변형본을 제어본 밑에 흩어 놓기 때문인데,
    /// 조인트는 계층이 아니라 <c>connectedBody</c> 로 잇고 뼈 탐색도 인스턴스 전체를 훑으므로
    /// 래그돌에는 영향이 없다. <c>MCH-</c>/<c>ORG-</c>/<c>tweak</c>/<c>spine_fk</c> 는 웨이트를
    /// 하나도 안 들고 있어 물리 바디를 안 준다 — 주면 몸이 두 갈래로 끌린다.
    ///
    /// 각 팔다리는 <c>DEF-upper_arm.L</c> + <c>DEF-upper_arm.L.001</c> 처럼 두 마디로 쪼개져 있다.
    /// 바디는 <b>앞마디에만</b> 주고 길이는 다음 관절 뼈까지로 잰다 — 뒷마디는 앞마디를 따라오는
    /// 순수 변형용이라 바디를 주면 팔꿈치가 두 번 접힌다.
    ///
    /// <b>손·발에는 바디를 안 둔다.</b> 13개 → 12개로 줄이면 저중력에서 솔버가 눈에 띄게 안정되고,
    /// 대신 아래팔·정강이 캡슐이 손끝·발끝까지 덮게 길이를 잡는다. 프로토타입 목표가 "머리·팔이
    /// 덜렁거리는가"라 손가락 단위 반응은 아직 필요 없다.
    /// </summary>
    public static class LastShiftRagdollRig
    {
        /// <summary>모델 루트 밑의 아마추어 오브젝트 이름.</summary>
        public const string ArmatureName = "LastShift_LimeAlien_Rig";

        /// <summary>
        /// <b>이 표는 Rigify 변형본(<c>DEF-</c>) 이름을 쓴다.</b> 2026-08-18 사용자 결정으로
        /// 옛 리그(<c>pelvis/spine/chest/head/upper_arm…</c>)를 버리고 새 Rigify 리그를 정본으로
        /// 삼았다. 옛 이름은 이제 어디에도 없으므로 되돌리지 말 것.
        ///
        /// <b>척추와 팔다리가 서로 다른 가지에 있다.</b> <c>DEF-spine…006</c> 은 <c>root</c> 밑에,
        /// 팔다리는 제어 리그(<c>MCH-/ORG-/tweak</c>) 밑에 달려 있다. 래그돌은 조인트의
        /// <c>connectedBody</c> 로 부위를 잇지 계층으로 잇지 않으므로 이 갈림은 문제가 안 된다 —
        /// <see cref="LastShiftRagdoll"/> 의 뼈 탐색도 아마추어가 아니라 인스턴스 전체를 훑는다.
        ///
        /// 제어본은 웨이트를 하나도 안 들고 있다(실측: 변형 합 6,921 = 정점 수, 제어 합 0).
        /// 그래서 물리가 <c>DEF-</c> 만 움직여도 메시가 안 찢어진다. 리그를 다시 뽑았는데 이
        /// 전제가 깨지면 <c>Last Shift/Prototype/Dump Character Rig</c> 가 바로 보여 준다.
        /// </summary>
        public const string PelvisBoneName = "DEF-spine";

        public const string SpineBoneName = "DEF-spine.001";
        public const string ChestBoneName = "DEF-spine.003";
        public const string HeadBoneName = "DEF-spine.006";

        /// <summary>좌우 간격을 재는 기준 뼈들. 몸통 두께가 여기서 나온다.</summary>
        public const string LeftHipBoneName = "DEF-thigh.L";
        public const string RightHipBoneName = "DEF-thigh.R";
        public const string LeftShoulderBoneName = "DEF-upper_arm.L";
        public const string RightShoulderBoneName = "DEF-upper_arm.R";

        /// <summary>
        /// <b>물리를 안 따라가던 변형본을 어디에 매달 것인가.</b>
        ///
        /// Rigify 는 변형본을 제어본(<c>ORG-</c>/<c>MCH-</c>/<c>tweak</c>) 밑에 흩어 놓는다.
        /// 래그돌은 그중 열둘에만 바디를 주므로, 나머지 변형본은 부모가 물리를 안 받아
        /// 몸이 날아가는 동안 <b>바인드 포즈에 박혀</b> 있었다 — 어깨·가슴·골반 정점이 제자리에
        /// 남고 나머지가 끌려가 메시가 찢어졌다(사용자 판정 2026-08-18).
        ///
        /// 팔다리 본은 바디가 있어 스스로는 움직이지만 계층상으로는 여전히 정적 제어본의
        /// 자식이었다. 그래서 <b>변형 골격 전체를 물리 본 밑으로 다시 엮는다</b> — 골반에서
        /// 시작하는 하나의 트리가 되고, 제어 계층은 변형 경로에서 완전히 빠진다.
        ///
        /// 월드 변환을 유지한 채 부모만 바꾸므로 정지 자세는 한 밀리미터도 안 움직인다.
        /// </summary>
        public static readonly (string BoneName, LastShiftRagdollPart AttachTo)[] DeformAttachments =
        {
            ("DEF-thigh.L", LastShiftRagdollPart.Pelvis),
            ("DEF-thigh.R", LastShiftRagdollPart.Pelvis),
            ("DEF-pelvis.L", LastShiftRagdollPart.Pelvis),
            ("DEF-pelvis.R", LastShiftRagdollPart.Pelvis),
            ("DEF-upper_arm.L", LastShiftRagdollPart.Chest),
            ("DEF-upper_arm.R", LastShiftRagdollPart.Chest),
            ("DEF-shoulder.L", LastShiftRagdollPart.Chest),
            ("DEF-shoulder.R", LastShiftRagdollPart.Chest),
            ("DEF-breast.L", LastShiftRagdollPart.Chest),
            ("DEF-breast.R", LastShiftRagdollPart.Chest)
        };

        /// <summary>
        /// 승무원 한 명의 총 질량(kg). 절대값 자체는 재미에 안 걸리지만 <b>비율</b>은 걸린다 —
        /// 머리가 무거우면 목이 끌려가 코믹해지고, 가벼우면 톡 튀고 만다. 임펄스 세기는 전부
        /// 이 값을 기준으로 잡혀 있어서 여기를 바꾸면 <see cref="LastShiftRagdollTuning"/> 의
        /// 임펄스도 같이 봐야 한다.
        /// </summary>
        public const float TotalMass = 62f;

        /// <summary>
        /// 부위 표. 관절 한계는 "사람이 안 되는 자세"를 막는 게 아니라 <b>어디까지 덜렁거려도
        /// 되는가</b>를 정한다 — 목·어깨를 크게 열고 팔꿈치·무릎을 경첩으로 좁히는 배분이다.
        /// </summary>
        public static readonly LastShiftRagdollBone[] Bones =
        {
            // 골반. 조인트가 없는 유일한 바디라 여기가 래그돌 전체의 무게 중심이자 임펄스 기준점이다.
            new LastShiftRagdollBone(LastShiftRagdollPart.Pelvis, PelvisBoneName, SpineBoneName,
                LastShiftRagdollPart.Pelvis, true, 0.16f,
                LastShiftRagdollGirth.HipSpan, 0.45f, 0f, 0f, 0f),

            // 허리·가슴. 여기를 너무 열면 몸이 접혀 코믹이 아니라 고장으로 보인다.
            new LastShiftRagdollBone(LastShiftRagdollPart.Spine, SpineBoneName, ChestBoneName,
                LastShiftRagdollPart.Pelvis, false, 0.10f,
                LastShiftRagdollGirth.ShoulderSpan, 0.30f, 25f, 25f, 20f),
            new LastShiftRagdollBone(LastShiftRagdollPart.Chest, ChestBoneName, HeadBoneName,
                LastShiftRagdollPart.Spine, false, 0.20f,
                LastShiftRagdollGirth.ShoulderSpan, 0.34f, 20f, 20f, 15f),

            // 머리. 가장 크게 열어 둔 관절 — "머리가 덜렁거린다"가 이 카드의 1순위 목표다.
            // 비율 0.15 는 사람 비율(약 0.08)의 두 배 가까이인데, 이 승무원의 머리가 실제로
            // 몸의 절반쯤 되는 두들 비례라서다(실측: 신장 1.65m 중 머리 뼈부터 정수리까지 0.77m).
            // 사람 비율을 그대로 쓰면 큰 머리가 가볍게 톡 튀어 목이 안 끌린다.
            new LastShiftRagdollBone(LastShiftRagdollPart.Head, HeadBoneName, null,
                LastShiftRagdollPart.Chest, false, 0.15f,
                LastShiftRagdollGirth.CrownRise, 0.5f, 55f, 40f, 45f),

            // 팔. 어깨는 넓게, 팔꿈치는 한 축만 열어 경첩처럼.
            new LastShiftRagdollBone(LastShiftRagdollPart.UpperArmL, LeftShoulderBoneName, "DEF-forearm.L",
                LastShiftRagdollPart.Chest, false, 0.028f,
                LastShiftRagdollGirth.BoneLength, 0.30f, 85f, 70f, 60f),
            new LastShiftRagdollBone(LastShiftRagdollPart.ForearmL, "DEF-forearm.L", "DEF-hand.L",
                LastShiftRagdollPart.UpperArmL, false, 0.018f,
                LastShiftRagdollGirth.BoneLength, 0.26f, 90f, 10f, 10f, true, true),
            new LastShiftRagdollBone(LastShiftRagdollPart.UpperArmR, RightShoulderBoneName, "DEF-forearm.R",
                LastShiftRagdollPart.Chest, false, 0.028f,
                LastShiftRagdollGirth.BoneLength, 0.30f, 85f, 70f, 60f),
            new LastShiftRagdollBone(LastShiftRagdollPart.ForearmR, "DEF-forearm.R", "DEF-hand.R",
                LastShiftRagdollPart.UpperArmR, false, 0.018f,
                LastShiftRagdollGirth.BoneLength, 0.26f, 90f, 10f, 10f, true, true),

            // 다리. 무릎도 경첩. 저중력에서 다리가 자유로우면 착지가 성립을 안 한다.
            new LastShiftRagdollBone(LastShiftRagdollPart.ThighL, LeftHipBoneName, "DEF-shin.L",
                LastShiftRagdollPart.Pelvis, false, 0.095f,
                LastShiftRagdollGirth.BoneLength, 0.32f, 70f, 35f, 30f),
            new LastShiftRagdollBone(LastShiftRagdollPart.ShinL, "DEF-shin.L", "DEF-foot.L",
                LastShiftRagdollPart.ThighL, false, 0.048f,
                LastShiftRagdollGirth.BoneLength, 0.26f, 85f, 10f, 10f, true, false),
            new LastShiftRagdollBone(LastShiftRagdollPart.ThighR, RightHipBoneName, "DEF-shin.R",
                LastShiftRagdollPart.Pelvis, false, 0.095f,
                LastShiftRagdollGirth.BoneLength, 0.32f, 70f, 35f, 30f),
            new LastShiftRagdollBone(LastShiftRagdollPart.ShinR, "DEF-shin.R", "DEF-foot.R",
                LastShiftRagdollPart.ThighR, false, 0.048f,
                LastShiftRagdollGirth.BoneLength, 0.26f, 85f, 10f, 10f, true, false)
        };

        /// <summary>비율 합. 표를 고쳐도 총 질량이 안 흔들리게 정규화 분모로 쓴다.</summary>
        public static float MassRatioSum
        {
            get
            {
                var sum = 0f;
                for (var i = 0; i < Bones.Length; i++) sum += Bones[i].MassRatio;
                return sum;
            }
        }

        /// <summary>부위 하나의 실제 질량(kg).</summary>
        public static float MassOf(LastShiftRagdollPart part)
        {
            return TotalMass * SpecOf(part).MassRatio / MassRatioSum;
        }

        public static LastShiftRagdollBone SpecOf(LastShiftRagdollPart part)
        {
            for (var i = 0; i < Bones.Length; i++)
                if (Bones[i].Part == part)
                    return Bones[i];
            throw new ArgumentOutOfRangeException(nameof(part), $"래그돌 표에 {part} 가 없다.");
        }

        /// <summary>
        /// 두 부위가 뼈 그래프에서 몇 다리 떨어져 있는지. 자기 몸끼리 충돌을 어디까지 끌지
        /// 정하는 데 쓴다 — 어깨·사타구니처럼 생성 시점부터 겹쳐 있는 쌍을 그대로 두면
        /// 첫 프레임에 래그돌이 터진다.
        /// </summary>
        public static int GraphDistance(LastShiftRagdollPart a, LastShiftRagdollPart b)
        {
            if (a == b) return 0;

            var depthA = DepthOf(a);
            var depthB = DepthOf(b);
            var steps = 0;

            while (depthA > depthB)
            {
                a = SpecOf(a).Parent;
                depthA--;
                steps++;
            }

            while (depthB > depthA)
            {
                b = SpecOf(b).Parent;
                depthB--;
                steps++;
            }

            while (a != b)
            {
                a = SpecOf(a).Parent;
                b = SpecOf(b).Parent;
                steps += 2;
            }

            return steps;
        }

        private static int DepthOf(LastShiftRagdollPart part)
        {
            var depth = 0;
            while (!SpecOf(part).IsRoot)
            {
                part = SpecOf(part).Parent;
                depth++;
            }

            return depth;
        }
    }
}
