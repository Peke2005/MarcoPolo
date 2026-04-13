using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using UnityEngine;

namespace FrentePartido.Core
{
    public static class ServiceInitializer
    {
        public static bool IsInitialized { get; private set; }

        public static async Task InitializeAsync()
        {
            if (IsInitialized) return;

            try
            {
                await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Debug.Log($"[ServiceInit] Signed in. PlayerId: {AuthenticationService.Instance.PlayerId}");
                }

                IsInitialized = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ServiceInit] Failed to initialize: {e}");

                // Log all inner exceptions for diagnostics
                var inner = e.InnerException;
                while (inner != null)
                {
                    Debug.LogError($"[ServiceInit] Inner: {inner.Message}");
                    inner = inner.InnerException;
                }

                throw;
            }
        }
    }
}
