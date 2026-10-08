using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace LGH
{
    // On the network player prefab. The owner gets movement, camera, sound and door controls;
    // the other player's copy is just a body carrying a candle.
    public class NetworkPlayer : NetworkBehaviour
    {
        public static NetworkPlayer Local { get; private set; }

        [Tooltip("Components only the owner should run (FirstPersonController, PlayerInteractor, Camera, AudioListener).")]
        public Behaviour[] ownerOnly;
        [Tooltip("Own body renderers: hidden for the owner (shadow only), visible for the other player.")]
        public Renderer[] body;
        public Vector3[] spawnPoints = { new Vector3(43f, 0.05f, 14.6f), new Vector3(45f, 0.05f, 14.6f) };
        public float spawnYaw = 180f;

        CharacterController controller;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            SetOwnerParts(false);
        }

        public override void OnNetworkSpawn()
        {
            bool mine = IsOwner;
            SetOwnerParts(mine);
            foreach (var r in body)
                if (r != null) r.shadowCastingMode = mine ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            if (controller != null) controller.enabled = mine;
            gameObject.name = mine ? "Player (나)" : "Player (상대)";
            var avatar = GetComponentInChildren<PlayerAvatar>(true);
            if (avatar != null) avatar.SetSlot((int)OwnerClientId);

            if (mine)
            {
                Local = this;
                int slot = (int)(OwnerClientId % (ulong)Mathf.Max(1, spawnPoints.Length));
                if (controller != null) controller.enabled = false;
                transform.SetPositionAndRotation(spawnPoints[slot], Quaternion.Euler(0f, spawnYaw, 0f));
                if (controller != null) controller.enabled = true;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Local == this) Local = null;
        }

        void SetOwnerParts(bool on)
        {
            if (ownerOnly == null) return;
            foreach (var b in ownerOnly) if (b != null) b.enabled = on;
        }
    }
}
