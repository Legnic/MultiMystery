using System.Collections;
using LGH;
using Unity.Netcode;
using UnityEngine;

// 갤러리 액자 퍼즐 판정. 씬에 NetworkObject와 함께 하나만 둔다(DoorNetworkSync와 같은 방식).
// 호스트가 각 액자의 단계(-1/0/+1)를 관리하고, 정답(E0=+1, E1=+1, E2=-1)이 되면 모두에게 알려
// 마지막 액자(E3)를 떨어뜨려 뒤에 붙은 열쇠를 드러낸다. 네트워크 없이 단독 실행해도 같은 로직으로 동작한다.
public class PortraitPuzzle : NetworkBehaviour
{
    public static PortraitPuzzle Instance { get; private set; }

    [Tooltip("기울일 수 있는 액자들. 앞에서부터 Answer 개수만큼이 정답 판정 대상이고, 그 뒤는 정답과 무관한 장식 액자(상태만 동기화)")]
    [SerializeField] PortraitTilt[] portraits;
    [Tooltip("각 액자의 정답 단계: -1 = 왼쪽 30도, +1 = 오른쪽 30도")]
    [SerializeField] int[] answer = { 1, 1, -1 };
    [Tooltip("정답일 때 떨어지는 액자")]
    [SerializeField] Transform fallingFrame;
    [SerializeField] float dropDelay = 0.7f;

    public bool Solved { get; private set; }

    void Awake() { Instance = this; }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) RequestFullStateRpc();
    }

    int IndexOf(PortraitTilt p) { return System.Array.IndexOf(portraits, p); }

    // ---- 로컬 플레이어가 호출: delta(-1 = Q, +1 = E)만큼 한 칸 이동 요청 ----
    public void RequestShift(PortraitTilt portrait, int delta)
    {
        int i = IndexOf(portrait);
        if (i < 0 || (Solved && i < answer.Length)) return;
        if (IsSpawned) ShiftRpc(i, delta);
        else ServerShift(i, delta);
    }

    [Rpc(SendTo.Server)]
    void ShiftRpc(int index, int delta) { ServerShift(index, delta); }

    // 호스트(또는 단독 실행)에서만 호출
    void ServerShift(int index, int delta)
    {
        if (index < 0 || index >= portraits.Length || (Solved && index < answer.Length)) return;
        int next = Mathf.Clamp(portraits[index].Step + delta, -1, 1);
        if (next == portraits[index].Step) return;

        if (IsSpawned) ApplyStepRpc(index, next);
        else portraits[index].ApplyStep(next);

        if (!Solved && index < answer.Length && IsAnswer(index, next))
        {
            if (IsSpawned) SolveRpc();
            else Solve();
        }
    }

    // 방금 바꾼 단계까지 반영했을 때 전체가 정답인지 (RPC 전달 전이라 방금 값은 직접 대입해서 본다)
    bool IsAnswer(int changedIndex, int changedStep)
    {
        for (int i = 0; i < portraits.Length && i < answer.Length; i++)
        {
            int step = i == changedIndex ? changedStep : portraits[i].Step;
            if (step != answer[i]) return false;
        }
        return true;
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ApplyStepRpc(int index, int step)
    {
        if (index >= 0 && index < portraits.Length) portraits[index].ApplyStep(step);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SolveRpc() { Solve(); }

    void Solve(bool instant = false)
    {
        if (Solved) return;
        Solved = true;
        if (fallingFrame != null) StartCoroutine(Drop(instant));
        if (!instant && PlayerInteractor.Active != null) PlayerInteractor.Active.ShowMessage("철컥 — 마지막 액자가 떨어졌다");
    }

    // 중력 낙하 + 앞으로 기울며 바닥에 쓰러지는 연출 (물리 엔진 없이 계산해서 모든 클라이언트가 같은 결과를 본다)
    IEnumerator Drop(bool instant)
    {
        Transform f = fallingFrame;
        Vector3 startPos = f.position;
        Quaternion startRot = f.rotation;
        float half = f.lossyScale.y * 0.5f;
        float floorY = 0f;
        if (Physics.Raycast(startPos, Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore)) floorY = hit.point.y;
        float landY = floorY + half * 0.9f;
        // 보는 사람 → 액자 방향(transform.right)의 반대, 즉 방 안쪽으로 윗부분이 쏟아지도록 기울인다
        Vector3 lean = Vector3.Slerp(Vector3.up, -f.right, 0.35f);
        Quaternion endRot = Quaternion.FromToRotation(Vector3.up, lean) * startRot;

        if (!instant) yield return new WaitForSeconds(dropDelay);

        float vy = 0f, tilt = 0f;
        Vector3 pos = startPos;
        while (!instant && pos.y > landY)
        {
            vy -= 9.81f * Time.deltaTime;
            pos.y = Mathf.Max(landY, pos.y + vy * Time.deltaTime);
            tilt = Mathf.Clamp01(tilt + Time.deltaTime * 2f);
            f.SetPositionAndRotation(pos, Quaternion.Slerp(startRot, endRot, tilt));
            yield return null;
        }
        pos.y = landY;
        f.SetPositionAndRotation(pos, endRot);
    }

    // ---- 나중에 들어온 플레이어에게 현재 상태 전달 ----
    [Rpc(SendTo.Server)]
    void RequestFullStateRpc(RpcParams rpcParams = default)
    {
        var steps = new int[portraits.Length];
        for (int i = 0; i < steps.Length; i++) steps[i] = portraits[i].Step;
        FullStateRpc(steps, Solved, RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void FullStateRpc(int[] steps, bool solved, RpcParams rpcParams)
    {
        for (int i = 0; i < portraits.Length && i < steps.Length; i++) portraits[i].ApplyStep(steps[i], true);
        if (solved) Solve(true);
    }
}
