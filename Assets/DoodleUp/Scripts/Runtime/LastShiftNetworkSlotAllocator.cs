using System.Collections.Generic;

namespace DoodleUp.Runtime
{
    public sealed class LastShiftNetworkSlotAllocator
    {
        private readonly Dictionary<ulong, int> slots = new();
        private readonly HashSet<int> reservedSlots = new();

        public int Count => slots.Count;

        public bool TryReserve(ulong clientId, out int slot)
        {
            if (slots.TryGetValue(clientId, out slot)) return true;
            for (var candidate = 0; candidate < LastShiftNetworkSession.MaxPlayers; candidate++)
            {
                if (reservedSlots.Contains(candidate)) continue;
                reservedSlots.Add(candidate);
                slots.Add(clientId, candidate);
                slot = candidate;
                return true;
            }

            slot = -1;
            return false;
        }

        public bool TryGet(ulong clientId, out int slot)
        {
            return slots.TryGetValue(clientId, out slot);
        }

        public bool Release(ulong clientId)
        {
            if (!slots.Remove(clientId, out var slot)) return false;
            reservedSlots.Remove(slot);
            return true;
        }

        public void Clear()
        {
            slots.Clear();
            reservedSlots.Clear();
        }
    }
}
