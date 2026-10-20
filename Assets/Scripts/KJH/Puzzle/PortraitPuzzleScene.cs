using UnityEngine;

// 갤러리 액자 퍼즐의 "씬 쪽 설정". 액자들/떨어질 액자/열쇠 상자처럼 씬 오브젝트 참조만 들고 있고 네트워크와는 무관하다.
// 네트워크 동기화를 맡는 PortraitPuzzle은 프리팹(PortraitPuzzleSync)으로 등록되어야 클라이언트에도 생기는데(씬 관리가 꺼져 있어서
// 등록되지 않은 씬 NetworkObject는 클라이언트에서 사라진다), 프리팹은 씬 오브젝트를 참조할 수 없으므로 참조는 이 컴포넌트가 들고 있고
// PortraitPuzzle이 런타임에 Instance로 찾아 쓴다.
public class PortraitPuzzleScene : MonoBehaviour
{
    public static PortraitPuzzleScene Instance { get; private set; }

    [Tooltip("기울일 수 있는 액자들. 앞에서부터 Answer 개수만큼이 정답 판정 대상이고, 그 뒤는 정답과 무관한 장식 액자(상태만 동기화)")]
    public PortraitTilt[] portraits;
    [Tooltip("각 액자의 정답 단계: -1 = 왼쪽 30도, +1 = 오른쪽 30도")]
    public int[] answer = { 1, 1, -1 };
    [Tooltip("정답일 때 떨어지는 액자")]
    public Transform fallingFrame;
    public float dropDelay = 0.7f;
    [Tooltip("정답일 때 액자와 동시에 뚜껑이 열리는 열쇠 상자")]
    public KeyBoxLid keyBox;

    void Awake() { Instance = this; }

    void OnDestroy() { if (Instance == this) Instance = null; }
}
