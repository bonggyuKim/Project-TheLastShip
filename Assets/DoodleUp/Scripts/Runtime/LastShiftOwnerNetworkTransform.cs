using Unity.Netcode.Components;

namespace DoodleUp.Runtime
{
    public sealed class LastShiftOwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
