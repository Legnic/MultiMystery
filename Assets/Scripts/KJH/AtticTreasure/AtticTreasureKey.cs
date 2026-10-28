namespace AtticTreasure
{
    // 다락 별 상자 속 현관 열쇠. E → 바로 왼손에 든다 (AtticTreasureSync). 들고 현관문을 보고 E → 열림 (AtticTreasureFrontDoor).
    // 모델 교체: 자식 "Visual"(AtticTreasure_KeyVisual 프리팹)만 바꾸면 된다.
    public class AtticTreasureKey : AtticTreasureBoxItem
    {
        protected override bool InBox { get { return Sync != null && !Sync.KeyTaken; } }
        protected override void RequestTake() { Sync.RequestTakeKey(); }
        protected override void RequestReturn() { if (Sync != null) Sync.RequestReturnKey(); }
    }
}
