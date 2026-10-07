using UnityEngine;

// "사진을 찍을 수 있는 대상"이 공통으로 구현하는 인터페이스. PhotoCaptureSystem은 이 인터페이스만 알면 되므로,
// 정해진 지점(PhotoSpot)이든 손에 들고 살펴보는 단서(InspectPhotoCapture)든 같은 촬영 흐름(E 촬영 / Esc 취소)을 쓴다.
public interface IPhotoSubject
{
    // 촬영 모드에서 카메라가 이동해 고정될 위치/각도. null이면 카메라를 옮기지 않고 지금 보고 있는 시점 그대로 촬영한다
    // (예: 단서를 눈앞에 들고 있는 상태에서 촬영).
    Transform CameraAnchor { get; }

    // 촬영했을 때 지급할 사진 데이터.
    PhotoItemData PhotoItem { get; }

    // 촬영 모드를 나온 뒤 플레이어 이동/시점 조작을 돌려줄지. 살펴보기처럼 다른 상호작용이 아직 진행 중이라
    // 조작을 계속 막아야 하면 false로 둔다 (조작은 그 상호작용이 끝날 때 돌려준다).
    bool RestorePlayerControlOnExit { get; }

    // 촬영이 확정됐을 때 호출된다 (다시 못 찍게 표시하는 등).
    void OnPhotoCaptured();

    // 촬영 모드에서 Esc로 취소했을 때 호출된다.
    void OnPhotoCancelled();
}
