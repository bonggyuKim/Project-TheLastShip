using UnityEngine;

namespace DoodleUp.Core
{
    public static class Du02Profile
    {
        public const string ProfileId = "DU02_PROFILE_V1";
        public const string CourseId = "DU02_THREE_TASK_V1";
        public const string SceneId = "DU02_SoloCourse";

        public const float FixedDeltaTime = 0.020f;
        public const float GroundSpeed = 2.50f;
        public const float AirSpeed = 2.00f;
        public const float JumpSpeed = 4.00f;
        public const float Gravity = -9.81f;
        public const float DepthTolerance = 0.001f;

        public const float CameraDistance = 4.50f;
        public const float CameraHeight = 1.20f;
        public const float CameraPitch = 10.00f;
        public const float CameraVerticalFov = 60.00f;

        public static readonly Vector3 HandLocalPosition = new Vector3(0.35f, 0.80f, 0.00f);

        public const float T1Gap = 0.70f;
        public const float T2HorizontalOffset = 0.65f;
        public const float T2VerticalOffset = 0.55f;
        public const float T3Gap = 0.95f;
        public const float T3ContactBandWidth = 0.12f;
    }
}
