using System.Collections.Generic;
using DoodleUp.Stroke;
using UnityEngine;

namespace DoodleUp.Runtime
{
    public sealed class Du03ADeterministicIntentSource : MonoBehaviour, IDu03ADrawIntentSource
    {
        private readonly Queue<Du03ADrawIntent> intents = new();

        public int PendingIntentCount => intents.Count;
        public long ReadCount { get; private set; }

        public void Enqueue(in Du03ADrawIntent intent)
        {
            intents.Enqueue(intent);
        }

        public void Clear()
        {
            intents.Clear();
            ReadCount = 0;
        }

        public Du03ADrawIntent ReadIntent()
        {
            ReadCount++;
            return intents.Count > 0 ? intents.Dequeue() : default;
        }
    }
}
