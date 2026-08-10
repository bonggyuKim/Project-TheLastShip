using System;
using System.Text;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 방 코드. 호스트가 방을 열 때 발급하고, 클라이언트가 그것을 받아 적어 입장한다.
    ///
    /// 코드는 <b>사람이 음성으로 불러 주고 손으로 받아 적는</b> 물건이다. 그래서 알파벳에서
    /// 서로 헷갈리는 글자를 통째로 뺐다 — <c>I/1/L</c>, <c>O/0</c>. 발급 쪽에서 애초에 쓰지
    /// 않으므로, 옮겨 적기가 맞다면 <see cref="IsValid"/> 는 항상 통과한다. 반대로 통과하지
    /// 못했다면 그것은 진짜로 잘못 받아 적은 것이고, 그때는 조용히 고쳐 주는 대신 틀렸다고
    /// 말해 주는 편이 "왜 안 되는지 모르는 방"보다 낫다.
    /// </summary>
    public static class LastShiftRoomCode
    {
        public const int Length = 6;

        private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        public static string Generate()
        {
            var bytes = Guid.NewGuid().ToByteArray();
            var builder = new StringBuilder(Length);
            for (var index = 0; index < Length; index++)
                builder.Append(Alphabet[bytes[index] % Alphabet.Length]);
            return builder.ToString();
        }

        /// <summary>
        /// 입력창에서 온 문자열을 비교 가능한 모양으로 맞춘다. 대소문자와 사람이 습관적으로
        /// 끼워 넣는 공백·하이픈만 걷어내고, 글자 자체는 바꾸지 않는다.
        /// </summary>
        public static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var builder = new StringBuilder(raw.Length);
            foreach (var character in raw)
            {
                if (character == ' ' || character == '-' || character == '_') continue;
                builder.Append(char.ToUpperInvariant(character));
            }
            return builder.ToString();
        }

        public static bool IsValid(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length != Length) return false;
            foreach (var character in code)
                if (Alphabet.IndexOf(character) < 0)
                    return false;
            return true;
        }
    }
}
