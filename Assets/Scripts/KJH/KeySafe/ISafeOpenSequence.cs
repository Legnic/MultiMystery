using System.Collections;

// 금고가 열릴 때의 연출(문 회전, 열쇠 꽂기 모션 등)을 갈아 끼울 수 있게 해주는 인터페이스 (교체용 훅).
// LockedSafe는 연출이 "무엇을 하는지" 모르고, 같은 오브젝트에 붙은 이 인터페이스의 Play()만 끝까지 기다렸다가 Opened로 바꾼다.
// 그래서 나중에 KeyInsertOpenSequence(열쇠 생성 → 열쇠구멍 삽입 → 회전 + 딸깍 → 문 열림)를 만들 때
// LockedSafe를 고치지 않고 SimpleDoorOpenSequence 컴포넌트만 빼고 새 컴포넌트를 붙이면 된다.
// 이 컴포넌트가 아예 없으면 LockedSafe는 연출 없이 즉시 열린다.
public interface ISafeOpenSequence
{
    // 열림 연출 코루틴. key는 금고를 연 열쇠 (열쇠 모델을 꺼내 보여주는 연출에서 KeyData.worldModelPrefab을 쓰기 위함).
    IEnumerator Play(KeyData key);
}
