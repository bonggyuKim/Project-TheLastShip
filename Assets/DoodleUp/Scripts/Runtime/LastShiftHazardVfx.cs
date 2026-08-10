using UnityEngine;
using UnityEngine.Rendering;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// Last Shift의 위험 상태를 읽기 쉬운 저비용 파티클로 표시한다. VFX Graph나 화면 후처리를
    /// 요구하지 않아 저사양 클라이언트와 네트워크 복제 프리팹에서도 같은 방식으로 동작한다.
    /// 상태 시스템은 Set* 메서드만 호출하며, 파티클 자체는 로컬에서 시뮬레이션한다.
    /// </summary>
    public sealed class LastShiftHazardVfx : MonoBehaviour
    {
        [SerializeField] private bool previewAll = true;
        [SerializeField] private bool oxygenLeakActive = true;
        [SerializeField] private bool repairHazardActive = true;
        [SerializeField] private bool emergencyActive = true;

        private ParticleSystem oxygenMist;
        private ParticleSystem oxygenJet;
        private ParticleSystem repairSparks;
        private Light beaconLight;
        private Light floorBounce;
        private Renderer floorReflection;

        private static readonly Color Oxygen = new(0.16f, 0.92f, 0.78f, 0.42f);
        private static readonly Color Hazard = new(1f, 0.29f, 0.035f, 1f);

        // 기존 씬/프리팹도 다음 실행부터 즉시 읽는다. 이후 씬 빌드는 Editor가 프리팹에
        // 컴포넌트를 직렬화하므로 이 경로는 구버전 저장 데이터의 호환 레이어다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForExistingScene()
        {
            var ship = GameObject.Find("LastShiftShipGraybox");
            if (ship != null && ship.GetComponent<LastShiftHazardVfx>() == null)
                ship.AddComponent<LastShiftHazardVfx>();
        }

        private void Awake()
        {
            BuildIfNeeded();
            ApplyState();
        }

        private void Update()
        {
            if (!emergencyActive) return;
            // 짧고 명확한 주황 경광. 반사광은 직접등보다 약하게 유지한다.
            var pulse = Mathf.SmoothStep(0.08f, 1f, Mathf.PingPong(Time.time * 2.5f, 1f));
            beaconLight.intensity = pulse * 3.4f;
            floorBounce.intensity = pulse * 0.45f;
            floorReflection.material.color = new Color(Hazard.r, Hazard.g, Hazard.b, 0.10f + pulse * 0.16f);
        }

        public void SetOxygenLeak(bool active) { oxygenLeakActive = active; ApplyState(); }
        public void SetRepairHazard(bool active) { repairHazardActive = active; ApplyState(); }
        public void SetEmergency(bool active) { emergencyActive = active; ApplyState(); }

        private void BuildIfNeeded()
        {
            if (oxygenMist != null) return;

            // 생명유지실 외벽 쪽: 넓은 청록 안개 + 통로 반대 방향의 짧은 누출 분출.
            var life = new Vector3(LastShiftShipDimensions.RoomCenterX(LastShiftZone.LifeSupport) - 1.7f, 1.15f,
                LastShiftShipDimensions.RoomCenterZ(LastShiftZone.LifeSupport) + 2.2f);
            oxygenMist = CreateParticles("OxygenLeak_Mist", life, Oxygen, 10, 1.7f, 0.22f, 0.62f, 0.55f, Vector3.up * 0.22f);
            oxygenJet = CreateParticles("OxygenLeak_Jet", life + Vector3.up * 0.12f, Oxygen, 26, 0.42f, 0.055f, 0.16f, 0.85f,
                Vector3.left * 1.5f + Vector3.up * 0.12f);

            // 손상 배관/콘솔의 스파크는 전력실에서 문 정면을 비켜 둔다.
            var repair = new Vector3(LastShiftShipDimensions.RoomCenterX(LastShiftZone.Power) + 1.55f, 1.35f,
                LastShiftShipDimensions.RoomCenterZ(LastShiftZone.Power) - 1.85f);
            repairSparks = CreateParticles("RepairHazard_Sparks", repair, Hazard, 16, 0.32f, 0.045f, 0.05f, 0.95f,
                new Vector3(0.15f, 1.35f, 0.1f));
            repairSparks.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 2, 5, 0.28f) });

            // 경광은 에어록 진입부의 상단, 반사 쿼드는 갑판 바로 위에 둔다.
            var beacon = new Vector3(LastShiftShipDimensions.RoomCenterX(LastShiftZone.Cockpit) - 1.1f, 2.22f,
                LastShiftShipDimensions.RoomCenterZ(LastShiftZone.Cockpit) - 1.7f);
            beaconLight = CreateLight("EmergencyBeacon_Flash", beacon, 3.4f, 3.2f);
            floorBounce = CreateLight("EmergencyBeacon_FloorBounce", beacon + Vector3.down * 1.95f, 0.45f, 2.0f);
            floorBounce.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            floorReflection = CreateReflection(beacon + new Vector3(0f, -2.16f, 0f));
        }

        private ParticleSystem CreateParticles(string label, Vector3 position, Color color, int maxParticles,
            float lifetime, float size, float rate, float speed, Vector3 velocity)
        {
            var host = new GameObject(label);
            host.transform.SetParent(transform, false);
            host.transform.localPosition = position;
            var particles = host.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true; main.playOnAwake = true; main.maxParticles = maxParticles;
            main.startLifetime = lifetime; main.startSize = size; main.startColor = color; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission; emission.rateOverTime = rate;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.045f;
            var velocityOverLifetime = particles.velocityOverLifetime; velocityOverLifetime.enabled = true;
            velocityOverLifetime.x = velocity.x; velocityOverLifetime.y = velocity.y; velocityOverLifetime.z = velocity.z;
            var colorOverLifetime = particles.colorOverLifetime; colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(color.a, 0.15f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return particles;
        }

        private Light CreateLight(string label, Vector3 position, float intensity, float range)
        {
            var light = new GameObject(label).AddComponent<Light>();
            light.transform.SetParent(transform, false); light.transform.localPosition = position;
            light.type = LightType.Point; light.color = Hazard; light.range = range; light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        private Renderer CreateReflection(Vector3 position)
        {
            var reflection = GameObject.CreatePrimitive(PrimitiveType.Quad);
            reflection.name = "EmergencyBeacon_FloorReflection";
            reflection.transform.SetParent(transform, false); reflection.transform.localPosition = position;
            reflection.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); reflection.transform.localScale = new Vector3(1.15f, 0.65f, 1f);
            Destroy(reflection.GetComponent<Collider>());
            var material = new Material(Shader.Find("Standard"));
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor("_Color", new Color(Hazard.r, Hazard.g, Hazard.b, 0.16f));
            material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Hazard * 0.18f);
            reflection.GetComponent<Renderer>().sharedMaterial = material;
            return reflection.GetComponent<Renderer>();
        }

        private void ApplyState()
        {
            if (oxygenMist == null) return;
            oxygenMist.gameObject.SetActive(previewAll || oxygenLeakActive);
            oxygenJet.gameObject.SetActive(previewAll || oxygenLeakActive);
            repairSparks.gameObject.SetActive(previewAll || repairHazardActive);
            beaconLight.gameObject.SetActive(previewAll || emergencyActive);
            floorBounce.gameObject.SetActive(previewAll || emergencyActive);
            floorReflection.gameObject.SetActive(previewAll || emergencyActive);
        }
    }
}
