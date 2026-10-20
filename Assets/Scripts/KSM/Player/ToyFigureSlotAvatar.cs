using Unity.Netcode;
using UnityEngine;

namespace KSM
{
    // Picks which pre-colored Toy Figure body (Blue = host/slot 0, Orange = joiner/slot 1)
    // this player shows, based on OwnerClientId. Standalone NetworkBehaviour so it doesn't
    // require any changes to LGH's NetworkPlayer/PlayerAvatar scripts.
    public class ToyFigureSlotAvatar : NetworkBehaviour
    {
        public GameObject blueFigure, orangeFigure;

        public override void OnNetworkSpawn()
        {
            bool useBlue = (int)(OwnerClientId % 2) == 0;
            if (blueFigure != null) blueFigure.SetActive(useBlue);
            if (orangeFigure != null) orangeFigure.SetActive(!useBlue);
        }
    }
}
