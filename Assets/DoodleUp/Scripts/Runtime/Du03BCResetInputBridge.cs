using DoodleUp.Input;
using UnityEngine;

namespace DoodleUp.Runtime
{
    [DefaultExecutionOrder(-25)]
    public sealed class Du03BCResetInputBridge : MonoBehaviour
    {
        [SerializeField] private Du03BCInputEdgeLatch inputLatch;
        [SerializeField] private Du02RuntimeController runtimeController;

        public void Configure(Du03BCInputEdgeLatch latch, Du02RuntimeController controller)
        {
            inputLatch = latch;
            runtimeController = controller;
        }

        private void Update()
        {
            if (inputLatch != null && inputLatch.ConsumeResetPressed())
            {
                runtimeController.ResetCurrentLaneForProbe();
                Debug.Log($"[DU03BC_INPUT_RESET] frame={Time.frameCount} control=R consumed=True path=CANONICAL_RESET");
            }
        }
    }
}
