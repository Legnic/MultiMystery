using Unity.Netcode;

namespace CoopDemo
{
    // 2인 협동의 "플레이어 자리" 규칙. 호스트 = 0번(1층, 주황이), 호스트가 아닌 사람 = 1번(2층, 파랑이).
    //
    // 예전에는 clientId % 2 로 정했는데, 게스트가 나갔다가 다시 들어오면 새 clientId(2, 3, ...)를 받아서
    // 0번(호스트 자리)이 되어 버렸다. 호스트인지 아닌지만 보면 몇 번을 다시 들어와도 같은 자리가 된다.
    public static class PlayerSlot
    {
        public const int Host = 0;
        public const int Guest = 1;

        public static int Of(ulong clientId)
        {
            return clientId == NetworkManager.ServerClientId ? Host : Guest;
        }
    }
}
