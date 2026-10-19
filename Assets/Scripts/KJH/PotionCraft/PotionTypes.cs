namespace PotionCraft
{
    // 판정은 표시 이름이 아니라 항상 이 ID로 한다.
    public enum MaterialId { RedRoot, SilverLeaf, GreenLeaf, BlueFlower }
    public enum WaterId { Red, Blue, Green }
    public enum PowderColor { Green, Red, Blue, Pink, Black, Gold }

    // 플레이어가 손에 드는 아이템의 종류. 아이템은 int 하나("코드")로 표현해서 네트워크로 주고받는다.
    public enum ItemKind { None = 0, Material = 1, Water = 2, Lump = 3, Powder = 4 }

    // 스테이션에서 일어나는 연출 종류 (호스트가 모두에게 같은 연출을 재생시킨다).
    public enum PotionFx { Reject = 0, Explode = 1, Success = 2 }

    // 아이템 코드 = (종류 << 8) | 세부값. 0은 "빈손".
    // 세부값: 재료=MaterialId, 물=WaterId, 덩이/가루=PowderColor.
    public static class Item
    {
        public const int None = 0;
        public static int Make(ItemKind kind, int sub) { return ((int)kind << 8) | (sub & 0xFF); }
        public static ItemKind KindOf(int code) { return (ItemKind)(code >> 8); }
        public static int SubOf(int code) { return code & 0xFF; }
    }
}
