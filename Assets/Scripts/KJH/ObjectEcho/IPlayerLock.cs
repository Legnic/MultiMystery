using UnityEngine;

// ObjectEchoController가 "플레이어의 이동/시점을 잠그고, 카메라를 특정 방향으로 돌리는" 데 필요한
// 최소한의 기능만 뽑은 인터페이스. 싱글플레이용 PlayerMovement뿐 아니라, 네트워크 플레이어(예:
// NetworkFirstPersonController)처럼 전혀 다른 이동 스크립트도 이 인터페이스만 구현하면
// ObjectEchoController를 그대로 붙일 수 있다 (IHandReach와 같은 이유로 분리).
public interface IPlayerLock
{
    // 현재 좌우(yaw) / 위아래(pitch) 시점 각도 (도). 카메라 회전 연출이 "지금 어디를 보고 있는지" 읽을 때 쓴다.
    float Yaw { get; }
    float Pitch { get; }

    // 1 = 평소 속도, 0 = 이동 정지. 연출 중 이동을 멈추는 데 쓴다.
    float SpeedMultiplier { get; set; }

    // false면 시점 회전(마우스 입력)을 멈춘다. 연출이 카메라를 대신 돌리는 동안 꺼둔다.
    bool LookEnabled { get; set; }

    // 시점을 yaw/pitch로 즉시 맞춘다. 내부 상태(다음 프레임 계산에 쓰는 값)까지 함께 갱신해야
    // LookEnabled가 다시 켜졌을 때 이 방향을 기준으로 이어서 움직인다.
    void SetLookAngles(float yaw, float pitch);
}
