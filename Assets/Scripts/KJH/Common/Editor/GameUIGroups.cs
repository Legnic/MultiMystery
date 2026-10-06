using UnityEditor;
using UnityEngine;

// 게임 화면 UI Canvas(GameUICanvas) 아래의 "기능별 묶음"을 찾거나 만들어 주는 에디터 도우미.
// 각 기능의 Setup 메뉴가 새 UI를 만들 때 Canvas 바로 아래가 아니라 자기 기능 묶음 안에 넣도록 할 때 쓴다.
// Editor 폴더 안이라 게임 빌드에는 포함되지 않는다.
//
// 구조 (위에서 아래 순서 = 먼저 그려짐 → 나중에 그려진 것이 위에 보임):
//   GameUICanvas
//     Photo      : 뷰파인더, 플래시, 사진 인벤토리, 사진 크게 보기
//     Clue       : 단서 살펴보기 화면
//     ObjectEcho : 화면 암전 덮개, 소리 파형
//     Common     : 상호작용 안내 문구, 획득 알림 (여러 기능이 같이 씀 → 항상 맨 위에 보이도록 마지막)
public static class GameUIGroups
{
    public const string CanvasName = "GameUICanvas";
    public const string Photo = "Photo";
    public const string Clue = "Clue";
    public const string ObjectEcho = "ObjectEcho";
    public const string Common = "Common";

    // 그리는 순서. 새 묶음을 만들 때 이 순서에 맞는 자리에 끼워 넣는다.
    private static readonly string[] Order = { Photo, Clue, ObjectEcho, Common };

    // canvas 아래에서 groupName 묶음을 찾고, 없으면 화면 전체 크기의 빈 RectTransform으로 만든다.
    public static Transform GetOrCreate(Canvas canvas, string groupName)
    {
        if (canvas == null) return null;
        Transform existing = canvas.transform.Find(groupName);
        if (existing != null) return existing;

        GameObject go = new GameObject(groupName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create UI Group " + groupName);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvas.transform, false);
        // 화면 전체를 채워야 안에 넣은 UI의 앵커/위치가 Canvas 바로 아래에 있을 때와 똑같이 동작한다.
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        // 정해진 순서에서 자기보다 뒤에 와야 할 묶음이 이미 있으면 그 바로 앞에 둔다.
        int myIndex = System.Array.IndexOf(Order, groupName);
        for (int i = myIndex + 1; myIndex >= 0 && i < Order.Length; i++)
        {
            Transform after = canvas.transform.Find(Order[i]);
            if (after == null) continue;
            rt.SetSiblingIndex(after.GetSiblingIndex());
            break;
        }
        return rt;
    }
}
