using UnityEngine;

namespace CoopDemo
{
    // 야외 가로등의 불빛을 아주 약하게 일렁이게 한다 (로컬 연출, 네트워크 동기화 없음).
    // 점광원 세기와 등갓(Head)의 발광을 같은 비율로 흔든다.
    public class LanternFlicker : MonoBehaviour
    {
        public Light lanternLight;
        public Renderer head;
        [Tooltip("세기가 흔들리는 폭 (0.08 = ±8%)")] public float amount = 0.08f;
        public float speed = 1.6f;

        float baseIntensity;
        Color baseEmission;
        float seed;
        MaterialPropertyBlock block;
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            seed = Random.value * 100f;
            if (lanternLight != null) baseIntensity = lanternLight.intensity;
            if (head != null && head.sharedMaterial != null && head.sharedMaterial.HasProperty(Emission))
                baseEmission = head.sharedMaterial.GetColor(Emission);
            block = new MaterialPropertyBlock();
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(Time.time * speed + seed, seed) * 2f - 1f;          // 느린 일렁임
            n += (Mathf.PerlinNoise(Time.time * speed * 4.3f + seed, 7.7f) * 2f - 1f) * 0.35f;   // 잔떨림
            float k = 1f + n * amount;
            if (lanternLight != null) lanternLight.intensity = baseIntensity * k;
            if (head != null)
            {
                head.GetPropertyBlock(block);
                block.SetColor(Emission, baseEmission * k);
                head.SetPropertyBlock(block);
            }
        }
    }
}
