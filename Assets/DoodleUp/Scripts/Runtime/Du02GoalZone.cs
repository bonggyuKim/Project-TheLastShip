using DoodleUp.Core;
using UnityEngine;

namespace DoodleUp.Runtime
{
    [RequireComponent(typeof(Collider))]
    public sealed class Du02GoalZone : MonoBehaviour
    {
        [SerializeField] private Du02TaskId taskId;
        private Du02TaskState taskState;

        public void Configure(Du02TaskId id)
        {
            taskId = id;
        }

        private void Awake()
        {
            taskState = FindFirstObjectByType<Du02TaskState>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (taskState != null && taskState.TaskId == taskId && other.gameObject.layer == LayerMask.NameToLayer("Player"))
                taskState.SetInsideGoal(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (taskState != null && taskState.TaskId == taskId && other.gameObject.layer == LayerMask.NameToLayer("Player"))
                taskState.SetInsideGoal(false);
        }
    }
}
