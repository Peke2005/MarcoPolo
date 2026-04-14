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
            EnsureDefaultBindings();
        }

        private void EnsureDefaultBindings()
        {
            if (moveAction == null) moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            if (moveAction.bindings.Count == 0)
            {
                moveAction.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w")
                    .With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d");
                moveAction.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/upArrow")
                    .With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/leftArrow")
                    .With("Right", "<Keyboard>/rightArrow");
                moveAction.AddBinding("<Gamepad>/leftStick");
            }

            if (lookAction == null) lookAction = new InputAction("Look", InputActionType.Value, expectedControlType: "Vector2");
            if (lookAction.bindings.Count == 0)
                lookAction.AddBinding("<Mouse>/position");

            if (fireAction == null) fireAction = new InputAction("Fire", InputActionType.Button);
            if (fireAction.bindings.Count == 0)
            {
                fireAction.AddBinding("<Mouse>/leftButton");
                fireAction.AddBinding("<Gamepad>/rightTrigger");
            }

            if (reloadAction == null) reloadAction = new InputAction("Reload", InputActionType.Button);
            if (reloadAction.bindings.Count == 0)
            {
                reloadAction.AddBinding("<Keyboard>/r");
                reloadAction.AddBinding("<Gamepad>/buttonWest");
            }

            if (grenadeAction == null) grenadeAction = new InputAction("Grenade", InputActionType.Button);
            if (grenadeAction.bindings.Count == 0)
            {
                grenadeAction.AddBinding("<Keyboard>/g");
                grenadeAction.AddBinding("<Mouse>/rightButton");
                grenadeAction.AddBinding("<Gamepad>/leftTrigger");
            }

            if (abilityAction == null) abilityAction = new InputAction("Ability", InputActionType.Button);
            if (abilityAction.bindings.Count == 0)
            {
                abilityAction.AddBinding("<Keyboard>/q");
                abilityAction.AddBinding("<Keyboard>/space");
                abilityAction.AddBinding("<Gamepad>/buttonEast");
            }

            if (pauseAction == null) pauseAction = new InputAction("Pause", InputActionType.Button);
            if (pauseAction.bindings.Count == 0)
            {
                pauseAction.AddBinding("<Keyboard>/escape");
                pauseAction.AddBinding("<Gamepad>/start");
            }
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

            Vector2 move = Vector2.zero;
            if (moveAction != null && moveAction.enabled)
                move = moveAction.ReadValue<Vector2>();

            // Fallback: read Keyboard.current directly (works even if InputAction failed to bind).
            var kb = Keyboard.current;
            if (kb != null && move.sqrMagnitude < 0.01f)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
            }
            var pad = Gamepad.current;
            if (pad != null && move.sqrMagnitude < 0.01f)
                move = pad.leftStick.ReadValue();

            MoveInput = move;
            if (move.sqrMagnitude > 0.01f)
                OnMoveInput?.Invoke(move);

            // Convert mouse screen position to world position
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                Vector2 screenPos = Vector2.zero;
                if (lookAction != null && lookAction.enabled)
                    screenPos = lookAction.ReadValue<Vector2>();
                if (screenPos == Vector2.zero && Mouse.current != null)
                    screenPos = Mouse.current.position.ReadValue();

                Vector3 worldPos = _mainCamera.ScreenToWorldPoint(
                    new Vector3(screenPos.x, screenPos.y, -_mainCamera.transform.position.z));
                AimWorldPosition = new Vector2(worldPos.x, worldPos.y);
            }

            // Fallback button events ONLY if the corresponding InputAction failed to bind/enable.
            bool fireOk = fireAction != null && fireAction.enabled && fireAction.bindings.Count > 0;
            bool reloadOk = reloadAction != null && reloadAction.enabled && reloadAction.bindings.Count > 0;
            bool grenadeOk = grenadeAction != null && grenadeAction.enabled && grenadeAction.bindings.Count > 0;
            bool abilityOk = abilityAction != null && abilityAction.enabled && abilityAction.bindings.Count > 0;
            bool pauseOk = pauseAction != null && pauseAction.enabled && pauseAction.bindings.Count > 0;

            if (kb != null)
            {
                if (!reloadOk && kb.rKey.wasPressedThisFrame) OnReloadPressed?.Invoke();
                if (!grenadeOk && kb.gKey.wasPressedThisFrame) OnGrenadePressed?.Invoke();
                if (!abilityOk && (kb.qKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) OnAbilityPressed?.Invoke();
                if (!pauseOk && kb.escapeKey.wasPressedThisFrame) OnPausePressed?.Invoke();
            }
            if (!fireOk && Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame) OnFirePressed?.Invoke();
                if (Mouse.current.leftButton.wasReleasedThisFrame) OnFireReleased?.Invoke();
            }
            if (!grenadeOk && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                OnGrenadePressed?.Invoke();
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
