using DoodleUp.Runtime;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 드레싱 데이터 검증의 CLI 진입점. 씬을 굽지 않고 데이터만 본다 —
    /// art 가 에셋을 고친 뒤 <b>씬 빌드를 기다리지 않고</b> 위반 여부를 확인할 수 있어야
    /// 하고, 씬 빌드는 몇 분이 걸리는데 이 검사는 초 단위다.
    ///
    /// <code>
    /// unity run &lt;project&gt; -- -executeMethod DoodleUp.Editor.LastShiftDressingValidation.ValidateForAutomation -quit
    /// </code>
    ///
    /// 위반이 있으면 종료코드가 0이 아니다 — 로그를 사람이 읽어야만 알 수 있는 실패는
    /// 자동화에서 통과와 구분되지 않는다.
    /// </summary>
    public static class LastShiftDressingValidation
    {
        public const string LogTag = "[LAST_SHIFT_DRESSING_VALIDATION]";

        [MenuItem("Last Shift/SP-02A/드레싱 데이터 검증")]
        public static void ValidateFromMenu()
        {
            var (ok, count, message) = Run();
            if (ok) Debug.Log(message);
            else Debug.LogError(message);
            EditorUtility.DisplayDialog("드레싱 데이터 검증",
                ok ? $"통과 — 소품 {count}개" : $"위반 {count}건. Console 로그를 본다.", "확인");
        }

        public static void ValidateForAutomation()
        {
            var (ok, _, message) = Run();
            if (ok)
            {
                Debug.Log(message);
                return;
            }

            Debug.LogError(message);
            // 예외를 던지면 -quit 이 먹혀 0으로 끝나는 경우가 있어 종료코드를 직접 세운다.
            EditorApplication.Exit(1);
        }

        private static (bool ok, int count, string message) Run()
        {
            var set = AssetDatabase.LoadAssetAtPath<LastShiftDressingSet>(LastShiftDressingSet.AssetPath);
            if (set == null)
                return (false, 0, $"{LogTag} path={LastShiftDressingSet.AssetPath} result=FAIL reason=asset-missing");

            var violations = LastShiftDressingRules.Validate(set.Props);
            if (violations.Count == 0)
                return (true, set.Props.Count,
                    $"{LogTag} path={LastShiftDressingSet.AssetPath} props={set.Props.Count} violations=0 result=PASS");

            foreach (var violation in violations)
                Debug.LogError($"{LogTag} {violation}");
            return (false, violations.Count,
                $"{LogTag} path={LastShiftDressingSet.AssetPath} props={set.Props.Count} " +
                $"violations={violations.Count} result=FAIL");
        }
    }
}
