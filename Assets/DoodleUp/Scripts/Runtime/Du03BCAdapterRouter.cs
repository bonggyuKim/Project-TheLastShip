using System;
using DoodleUp.Input;
using DoodleUp.Stroke;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoodleUp.Runtime
{
    public enum Du03BCAdapterRoute
    {
        DeterministicEvidence,
        Aim,
        Trajectory,
        ArmDirect
    }

    public sealed class Du03BCAdapterRouter : MonoBehaviour, IDu03ADrawIntentSource
    {
        [SerializeField] private Du03ADeterministicIntentSource deterministicSource;
        [SerializeField] private Du03BCAimInputAdapter aimAdapter;
        [SerializeField] private Du03BCTrajectoryInputAdapter trajectoryAdapter;
        [SerializeField] private Du03BCArmDirectInputAdapter armDirectAdapter;
        [SerializeField] private Du03AStrokeDriver strokeDriver;
        [SerializeField] private Du03BCAdapterRoute activeRoute = Du03BCAdapterRoute.Aim;
        [SerializeField] private Du03BCAdapterRoute playableStartRoute = Du03BCAdapterRoute.Aim;

        public event Action<Du03BCAdapterRoute> RouteChanged;

        public Du03BCAdapterRoute ActiveRoute => activeRoute;
        public IDu03BCInputAdapter ActiveAdapter => activeRoute switch
        {
            Du03BCAdapterRoute.Aim => aimAdapter,
            Du03BCAdapterRoute.Trajectory => trajectoryAdapter,
            Du03BCAdapterRoute.ArmDirect => armDirectAdapter,
            _ => null
        };

        public void Configure(
            Du03ADeterministicIntentSource deterministic,
            Du03BCAimInputAdapter aim,
            Du03BCTrajectoryInputAdapter trajectory,
            Du03AStrokeDriver driver = null,
            Du03BCArmDirectInputAdapter armDirect = null)
        {
            deterministicSource = deterministic;
            aimAdapter = aim;
            trajectoryAdapter = trajectory;
            armDirectAdapter = armDirect;
            strokeDriver = driver;
        }

        public void SetStrokeDriver(Du03AStrokeDriver driver)
        {
            strokeDriver = driver;
        }

        public void ConfigurePlayableStartRoute(Du03BCAdapterRoute route)
        {
            if (route == Du03BCAdapterRoute.DeterministicEvidence)
                throw new ArgumentOutOfRangeException(nameof(route), route, "Playable start route cannot be deterministic evidence.");
            playableStartRoute = route;
        }

        public void ApplyPlayableStartRouteForProbe()
        {
            ApplyPlayableStartRoute();
        }

        public void SetRoute(Du03BCAdapterRoute route)
        {
            if (route == Du03BCAdapterRoute.DeterministicEvidence && deterministicSource == null)
                throw new InvalidOperationException("DU-03BC deterministic route is not configured.");
            if (route == Du03BCAdapterRoute.Aim && aimAdapter == null)
                throw new InvalidOperationException("DU-03BC Aim route is not configured.");
            if (route == Du03BCAdapterRoute.Trajectory && trajectoryAdapter == null)
                throw new InvalidOperationException("DU-03BC Trajectory route is not configured.");
            if (route == Du03BCAdapterRoute.ArmDirect && armDirectAdapter == null)
                throw new InvalidOperationException("DU-03BC ArmDirect route is not configured.");

            aimAdapter?.ResetAdapter();
            trajectoryAdapter?.ResetAdapter();
            armDirectAdapter?.ResetAdapter();
            deterministicSource?.Clear();
            activeRoute = route;
            RouteChanged?.Invoke(activeRoute);
            Debug.Log($"[DU03BC_ROUTE] frame={Time.frameCount} route={activeRoute} result=PASS");
        }

        public Du03ADrawIntent ReadIntent()
        {
            return activeRoute switch
            {
                Du03BCAdapterRoute.DeterministicEvidence => deterministicSource.ReadIntent(),
                Du03BCAdapterRoute.Aim => aimAdapter.ReadIntent(),
                Du03BCAdapterRoute.Trajectory => trajectoryAdapter.ReadIntent(),
                Du03BCAdapterRoute.ArmDirect => armDirectAdapter.ReadIntent(),
                _ => default
            };
        }

        private void Start()
        {
            if (Application.isBatchMode)
                return;

            ApplyPlayableStartRoute();
            if (Application.isEditor && GetComponent<Du03BCPlayabilityVisuals>() == null)
                gameObject.AddComponent<Du03BCPlayabilityVisuals>();
            Debug.Log($"[DU03BC_PLAY_MODE] frame={Time.frameCount} control=SCENE_START route={activeRoute} sessionReset=True result=PASS");
        }

        private void ApplyPlayableStartRoute()
        {
            if (activeRoute == playableStartRoute)
                return;

            strokeDriver?.ResetSession();
            SetRoute(playableStartRoute);
            strokeDriver?.SetModeForProbe(ModeForRoute(playableStartRoute));
        }

        private void Update()
        {
            if (Application.isBatchMode || Keyboard.current == null || !Keyboard.current.tabKey.wasPressedThisFrame)
                return;

            CyclePlayableRoute();
        }

        public void CyclePlayableRoute()
        {
            var nextRoute = activeRoute switch
            {
                Du03BCAdapterRoute.Aim => Du03BCAdapterRoute.Trajectory,
                Du03BCAdapterRoute.Trajectory when armDirectAdapter != null => Du03BCAdapterRoute.ArmDirect,
                _ => Du03BCAdapterRoute.Aim
            };
            strokeDriver?.ResetSession();
            SetRoute(nextRoute);
            strokeDriver?.SetModeForProbe(ModeForRoute(nextRoute));
            Debug.Log($"[DU03BC_PLAY_MODE] frame={Time.frameCount} control=TAB route={nextRoute} sessionReset=True result=PASS");
        }

        private static Du03AStrokeMode ModeForRoute(Du03BCAdapterRoute route)
        {
            return route switch
            {
                Du03BCAdapterRoute.Trajectory => Du03AStrokeMode.Trajectory,
                Du03BCAdapterRoute.ArmDirect => Du03AStrokeMode.Spatial,
                _ => Du03AStrokeMode.Aim
            };
        }

        public void ResetActiveAdapter()
        {
            ActiveAdapter?.ResetAdapter();
        }
    }
}
