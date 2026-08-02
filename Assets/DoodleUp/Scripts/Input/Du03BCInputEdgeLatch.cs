using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoodleUp.Input
{
    public readonly struct Du03BCInputSnapshot
    {
        public readonly long EventSequence;
        public readonly bool DrawPressed;
        public readonly bool DrawReleased;
        public readonly bool DrawHeld;
        public readonly bool ConfirmPressed;
        public readonly bool CancelPressed;
        public readonly string ExecutionPath;

        public Du03BCInputSnapshot(
            long eventSequence,
            bool drawPressed,
            bool drawReleased,
            bool drawHeld,
            bool confirmPressed,
            bool cancelPressed,
            string executionPath)
        {
            EventSequence = eventSequence;
            DrawPressed = drawPressed;
            DrawReleased = drawReleased;
            DrawHeld = drawHeld;
            ConfirmPressed = confirmPressed;
            CancelPressed = cancelPressed;
            ExecutionPath = executionPath;
        }
    }

    [DefaultExecutionOrder(-50)]
    public sealed class Du03BCInputEdgeLatch : MonoBehaviour
    {
        [SerializeField] private bool verboseInputLogging;

        private InputAction drawAction;
        private InputAction cancelMouseAction;
        private InputAction cancelEscapeAction;
        private InputAction resetAction;
        private InputAction inkResetAction;

        private bool drawPressed;
        private bool drawReleased;
        private bool drawHeld;
        private bool confirmPressed;
        private bool cancelPressed;
        private bool resetPressed;
        private bool inkResetPressed;
        private long eventSequence;
        private bool hasProbeSnapshot;
        private Du03BCInputSnapshot probeSnapshot;

        public long EventSequence => eventSequence;
        public bool DrawHeld => drawHeld;

        public static string BindingManifest =>
            "Draw=<Mouse>/leftButton;Commit=<Mouse>/leftButton#release;Cancel=<Mouse>/rightButton|<Keyboard>/escape;Reset=<Keyboard>/r;InkReset=<Keyboard>/q";

        public Du03BCInputSnapshot ConsumeStrokeEdges()
        {
            if (hasProbeSnapshot)
            {
                hasProbeSnapshot = false;
                drawHeld = probeSnapshot.DrawHeld;
                return probeSnapshot;
            }

            var snapshot = new Du03BCInputSnapshot(
                eventSequence,
                drawPressed,
                drawReleased,
                drawHeld,
                confirmPressed,
                cancelPressed,
                "INPUT_SYSTEM");
            drawPressed = false;
            drawReleased = false;
            confirmPressed = false;
            cancelPressed = false;
            return snapshot;
        }

        public bool ConsumeResetPressed()
        {
            var consumed = resetPressed;
            resetPressed = false;
            return consumed;
        }

        public bool ConsumeInkResetPressed()
        {
            var consumed = inkResetPressed;
            inkResetPressed = false;
            return consumed;
        }

        public void EnqueueInkResetForProbe()
        {
            inkResetPressed = true;
        }

        public void EnqueueProbeSnapshot(in Du03BCInputSnapshot snapshot)
        {
            probeSnapshot = snapshot;
            hasProbeSnapshot = true;
            drawHeld = snapshot.DrawHeld;
        }

        public void ClearLatchedEdges(string reason)
        {
            drawPressed = false;
            drawReleased = false;
            drawHeld = false;
            confirmPressed = false;
            cancelPressed = false;
            resetPressed = false;
            inkResetPressed = false;
            hasProbeSnapshot = false;
            Debug.Log($"[DU03BC_INPUT_CLEAR] frame={Time.frameCount} reason={reason}");
        }

        private void Awake()
        {
            InitializeActions();
        }

        private void OnEnable()
        {
            InitializeActions();
            drawAction.Enable();
            cancelMouseAction.Enable();
            cancelEscapeAction.Enable();
            resetAction.Enable();
            inkResetAction.Enable();
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            drawAction?.Disable();
            cancelMouseAction?.Disable();
            cancelEscapeAction?.Disable();
            resetAction?.Disable();
            inkResetAction?.Disable();
            ClearLatchedEdges("DISABLE");
        }

        private void OnDestroy()
        {
            drawAction?.Dispose();
            cancelMouseAction?.Dispose();
            cancelEscapeAction?.Dispose();
            resetAction?.Dispose();
            inkResetAction?.Dispose();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) ClearLatchedEdges("FOCUS_LOST");
        }

        private void InitializeActions()
        {
            if (drawAction != null) return;

            drawAction = new InputAction("DU03BC_Draw", InputActionType.Button, "<Mouse>/leftButton");
            cancelMouseAction = new InputAction("DU03BC_CancelMouse", InputActionType.Button, "<Mouse>/rightButton");
            cancelEscapeAction = new InputAction("DU03BC_CancelEscape", InputActionType.Button, "<Keyboard>/escape");
            resetAction = new InputAction("DU03BC_Reset", InputActionType.Button, "<Keyboard>/r");
            inkResetAction = new InputAction("DU03BC_InkReset", InputActionType.Button, "<Keyboard>/q");

            drawAction.started += _ =>
            {
                drawHeld = true;
                drawPressed = true;
                LogEdge("LMB", "PRESSED");
            };
            drawAction.canceled += _ =>
            {
                drawHeld = false;
                drawReleased = true;
                LogEdge("LMB", "RELEASED");
            };
            cancelMouseAction.performed += _ => LatchCancel("RMB");
            cancelEscapeAction.performed += _ => LatchCancel("ESC");
            resetAction.performed += _ =>
            {
                resetPressed = true;
                LogEdge("R", "PRESSED");
            };
            inkResetAction.performed += _ =>
            {
                inkResetPressed = true;
                LogEdge("Q", "PRESSED");
            };
        }

        private void LatchCancel(string control)
        {
            cancelPressed = true;
            drawHeld = false;
            LogEdge(control, "PRESSED");
        }

        private void LogEdge(string control, string phase)
        {
            eventSequence++;
            if (verboseInputLogging)
                Debug.Log($"[DU03BC_INPUT] frame={Time.frameCount} seq={eventSequence} control={control} phase={phase} latched=True path=INPUT_SYSTEM");
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change != InputDeviceChange.Disconnected && change != InputDeviceChange.Removed) return;
            if (device is Mouse || device is Keyboard) ClearLatchedEdges("DEVICE_DISCONNECT");
        }
    }
}
