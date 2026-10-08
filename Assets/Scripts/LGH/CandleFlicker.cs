using UnityEngine;

namespace LGH
{
    // Makes a candle light (and its flame mesh) flicker a little.
    public class CandleFlicker : MonoBehaviour
    {
        public Light candleLight;
        public Transform flame;
        public float baseIntensity = 2.2f;
        public float intensityJitter = 0.45f;
        public float baseRange = 7f;
        public float speed = 7f;

        Vector3 flameScale, lightPos;
        float seed;

        void Awake()
        {
            if (candleLight == null) candleLight = GetComponentInChildren<Light>();
            if (flame != null) flameScale = flame.localScale;
            if (candleLight != null) lightPos = candleLight.transform.localPosition;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float t = Time.time * speed + seed;
            float n = Mathf.PerlinNoise(t, seed) * 2f - 1f;
            float n2 = Mathf.PerlinNoise(seed, t * 1.7f) * 2f - 1f;
            if (candleLight != null)
            {
                candleLight.intensity = baseIntensity + n * intensityJitter;
                candleLight.range = baseRange + n2 * 0.4f;
                candleLight.transform.localPosition = lightPos + new Vector3(n2, 0f, n) * 0.01f;
            }
            if (flame != null)
                flame.localScale = new Vector3(flameScale.x * (1f + n2 * 0.12f), flameScale.y * (1f + n * 0.2f), flameScale.z * (1f + n2 * 0.12f));
        }
    }
}
