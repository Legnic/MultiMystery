using System.Collections.Generic;
using UnityEngine;

// 씬에 놓는 플레이어 시작 위치 표시. slot 번호는 접속 순서(호스트 = 0, 참가자 = 1)와 맞춘다.
// NetworkPlayer가 스폰될 때 같은 slot의 표시가 씬에 있으면 프리팹의 기본 좌표 대신 이 위치를 쓴다.
public class PlayerSpawnPoint : MonoBehaviour
{
    static readonly List<PlayerSpawnPoint> All = new List<PlayerSpawnPoint>();

    [Tooltip("0 = 호스트(왼쪽 방), 1 = 참가자(오른쪽 방)")]
    [SerializeField] int slot;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public static bool TryGet(int slot, out Vector3 position, out float yaw)
    {
        foreach (var p in All)
        {
            if (p.slot != slot) continue;
            position = p.transform.position;
            yaw = p.transform.eulerAngles.y;
            return true;
        }
        position = default;
        yaw = 0f;
        return false;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = slot == 0 ? Color.cyan : Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.4f);
        Gizmos.DrawRay(transform.position + Vector3.up, transform.forward);
    }
}
