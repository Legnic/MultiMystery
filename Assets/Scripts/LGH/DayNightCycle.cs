using UnityEngine;

namespace LGH
{
    // Simple day/night cycle: 1 minute (default) covers dawn -> noon -> dusk -> night -> dawn.
    // The window wall (+Z) is treated as the 'south' face, so the sun arcs from East(+X) through
    // South(+Z, noon, strongest beam through the window) to West(-X) during the day half of the cycle,
    // and dips below the horizon (elevation goes negative) during the night half.
    // Play-mode-only helper script for visual testing.
    public class DayNightCycle : MonoBehaviour
    {
        [Header("Targets")]
        public Light sunLight;
        public Renderer windowGlassRenderer;

        [Header("Timing")]
        public float dayLengthSeconds = 60f;
        [Range(0f, 1f)] public float startTime01 = 0f;

        [Header("Sun path (south-facing window)")]
        public float maxElevation = 52f;
        public float azimuthOffset = 0f;

        [HideInInspector] public float t01;

        Gradient sunColor;
        AnimationCurve sunIntensity;
        Gradient ambientColor;
        Gradient fogColorGradient;

        void Awake()
        {
            t01 = startTime01;
            if (sunLight == null)
            {
                var go = GameObject.Find("Directional Light");
                if (go != null) sunLight = go.GetComponent<Light>();
            }
            BuildGradients();
        }

        void BuildGradients()
        {
            sunColor = new Gradient();
            sunColor.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1.00f, 0.75f, 0.55f), 0.00f), // dawn (east)
                    new GradientColorKey(new Color(1.00f, 0.85f, 0.65f), 0.10f), // morning
                    new GradientColorKey(new Color(1.00f, 0.96f, 0.88f), 0.25f), // noon (south, peak)
                    new GradientColorKey(new Color(1.00f, 0.85f, 0.65f), 0.40f), // afternoon
                    new GradientColorKey(new Color(1.00f, 0.45f, 0.25f), 0.50f), // sunset (west)
                    new GradientColorKey(new Color(0.30f, 0.22f, 0.35f), 0.62f), // twilight
                    new GradientColorKey(new Color(0.12f, 0.15f, 0.30f), 0.75f), // night
                    new GradientColorKey(new Color(0.06f, 0.07f, 0.16f), 0.90f)  // deep night
                },
                new GradientAlphaKey[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) }
            );

            ambientColor = new Gradient();
            ambientColor.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.18f, 0.14f, 0.20f), 0.00f),
                    new GradientColorKey(new Color(0.28f, 0.26f, 0.28f), 0.12f),
                    new GradientColorKey(new Color(0.40f, 0.42f, 0.46f), 0.25f),
                    new GradientColorKey(new Color(0.32f, 0.28f, 0.28f), 0.40f),
                    new GradientColorKey(new Color(0.26f, 0.17f, 0.19f), 0.50f),
                    new GradientColorKey(new Color(0.10f, 0.09f, 0.16f), 0.62f),
                    new GradientColorKey(new Color(0.035f, 0.035f, 0.06f), 0.75f),
                    new GradientColorKey(new Color(0.02f, 0.02f, 0.04f), 0.90f)
                },
                new GradientAlphaKey[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) }
            );

            fogColorGradient = ambientColor;

            sunIntensity = new AnimationCurve(
                new Keyframe(0.00f, 0.5f),
                new Keyframe(0.10f, 1.3f),
                new Keyframe(0.25f, 2.8f),
                new Keyframe(0.40f, 1.6f),
                new Keyframe(0.50f, 0.7f),
                new Keyframe(0.60f, 0.10f),
                new Keyframe(0.68f, 0.0f),
                new Keyframe(0.90f, 0.0f),
                new Keyframe(0.97f, 0.15f),
                new Keyframe(1.00f, 0.5f)
            );
        }

        void Update()
        {
            if (dayLengthSeconds <= 0f) return;
            t01 += Time.deltaTime / dayLengthSeconds;
            if (t01 > 1f) t01 -= 1f;

            // Azimuth sweeps East(90) -> South(0, noon) -> West(-90) -> ... ; elevation follows one sine cycle (up in day, below horizon at night).
            float azimuth = 90f - t01 * 360f + azimuthOffset;
            float elevation = maxElevation * Mathf.Sin(t01 * 2f * Mathf.PI);
            float azRad = azimuth * Mathf.Deg2Rad;
            float elRad = elevation * Mathf.Deg2Rad;

            // Direction light TRAVELS (opposite of where the sun sits). Through the +Z (south) window,
            // light enters the room whenever this vector's Z component is negative (i.e. heading from +Z to -Z).
            Vector3 travelDir = new Vector3(
                -Mathf.Sin(azRad) * Mathf.Cos(elRad),
                -Mathf.Sin(elRad),
                -Mathf.Cos(azRad) * Mathf.Cos(elRad)
            );

            float intensityVal = sunIntensity.Evaluate(t01);
            bool sunUp = intensityVal > 0.015f;

            if (sunLight != null)
            {
                sunLight.enabled = sunUp;
                if (travelDir.sqrMagnitude > 0.0001f)
                    sunLight.transform.rotation = Quaternion.LookRotation(travelDir, Vector3.up);
                sunLight.color = sunColor.Evaluate(t01);
                sunLight.intensity = intensityVal;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor.Evaluate(t01);
            RenderSettings.fogColor = fogColorGradient.Evaluate(t01);

            if (windowGlassRenderer != null)
            {
                Color glow = sunColor.Evaluate(t01) * Mathf.Lerp(0.08f, 0.55f, Mathf.Clamp01(intensityVal / 2.8f));
                windowGlassRenderer.material.SetColor("_EmissionColor", glow);
            }
        }

        public string CurrentPhaseName()
        {
            if (t01 < 0.125f || t01 >= 0.97f) return "새벽/아침";
            if (t01 < 0.375f) return "낮(정오)";
            if (t01 < 0.60f) return "저녁";
            return "밤";
        }

        void OnGUI()
        {
            GUI.color = Color.white;
            GUI.Label(new Rect(12, 12, 400, 24), CurrentPhaseName() + "  -  " + Mathf.RoundToInt(t01 * 100f) + "%");
        }
    }
}
