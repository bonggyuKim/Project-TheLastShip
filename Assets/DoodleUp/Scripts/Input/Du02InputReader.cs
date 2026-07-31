using UnityEngine;
using UnityEngine.InputSystem;

namespace DoodleUp.Input
{
    public readonly struct Du02InputFrame
    {
        public readonly float Horizontal;
        public readonly bool JumpPressed;
        public readonly bool ResetPressed;
        public readonly int LaneSelection;

        public Du02InputFrame(float horizontal, bool jumpPressed, bool resetPressed, int laneSelection)
        {
            Horizontal = horizontal;
            JumpPressed = jumpPressed;
            ResetPressed = resetPressed;
            LaneSelection = laneSelection;
        }
    }

    public sealed class Du02InputReader : MonoBehaviour
    {
        public Du02InputFrame Current { get; private set; }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                Current = default;
                return;
            }

            var horizontal = 0f;
            if (keyboard.aKey.isPressed) horizontal -= 1f;
            if (keyboard.dKey.isPressed) horizontal += 1f;

            var laneSelection = 0;
            if (keyboard.digit1Key.wasPressedThisFrame) laneSelection = 1;
            else if (keyboard.digit2Key.wasPressedThisFrame) laneSelection = 2;
            else if (keyboard.digit3Key.wasPressedThisFrame) laneSelection = 3;

            Current = new Du02InputFrame(
                horizontal,
                keyboard.spaceKey.wasPressedThisFrame,
                keyboard.rKey.wasPressedThisFrame,
                laneSelection);
        }
    }
}
