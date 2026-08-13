using System.Collections.Generic;
using System.Text;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 평평한 <c>{ "key": "value" }</c> JSON 하나를 문자열 표로 읽는다.
    ///
    /// <b>왜 직접 읽는가.</b> 이 프로젝트에는 Newtonsoft 가 없고(<c>Packages/manifest.json</c>),
    /// <c>JsonUtility</c> 는 사전을 못 읽어 <c>{"entries":[{"key":…,"value":…}]}</c> 같은 포장을
    /// 강요한다 — 그러면 파일이 번역 도구가 아는 모양을 벗어난다. 다국어 파일은 다른 사람(번역가·
    /// 도구)이 열어야 하므로 <b>파일 모양이 표준인 쪽</b>이 중요하고, 파서는 우리 안에서만 쓰는
    /// 코드라 우리가 감당하면 된다.
    ///
    /// 그래서 문법을 좁게 받는다 — 최상위는 객체 하나, 값은 전부 문자열이다. 중첩 객체·배열·
    /// 숫자는 <b>오류</b>다. 넓게 받아 두면 언젠가 대사 파일에 구조가 생기고, 그때 이 파서가
    /// 조용히 절반만 읽는다.
    /// </summary>
    public static class LastShiftTextTable
    {
        /// <summary>
        /// JSON 본문을 표로 읽는다. 같은 키가 두 번 나오면 <b>나중 것이 이긴다</b> — 파일을
        /// 이어붙여 만드는 경우가 있어서이고, 중복 자체는 <paramref name="duplicates"/> 로 알린다.
        /// </summary>
        public static Dictionary<string, string> Parse(string json, List<string> duplicates = null)
        {
            var table = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(json)) return table;

            var at = 0;
            SkipWhitespace(json, ref at);
            Expect(json, ref at, '{');

            SkipWhitespace(json, ref at);
            if (Peek(json, at) == '}') return table;

            while (true)
            {
                SkipWhitespace(json, ref at);
                var key = ReadString(json, ref at);
                SkipWhitespace(json, ref at);
                Expect(json, ref at, ':');
                SkipWhitespace(json, ref at);
                var value = ReadString(json, ref at);

                if (table.ContainsKey(key)) duplicates?.Add(key);
                table[key] = value;

                SkipWhitespace(json, ref at);
                var next = Read(json, ref at);
                if (next == ',') continue;
                if (next == '}') break;
                throw new LastShiftTextFormatException($"',' 나 '}}' 가 와야 하는 자리에 '{next}' (offset {at - 1})");
            }

            return table;
        }

        private static void SkipWhitespace(string json, ref int at)
        {
            while (at < json.Length && char.IsWhiteSpace(json[at])) at++;
        }

        private static char Peek(string json, int at) =>
            at < json.Length ? json[at] : '\0';

        private static char Read(string json, ref int at)
        {
            if (at >= json.Length) throw new LastShiftTextFormatException("파일이 중간에서 끝났다");
            return json[at++];
        }

        private static void Expect(string json, ref int at, char expected)
        {
            var actual = Read(json, ref at);
            if (actual != expected)
                throw new LastShiftTextFormatException($"'{expected}' 가 와야 하는 자리에 '{actual}' (offset {at - 1})");
        }

        private static string ReadString(string json, ref int at)
        {
            Expect(json, ref at, '"');
            var text = new StringBuilder();

            while (true)
            {
                var c = Read(json, ref at);
                if (c == '"') return text.ToString();
                if (c != '\\')
                {
                    text.Append(c);
                    continue;
                }

                var escape = Read(json, ref at);
                switch (escape)
                {
                    case '"': text.Append('"'); break;
                    case '\\': text.Append('\\'); break;
                    case '/': text.Append('/'); break;
                    case 'b': text.Append('\b'); break;
                    case 'f': text.Append('\f'); break;
                    case 'n': text.Append('\n'); break;
                    case 'r': text.Append('\r'); break;
                    case 't': text.Append('\t'); break;
                    case 'u':
                        if (at + 4 > json.Length)
                            throw new LastShiftTextFormatException("\\u 뒤 네 자리가 모자라다");
                        text.Append((char)System.Convert.ToInt32(json.Substring(at, 4), 16));
                        at += 4;
                        break;
                    default:
                        throw new LastShiftTextFormatException($"모르는 이스케이프 '\\{escape}' (offset {at - 1})");
                }
            }
        }
    }

    /// <summary>대사 파일이 문법에 안 맞을 때. 조용히 절반만 읽는 것보다 시끄럽게 죽는 편이 낫다.</summary>
    public sealed class LastShiftTextFormatException : System.Exception
    {
        public LastShiftTextFormatException(string message) : base(message) { }
    }
}
