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
        Trajectory
    }

    public sealed class Du03BCAdapterRouter : MonoBehaviour, IDu03ADrawIntentSource
    {
        [SerializeField] private Du03ADeterministicIntentSource deterministicSource;
        [SerializeField] private Du03BCAimInputAdapter aimAdapter;
        [SerializeField] private Du03BCTrajectoryInputAdapter trajectoryAdapter;
        [SerializeField] private Du03AStrokeDriver strokeDriver;
        [SerializeField] private Du03BCAdapterRoute activeRoute = Du03BCAdapterRoute.Trajectory;

        public Du03BCAdapterRoute ActiveRoute => activeRoute;
        public IDu03BCInputAdapter ActiveAdapter => activeRoute switch
        {
            Du03BCAdapterRoute.Aim => aimAdapter,
            Du03BCAdapterRoute.Trajectory => trajectoryAdapter,
            _ => null
        };

        public void Configure(
            Du03ADeterministicIntentSource deterministic,
            Du03BCAimInputAdapter aim,
            Du03BCTrajectoryInputAdapter trajectory,
            Du03AStrokeDriver driver = null)
        {
            deterministicSource = deterministic;
            aimAdapter = aim;
            trajectoryAdapter = trajectory;
            strokeDriver = driver;
        }

        public void SetStrokeDriver(Du03AStrokeDriver driver)
        {
            strokeDriver = driver;
        }

        public void SetRoute(Du03BCAdapterRoute route)
        {
            if (route == Du03BCAdapterRoute.DeterministicEvidence && deterministicSource == null)
                throw new InvalidOperationException("DU-03BC deterministic route is not configured.");
            if (route == Du03BCAdapterRoute.Aim && aimAdapter == null)
                throw new InvalidOperationException("DU-03BC Aim route is not configured.");
            if (route == Du03BCAdapterRoute.Trajectory && trajectoryAdapter == null)
                throw new InvalidOperationException("DU-03BC Trajectory route is not configured.");

            aimAdapter?.ResetAdapter();
            trajectoryAdapter?.ResetAdapter();
            deterministicSource?.Clear();
            activeRoute = route;
            Debug.Log($"[DU03BC_ROUTE] frame={Time.frameCount} route={activeRoute} result=PASS");
        }

        public Du03ADrawIntent ReadIntent()
        {
            return activeRoute switch
            {
                Du03BCAdapterRoute.DeterministicEvidence => deterministicSource.ReadIntent(),
                Du03BCAdapterRoute.Aim => aimAdapter.ReadIntent(),
                Du03BCAdapterRoute.Trajectory => trajectoryAdapter.ReadIntent(),
                _ => default
            };
        }

        private void Update()
        {
            if (Application.isBatchMode || Keyboard.current == null || !Keyboard.current.tabKey.wasPressedThisFrame)
                return;

            CyclePlayableRoute();
        }

        public void CyclePlayableRoute()
        {
            var nextRoute = activeRoute == Du03BCAdapterRoute.Aim
                ? Du03BCAdapterRoute.Trajectory
                : Du03BCAdapterRoute.Aim;
            strokeDriver?.ResetSession();
            SetRoute(nextRoute);
            strokeDriver?.SetModeForProbe(nextRoute == Du03BCAdapterRoute.Aim
                ? Du03AStrokeMode.Aim
                : Du03AStrokeMode.Trajectory);
            Debug.Log($"[DU03BC_PLAY_MODE] frame={Time.frameCount} control=TAB route={nextRoute} sessionReset=True result=PASS");
        }

        public void ResetActiveAdapter()
        {
            ActiveAdapter?.ResetAdapter();
        }
    }
}
