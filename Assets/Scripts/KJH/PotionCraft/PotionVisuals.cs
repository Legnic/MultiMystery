using System.Collections;
using UnityEngine;

namespace PotionCraft
{
    // 임시 도형용 색칠/연출 도우미 (머티리얼을 복제하지 않고 MaterialPropertyBlock으로 색만 바꾼다).
    public static class PotionVisuals
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Color_ = Shader.PropertyToID("_Color");

        public static void Tint(Renderer r, Color c)
        {
            if (r == null) return;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColor, c);
            mpb.SetColor(Color_, c);
            r.SetPropertyBlock(mpb);
        }

        // 이름이 "Tint"로 시작하는 하위 렌더러만 색을 입힌다 (프리팹 안에서 색이 바뀌는 부분 표시용).
        public static void TintMarked(GameObject root, Color c)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("Tint")) Tint(r, c);
        }

        // root 밑에서 "Dish_<색이름>"(예: Dish_Red) 자식을 켜고 나머지는 끈다. 그런 색 전용 모델이 없으면
        // fallbackName(기본 "Tint_Pile") 자식을 켜고 그걸 색칠한다 — 아직 모델이 없는 색(Gold 등)의 임시 대체.
        public static void ShowPowderDish(GameObject root, PowderColor color, Color fallbackTint, string fallbackName = "Tint_Pile")
        {
            if (root == null) return;
            string dishName = "Dish_" + color;
            Transform dish = null, fallback = null;
            foreach (Transform t in root.transform)
            {
                if (t.name == dishName) dish = t;
                else if (t.name == fallbackName) fallback = t;
                t.gameObject.SetActive(false);
            }
            if (dish != null) dish.gameObject.SetActive(true);
            else if (fallback != null)
            {
                fallback.gameObject.SetActive(true);
                Tint(fallback.GetComponent<Renderer>(), fallbackTint);
            }
        }

        public static void StripColliders(GameObject root)
        {
            foreach (var col in root.GetComponentsInChildren<Collider>(true)) Object.Destroy(col);
        }

        public static void Burst(Vector3 pos, Color color, int count, float speed, float size, Material mat, float gravity = 0.3f)
        {
            var go = new GameObject("PotionFx");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = 1.0f;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.gravityModifier = gravity;
            main.playOnAwake = false;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            if (mat != null) rend.sharedMaterial = mat;
            ps.Play();
            Object.Destroy(go, 2.5f);
        }

        public static void Flash(Vector3 pos, Color color, float intensity, float seconds)
        {
            var go = new GameObject("PotionFlash");
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = 5f;
            l.intensity = intensity;
            go.AddComponent<FadeLight>().seconds = seconds;
        }

        class FadeLight : MonoBehaviour
        {
            public float seconds = 1f;
            IEnumerator Start()
            {
                var l = GetComponent<Light>();
                float start = l.intensity;
                for (float t = 0f; t < seconds; t += Time.deltaTime)
                {
                    l.intensity = Mathf.Lerp(start, 0f, t / seconds);
                    yield return null;
                }
                Destroy(gameObject);
            }
        }
    }
}
