using System.Collections.Generic;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 승무원 프리팹의 <c>Remote Body</c> 안에서 몸 렌더러를 찾는 규칙의 정본.
    ///
    /// <b>메시 이름으로 찾지 않는다.</b> 예전에는 씬 빌더·검증기·런타임이 각자
    /// <c>name.Contains("Combined")</c> 로 골랐다. 아트가 승무원을 래그돌 셸로 쪼개면서
    /// (<c>696cfff</c>) 단일 메시 <c>LastShift_LimeAlien_Combined</c> 가
    /// <c>RagdollShell_Torso/Head/Arm_L/Arm_R/Leg_L/Leg_R</c> 로 갈렸고, 그 이름은
    /// 어디에도 남지 않았다. 세 자리가 동시에 같은 문자열을 놓쳤는데도 아무도 못 봤고,
    /// Windows 플레이어 빌드의 prebuild 검증이 죽고 나서야 드러났다.
    ///
    /// 그래서 규칙을 한 자리에 모으고, 이름이 아니라 <b>구조</b>로 판단한다 — 스킨드
    /// 메시가 붙어 있는가. 다음 재익스포트에서 이름이 또 바뀌어도 이 규칙은 따라간다.
    /// </summary>
    public static class LastShiftCrewBody
    {
        /// <summary>승무원 비주얼이 붙는 자식 오브젝트 이름. 프리팹 계약이라 이것은 이름으로 찾는다.</summary>
        public const string RootName = "Remote Body";

        /// <summary>
        /// 1인칭에서 접는 머리 뼈 이름. 리그가 주는 이름이고
        /// <see cref="LastShiftNetworkPlayer.ApplyLocalPresentation"/> 이 이것으로 머리를 찾는다.
        /// </summary>
        public const string HeadBoneName = LastShiftRagdollRig.HeadBoneName;

        /// <summary>
        /// 몸을 이루는 스킨드 렌더러 전부. 부피가 큰 것부터, 같으면 정점 수, 그 다음 이름순 —
        /// 순서가 흔들리면 <see cref="Primary"/> 가 임포트마다 다른 셸을 고른다.
        /// </summary>
        public static List<SkinnedMeshRenderer> Renderers(Transform body)
        {
            var found = new List<SkinnedMeshRenderer>();
            if (body == null) return found;
            foreach (var renderer in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.sharedMesh != null && renderer.sharedMesh.vertexCount > 0)
                    found.Add(renderer);
            found.Sort(CompareByMeshSize);
            return found;
        }

        /// <summary>
        /// 대표 렌더러 — <b>머리를 뺀</b> 몸에서 가장 큰 셸.
        ///
        /// <b>머리는 대표가 될 수 없다.</b> 이 승무원은 머리가 제일 큰 부위라 "가장 큰 셸"
        /// 로만 고르면 머리가 뽑히는데, 머리는
        /// <see cref="LastShiftNetworkPlayer.ApplyLocalPresentation"/> 이 소유자 화면에서
        /// 뼈 스케일 0 으로 접는 <b>유일한</b> 부위다. 대표가 머리면
        /// <see cref="LastShiftNetworkPlayer.IsBodyVisible"/> 이 접혀서 안 보이는 렌더러를
        /// 두고 "보인다" 고 답하고, 플레이어 색과 유령 반투명도 머리에만 걸려 동료 화면에
        /// 머리만 색칠된 승무원이 선다.
        ///
        /// 그래서 이름이 아니라 <b>위치</b>로 뺀다 — 자세한 이유는
        /// <see cref="SitsInHeadRegion"/> 에 적었다. 셸 이름이 또 바뀌어도 이 규칙은 따라간다.
        ///
        /// <b>이 결과를 빌드 통과 조건으로 쓰지 않는다.</b> 검증기는 저장된 링크가 몸 셸
        /// 중 하나인지만 보고, 여기서 계산한 값과 같은지는 묻지 않는다. 휴리스틱을 합격
        /// 기준에 걸면 아트가 몸을 손댈 때마다 빌드가 다시 죽는다 — 이름으로 찾다가 셸
        /// 분리에 걸려 죽은 그 사고와 같은 모양이다. 여기는 <b>링크가 없을 때 무엇을 걸지</b>
        /// 를 정할 뿐이다.
        /// </summary>
        public static SkinnedMeshRenderer Primary(Transform body)
        {
            var renderers = Renderers(body);
            if (renderers.Count == 0) return null;
            var head = FindHeadBone(body);
            foreach (var renderer in renderers)
                if (!SitsInHeadRegion(renderer, body, head)) return renderer;
            // 몸이 통째로 머리 위에 있는 리그는 없다. 그래도 여기까지 왔다면 리그 쪽이
            // 이상한 것이므로, 아무것도 못 돌려주는 것보다 가장 큰 셸을 내주는 편이 낫다.
            return renderers[0];
        }

        private static Transform FindHeadBone(Transform body)
        {
            foreach (var bone in body.GetComponentsInChildren<Transform>(true))
                if (bone.name == HeadBoneName) return bone;
            return null;
        }

        /// <summary>
        /// 머리 관절보다 위에 중심이 있는 셸인가 — 머리 껍질과 눈알이 여기 걸린다.
        ///
        /// <b>본 목록으로는 못 가른다.</b> 이 리그는 셸마다 전체 본에 바인딩돼 있어서
        /// (스킨 7 개에 클러스터 175 개) "머리 뼈에만 매달린 셸" 이 하나도 없다. 반면
        /// 머리가 접히면 사라지는 것은 머리 관절 위에 놓인 기하이므로, 위치로 가르는 것이
        /// 실제로 벌어지는 일과 맞다. 바인드 포즈의 <see cref="Mesh.bounds"/> 만 쓰므로
        /// 프리팹 에셋 상태에서도 값이 나온다.
        /// </summary>
        private static bool SitsInHeadRegion(SkinnedMeshRenderer renderer, Transform body, Transform head)
        {
            if (head == null) return false;
            var center = body.InverseTransformPoint(
                renderer.transform.TransformPoint(renderer.sharedMesh.bounds.center));
            return center.y >= body.InverseTransformPoint(head.position).y;
        }

        /// <summary>플레이어 루트에서 <c>Remote Body</c> 를 거쳐 대표 렌더러까지 한 번에.</summary>
        public static SkinnedMeshRenderer PrimaryUnderRoot(Transform playerRoot)
        {
            return playerRoot == null ? null : Primary(playerRoot.Find(RootName));
        }

        private static int CompareByMeshSize(SkinnedMeshRenderer left, SkinnedMeshRenderer right)
        {
            var byVolume = Volume(right.sharedMesh).CompareTo(Volume(left.sharedMesh));
            if (byVolume != 0) return byVolume;
            var byVertices = right.sharedMesh.vertexCount.CompareTo(left.sharedMesh.vertexCount);
            return byVertices != 0 ? byVertices : string.CompareOrdinal(left.name, right.name);
        }

        private static float Volume(Mesh mesh)
        {
            var size = mesh.bounds.size;
            return size.x * size.y * size.z;
        }
    }
}
