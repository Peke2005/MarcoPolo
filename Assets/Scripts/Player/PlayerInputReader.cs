using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FrentePartido.Player
{
    /// <summary>
    /// Reads player input via the new Input System and exposes events/properties.
    /// Attach to the player prefab. Only processes input for the local owner.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("Input Action References")]
        [SerializeField] private InputAction moveAction;
        [SerializeField] private InputAction lookAction;
        [SerializeField] private InputAction fireAction;
        [SerializeField] private InputAction reloadAction;
        [SerializeField] private InputAction grenadeAction;
        [SerializeField] private InputAction abilityAction;
        [SerializeField] private InputAction pauseAction;

        // Events
        public event Action<Vector2> OnMoveInput;
        public event Action OnFirePressed;
        public event Action OnFireReleased;
        public event Action OnReloadPressed;
        public event Action OnGrenadePressed;
        public event Action OnAbilityPressed;
        public event Action OnPausePressed;

        // Current input state
        public Vector2 MoveInput { get; private set; }
        public Vector2 AimWorldPosition { get; private set; }

        /// <summary>
        /// Set to false to suppress all input processing (e.g., not owner, dead, paused).
        /// </summary>
        public bool IsInputEnabled { get; set; } = true;

        private Camera _mainCamera;

        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        private void OnEnable()
        {
            EnableAction(moveAction);
            EnableAction(lookAction);
            EnableAction(fireAction);
            EnableAction(reloadAction);
            EnableAction(grenadeAction);
            EnableAction(abilityAction);
            EnableAction(pauseAction);

            if (fireAction != null)
            {
                fireAction.started += HandleFireStarted;
                fireAction.canceled += HandleFireCanceled;
            }

            if (reloadAction != null)
                reloadAction.started += HandleReloadStarted;

            if (grenadeAction != null)
                grenadeAction.started += HandleGrenadeStarted;

            if (abilityAction != null)
                abilityAction.started += HandleAbilityStarted;

            if (pauseAction != null)
                pauseAction.started += HandlePauseStarted;
        }

        private void OnDisable()
        {
            if (fireAction != null)
            {
                fireAction.started -= HandleFireStarted;
                fireAction.canceled -= HandleFireCanceled;
            }

            if (reloadAction != null)
                reloadAction.started -= HandleReloadStarted;

            if (grenadeAction != null)
                grenadeAction.started -= HandleGrenadeStarted;

            if (abilityAction != null)
                abilityAction.started -= HandleAbilityStarted;

            if (pauseAction != null)
                pauseAction.started -= HandlePauseStarted;

            DisableAction(moveAction);
            DisableAction(lookAction);
            DisableAction(fireAction);
            DisableAction(reloadAction);
            DisableAction(grenadeAction);
            DisableAction(abilityAction);
            DisableAction(pauseAction);
        }

        private void Update()
        {
            if (!IsInputEnabled)
            {
                MoveInput = Vector2.zero;
                return;
            }

            // Read movement
            if (moveAction != null)
            {
                MoveInput = moveAction.ReadValue<Vector2>();
                if (MoveInput.sqrMagnitude > 0.01f)
                    OnMoveInput?.Invoke(MoveInput);
            }

            // Convert mouse screen position to world position
            if (lookAction != null && _mainCamera != null)
            {
                Vector2 screenPos = lookAction.ReadValue<Vector2>();
                Vector3 worldPos = _mainCamera.ScreenToWorldPoint(
                    new Vector3(screenPos.x, screenPos.y, -_mainCamera.transform.position.z));
                AimWorldPosition = new Vector2(worldPos.x, worldPos.y);
            }
        }

        private void HandleFireStarted(InputAction.CallbackContext ctx)
        {
            if (IsInputEnabled) OnFirePressed?.Invoke();
        }

        private void HandleFireCanceled(InputAction.CallbackContext ctx)
        {
            if (IsInputEnabled) OnFireReleased?.Invoke();
        }

        private void HandleReloadStarted(InputAction.CallbackContext ctx)
        {
            if (IsInputEnabled) OnReloadPressed?.Invoke();
        }

        private void HandleGrenadeStarted(InputAction.CallbackContext ctx)
        {
            if (IsInputEnabled) OnGrenadePressed?.Invoke();
        }

        private void HandleAbilityStarted(InputAction.CallbackContext ctx)
        {
            if (IsInputEnabled) OnAbilityPressed?.Invoke();
        }

        private void HandlePauseStarted(InputAction.CallbackContext ctx)
        {
            if (IsInputEnabled) OnPausePressed?.Invoke();
        }

        private static void EnableAction(InputAction action)
        {
            if (action != null && !action.enabled)
                action.Enable();
        }

        private static void DisableAction(InputAction action)
        {
            if (action != null && action.enabled)
                action.Disable();
        }
    }
}
