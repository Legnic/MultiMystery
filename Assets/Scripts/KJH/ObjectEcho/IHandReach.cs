using System;
using UnityEngine;

// "손을 어떤 지점으로 뻗고, 다시 거두는" 동작만 담당하는 인터페이스.
// ObjectEchoController(연출 제어)는 이 인터페이스만 알고, 손이 실제로 어떻게 움직이는지는 모른다.
// 그래서 지금의 임시 손(PlaceholderHandReach)을 나중에 리깅된 캐릭터의 팔(IK)로 바꿔도
// 연출 코드는 한 줄도 고치지 않고 Inspector에서 연결만 바꾸면 된다.
//
// ─────────────────────────────────────────────────────────────────────────────
// [나중에 할 일] 리깅 캐릭터용 구현: TwoBoneIKHandReach (지금은 만들지 않음)
// ─────────────────────────────────────────────────────────────────────────────
// 1) 패키지 설치: Window > Package Manager > Unity Registry > "Animation Rigging" 설치.
// 2) 캐릭터 루트(Animator가 있는 오브젝트)에 "Rig Builder" 컴포넌트를 붙이고,
//    자식으로 빈 오브젝트 "HandRig"를 만들어 "Rig" 컴포넌트를 붙인 뒤 Rig Builder의 Layers에 등록한다.
// 3) HandRig 아래에 "RightHandIK" 오브젝트를 만들고 "Two Bone IK Constraint"를 붙인다.
//    - Root = 오른쪽 위팔(UpperArm), Mid = 아래팔(LowerArm), Tip = 손(Hand) 본
//    - Target = 빈 오브젝트 "RightHandIK_Target" (손이 따라갈 목표)
//    - Hint   = 빈 오브젝트 "RightHandIK_Hint" (팔꿈치가 향할 방향, 몸 바깥쪽 뒤에 둔다)
//    - 오른쪽 클릭 메뉴의 "Auto Setup from Tip Transform"을 쓰면 본 연결이 자동으로 된다.
// 4) TwoBoneIKHandReach : MonoBehaviour, IHandReach 를 만든다.
//    - ReachTo(target, duration, onArrived):
//        · RightHandIK_Target을 "지금 손 본의 위치/회전"에서 시작해 target으로 곡선 이동시킨다
//          (PlaceholderHandReach의 베지어 곡선 계산을 그대로 가져다 쓰면 된다).
//        · 동시에 TwoBoneIKConstraint.weight를 0 → 1로 올린다 (애니메이션 자세 → IK 자세로 부드럽게 전환).
//        · 다 도착하면 onArrived 호출.
//    - Retract(duration, onRetracted):
//        · weight를 1 → 0으로 내린다. 그러면 손이 원래 애니메이션 자세(대기 모션)로 자연스럽게 돌아간다.
//        · 다 내려가면 onRetracted 호출.
//    - 손바닥이 사물 면에 붙어 보이게 하려면 HandTarget의 회전(파란 Z축 = 손끝 방향, 초록 Y축 = 손등 방향)을
//      Target의 회전으로 그대로 쓰고, 필요하면 Inspector에 "회전 보정값(Vector3)"을 하나 둔다.
// 5) Player의 ObjectEchoController > "Hand Reach" 칸에 PlaceholderHandReach 대신 TwoBoneIKHandReach를 연결하고,
//    카메라 아래의 PlaceholderHand 오브젝트는 지운다. (연출 코드는 수정할 필요 없음)
// ─────────────────────────────────────────────────────────────────────────────
public interface IHandReach
{
    // 손이 지금 움직이는 중인지 (뻗는 중 / 거두는 중). 디버그나 중복 호출 방지용.
    bool IsMoving { get; }

    // 손이 원래 자리(대기 자세)에서 벗어나 있는지. 연출이 중단됐을 때 "거두기가 필요한가"를 판단하는 데 쓴다.
    bool IsExtended { get; }

    // 손을 target 위치/회전으로 duration초 동안 뻗는다. 도착하면 onArrived를 한 번 호출한다.
    // 이미 움직이는 중이었다면 "지금 위치"에서 새 목표로 다시 출발한다 (순간이동하지 않도록).
    void ReachTo(Transform target, float duration, Action onArrived);

    // 손을 원래 자리로 duration초 동안 거둔다. 다 돌아오면 onRetracted를 한 번 호출한다.
    // 손이 어디에 있든(뻗는 도중이라도) 지금 위치에서 출발한다.
    void Retract(float duration, Action onRetracted);

    // 연출 오브젝트가 꺼지는 등 비상 상황에서 애니메이션 없이 즉시 원래 자리로 되돌린다.
    void SnapToRest();
}
