using System.Collections.Generic;

namespace Gim002
{
    // 판정에 필요한 장면 정보만 뽑은 순수 C# 클래스.
    // ScriptableObject(Gim002SceneData)를 직접 쓰지 않는 이유: 판정 로직을 Unity 에셋 없이 테스트에서 바로 만들 수 있게 하기 위해서다.
    public class Gim002SceneRule
    {
        public readonly DollId Doll;
        public readonly BackgroundId Background;
        public readonly IReadOnlyList<SymbolId> Symbols;

        public Gim002SceneRule(DollId doll, BackgroundId background, IReadOnlyList<SymbolId> symbols)
        {
            Doll = doll;
            Background = background;
            Symbols = symbols ?? new SymbolId[0];
        }

        // 무대 판정: 인형과 배경이 둘 다 이 장면의 정답인가
        public bool IsStageCorrect(DollId doll, BackgroundId background)
        {
            return doll == Doll && background == Background;
        }

        // 기획 "한 장 요약" 표의 기본 3장면 (가안). 테스트와 에셋 자동 생성에서 같은 값을 쓰기 위해 한곳에 둔다.
        public static Gim002SceneRule[] Defaults()
        {
            return new[]
            {
                new Gim002SceneRule(DollId.OwlScope, BackgroundId.NightForest, new[] { SymbolId.Sun, SymbolId.Moon, SymbolId.Star }),
                new Gim002SceneRule(DollId.FrogCan, BackgroundId.Greenhouse, new[] { SymbolId.Cloud, SymbolId.Sun, SymbolId.Flower }),
                new Gim002SceneRule(DollId.CatViolin, BackgroundId.MusicRoom, new[] { SymbolId.Star, SymbolId.Flower, SymbolId.Moon, SymbolId.Sun }),
            };
        }
    }
}
