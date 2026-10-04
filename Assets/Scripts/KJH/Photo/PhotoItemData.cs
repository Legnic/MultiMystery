using UnityEngine;

// 사진 한 종류(=촬영 지점 하나에 대응)의 데이터를 담는 ScriptableObject.
// 코드가 아니라 "에셋 파일"이라서, 디자인팀이 스크립트를 몰라도 Project 창에서
// 우클릭 > Create > MultiMystery > Photo > PhotoItemData 로 새 사진 아이템을 추가하고
// 이미지/제목/설명만 채워 넣을 수 있다.
[CreateAssetMenu(fileName = "PhotoItem_", menuName = "MultiMystery/Photo/PhotoItemData")]
public class PhotoItemData : ScriptableObject
{
    [Tooltip("이 사진을 구분하는 고유 ID. 저장/불러오기나 나중의 네트워크 동기화 때 이 문자열만 주고받는다. " +
             "씬 안의 다른 사진과 겹치지 않아야 한다.")]
    public string photoId;

    [Tooltip("인벤토리 확대보기에 표시될 제목.")]
    public string title;

    [TextArea(2, 5)]
    [Tooltip("인벤토리 확대보기에 표시될 짧은 설명.")]
    public string description;

    [Tooltip("실제로 보여줄 사진 이미지.")]
    public Sprite image;
}
