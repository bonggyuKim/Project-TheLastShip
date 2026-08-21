using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 관절 한계를 <b>스킨이 버티는 각도</b>와 <b>손으로 잡아 둔 원본</b>의 교집합으로 맞춘다.
    ///
    /// <b>규칙은 하나다.</b> `min(원본, 스킨여유)`. 두 상한을 모두 지킨다.
    /// <list type="bullet">
    /// <item>원본을 <b>넘겨 열지 않는다</b> — 무릎·팔꿈치의 5도 경첩은 일부러 좁힌 설계다.
    /// 스킨이 60도를 버틴다고 열어 주면 다리가 반대로 꺾인다.</item>
    /// <item>스킨 여유를 <b>넘겨 열지 않는다</b> — 넘기면 그 각에서 메시가 찢어진다.</item>
    /// </list>
    ///
    /// <b>측정 상한(60도)에 닿은 축은 원본을 그대로 쓴다.</b> <see cref="LastShiftSkinToleranceProbe"/>
    /// 는 60도까지만 벌려 보므로, 60이 나왔다는 것은 "60까지 멀쩡했다"이지 "60이 한계다"가 아니다.
    /// 근거 없이 원본을 깎지 않기 위해 이 경우는 제한 없음으로 본다.
    ///
    /// <b>두 번 쟀고 값이 크게 달라졌다(2026-08-19).</b> 첫 측정은 굽힘 분산을 넣기 <b>전</b>이라
    /// 스킨 여유를 실제보다 좁게 봤고, 그 값으로 관절 넷을 과하게 조였다. 분산을 켜고 다시 재니
    /// 팔다리는 대부분 측정 상한에 닿았다. 그래서 그때 깎은 것을 여기서 원복한다.
    /// 재측정은 <see cref="LastShiftSkinToleranceProbe"/> 가 한다.
    /// </summary>
    public static class LastShiftRagdollSkinLimits
    {
        /// <summary>
        /// 손으로 잡아 둔 원본 한계. <b>상한이다 — 여기를 넘겨 열지 않는다.</b>
        ///
        /// 2026-08-19 첫 조임 직전에 씬에서 읽은 값이다. 그 뒤 프리팹에도 조인 값을 올려서
        /// 지금은 어느 애셋에서도 원본을 되읽을 수 없으므로 여기에 남긴다.
        ///
        /// <b>엉덩이만 2026-08-21 에 다시 뽑았다.</b> 이 표의 값들은 조인트 축이 뼈와 어긋나
        /// 있던 때(엉덩이 비틀림 축·뼈 내적 <c>0.21</c>, 78도 어긋남) 손으로 잡은 것이다.
        /// 커밋 <c>1764aa9</c> 가 축을 뼈에 맞추면서 <b>같은 숫자가 다른 자유도에 걸렸고</b>,
        /// 엉덩이에서 그 차이가 컸다 — 옛 축에서 "굽힘"이던 <c>-20..70</c>(90도)이 축을 고친 뒤로는
        /// <b>넓적다리를 제 축 둘레로 90도 돌리는 허가</b>가 됐다. 실측(2026-08-21)에서 넘어질 때마다
        /// 엉덩이 비틀림이 정확히 그 <c>70</c> 도 끝에서 멈췄고, 무릎 경첩이 그 돌아간 평면을 따라
        /// 접히면서 다리가 개구리처럼 옆으로 퍼졌다. 반대로 스윙 콘 <c>30/10</c> 은 <b>너무 좁아
        /// 솔버가 못 지킨다</b> — 같은 측정에서 스윙2 가 10도 한계를 47도까지 뚫었다.
        /// 솔버 반복을 40/16 까지 올려도, TGS 로 바꿔도, 전처리를 켜도 안 잡혔다.
        ///
        /// 그래서 엉덩이만 <see cref="LastShiftRagdollRig"/> 설계표
        /// (<c>ThighL/R</c>: 스윙 <c>70/35</c> · 비틀림 <c>30</c>)로 되돌린다. 그 표는 축이 뼈를
        /// 따른다는 전제로 쓰인 유일한 값이라 지금 프레임에서 뜻이 통한다.
        /// 나머지 일곱은 축 어긋남이 작았거나(발목 <c>0.71</c>) 바디 강화만으로 한계를 지켰다
        /// (실측 초과 <c>1.7~13.5</c>도) — 근거 없이 같이 흔들지 않는다.
        /// </summary>
        private static readonly Dictionary<string, (float Swing1, float Swing2, float TwistLow, float TwistHigh)> Authored =
            new Dictionary<string, (float, float, float, float)>
            {
                { "DEF-thigh", (70f, 35f, -30f, 30f) },
                { "DEF-shin", (5f, 5f, -80f, 0f) },
                { "DEF-foot", (30f, 20f, -30f, 30f) },
                { "DEF-upper_arm", (50f, 30f, -70f, 10f) },
                { "DEF-forearm", (5f, 5f, -90f, 0f) },
                { "DEF-hand", (30f, 20f, -40f, 40f) },
                { "DEF-spine.003", (20f, 10f, -20f, 20f) },
                { "DEF-spine.006", (40f, 25f, -40f, 25f) }
            };

        /// <summary>
        /// 스킨이 버티는 각. <see cref="LastShiftSkinToleranceProbe"/> 를 <b>굽힘 분산을 켠 채</b>
        /// 돌려 얻은 값이다(2026-08-19 재측정). 60 은 측정 상한이라 "제한 없음"을 뜻한다.
        /// </summary>
        private static readonly Dictionary<string, (float Swing1, float Swing2, float TwistLow, float TwistHigh)> SkinSafe =
            new Dictionary<string, (float, float, float, float)>
            {
                { "DEF-thigh", (60f, 60f, -60f, 60f) },
                { "DEF-shin", (60f, 60f, -60f, 60f) },
                { "DEF-foot", (60f, 60f, -60f, 60f) },
                { "DEF-upper_arm", (40f, 60f, -50f, 60f) },
                { "DEF-forearm", (60f, 60f, -60f, 60f) },
                { "DEF-hand", (60f, 60f, -60f, 60f) },
                { "DEF-spine.003", (15f, 20f, -60f, 50f) },
                { "DEF-spine.006", (20f, 10f, -10f, 5f) }
            };

        [MenuItem("Last Shift/Prototype/Fit Ragdoll Joint Limits To Skin")]
        private static void ApplyToOpenScene()
        {
            var subject = GameObject.Find("RagdollSubject");
            if (subject == null)
            {
                Debug.LogError("RagdollSubject 를 못 찾았다 — 래그돌 랩 씬을 먼저 열어라.");
                return;
            }

            var changed = Apply(subject);
            Debug.Log(changed.Count == 0
                ? "관절 한계: 바꿀 것이 없다 — 이미 원본∩스킨여유 안에 있다."
                : "관절 한계를 맞췄다:\n" + string.Join("\n", changed));
        }

        /// <summary>
        /// 한계를 맞추고 바뀐 내용을 돌려준다. <b>콜라이더·리지드바디·계층은 안 건드린다.</b>
        /// </summary>
        public static List<string> Apply(GameObject subject)
        {
            var changed = new List<string>();

            foreach (var joint in subject.GetComponentsInChildren<CharacterJoint>(true))
            {
                var kind = LastShiftSkinToleranceProbe.KindOf(joint.name);
                if (!Authored.TryGetValue(kind, out var authored)) continue;
                if (!SkinSafe.TryGetValue(kind, out var safe)) continue;

                Undo.RecordObject(joint, "Fit ragdoll joint limits to skin");

                var before = Describe(joint);

                var swing1 = joint.swing1Limit;
                swing1.limit = Mathf.Min(authored.Swing1, Ceiling(safe.Swing1, authored.Swing1));
                joint.swing1Limit = swing1;

                var swing2 = joint.swing2Limit;
                swing2.limit = Mathf.Min(authored.Swing2, Ceiling(safe.Swing2, authored.Swing2));
                joint.swing2Limit = swing2;

                // 비틀림 낮은 쪽은 음수다. "덜 연다" 는 값을 <b>올리는</b> 것이다.
                var low = joint.lowTwistLimit;
                low.limit = Mathf.Max(authored.TwistLow, -Ceiling(-safe.TwistLow, -authored.TwistLow));
                joint.lowTwistLimit = low;

                var high = joint.highTwistLimit;
                high.limit = Mathf.Min(authored.TwistHigh, Ceiling(safe.TwistHigh, authored.TwistHigh));
                joint.highTwistLimit = high;

                var after = Describe(joint);
                if (before == after) continue;

                EditorUtility.SetDirty(joint);
                changed.Add($"  {joint.name}: {before} -> {after}");
            }

            return changed;
        }

        /// <summary>
        /// 측정 상한에 닿은 축은 제한 없음으로 본다 — 그 축에서는 원본이 유일한 상한이다.
        /// </summary>
        private static float Ceiling(float measured, float authored) =>
            measured >= LastShiftSkinToleranceProbe.MaxAngleDegrees ? Mathf.Abs(authored) : measured;

        private static string Describe(CharacterJoint joint) =>
            $"{joint.swing1Limit.limit:F0}/{joint.swing2Limit.limit:F0}/"
            + $"{joint.lowTwistLimit.limit:F0}..{joint.highTwistLimit.limit:F0}";
    }
}
