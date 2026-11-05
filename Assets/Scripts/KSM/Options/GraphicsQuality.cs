using UnityEngine;

namespace CoopDemo
{
    // 그래픽 품질 단계 (낮음 / 높음 / 자동). 단계마다 URP 에셋과 렌더러가 따로 있다.
    //  - 높음 = Quality 레벨 "PC"  (PC_RPAsset + PC_Renderer): 그림자 거리 50m·4단계·2048·부드러운 그림자, SSAO 켬
    //  - 낮음 = Quality 레벨 "Low" (Low_RPAsset + Low_Renderer): 그림자 거리 25m·2단계·1024·딱딱한 그림자, SSAO 끔, 불투명 텍스처 복사 끔
    //  - 자동 = 이 PC의 그래픽 메모리·시스템 메모리를 보고 고른다 (목표 최소 사양 GTX 1650급 4GB / RAM 8GB는 "낮음").
    // 저장 값은 PlayerOptionsData.graphicsQuality (-1 자동, 0 낮음, 1 높음). 적용은 OptionsManager가 한다.
    public static class GraphicsQuality
    {
        public const int Auto = -1;
        public const int Low = 0;
        public const int High = 1;

        const string LowLevel = "Low";
        const string HighLevel = "PC";

        // 자동 판정 기준(MB): 둘 다 넉넉할 때만 높음
        const int HighMinVideoMemory = 6000;
        const int HighMinSystemMemory = 12000;

        static int appliedTier = -2;

        // 지금 적용된 단계 (아직 아무것도 적용 안 했으면 -2)
        public static int AppliedTier { get { return appliedTier; } }

        public static int AutoTier()
        {
            bool strong = SystemInfo.graphicsMemorySize >= HighMinVideoMemory && SystemInfo.systemMemorySize >= HighMinSystemMemory;
            return strong ? High : Low;
        }

        public static int Resolve(int setting)
        {
            return setting == Low || setting == High ? setting : AutoTier();
        }

        public static string Label(int setting)
        {
            if (setting == Low) return "낮음";
            if (setting == High) return "높음";
            return AutoTier() == Low ? "자동 (낮음)" : "자동 (높음)";
        }

        // 같은 단계를 연달아 부르면 아무것도 안 한다 (옵션 슬라이더를 움직일 때마다 Apply가 불리므로)
        public static void Apply(int tier)
        {
            if (tier == appliedTier) return;
            int index = FindLevel(tier == Low ? LowLevel : HighLevel);
            if (index < 0)
            {
                Debug.LogWarning("[GraphicsQuality] Quality 레벨을 찾지 못했습니다: " + (tier == Low ? LowLevel : HighLevel));
                return;
            }
            appliedTier = tier;
            if (QualitySettings.GetQualityLevel() != index) QualitySettings.SetQualityLevel(index, true);
        }

        static int FindLevel(string name)
        {
            var names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++) if (names[i] == name) return i;
            return -1;
        }
    }
}
