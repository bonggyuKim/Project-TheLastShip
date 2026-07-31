using DoodleUp.Core;
using UnityEngine;

namespace DoodleUp.Physics
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class Du02PlayerMotor : MonoBehaviour
    {
        private Rigidbody body;
        private float horizontalInput;
        private bool jumpRequested;
        private float depth;

        public bool IsGrounded { get; private set; }
        public Vector3 Velocity => body == null ? Vector3.zero : body.linearVelocity;
        public Vector3 AngularVelocity => body == null ? Vector3.zero : body.angularVelocity;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            depth = transform.position.z;
        }

        public void SetProbeState(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
        {
            body.constraints = RigidbodyConstraints.FreezePositionZ;
            SetState(position, rotation, velocity, angularVelocity);
        }

        public void SetInput(float horizontal, bool jumpPressed)
        {
            horizontalInput = Mathf.Clamp(horizontal, -1f, 1f);
            jumpRequested |= jumpPressed;
        }

        public void ResetState(Vector3 position)
        {
            ResetState(position, Vector3.zero);
        }

        public void ResetState(Vector3 position, Vector3 velocity)
        {
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            SetState(position, Quaternion.identity, velocity, Vector3.zero);
        }

        public void SetState(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
        {
            depth = position.z;
            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = velocity;
            body.angularVelocity = angularVelocity;
            horizontalInput = 0f;
            jumpRequested = false;
            IsGrounded = false;
            if (velocity == Vector3.zero && angularVelocity == Vector3.zero) body.Sleep();
            else body.WakeUp();
            UnityEngine.Physics.SyncTransforms();
        }

        private void FixedUpdate()
        {
            IsGrounded = UnityEngine.Physics.Raycast(body.position, Vector3.down, 0.56f, ~0, QueryTriggerInteraction.Ignore);
            var velocity = body.linearVelocity;
            velocity.x = horizontalInput * (IsGrounded ? Du02Profile.GroundSpeed : Du02Profile.AirSpeed);
            if (jumpRequested && IsGrounded)
            {
                velocity.y = Du02Profile.JumpSpeed;
            }

            jumpRequested = false;
            body.linearVelocity = velocity;
            body.position = new Vector3(body.position.x, body.position.y, depth);
        }
    }
}
