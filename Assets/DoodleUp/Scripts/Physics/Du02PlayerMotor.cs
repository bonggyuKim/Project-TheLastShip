using System;
using DoodleUp.Core;
using UnityEngine;

namespace DoodleUp.Physics
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class Du02PlayerMotor : MonoBehaviour
    {
        private const float GroundProbeMargin = 0.06f;

        private Rigidbody body;
        private CapsuleCollider capsule;
        private PhysicsMaterial frictionlessMaterial;
        private float horizontalInput;
        private float depthInput;
        private bool depthLocomotionEnabled;
        private bool jumpRequested;
        private float depth;
        private bool hasGroundState;

        public bool IsGrounded { get; private set; }
        public Vector3 Velocity => body == null ? Vector3.zero : body.linearVelocity;
        public Vector3 AngularVelocity => body == null ? Vector3.zero : body.angularVelocity;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            frictionlessMaterial = new PhysicsMaterial("DU02_PlayerFrictionless")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            capsule.sharedMaterial = frictionlessMaterial;
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
            SetInput(horizontal, 0f, jumpPressed, false);
        }

        public void SetInput(float horizontal, float forward, bool jumpPressed, bool allowDepthLocomotion)
        {
            var movement = allowDepthLocomotion
                ? Vector2.ClampMagnitude(new Vector2(horizontal, forward), 1f)
                : new Vector2(Mathf.Clamp(horizontal, -1f, 1f), 0f);
            horizontalInput = movement.x;
            depthInput = movement.y;
            SetDepthLocomotionAllowed(allowDepthLocomotion);
            jumpRequested |= jumpPressed;
        }

        public void SetDepthLocomotionAllowed(bool allowed)
        {
            if (depthLocomotionEnabled && !allowed)
            {
                depth = body.position.z;
                var velocity = body.linearVelocity;
                velocity.z = 0f;
                body.linearVelocity = velocity;
                body.position = new Vector3(body.position.x, body.position.y, depth);
                UnityEngine.Physics.SyncTransforms();
            }

            depthLocomotionEnabled = allowed;
            if (!depthLocomotionEnabled)
                depthInput = 0f;
            body.constraints = RigidbodyConstraints.FreezeRotation
                | (depthLocomotionEnabled ? RigidbodyConstraints.None : RigidbodyConstraints.FreezePositionZ);
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
            depthInput = 0f;
            depthLocomotionEnabled = false;
            jumpRequested = false;
            IsGrounded = false;
            hasGroundState = false;
            if (velocity == Vector3.zero && angularVelocity == Vector3.zero) body.Sleep();
            else body.WakeUp();
            UnityEngine.Physics.SyncTransforms();
        }

        private void OnDestroy()
        {
            if (frictionlessMaterial != null)
                Destroy(frictionlessMaterial);
        }

        private void FixedUpdate()
        {
            var bounds = capsule.bounds;
            var probeDistance = bounds.extents.y + GroundProbeMargin;
            var grounded = UnityEngine.Physics.Raycast(
                bounds.center,
                Vector3.down,
                probeDistance,
                ~0,
                QueryTriggerInteraction.Ignore);
            if (!hasGroundState || grounded != IsGrounded)
            {
                IsGrounded = grounded;
                hasGroundState = true;
                Debug.Log(FormattableString.Invariant($"[DU02_GROUND] frame={Time.frameCount} grounded={IsGrounded} center=({bounds.center.x:F6},{bounds.center.y:F6},{bounds.center.z:F6}) distance={probeDistance:F6}"));
            }
            else
            {
                IsGrounded = grounded;
            }
            var speed = IsGrounded ? Du02Profile.GroundSpeed : Du02Profile.AirSpeed;
            var velocity = body.linearVelocity;
            velocity.x = horizontalInput * speed;
            velocity.z = depthInput * speed;
            if (jumpRequested && IsGrounded)
            {
                velocity.y = Du02Profile.JumpSpeed;
            }

            jumpRequested = false;
            body.linearVelocity = velocity;
            if (!depthLocomotionEnabled)
                body.position = new Vector3(body.position.x, body.position.y, depth);
        }
    }
}
