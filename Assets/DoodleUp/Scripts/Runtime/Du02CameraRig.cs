using DoodleUp.Core;
using UnityEngine;

namespace DoodleUp.Runtime
{
    [DefaultExecutionOrder(50)]
    public sealed class Du02CameraRig : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform playerRoot;

        public void Configure(Camera cameraComponent, Transform root)
        {
            targetCamera = cameraComponent;
            playerRoot = root;
        }

        public void ResetPose(Vector3 playerPosition)
        {
            var rotation = Quaternion.Euler(Du02Profile.CameraPitch, 0f, 0f);
            transform.SetPositionAndRotation(
                playerPosition + new Vector3(0f, Du02Profile.CameraHeight, -Du02Profile.CameraDistance),
                rotation);
            targetCamera.fieldOfView = Du02Profile.CameraVerticalFov;
        }
    }
}
