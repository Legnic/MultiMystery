namespace AtticTreasure
{
    // 다락 별 상자 속 황금상. E → 두 사람 모두에게 획득 연출 → 연출이 끝나면 집은 사람 인벤토리에 들어간다 (AtticTreasureSync).
    // 모델 교체: 자식 "Visual"(AtticTreasure_StatueVisual 프리팹)만 바꾸면 된다.
    public class AtticTreasureStatue : AtticTreasureBoxItem
    {
        protected override bool InBox { get { return Sync != null && Sync.State == StatueState.InBox; } }
        protected override void RequestTake() { Sync.RequestTake(); }
    }
}
