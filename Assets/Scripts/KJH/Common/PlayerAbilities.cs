using System.Collections.Generic;
using UnityEngine;

// 이 플레이어가 어떤 고유 능력을 가졌는지 정하는 컴포넌트.
// 플레이어 A 프리팹에는 SoundEcho, 플레이어 B 프리팹에는 PastPhoto만 켜 두면
// 서로의 능력 대상(소리 듣기 사물 / 사진 촬영 지점 / 사진 인벤토리)이 아예 반응하지 않는다.
// 혼자 테스트하는 씬의 Player는 두 능력을 다 켜 둔다.
//
// 능력 확인은 각 플레이어 자기 화면에서만 하면 되므로, 나중에 네트워크가 붙어도 동기화할 필요가 없다.
// 사용법: Player 오브젝트(InteractionController가 있는 곳)에 붙이고 Abilities를 고른다.
public class PlayerAbilities : MonoBehaviour
{
    [Tooltip("이 플레이어가 가진 능력. 여러 개를 함께 고를 수 있다. Nothing이면 능력이 필요한 상호작용은 전부 할 수 없다.")]
    [SerializeField] private PlayerAbility abilities = PlayerAbility.None;

    public PlayerAbility Abilities => abilities;

    // 이 능력을 가졌는지. None(능력이 필요 없음)이면 항상 true.
    public bool Has(PlayerAbility ability) => ability == PlayerAbility.None || (abilities & ability) == ability;

    // 경고를 플레이어마다 한 번만 띄우기 위한 기록 (매 프레임 콘솔이 넘치지 않도록).
    private static readonly HashSet<GameObject> warnedPlayers = new HashSet<GameObject>();

    // player 오브젝트가 이 능력을 가졌는지 확인한다. 각 기능의 "이중 안전장치"에서 쓴다.
    // PlayerAbilities가 없는 플레이어는 "능력 없음"으로 본다 — 능력이 섞여 쓰이는 것보다 안 되는 쪽이 안전하기 때문.
    public static bool Has(GameObject player, PlayerAbility ability)
    {
        if (ability == PlayerAbility.None) return true;
        if (player == null) return false;

        PlayerAbilities found = player.GetComponent<PlayerAbilities>();
        if (found != null) return found.Has(ability);

        if (warnedPlayers.Add(player))
        {
            Debug.LogWarning($"[PlayerAbilities] '{player.name}'에 PlayerAbilities가 없어 능력이 필요한 상호작용({ability})을 막았습니다. " +
                             "Player에 PlayerAbilities를 붙이고 능력을 골라 주세요.", player);
        }
        return false;
    }
}
