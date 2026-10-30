using UnityEngine;

namespace CoopDemo
{
    // 밤하늘 셰이더(KSM/NightSky)에 달 방향을 알려 준다. 달빛 방향광이 비추는 쪽의 반대 방향(= 하늘에서 달이 보이는 쪽)에 달을 그린다.
    // 에디터에서도 Scene 뷰에 보이도록 ExecuteAlways.
    [ExecuteAlways]
    public class NightSkyMoon : MonoBehaviour
    {
        public Light moonLight;
        static readonly int MoonDir = Shader.PropertyToID("_NightMoonDir");

        void OnEnable() { Apply(); }
        void Update() { Apply(); }

        void Apply()
        {
            if (moonLight == null) return;
            Shader.SetGlobalVector(MoonDir, -moonLight.transform.forward);
        }
    }
}
