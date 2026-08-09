using System.Collections.Generic;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 운석 충격의 관측 채널. CT-02 수용 기준 3 은 "상황이 터지면 무슨 상황인지" 알 수 있어야
    /// 한다는 것이고, 그 전제는 충격 자체가 감각으로 잡히는 것이다. 기존에는 CanonicalMeteorStimulus
    /// 수치만 반영돼 화면에서 아무 일도 일어나지 않았다.
    ///
    /// 여기서 만드는 것은 채널뿐이다. 어떤 문구로 무엇을 알릴지는 CT-01(game-planning) 소관이라
    /// 손대지 않는다. 채널은 셋이다: 카메라 흔들림, 충격음, 손상 구역 시각 표시.
    ///
    /// 매 프레임 로그는 금지(SP-04 규칙)이므로 충격 1회당 한 줄만 남긴다.
    /// </summary>
    public sealed class LastShiftImpactFeedback : MonoBehaviour
    {
        // 구역 판정 기준 x 경계 둘(`CockpitZoneMaxX` / `LifeSupportZoneMinX`)을 여기 재노출해
        // 두었는데, 방사형 배치에서 <b>구역을 x 하나로 못 가르게 되면서</b> 두 상수 자체가
        // 없어졌다 — 전력실과 냉각실이 같은 x 범위를 z 좌우로 나눠 쓴다(§6.2). 부르는 자리가
        // 하나도 없었고, 필요해지면 LastShiftZoneAtlas.Resolve 를 직접 부른다.

        public const float ShakeDurationSeconds = 0.9f;
        public const float ShakeMaxAngleDegrees = 3.2f;
        public const float DamageMarkerSeconds = 8f;

        private static readonly Color DamageColor = new(0.95f, 0.22f, 0.12f);
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_Color");

        private readonly List<Renderer> damageMarkers = new();
        private readonly List<Color> damageMarkerOriginalColors = new();

        // 필드 초기화자에서 만들면 MonoBehaviour 생성자 시점에 MaterialPropertyBlock 이
        // 생성되어 Unity 가 "CreateImpl is not allowed to be called from a MonoBehaviour
        // constructor" 예외를 던진다(EditMode 테스트 11건이 이 예외로 실패했다).
        // Awake 는 씬 빌더가 컴포넌트를 붙이는 경로에서 호출 시점이 갈리므로 첫 사용 때 만든다.
        private MaterialPropertyBlock damageMarkerProperties;

        private AudioSource impactAudio;
        private Transform impactAudioTransform;
        private float shakeRemaining;
        private float damageMarkerRemaining;
        private int shakeSeed;

        public bool IsShaking => shakeRemaining > 0f;
        public bool HasActiveDamageMarker => damageMarkerRemaining > 0f;
        public string DamagedZoneName { get; private set; } = string.Empty;

        /// <summary>
        /// 충격 지점 x 로 손상 구역을 정한다. 운석 벡터가 아니라 지점을 쓰는 이유는 사용자가
        /// 보는 것이 "어디가 맞았는가" 이기 때문이다.
        /// </summary>
        public static string ResolveDamagedZone(Vector3 impactPoint)
        {
            return LastShiftZoneAtlas.NameOf(LastShiftZoneAtlas.Resolve(impactPoint));
        }

        private void Awake()
        {
            // A2. 음원을 <b>자식 오브젝트</b>로 뺀다. 이 컴포넌트는 씬 런타임 루트에 붙어 있어서
            // 3D 로만 바꾸면 배 안 모든 충격이 루트 좌표 한 점에서 나고, "어느 구역에서 났는가"
            // 라는 정보가 그대로 사라진다. 재생 직전에 충격 지점으로 옮긴다.
            //
            // 루트에 붙은 것을 재사용하지 않는다. 재사용하면 옮기는 대상이 런타임 루트가 되어
            // 충격 한 번에 배 전체가 이동한다. 씬·프리팹에 직렬화된 AudioSource 는 0 개이므로
            // (전부 런타임 생성이었다) 재사용할 것도 없다.
            var emitter = new GameObject("Impact Audio");
            emitter.transform.SetParent(transform, false);
            impactAudio = emitter.AddComponent<AudioSource>();
            impactAudioTransform = emitter.transform;
            impactAudio.playOnAwake = false;
            LastShiftZoneAudio.ConfigureLocal(impactAudio, LastShiftZoneAudio.ImpactMaxDistance);
            if (impactAudio.clip == null) impactAudio.clip = CreateImpactClip();
        }

        /// <summary>
        /// 충격 연출을 시작한다. 서버·클라이언트 양쪽에서 호출되어도 같은 결과를 내도록
        /// 무작위 시드를 충격 세대(generation)로 고정한다.
        /// </summary>
        public void PlayImpact(Vector3 impactPoint, float severity, int generation)
        {
            shakeSeed = generation * 7919 + 13;
            shakeRemaining = ShakeDurationSeconds;
            // MarkDamagedZone 은 앞선 표시를 걷어내기 위해 ClearDamageMarkers 를 부르고,
            // 그 안에서 DamagedZoneName 과 damageMarkerRemaining 이 초기화된다. 그래서
            // 구역 이름과 지속 시간은 반드시 MarkDamagedZone 이후에 세워야 한다.
            // 순서가 뒤바뀌어 있던 동안 손상 구역 표시 채널이 통째로 죽어 있었다.
            var damagedZone = ResolveDamagedZone(impactPoint);
            MarkDamagedZone(damagedZone);
            DamagedZoneName = damagedZone;
            damageMarkerRemaining = DamageMarkerSeconds;
            if (impactAudio != null && impactAudio.clip != null)
            {
                // 3D 음원이므로 재생 위치가 곧 판독 정보다. 옮기지 않으면 어느 구역이 맞았는지
                // 귀로는 알 수 없고 시각 표시만 남는다.
                if (impactAudioTransform != null) impactAudioTransform.position = impactPoint;
                impactAudio.volume = Mathf.Clamp(0.35f + severity * 0.25f, 0.2f, 0.9f);
                impactAudio.Play();
            }

            Debug.Log($"[LAST_SHIFT_IMPACT_FEEDBACK] generation={generation} point={impactPoint:F2} severity={severity:F2} zone={DamagedZoneName} shake={ShakeDurationSeconds:F2}s marker={DamageMarkerSeconds:F0}s audio={(impactAudio != null && impactAudio.clip != null ? "played" : "missing")}");
        }

        private void LateUpdate()
        {
            var deltaTime = Time.deltaTime;
            if (shakeRemaining > 0f)
            {
                shakeRemaining = Mathf.Max(0f, shakeRemaining - deltaTime);
                ApplyCameraShake();
            }

            if (damageMarkerRemaining <= 0f) return;
            damageMarkerRemaining = Mathf.Max(0f, damageMarkerRemaining - deltaTime);
            if (damageMarkerRemaining > 0f) PulseDamageMarkers();
            else ClearDamageMarkers();
        }

        /// <summary>
        /// 로컬 플레이어 카메라만 흔든다. 카메라 자체 회전이 아니라 부모 기준 추가 회전으로
        /// 넣어야 마우스 조준(pitch/yaw 를 localRotation 에 직접 쓰는 경로)과 싸우지 않는다.
        /// 그래서 조준을 소유한 LastShiftPlayerController 에 흔들림 각을 넘긴다.
        /// </summary>
        private void ApplyCameraShake()
        {
            var normalized = shakeRemaining / ShakeDurationSeconds;
            var amplitude = ShakeMaxAngleDegrees * normalized * normalized;
            var time = (ShakeDurationSeconds - shakeRemaining) * 24f;
            var pitch = (Mathf.PerlinNoise(shakeSeed * 0.017f, time) - 0.5f) * 2f * amplitude;
            var yaw = (Mathf.PerlinNoise(time, shakeSeed * 0.023f) - 0.5f) * 2f * amplitude;
            var roll = (Mathf.PerlinNoise(time * 0.7f, shakeSeed * 0.031f) - 0.5f) * 2f * amplitude;
            var offset = new Vector3(pitch, yaw, roll);

            foreach (var player in FindObjectsByType<LastShiftPlayerController>(FindObjectsSortMode.None))
                if (player != null) player.SetCameraShakeOffset(offset);
        }

        private void MarkDamagedZone(string zoneName)
        {
            ClearDamageMarkers();
            var zone = FindZone(zoneName);
            if (zone == null) return;
            foreach (var renderer in zone.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.sharedMaterial == null) continue;
                damageMarkers.Add(renderer);
                damageMarkerOriginalColors.Add(renderer.sharedMaterial.color);
            }
        }

        private void PulseDamageMarkers()
        {
            // 8초 동안 점멸시켜 "여기가 손상됐다"를 눈으로 잡게 한다. 정적인 색 변경은
            // 원래 구역 색과 구분되지 않아 놓치기 쉽다.
            var pulse = 0.5f + 0.5f * Mathf.Sin((DamageMarkerSeconds - damageMarkerRemaining) * 7f);
            for (var index = 0; index < damageMarkers.Count; index++)
            {
                var renderer = damageMarkers[index];
                if (renderer == null) continue;
                SetMarkerColor(renderer, Color.Lerp(damageMarkerOriginalColors[index], DamageColor, pulse * 0.75f));
            }
        }

        /// <summary>
        /// 렌더러별 색을 MaterialPropertyBlock 으로 덮는다. 재질 경로 두 가지가 다 막혀 있어서다.
        /// sharedMaterial 을 물들이면 세 구역 바닥이 floorMaterial 하나를 공유하므로 전부 함께
        /// 붉어져 "어느 구역이 맞았는가" 가 사라진다. renderer.material 은 인스턴스를 떠서 그
        /// 문제는 없지만 편집 모드에서 씬에 재질을 누수시켜 Unity 가 에러를 낸다
        /// (EditMode 테스트가 이 에러로 실패했다). property block 은 재질을 만들지 않고
        /// 렌더러 단위로만 값을 덮으므로 두 조건을 모두 만족한다.
        /// </summary>
        private void SetMarkerColor(Renderer renderer, Color color)
        {
            damageMarkerProperties ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(damageMarkerProperties);
            damageMarkerProperties.SetColor(BaseColorPropertyId, color);
            renderer.SetPropertyBlock(damageMarkerProperties);
        }

        public void ClearDamageMarkers()
        {
            for (var index = 0; index < damageMarkers.Count; index++)
            {
                var renderer = damageMarkers[index];
                if (renderer != null) SetMarkerColor(renderer, damageMarkerOriginalColors[index]);
            }
            damageMarkers.Clear();
            damageMarkerOriginalColors.Clear();
            damageMarkerRemaining = 0f;
            DamagedZoneName = string.Empty;
        }

        private static Transform FindZone(string zoneName)
        {
            foreach (var zone in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (zone != null && zone.name == zoneName) return zone;
            return null;
        }

        /// <summary>
        /// 충격음을 절차적으로 만든다. 오디오 자산을 요청하지 않고도 "쿵" 이 들려야 하기 때문이다.
        /// 저주파 사인에 노이즈를 섞고 지수 감쇠를 걸어 선체 타격음 형태로 만든다.
        /// game-art 가 실제 사운드를 주면 clip 을 교체하면 된다.
        /// </summary>
        private static AudioClip CreateImpactClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.85f;
            var sampleCount = (int)(sampleRate * duration);
            var samples = new float[sampleCount];
            var random = new System.Random(20260804);
            for (var index = 0; index < sampleCount; index++)
            {
                var time = index / (float)sampleRate;
                var decay = Mathf.Exp(-time * 5.5f);
                var lowBody = Mathf.Sin(2f * Mathf.PI * 58f * time) * 0.65f;
                var thud = Mathf.Sin(2f * Mathf.PI * 96f * time) * 0.25f;
                var noise = (float)(random.NextDouble() * 2.0 - 1.0) * 0.35f * Mathf.Exp(-time * 16f);
                samples[index] = Mathf.Clamp((lowBody + thud + noise) * decay, -1f, 1f);
            }

            var clip = AudioClip.Create("LS_MeteorImpact", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
