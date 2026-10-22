using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopDemo
{
    // 임시 디버그용. 프로젝트에 아직 "탈출 성공" 기믹이 없어서, 엔딩/결과창 흐름을 테스트할 수 있도록
    // 비밀 커맨드(키보드로 ESCAPE 순서 입력)를 치면 GameFlowSync.RequestEscapeSuccess()를 대신 호출해 준다.
    // 실제 최종 기믹이 생기면 그 기믹 스크립트에서 GameFlowSync.Instance.RequestEscapeSuccess()를 직접 부르고,
    // 이 컴포넌트는(혹은 최소한 이 트리거는) 지워도 된다.
    public class DebugEscapeTrigger : MonoBehaviour
    {
        static readonly Key[] Sequence = { Key.E, Key.S, Key.C, Key.A, Key.P, Key.E };

        [Tooltip("마지막 입력 이후 이만큼(초) 지나면 입력 진행 상황을 초기화한다.")]
        public float resetAfterSeconds = 2f;

        int progress;
        float lastKeyTime = -999f;

        void Update()
        {
            var sync = GameFlowSync.Instance;
            if (sync == null || !sync.IsSpawned || sync.Stage != GameStage.Playing) { progress = 0; return; }

            var kb = Keyboard.current;
            if (kb == null) return;

            if (progress > 0 && Time.unscaledTime - lastKeyTime > resetAfterSeconds) progress = 0;

            Key expected = Sequence[progress];
            if (kb[expected].wasPressedThisFrame)
            {
                progress++;
                lastKeyTime = Time.unscaledTime;
                if (progress >= Sequence.Length)
                {
                    progress = 0;
                    Debug.Log("[DebugEscapeTrigger] 비밀 커맨드 입력됨 — 탈출 성공 요청(테스트용)");
                    sync.RequestEscapeSuccess();
                }
                return;
            }

            // 시퀀스와 다른 키를 눌렀으면 처음부터 — 단, 그 키 자체가 시퀀스의 첫 글자면 거기서부터 다시 시작한다.
            foreach (var k in Sequence)
            {
                if (!kb[k].wasPressedThisFrame) continue;
                progress = (k == Sequence[0]) ? 1 : 0;
                lastKeyTime = Time.unscaledTime;
                break;
            }
        }
    }
}
