namespace AtticTreasure
{
    // 다락 별 상자 속 황금상. 처음 집을 때: E → 두 사람 모두에게 획득 연출 → 연출이 끝나면 집은 사람 왼손에 들린다 (AtticTreasureSync).
    // 상자에 돌려놨다가 다시 집을 때는 연출 없이 바로 손에 든다.
    // 모델 교체: 자식 "Visual"(AtticTreasure_StatueVisual 프리팹)만 바꾸면 된다.
    public class AtticTreasureStatue : AtticTreasureBoxItem
    {
        protected override bool InBox { get { return Sync != null && Sync.State == StatueState.InBox; } }
        // 연출 중에는 숨긴다. 연출 화면에는 AtticTreasureGlowCutscene가 만든 임시 황금상이 대신 보인다 (2026-10-29)
        protected override bool Hidden { get { return Sync != null && Sync.State == StatueState.Cutscene; } }
        protected override void RequestTake() { Sync.RequestTake(); }
        protected override void RequestReturn() { if (Sync != null) Sync.RequestReturnStatue(); }
    }
}
