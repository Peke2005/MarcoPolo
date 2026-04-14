using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FrentePartido.Player
{
    public class PlayerAimController : NetworkBehaviour
    {
        [SerializeField] private Transform weaponPivot;

        public NetworkVariable<float> AimAngle = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private Camera _cam;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            AimAngle.OnValueChanged += OnAimAngleChanged;
            ApplyRotation(AimAngle.Value);
        }

        public override void OnNetworkDespawn()
        {
            AimAngle.OnValueChanged -= OnAimAngleChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsOwner) return;

            if (_cam == null) _cam = Camera.main;
            if (_cam == null || Mouse.current == null) return;

            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector3 mouseWorld = _cam.ScreenToWorldPoint(
                new Vector3(mouseScreen.x, mouseScreen.y, -_cam.transform.position.z));

            Vector2 dir = (Vector2)mouseWorld - (Vector2)transform.position;
            if (dir.sqrMagnitude < 0.0001f) return;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            AimAngle.Value = angle;
            ApplyRotation(angle);
        }

        private void OnAimAngleChanged(float oldValue, float newValue)
        {
            if (!IsOwner) ApplyRotation(newValue);
        }

        private void ApplyRotation(float angleDeg)
        {
            Quaternion rot = Quaternion.Euler(0f, 0f, angleDeg);
            if (weaponPivot != null) weaponPivot.rotation = rot;
            else transform.rotation = rot;
        }
    }
}
