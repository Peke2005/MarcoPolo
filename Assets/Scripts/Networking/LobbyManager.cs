using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace FrentePartido.Networking
{
    /// <summary>
    /// Static utility for Unity Lobby service operations.
    /// Manages lobby creation, joining, heartbeat, and player data updates.
    /// </summary>
    public static class LobbyManager
    {
        public const string KEY_JOIN_CODE = "JoinCode";
        public const string KEY_PLAYER_NAME = "PlayerName";
        public const string KEY_FACTION = "Faction";
        public const string KEY_ABILITY_ID = "AbilityId";
        public const string KEY_IS_READY = "IsReady";

        private static Lobby _currentLobby;

        public static Lobby CurrentLobby => _currentLobby;
        public static bool IsInLobby => _currentLobby != null;
        public static bool IsHost => _currentLobby != null &&
            _currentLobby.HostId == AuthenticationService.Instance.PlayerId;

        /// <summary>
        /// Creates a new lobby with relay join code stored in lobby data.
        /// </summary>
        public static async Task<Lobby> CreateLobby(string lobbyName, string joinCode, int maxPlayers = 2)
        {
            if (string.IsNullOrWhiteSpace(lobbyName))
                lobbyName = "FP_Match";

            try
            {
                CreateLobbyOptions options = new CreateLobbyOptions
                {
                    IsPrivate = true,
                    Data = new Dictionary<string, DataObject>
                    {
                        {
                            KEY_JOIN_CODE,
                            new DataObject(DataObject.VisibilityOptions.Member, joinCode)
                        }
                    },
                    Player = CreateLocalPlayer()
                };

                _currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);

                Debug.Log($"[Lobby] Created '{_currentLobby.Name}' | LobbyCode: {_currentLobby.LobbyCode} | Id: {_currentLobby.Id}");

                return _currentLobby;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[Lobby] Failed to create lobby: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Joins an existing lobby by its lobby code (not the relay join code).
        /// </summary>
        public static async Task<Lobby> JoinLobbyByCode(string lobbyCode)
        {
            if (string.IsNullOrWhiteSpace(lobbyCode))
            {
                throw new ArgumentException("[Lobby] Lobby code cannot be null or empty.", nameof(lobbyCode));
            }

            try
            {
                JoinLobbyByCodeOptions options = new JoinLobbyByCodeOptions
                {
                    Player = CreateLocalPlayer()
                };

                _currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, options);

                Debug.Log($"[Lobby] Joined '{_currentLobby.Name}' | Players: {_currentLobby.Players.Count}/{_currentLobby.MaxPlayers}");

                return _currentLobby;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[Lobby] Failed to join lobby with code '{lobbyCode}': {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Removes the local player from the current lobby.
        /// If host, this deletes the lobby entirely.
        /// </summary>
        public static async Task LeaveLobby()
        {
            if (_currentLobby == null)
            {
                Debug.LogWarning("[Lobby] Not in a lobby. Nothing to leave.");
                return;
            }

            try
            {
                string lobbyId = _currentLobby.Id;
                string playerId = AuthenticationService.Instance.PlayerId;

                if (IsHost)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
                    Debug.Log("[Lobby] Host deleted lobby.");
                }
                else
                {
                    await LobbyService.Instance.RemovePlayerAsync(lobbyId, playerId);
                    Debug.Log("[Lobby] Left lobby.");
                }

                _currentLobby = null;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[Lobby] Failed to leave lobby: {e.Message}");
                _currentLobby = null;
            }
        }

        /// <summary>
        /// Updates a single data field on the local player within the current lobby.
        /// </summary>
        public static async Task UpdatePlayerData(string key, string value)
        {
            if (_currentLobby == null)
            {
                return;
            }

            try
            {
                UpdatePlayerOptions options = new UpdatePlayerOptions
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        {
                            key,
                            new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, value)
                        }
                    }
                };

                string playerId = AuthenticationService.Instance.PlayerId;
                _currentLobby = await LobbyService.Instance.UpdatePlayerAsync(
                    _currentLobby.Id, playerId, options);

                Debug.Log($"[Lobby] Updated player data: {key} = {value}");
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[Lobby] Failed to update player data '{key}': {e.Message}");
            }
        }

        /// <summary>
        /// Sends a heartbeat ping to keep the lobby alive.
        /// Must be called periodically by the host (every ~15 seconds).
        /// </summary>
        public static async Task HeartbeatLobby()
        {
            if (_currentLobby == null || !IsHost) return;

            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(_currentLobby.Id);
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[Lobby] Heartbeat failed: {e.Message}");
            }
        }

        /// <summary>
        /// Refreshes the locally cached lobby data from the service.
        /// </summary>
        public static async Task<Lobby> RefreshLobby()
        {
            if (_currentLobby == null) return null;

            try
            {
                _currentLobby = await LobbyService.Instance.GetLobbyAsync(_currentLobby.Id);
                return _currentLobby;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[Lobby] Failed to refresh lobby: {e.Message}");
                _currentLobby = null;
                return null;
            }
        }

        /// <summary>
        /// Retrieves the Relay join code stored in the current lobby data.
        /// </summary>
        public static string GetRelayJoinCode()
        {
            if (_currentLobby == null) return null;

            if (_currentLobby.Data != null &&
                _currentLobby.Data.TryGetValue(KEY_JOIN_CODE, out DataObject joinCodeData))
            {
                return joinCodeData.Value;
            }

            Debug.LogWarning("[Lobby] Relay join code not found in lobby data.");
            return null;
        }

        private static Unity.Services.Lobbies.Models.Player CreateLocalPlayer()
        {
            string playerName = Core.GameConfig.Preferences != null
                ? Core.GameConfig.Preferences.playerName
                : "Soldado";

            return new Unity.Services.Lobbies.Models.Player
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    {
                        KEY_PLAYER_NAME,
                        new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName)
                    },
                    {
                        KEY_FACTION,
                        new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "")
                    },
                    {
                        KEY_ABILITY_ID,
                        new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "")
                    },
                    {
                        KEY_IS_READY,
                        new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "false")
                    }
                }
            };
        }
    }
}
