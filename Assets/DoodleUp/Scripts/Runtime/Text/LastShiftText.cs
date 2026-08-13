using System.Collections.Generic;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 플레이어에게 보이는 모든 글자의 조회 창구.
    ///
    /// <b>코드에는 키만 남는다.</b> 문안은 <c>Assets/DoodleUp/Resources/Text/&lt;로케일&gt;.json</c>
    /// 한 곳에 있고, 여기서는 그것을 게임 시작 때 한 번 올려 사전으로 들고 있는다. 지금까지는
    /// 같은 문장이 대본 파일·UI 파일·컨트롤러에 흩어져 있어서 용어를 하나 바꾸면 어디가 남는지
    /// 아무도 몰랐다 — 이 창구가 생기는 이유의 절반이 그것이고, 나머지 절반이 다국어다.
    ///
    /// <b>없는 키는 죽지 않고 드러난다.</b> 조회에 실패하면 예외 대신 <c>⟨key⟩</c> 를 돌려주고
    /// 한 번만 경고한다 — 대사 한 줄이 빠졌다고 게임이 멈추면 안 되지만, 화면에 그대로 보여야
    /// 다음 판에서 잡힌다. 빠진 키 목록은 <see cref="MissingKeys"/> 로 검사가 통째로 읽는다.
    ///
    /// <b>정적 상태다.</b> 씬·모드 전환에서 안 지워지는 대신, 검사는 <see cref="Clear"/> 로
    /// 격리한다 — 이 프로젝트의 다른 정적 상태(<see cref="LastShiftAirlock"/> 등)와 같은 규칙이다.
    /// </summary>
    public static class LastShiftText
    {
        /// <summary>대사 파일이 사는 자리. <c>Resources.Load</c> 가 쓰는 확장자 없는 경로다.</summary>
        public const string ResourceFolder = "Text";

        /// <summary>기본 로케일. 파일이 없거나 로케일을 안 정하면 이것으로 돌아온다.</summary>
        public const string DefaultLocale = "ko";

        private static readonly Dictionary<string, string> Table = new();
        private static readonly HashSet<string> Missing = new();

        /// <summary>지금 올라와 있는 로케일. 아직 안 올렸으면 <c>null</c>.</summary>
        public static string Locale { get; private set; }

        /// <summary>표가 올라와 있는가.</summary>
        public static bool IsLoaded => Locale != null;

        /// <summary>올라온 문안 수. 검사와 로그가 읽는다.</summary>
        public static int Count => Table.Count;

        /// <summary>
        /// 조회에 실패한 키들. <b>검사가 이걸로 "화면에 ⟨key⟩ 가 뜬 채로 나갔다" 를 막는다.</b>
        /// </summary>
        public static IReadOnlyCollection<string> MissingKeys => Missing;

        /// <summary>
        /// 게임 시작 때 한 번 올린다. 씬 로드 전이라 <b>어느 씬으로 들어가든</b> 첫 화면이
        /// 그려지기 전에 표가 서 있다. 검사·에디터 조립은 이 경로를 안 타므로
        /// <see cref="Get"/> 가 게으른 적재를 따로 들고 있다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnStart() => Load();

        /// <summary>
        /// 대사 파일을 올린다. 이미 같은 로케일이 올라와 있으면 아무 일도 안 한다 —
        /// 시작 경로가 둘(단독 실행·검사)이라 두 번 불릴 수 있고, 그때 표를 다시 만들 이유가 없다.
        /// </summary>
        public static bool Load(string locale = DefaultLocale)
        {
            if (string.IsNullOrEmpty(locale)) locale = DefaultLocale;
            if (Locale == locale) return true;

            var asset = Resources.Load<TextAsset>($"{ResourceFolder}/{locale}");
            if (asset == null)
            {
                Debug.LogError($"[LAST_SHIFT_TEXT] result=NO_FILE locale={locale} " +
                               $"path=Resources/{ResourceFolder}/{locale}.json");
                return false;
            }

            var duplicates = new List<string>();
            Dictionary<string, string> parsed;
            try
            {
                parsed = LastShiftTextTable.Parse(asset.text, duplicates);
            }
            catch (LastShiftTextFormatException error)
            {
                // 문법이 깨진 파일은 절반만 올리지 않는다. 절반이 올라가면 어느 줄이 옛 문안인지
                // 화면만 보고는 못 가른다.
                Debug.LogError($"[LAST_SHIFT_TEXT] result=BAD_FORMAT locale={locale} detail={error.Message}");
                return false;
            }

            Table.Clear();
            Missing.Clear();
            foreach (var entry in parsed) Table[entry.Key] = entry.Value;
            Locale = locale;

            if (duplicates.Count > 0)
                Debug.LogWarning($"[LAST_SHIFT_TEXT] result=DUPLICATE_KEYS locale={locale} " +
                                 $"count={duplicates.Count} first={duplicates[0]}");
            Debug.Log($"[LAST_SHIFT_TEXT] result=LOADED locale={locale} entries={Table.Count}");
            return true;
        }

        /// <summary>
        /// 키로 문안 한 줄. 아직 안 올라와 있으면 여기서 한 번 올린다 — 부르는 쪽마다
        /// "올렸나" 를 묻게 하면 그 조건이 언젠가 한 군데서 빠진다.
        /// </summary>
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (!IsLoaded) Load();

            if (Table.TryGetValue(key, out var value)) return value;

            if (Missing.Add(key))
                Debug.LogWarning($"[LAST_SHIFT_TEXT] result=MISSING_KEY key={key} locale={Locale ?? "(none)"}");
            return $"⟨{key}⟩";
        }

        /// <summary>
        /// 자리표(<c>{0}</c>)가 있는 문안. 서식을 문안 쪽에 두는 이유는 언어마다 어순이 달라서다 —
        /// 코드가 문자열을 이어 붙이면 그 어순이 코드에 박힌다.
        /// </summary>
        public static string Format(string key, params object[] args)
        {
            var template = Get(key);
            if (args == null || args.Length == 0) return template;

            try
            {
                return string.Format(template, args);
            }
            catch (System.FormatException)
            {
                // 번역문의 자리표가 깨져도 화면은 살아 있어야 한다. 원문을 그대로 보여 주면
                // 무엇이 깨졌는지가 화면에서 바로 읽힌다.
                Debug.LogWarning($"[LAST_SHIFT_TEXT] result=BAD_FORMAT_ARGS key={key}");
                return template;
            }
        }

        /// <summary>그 키가 표에 있는가. 검사가 대본과 파일을 맞대 볼 때 쓴다.</summary>
        public static bool Has(string key) =>
            !string.IsNullOrEmpty(key) && (IsLoaded || Load()) && Table.ContainsKey(key);

        /// <summary>올라온 키 전부. 파일에만 있고 아무도 안 쓰는 문안을 찾는 검사가 읽는다.</summary>
        public static IEnumerable<string> Keys => Table.Keys;

        /// <summary>검사 격리용. 정적 상태라 안 지우면 다음 판으로 새어 나간다.</summary>
        public static void Clear()
        {
            Table.Clear();
            Missing.Clear();
            Locale = null;
        }
    }
}
