using Unity.Netcode.Components;
using UnityEngine;

namespace FrentePartido.Networking
{
    /// <summary>
    /// Owner-authoritative NetworkTransform. Used on the player prefab so the
    /// local player no longer rubber-bands to a server-replicated position
    /// (server-authoritative NT was producing the laggy client feel).
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
