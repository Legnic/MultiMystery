using System.Collections;
using LGH;
using Unity.Netcode;
using UnityEngine;

// 갤러리 액자 퍼즐 판정. 네트워크 프리팹(PortraitPuzzleSync)으로 등록해 씬에 하나만 둔다(DoorNetworkSync와 같은 방식).
// 씬 관리가 꺼져 있어서 프리팹으로 등록되지 않은 씬 NetworkObject는 클라이언트에 존재하지 않는다.
// 호스트가 각 액자의 단계(-1/0/+1)를 관리하고, 정답(E0=+1, E1=+1, E2=-1)이 되면 모두에게 알려
// 마지막 액자(E3)를 떨어뜨려 뒤에 붙은 열쇠를 드러낸다. 네트워크 없이 단독 실행해도 같은 로직으로 동작한다.
public class PortraitPuzzle : NetworkBehaviour
{
    public static PortraitPuzzle Instance { get; private set; }

    // 씬 오브젝트 참조(액자들, 떨어질 액자, 열쇠 상자)는 프리팹이 직접 들 수 없어서 씬의 PortraitPuzzleScene에서 런타임에 가져온다.
    static PortraitPuzzleScene Cfg { get { return PortraitPuzzleScene.Instance; } }
    PortraitTilt[] portraits { get { return Cfg != null && Cfg.portraits != null ? Cfg.portraits : System.Array.Empty<PortraitTilt>(); } }
    int[] answer { get { return Cfg != null && Cfg.answer != null ? Cfg.answer : System.Array.Empty<int>(); } }
    Transform fallingFrame { get { return Cfg != null ? Cfg.fallingFrame : null; } }
    float dropDelay { get { return Cfg != null ? Cfg.dropDelay : 0.7f; } }
    KeyBoxLid keyBox { get { return Cfg != null ? Cfg.keyBox : null; } }

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
        // 1층·2층 어느 쪽 플레이어든 같은 문구를 본다. 아래 연출 중 하나가 예외를 던져도 문구가 사라지지 않게 가장 먼저 띄운다.
        if (!instant && PlayerInteractor.Active != null) PlayerInteractor.Active.ShowMessage("액자 주변에 변화가 생겼다");
        Debug.Log($"[PortraitPuzzle] Solve instant={instant} frame={(fallingFrame != null)} keyBox={(keyBox != null)}");
        if (fallingFrame != null) StartCoroutine(Drop(instant));
        if (keyBox != null)
        {
            try { keyBox.Open(dropDelay, instant); }
            catch (System.Exception e) { Debug.LogError("[PortraitPuzzle] 열쇠 상자 뚜껑 열기 실패: " + e); }
        }
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
