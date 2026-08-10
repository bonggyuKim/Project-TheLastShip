using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>Canonical map에서 구운 승무원 시작 시점. 런타임은 씬 좌표를 추정하지 않는다.</summary>
    public sealed class LastShiftMapSpawnPose : MonoBehaviour
    {
        [SerializeField] private Vector3 spawn;
        [SerializeField] private Vector3 lookAt;

        public Vector3 Spawn => spawn;
        public Vector3 LookAt => lookAt;

        public void Configure(Vector3 mapSpawn, Vector3 mapLookAt)
        {
            spawn = mapSpawn;
            lookAt = mapLookAt;
        }

        public Vector3 SpawnForSlot(int slot) => spawn + new Vector3(0f, 0f, (slot - 1.5f) * 0.85f);

        public Quaternion RotationFor(Vector3 position) => Quaternion.LookRotation((lookAt - position).normalized, Vector3.up);
    }
}
