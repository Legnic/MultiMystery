using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gim002
{
    // 1층 피아노 건반 하나 (OBJ_PIANO의 흰 건반). 바라보고 E → 음이 울리고, 기호 스티커가 있는 건반이면 판정 담당에게 입력을 보낸다.
    //  - 소리는 어떤 상태에서도 항상 난다.
    //  - 기록 여부(반주판이 열렸을 때만, 오답 연출 중 제외)는 판정 담당(Gim002Logic)이 정한다. 그래서 여기서는 그냥 보낸다.
    //  - 기호가 없는 건반은 소리만 나고 아무것도 보내지 않는다.
    public class Gim002PianoKey : MonoBehaviour, IWorldInteractable
    {
        [Tooltip("기호 스티커가 붙은 건반인지")]
        [SerializeField] bool hasSymbol = true;
        [Tooltip("붙은 기호")]
        [SerializeField] SymbolId symbol;
        [Tooltip("건반 소리. 비우면 아래 주파수로 만든 임시 음을 낸다")]
        [SerializeField] AudioClip note;
        [Tooltip("임시 음 주파수(Hz). 도=261.6, 레=293.7, 미=329.6, 파=349.2, 솔=392, 라=440, 시=493.9")]
        [SerializeField] float toneFrequency = 261.6f;
        [Tooltip("눌릴 때 내려가는 부분 (비우면 이 오브젝트)")]
        [SerializeField] Transform keyVisual;
        [Tooltip("눌림 깊이 (m)")]
        [SerializeField] float pressDepth = 0.012f;

        static readonly List<Gim002PianoKey> all = new List<Gim002PianoKey>();
        AudioSource source;
        Vector3 restPos;
        Coroutine pressing;

        public bool HasSymbol { get { return hasSymbol; } }
        public SymbolId Symbol { get { return symbol; } }
        public AudioClip NoteClip { get { return note != null ? note : Gim002Tones.Note(toneFrequency); } }

        // 2층 오르골 등에서 같은 기호의 음을 찾을 때 사용
        public static Gim002PianoKey FindBySymbol(SymbolId s)
        {
            foreach (var k in all) if (k != null && k.hasSymbol && k.symbol == s) return k;
            return null;
        }

        void Awake()
        {
            if (keyVisual == null) keyVisual = transform;
            restPos = keyVisual.localPosition;
            source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;   // 3D 소리: 피아노 근처에서만 들림
        }

        void OnEnable() { all.Add(this); }
        void OnDisable() { all.Remove(this); }

        public bool CanInteract { get { return true; } }

        public string InteractPrompt
        {
            get
            {
                var c = Gim002Controller.Instance;
                if (!hasSymbol || c == null) return "[E] 건반 누르기";
                return "[E] 건반 누르기  (" + c.Config.SymbolName(symbol) + ")";
            }
        }

        public void Interact(GameObject interactor)
        {
            // 1) 소리는 항상
            source.PlayOneShot(NoteClip);
            if (pressing != null) StopCoroutine(pressing);
            pressing = StartCoroutine(PressAnim());

            // 2) 기호 건반이면 판정 담당에게 입력 전달 (기록할지는 그쪽이 결정)
            var c = Gim002Controller.Instance;
            if (hasSymbol && c != null && c.Net != null) c.Net.RequestPianoKey(symbol);
        }

        IEnumerator PressAnim()
        {
            keyVisual.localPosition = restPos + Vector3.down * pressDepth;
            yield return new WaitForSeconds(0.15f);
            keyVisual.localPosition = restPos;
            pressing = null;
        }
    }
}
