using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 내레이션 신호음. <b>대본의 태그 하나가 파일 하나</b>이고(정본 §2), 그 대응이 여기
    /// 한 곳에만 있다.
    ///
    /// <b>줄이 바뀔 때만 운다.</b> <see cref="Announce"/> 가 id 를 기억하므로 같은 줄이 여러
    /// 프레임 떠 있어도 한 번이고, 재촉으로 문장이 갈려도 id 는 그대로라 다시 안 운다 —
    /// 조항 <c>N-1</c>("재촉에는 신호음을 붙이지 않는다")이 자동으로 지켜지는 자리다.
    /// 태그가 <see cref="LastShiftNarrationSfx.None"/> 인 줄도 마찬가지로 조용하다.
    ///
    /// <b>음원이 없어도 진행을 안 막는다.</b> 아직 안 들어온 태그가 있거나 경로가 바뀌면
    /// 소리만 빠지고 대사는 그대로 흐른다. 대신 <see cref="HasClip"/> 로 EditMode 가 셋 다
    /// 실제로 있는지 먼저 본다.
    /// </summary>
    public static class LastShiftNarrationAudio
    {
        /// <summary><c>Resources.Load</c> 경로. TA 통합본(deddacc)이 여기로 모았다.</summary>
        public const string ResourceFolder = "Audio/LastShift/Onboarding/";

        /// <summary>이 소리가 대사에 묻히지 않을 만큼만. 안내음이지 사건음이 아니다.</summary>
        public const float Volume = 0.55f;

        private static AudioSource source;
        private static string lastAnnouncedId;

        /// <summary>검사용. 켜면 대응은 그대로 기록하고 소리만 안 낸다.</summary>
        public static bool Muted { get; set; }

        /// <summary>마지막으로 소리를 낸 줄. 태그가 없는 줄은 여기 안 남는다.</summary>
        public static string LastPlayedId { get; private set; }

        /// <summary>그때 쓴 태그.</summary>
        public static LastShiftNarrationSfx LastPlayedSfx { get; private set; }

        /// <summary>
        /// 태그 하나의 파일 이름. <b>태그 이름이 곧 파일 이름</b>이라 표가 짧다 —
        /// 새 태그가 생기면 여기서 <c>null</c> 이 나오고 <see cref="HasClip"/> 가 먼저 걸린다.
        /// </summary>
        public static string ClipNameOf(LastShiftNarrationSfx sfx) => sfx switch
        {
            LastShiftNarrationSfx.ChimeLong => "LS_CHIME_LONG",
            LastShiftNarrationSfx.ChimeShort => "LS_CHIME_SHORT",
            LastShiftNarrationSfx.ChimeAlert => "LS_CHIME_ALERT",
            _ => null
        };

        /// <summary>그 태그의 음원이 실제로 프로젝트에 있는가.</summary>
        public static bool HasClip(LastShiftNarrationSfx sfx) => Load(sfx) != null;

        /// <summary>
        /// 이 줄이 떴다고 알린다. <b>같은 id 로 두 번 부르면 두 번째는 아무것도 안 한다</b> —
        /// 부르는 쪽이 매 프레임 불러도 되도록 판정을 이쪽에 뒀다.
        /// </summary>
        public static void Announce(string id, LastShiftNarrationSfx sfx)
        {
            if (string.IsNullOrEmpty(id) || id == lastAnnouncedId) return;
            lastAnnouncedId = id;
            if (sfx == LastShiftNarrationSfx.None) return;

            LastPlayedId = id;
            LastPlayedSfx = sfx;
            if (Muted) return;

            var clip = Load(sfx);
            if (clip == null) return;
            EnsureSource()?.PlayOneShot(clip, Volume);
        }

        public static void Clear()
        {
            lastAnnouncedId = null;
            LastPlayedId = null;
            LastPlayedSfx = LastShiftNarrationSfx.None;
        }

        private static AudioClip Load(LastShiftNarrationSfx sfx)
        {
            var name = ClipNameOf(sfx);
            // Resources.Load 는 없는 이름이면 디스크를 훑는다. 캐시는 Unity 가 이미 하므로
            // 여기서 또 들고 있지 않고, 대신 태그가 None 인 흔한 경우를 앞에서 끊는다.
            return name == null ? null : Resources.Load<AudioClip>(ResourceFolder + name);
        }

        private static AudioSource EnsureSource()
        {
            if (source != null) return source;

            var holder = new GameObject("LastShiftNarrationAudio") { hideFlags = HideFlags.HideAndDontSave };
            source = holder.AddComponent<AudioSource>();
            // 선내 방송이지 방 안의 물건이 아니다 — 배 전체 설정을 그대로 쓴다.
            LastShiftZoneAudio.ConfigureShipWide(source);
            source.playOnAwake = false;
            return source;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            source = null;
            Muted = false;
            Clear();
        }
    }
}
