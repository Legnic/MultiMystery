using System.Collections.Generic;
using UnityEngine;

namespace Gim002
{
    // GIM-002 전체 설정 하나. 장면 데이터 목록 + 시간 수치 + 화면 문구를 한곳에 모았다.
    // 씬의 Gim002Controller가 이 에셋 하나만 참조하면 된다.
    [CreateAssetMenu(menuName = "GIM-002/Config", fileName = "Gim002Config")]
    public class Gim002Config : ScriptableObject
    {
        [Tooltip("장면 순서대로 (기획상 3개). 순서가 곧 장면 번호(1, 2, 3)다.")]
        public List<Gim002SceneData> scenes = new List<Gim002SceneData>();

        [Tooltip("모든 시간 수치 (가안)")]
        public Gim002Timing timing = new Gim002Timing();

        [Header("놀이책")]
        [Tooltip("놀이책 첫 장(안내) 문구. 줄바꿈은 그대로 화면에 나온다.")]
        [TextArea(3, 6)]
        public string bookIntro = "한 사람은 반주를, 한 사람은 무대를.\n이야기를 들려주고 친구들의 공연을 완성해 보렴.\n반주는 무대가 알려 줄 거란다. 건반의 그림을 따라 치렴.";

        [Header("이름 (enum 순서대로)")]
        [Tooltip("DollId 순서: OwlScope, OwlBook, FrogCan, CatViolin")]
        public string[] dollNames = { "망원경 부엉이", "책 부엉이", "물뿌리개 개구리", "바이올린 고양이" };
        [Tooltip("BackgroundId 순서: NightForest, Greenhouse, MusicRoom, Sea, Castle")]
        public string[] backgroundNames = { "밤숲 카드", "온실 카드", "음악실 카드", "바다 카드", "성 카드" };
        [Tooltip("SymbolId 순서: Sun, Cloud, Moon, Flower, Star — 반주판·건반 스티커에 표시")]
        public string[] symbolNames = { "해", "구름", "달", "꽃", "별" };

        [Header("화면 문구")]
        public string msgCurtainClosed = "커튼이 닫혀 있다";
        public string msgNotReady = "손잡이가 꿈쩍도 하지 않는다";
        public string msgSlotLocked = "단단히 고정되어 빠지지 않는다";
        public string msgWrongSlotDoll = "인형은 무대 바닥의 꽂이에 세워야 한다";
        public string msgWrongSlotBg = "배경카드는 무대 뒤쪽 슬롯에 꽂아야 한다";
        public string msgWrongKey = "문양이 맞지 않는다";
        public string msgComplete = "인형극이 끝났다. 어디선가 박수 소리가 들린 것 같다";

        public string DollName(DollId d) { int i = (int)d; return i < dollNames.Length ? dollNames[i] : d.ToString(); }
        public string BackgroundName(BackgroundId b) { int i = (int)b; return i < backgroundNames.Length ? backgroundNames[i] : b.ToString(); }
        public string SymbolName(SymbolId s) { int i = (int)s; return i < symbolNames.Length ? symbolNames[i] : s.ToString(); }

        // 판정 로직이 쓰는 형태(Gim002SceneRule 배열)로 바꿔 준다. 비어 있는 칸은 건너뛴다.
        public Gim002SceneRule[] BuildRules()
        {
            var list = new List<Gim002SceneRule>();
            foreach (var s in scenes)
                if (s != null) list.Add(new Gim002SceneRule(s.doll, s.background, s.symbols));
            return list.ToArray();
        }

        // 장면 번호(1부터)로 데이터를 찾는다. 범위를 벗어나면 null.
        public Gim002SceneData GetScene(int sceneIndex)
        {
            int i = sceneIndex - 1;
            return i >= 0 && i < scenes.Count ? scenes[i] : null;
        }
    }
}
