using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace PotionCraft
{
    // 약품 제조 퍼즐의 서버 권한 동기화. DumbwaiterNetworkSync와 같은 방식:
    // 플레이어는 "어느 장치에 E를 눌렀다"는 요청만 보내고, 호스트가 손에 든 아이템·장치 상태를 보고 판정한 뒤 결과를 모두에게 알린다.
    // 나중에 접속한 플레이어는 전체 상태(장치 상태 + 모두의 손 + 완료 여부)를 받는다.
    // 사용법: DoorSync_LGH(NetworkObject가 있는 씬 오브젝트)에 같이 붙인다.
    public class PotionCraftSync : NetworkBehaviour
    {
        public static PotionCraftSync Instance { get; private set; }

        [Header("데이터")]
        [SerializeField] private PotionCatalog catalog;
        [SerializeField] private PotionRecipeTable recipeTable;
        [SerializeField] private RecipeBookData bookData;
        [SerializeField] private FinalRecipeData finalRecipe;

        [Header("완료")]
        [SerializeField] private string completionSubtitle = "제작에 성공하였다, 어딘가의 문이 열린것같다";
        [Tooltip("완료 자막이 떠 있는 시간(초).")]
        [SerializeField] private float subtitleSeconds = 5f;
        [Tooltip("기믹이 완료될 때 호스트(혼자 플레이면 로컬)에서 한 번 실행된다. 열릴 문 등은 나중에 여기에 연결한다.")]
        public UnityEvent OnGimmickCompleted;

        public PotionCatalog Catalog { get { return catalog; } }
        public PotionRecipeTable RecipeTable { get { return recipeTable; } }
        public RecipeBookData BookData { get { return bookData; } }
        public FinalRecipeData FinalRecipe { get { return finalRecipe; } }
        public bool Completed { get { return completed; } }

        private PotionStation[] stations = new PotionStation[0];
        private readonly Dictionary<ulong, int> held = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, GameObject> heldVisuals = new Dictionary<ulong, GameObject>();
        private readonly Dictionary<ulong, int> heldVisualCode = new Dictionary<ulong, int>();
        private bool completed;
        private GUIStyle hudStyle;

        private static bool Online { get { return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening; } }
        private ulong LocalId { get { return IsSpawned ? NetworkManager.LocalClientId : 0UL; } }

        public int LocalHeld { get { return GetHeld(LocalId); } }

        private void Awake()
        {
            Instance = this;
            stations = FindObjectsByType<PotionStation>(FindObjectsInactive.Include);
            Array.Sort(stations, (a, b) => string.CompareOrdinal(PathOf(a.transform), PathOf(b.transform)));
            Debug.Log("[PotionCraft] PotionCraftSync 준비됨 (장치 " + stations.Length + "개)", this);
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        private static string PathOf(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            else RequestFullStateRpc();
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        private void OnClientDisconnected(ulong client)
        {
            if (held.Remove(client)) HeldRpc(client, 0);
        }

        // ---------- 손에 든 아이템 ----------

        public int GetHeld(ulong client) { int code; return held.TryGetValue(client, out code) ? code : 0; }

        // 호스트(또는 혼자 플레이)에서만 호출한다.
        public void ServerSetHeld(ulong client, int code)
        {
            if (code == 0) held.Remove(client); else held[client] = code;
            if (IsSpawned) HeldRpc(client, code);
        }

        // ---------- 진입점 ----------

        public void RequestInteract(PotionStation s)
        {
            int i = Array.IndexOf(stations, s);
            if (i < 0) return;
            if (IsSpawned) { InteractRpc(i); return; }
            if (Online) return;
            ShowLocal(HandleInteract(i, 0UL));
        }

        // 호스트 전용: 장치에 E를 눌렀을 때의 처리.
        private string HandleInteract(int i, ulong client)
        {
            PotionStation st = stations[i];
            int before = GetHeld(client);
            int h = before;
            int[] stateBefore = st.GetState();
            string msg = st.ServerInteract(ref h);
            if (h != before) ServerSetHeld(client, h);
            if (!SameState(stateBefore, st.GetState())) Publish(st);
            return msg;
        }

        // 장치 상태를 모두에게 반영한다 (호스트 자신은 바로 Refresh).
        public void Publish(PotionStation s)
        {
            int i = Array.IndexOf(stations, s);
            if (i < 0) return;
            s.Refresh();
            if (IsSpawned) StateRpc(i, s.GetState());
        }

        public void Fx(PotionStation s, PotionFx fx)
        {
            int i = Array.IndexOf(stations, s);
            if (i < 0) return;
            if (IsSpawned) FxRpc(i, (int)fx);
            else s.PlayFx(fx);
        }

        // 최종 혼합 통이 정답일 때 호스트가 호출한다. 한 번만 실행된다.
        public void ServerComplete()
        {
            if (completed) return;
            completed = true;
            if (IsSpawned) CompleteRpc(); else ShowSubtitle();
            Debug.Log("[PotionCraft] OnGimmickCompleted 실행");
            if (OnGimmickCompleted != null) OnGimmickCompleted.Invoke();
        }

        private static bool SameState(int[] a, int[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static void ShowLocal(string msg)
        {
            if (!string.IsNullOrEmpty(msg) && LGH.PlayerInteractor.Active != null) LGH.PlayerInteractor.Active.ShowMessage(msg);
        }

        private void ShowSubtitle()
        {
            if (LGH.PlayerInteractor.Active != null) LGH.PlayerInteractor.Active.ShowMessage(completionSubtitle, subtitleSeconds);
        }

        private void Reply(string msg, ulong client)
        {
            if (string.IsNullOrEmpty(msg)) return;
            if (client == NetworkManager.LocalClientId) ShowLocal(msg);
            else MessageRpc(msg, RpcTarget.Single(client, RpcTargetUse.Temp));
        }

        // ---------- RPC ----------

        [Rpc(SendTo.Server)]
        private void InteractRpc(int index, RpcParams rpcParams = default)
        {
            if (index < 0 || index >= stations.Length) return;
            ulong sender = rpcParams.Receive.SenderClientId;
            Reply(HandleInteract(index, sender), sender);
        }

        [Rpc(SendTo.NotServer)]
        private void StateRpc(int index, int[] state)
        {
            if (index < 0 || index >= stations.Length) return;
            stations[index].ApplyState(state);
            stations[index].Refresh();
        }

        [Rpc(SendTo.NotServer)]
        private void HeldRpc(ulong client, int code)
        {
            if (code == 0) held.Remove(client); else held[client] = code;
        }

        [Rpc(SendTo.Everyone)]
        private void FxRpc(int index, int fx)
        {
            if (index >= 0 && index < stations.Length) stations[index].PlayFx((PotionFx)fx);
        }

        [Rpc(SendTo.Everyone)]
        private void CompleteRpc()
        {
            completed = true;
            ShowSubtitle();
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void MessageRpc(string message, RpcParams rpcParams)
        {
            ShowLocal(message);
        }

        [Rpc(SendTo.Server)]
        private void RequestFullStateRpc(RpcParams rpcParams = default)
        {
            // 배열 안의 배열은 직렬화가 안 되므로 상태를 한 줄로 펴서 보낸다 (counts로 장치별 길이를 나눔).
            var counts = new int[stations.Length];
            var flat = new List<int>();
            for (int i = 0; i < stations.Length; i++)
            {
                int[] s = stations[i].GetState();
                counts[i] = s.Length;
                flat.AddRange(s);
            }
            var clients = new ulong[held.Count];
            var codes = new int[held.Count];
            int k = 0;
            foreach (var kv in held) { clients[k] = kv.Key; codes[k] = kv.Value; k++; }
            FullStateRpc(counts, flat.ToArray(), clients, codes, completed, RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void FullStateRpc(int[] counts, int[] flat, ulong[] clients, int[] codes, bool done, RpcParams rpcParams)
        {
            int offset = 0;
            for (int i = 0; i < stations.Length && i < counts.Length; i++)
            {
                var s = new int[counts[i]];
                for (int k = 0; k < s.Length && offset < flat.Length; k++) s[k] = flat[offset++];
                stations[i].ApplyState(s);
                stations[i].Refresh();
            }
            held.Clear();
            for (int i = 0; i < clients.Length; i++) held[clients[i]] = codes[i];
            completed = done;
        }

        // ---------- 들고 있는 아이템 모양 (모든 클라이언트가 같은 상태를 본다) ----------

        private void LateUpdate()
        {
            var removed = new List<ulong>();
            foreach (var kv in heldVisuals) if (!held.ContainsKey(kv.Key)) removed.Add(kv.Key);
            foreach (ulong id in removed)
            {
                if (heldVisuals[id] != null) Destroy(heldVisuals[id]);
                heldVisuals.Remove(id);
                heldVisualCode.Remove(id);
            }

            foreach (var kv in held)
            {
                GameObject go;
                heldVisuals.TryGetValue(kv.Key, out go);
                int shown;
                heldVisualCode.TryGetValue(kv.Key, out shown);
                if (go != null && shown == kv.Value) continue;
                if (go != null) Destroy(go);
                heldVisuals.Remove(kv.Key);
                var made = BuildHeldVisual(kv.Key, kv.Value);
                if (made != null) { heldVisuals[kv.Key] = made; heldVisualCode[kv.Key] = kv.Value; }
            }
        }

        private GameObject BuildHeldVisual(ulong client, int code)
        {
            if (catalog == null) return null;
            GameObject prefab = catalog.VisualPrefab(code);
            if (prefab == null) return null;

            bool mine = client == LocalId;
            Transform parent;
            Vector3 localPos;
            if (mine)
            {
                var pi = LGH.PlayerInteractor.Active;
                if (pi == null || pi.viewCamera == null) return null;
                parent = pi.viewCamera;
                localPos = new Vector3(0.3f, -0.28f, 0.6f);
            }
            else
            {
                if (!IsSpawned) return null;
                var po = NetworkManager.SpawnManager.GetPlayerNetworkObject(client);
                if (po == null) return null; // 아직 스폰 전이면 다음 프레임에 다시 시도
                parent = po.transform;
                localPos = new Vector3(0.25f, 1.15f, 0.5f);
            }

            var go = Instantiate(prefab, parent);
            go.name = "Held_" + catalog.NameOf(code);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            PotionVisuals.StripColliders(go);
            if (Item.KindOf(code) == ItemKind.Powder) PotionVisuals.TintMarked(go, catalog.ColorOf(code));
            return go;
        }

        private void OnGUI()
        {
            int code = LocalHeld;
            if (code == 0 || catalog == null) return;
            if (hudStyle == null) hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            var v = LGH.UiScale.Apply();
            var r = new Rect(16f, v.y - 86f, 260f, 32f);
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = catalog.ColorOf(code);
            GUI.DrawTexture(new Rect(r.x + 6f, r.y + 7f, 18f, 18f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 32f, r.y, r.width - 36f, r.height), "손: " + catalog.NameOf(code), hudStyle);
            GUI.color = old;
        }
    }
}
