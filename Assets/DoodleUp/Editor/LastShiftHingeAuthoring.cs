using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 경첩 여닫이를 재고 굽는 공용 부분. <see cref="LastShiftHatchLeaf"/> 가 <b>규격</b>이라면
    /// 여기는 그 규격을 실제 킷과 클립에 옮기는 손이다.
    ///
    /// 상부 해치(킷 FBX)와 압력문(양산 프롭)이 서로 다른 계층·다른 단위를 쓰는데도 같은 코드를
    /// 타는 것이 요점이다 — 규격이 하나면 두 곳이 갈릴 일이 없다.
    /// </summary>
    public static class LastShiftHingeAuthoring
    {
        /// <summary>이름이 <paramref name="prefix"/> 로 시작하는 마디. 문틀처럼 여러 조각인 것을 한 벌로 잰다.</summary>
        public static Transform[] PartsNamed(Transform root, string prefix)
        {
            var parts = new List<Transform>();
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name.StartsWith(prefix, StringComparison.Ordinal)) parts.Add(child);
            if (parts.Count == 0) throw new InvalidOperationException($"No part named {prefix}* under {root.name}");
            return parts.ToArray();
        }

        public static Transform Require(Transform root, string name, string owner)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            throw new InvalidOperationException($"{owner} has no required part {name}");
        }

        /// <summary>
        /// 부품들이 차지하는 자리를 <paramref name="space"/> 로컬 좌표로 잰다.
        ///
        /// <see cref="Renderer.bounds"/> 를 안 쓴다 — 그건 축 정렬된 <b>월드</b> 상자라 판이
        /// 기울어 있으면 부풀고, 지금 고치려는 것이 바로 그 기울기다.
        /// </summary>
        public static Bounds LocalBounds(Transform space, params Transform[] parts)
        {
            var toSpace = space.worldToLocalMatrix;
            var bounds = new Bounds();
            var measured = false;
            foreach (var part in parts)
            foreach (var filter in part.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                var matrix = toSpace * filter.transform.localToWorldMatrix;
                var center = mesh.bounds.center;
                var extents = mesh.bounds.extents;
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = matrix.MultiplyPoint3x4(center + new Vector3(
                        (corner & 1) == 0 ? -extents.x : extents.x,
                        (corner & 2) == 0 ? -extents.y : extents.y,
                        (corner & 4) == 0 ? -extents.z : extents.z));
                    if (measured) bounds.Encapsulate(point);
                    else { bounds = new Bounds(point, Vector3.zero); measured = true; }
                }
            }
            if (!measured) throw new InvalidOperationException($"No mesh to measure under {space.name}");
            return bounds;
        }

        /// <summary>
        /// 경첩 여닫이를 클립으로 굽는다.
        ///
        /// <b>사원수 네 성분을 전부 적는다.</b> 예전에는 <c>m_LocalRotation.y</c> 한 줄만 적었는데,
        /// 그러면 나머지 셋이 <c>0</c> 으로 남아 닫힘 키가 길이 <c>0</c> 짜리 사원수가 된다 —
        /// 자세라고 부를 수 없는 값이고, 열림 키도 정규화되면서 의도한 각과 무관해진다.
        ///
        /// 자리도 같이 적는다. 경첩이 판 원점이 아니라 구멍 모서리에 있어 판이 호를 그리기 때문이다.
        /// </summary>
        public static void WriteSwing(AnimationClip clip, string path, in LastShiftHatchLeaf leaf)
        {
            var count = leaf.KeyCount;
            var times = new float[count];
            var positions = new Vector3[count];
            var rotations = new Quaternion[count];
            for (var i = 0; i < count; i++)
            {
                times[i] = (float)i / (count - 1);
                leaf.Pose(times[i], out positions[i], out rotations[i]);
                // q 와 -q 는 같은 자세다. 이웃 키와 부호가 갈리면 보간이 먼 길로 돌아가므로
                // 앞 키 쪽으로 맞춰 둔다.
                if (i > 0 && Quaternion.Dot(rotations[i - 1], rotations[i]) < 0f)
                    rotations[i] = new Quaternion(-rotations[i].x, -rotations[i].y, -rotations[i].z, -rotations[i].w);
            }

            WriteChannel(clip, path, "m_LocalPosition.x", times, i => positions[i].x);
            WriteChannel(clip, path, "m_LocalPosition.y", times, i => positions[i].y);
            WriteChannel(clip, path, "m_LocalPosition.z", times, i => positions[i].z);
            WriteChannel(clip, path, "m_LocalRotation.x", times, i => rotations[i].x);
            WriteChannel(clip, path, "m_LocalRotation.y", times, i => rotations[i].y);
            WriteChannel(clip, path, "m_LocalRotation.z", times, i => rotations[i].z);
            WriteChannel(clip, path, "m_LocalRotation.w", times, i => rotations[i].w);
        }

        /// <summary>
        /// 한 성분을 선형 접선으로 적는다. 키가 <c>10°</c> 간격이라 곡선을 매끄럽게 만들 이유가
        /// 없고, 접선을 자동으로 두면 굽는 쪽 환경에 따라 값이 달라져 <c>.anim</c> 이 매번 diff 난다.
        /// </summary>
        public static void WriteChannel(AnimationClip clip, string path, string property,
            float[] times, Func<int, float> value)
        {
            var keys = new Keyframe[times.Length];
            for (var i = 0; i < times.Length; i++)
            {
                var previous = i > 0 ? (value(i) - value(i - 1)) / (times[i] - times[i - 1]) : 0f;
                var next = i < times.Length - 1 ? (value(i + 1) - value(i)) / (times[i + 1] - times[i]) : 0f;
                keys[i] = new Keyframe(times[i], value(i),
                    i > 0 ? previous : next, i < times.Length - 1 ? next : previous);
            }
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(path, typeof(Transform), property), new AnimationCurve(keys));
        }
    }
}
