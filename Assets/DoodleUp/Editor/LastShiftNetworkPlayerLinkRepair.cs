using DoodleUp.Runtime;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 승무원 프리팹의 몸 렌더러 링크가 끊기면 임포트 직후에 다시 잇는다.
    ///
    /// <b>왜 끊기는가.</b> <c>LastShiftNetworkPlayer.bodyRenderer</c> 는 중첩 프리팹
    /// (<c>Remote Body</c>) 안의 컴포넌트를 가리키고, 그 참조는 결국 FBX 안의 fileID 로
    /// 내려간다. 아트가 캐릭터를 재익스포트해서 메시가 갈리거나 합쳐지면 그 fileID 가
    /// 사라지고 링크는 <c>null</c> 이 된다. 프리팹 파일 자체는 멀쩡해 보이고 diff 도 없다.
    ///
    /// 실제로 그렇게 됐다 — <c>696cfff</c> 에서 몸이 래그돌 셸로 갈렸는데 프리팹을 다시
    /// 굽지 않아 링크가 옛 통짜 메시를 가리킨 채 남았고, <b>Windows 플레이어 빌드의
    /// prebuild 검증이 죽을 때까지</b> 아무도 몰랐다. 사람이 프리팹을 다시 굽는 것을
    /// 기억해야만 성립하는 계약이라 그렇다.
    ///
    /// 그래서 임포트가 끝난 자리에서 기계가 잇는다. 끊겼을 때만 손대므로 정상 상태에서는
    /// 파일이 바뀌지 않는다 — 조용한 재직렬화 커밋을 만들지 않는다.
    /// </summary>
    internal sealed class LastShiftNetworkPlayerLinkRepair : AssetPostprocessor
    {
        /// <summary>
        /// 링크를 대표 셸로 다시 건다 — <b>끊기지 않았어도</b> 건다. 아트가 몸을 갈아
        /// 대표가 바뀌었을 때 사람이 눌러 맞추는 자리다.
        ///
        /// <see cref="LastShiftNetworkSceneBuilder.RebuildSandboxForAutomation"/> 도 결과적으로
        /// 고쳐 주지만 저쪽은 선체·드레싱·씬까지 통째로 다시 구워서 관계없는 재직렬화 수천
        /// 줄을 남긴다 — 링크 한 줄을 옮기려고 치를 값이 아니다.
        /// </summary>
        [MenuItem("Last Shift/SP-02A/Relink Player Body To Primary Shell")]
        public static void RelinkPlayerBodyToPrimary()
        {
            AssetDatabase.ImportAsset(
                LastShiftNetworkSceneBuilder.PlayerPrefabPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Relink(LastShiftNetworkSceneBuilder.PlayerPrefabPath, "RELINKED");
            AssetDatabase.SaveAssets();
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (var path in importedAssets)
            {
                if (path != LastShiftNetworkSceneBuilder.PlayerPrefabPath) continue;
                RepairIfBroken(path);
            }
        }

        /// <summary>
        /// <b>끊겼을 때만 잇는다.</b> 임포트마다 "대표로 계산한 셸과 다르면 고친다" 로 돌면
        /// 아트가 메시를 손대 계산이 뒤집힐 때마다 프리팹이 조용히 다시 써지고, 그 커밋을
        /// 아무도 의도하지 않는다. 자동 경로가 아는 것은 링크가 살아 있는가 하나다.
        /// </summary>
        private static void RepairIfBroken(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;
            var player = prefab.GetComponent<LastShiftNetworkPlayer>();
            if (player == null) return;

            var shells = LastShiftCrewBody.Renderers(prefab.transform.Find(LastShiftCrewBody.RootName));
            var linked = player.BodyRenderer as SkinnedMeshRenderer;
            if (linked != null && shells.Contains(linked)) return;
            Relink(path, "REPAIRED");
        }

        private static void Relink(string path, string outcome)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<LastShiftNetworkPlayer>() == null) return;

            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var editable = contents.GetComponent<LastShiftNetworkPlayer>();
                var target = LastShiftCrewBody.PrimaryUnderRoot(contents.transform);
                var controller = contents.GetComponent<LastShiftPlayerController>();
                var camera = contents.GetComponentInChildren<Camera>(true);
                // 셋 중 하나라도 없으면 손대지 않는다. Configure 는 셋을 한꺼번에 덮으므로
                // 여기서 null 을 넘기면 멀쩡한 컨트롤러·카메라 링크까지 같이 지운다.
                if (editable == null || target == null || controller == null || camera == null) return;
                if (editable.BodyRenderer == target) return;
                editable.Configure(controller, camera, target);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                Debug.Log($"[LAST_SHIFT_PLAYER_LINK] path={path} bodyRenderer={target.name} result={outcome}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
