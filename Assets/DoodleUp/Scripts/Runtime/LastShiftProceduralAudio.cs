using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 절차적 경보음 생성. 아트 자산을 기다리지 않고도 사이렌과 호흡음이 실제로 들려야
    /// CT-05 수용 기준 1·6 을 검증할 수 있다. game-art 가 실제 사운드를 주면 clip 만 교체하면 된다.
    /// 두 클립 모두 loop 재생을 전제로 경계에서 위상이 이어지도록 주기를 정수배로 맞췄다.
    /// </summary>
    public static class LastShiftProceduralAudio
    {
        private const int SampleRate = 44100;

        /// <summary>
        /// S-O3 전선 사이렌. 두 음 사이를 오가는 경보 패턴이라 엔진음이나 충격음과 혼동되지 않는다.
        /// 파공 위치는 알려주지 않고 "산소가 위험하다" 만 전달한다.
        /// </summary>
        public static AudioClip CreateSirenLoop()
        {
            const float duration = 1.6f;
            var sampleCount = (int)(SampleRate * duration);
            var samples = new float[sampleCount];
            for (var index = 0; index < sampleCount; index++)
            {
                var time = index / (float)SampleRate;
                // 0.8초마다 음이 바뀌는 2음 경보. 사람 귀가 "경보" 로 즉시 읽는 패턴이다.
                var high = time % 0.8f < 0.4f;
                var frequency = high ? 760f : 560f;
                var envelope = Mathf.Clamp01(Mathf.Sin(Mathf.PI * (time % 0.4f) / 0.4f) * 1.4f);
                samples[index] = Mathf.Clamp(Mathf.Sin(2f * Mathf.PI * frequency * time) * 0.5f * envelope, -1f, 1f);
            }

            var clip = AudioClip.Create("LS_OxygenSiren", sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// 헬멧 내부 호흡음. 4초 주기 흡기·호기 노이즈다. 사이렌보다 훨씬 낮고 가까이 들려야
        /// "선체 경보" 와 "내 산소" 가 귀에서 구분된다.
        /// </summary>
        public static AudioClip CreateBreathLoop()
        {
            const float duration = 4f;
            var sampleCount = (int)(SampleRate * duration);
            var samples = new float[sampleCount];
            var random = new System.Random(20260805);
            var lowPass = 0f;
            for (var index = 0; index < sampleCount; index++)
            {
                var time = index / (float)SampleRate;
                var noise = (float)(random.NextDouble() * 2.0 - 1.0);
                // 1극 저역 통과로 쉭 소리를 숨소리 대역까지 눌러 준다.
                lowPass += (noise - lowPass) * 0.06f;
                var cycle = time % 2f;
                var envelope = cycle < 1f
                    ? Mathf.Sin(Mathf.PI * cycle)          // 흡기
                    : Mathf.Sin(Mathf.PI * (cycle - 1f)) * 0.7f; // 호기는 조금 약하게
                samples[index] = Mathf.Clamp(lowPass * 3.2f * envelope, -1f, 1f);
            }

            var clip = AudioClip.Create("LS_HelmetBreath", sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
