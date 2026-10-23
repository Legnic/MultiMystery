using System.Collections.Generic;
using UnityEngine;

namespace Gim007
{
    // 사운드 에셋이 아직 없을 때 쓰는 임시 소리 생성기 (GIM-002의 Gim002Tones와 같은 역할, 여기는 잡음 기반).
    // 실제 사운드가 나오면 각 컴포넌트의 AudioClip 칸에 끼우면 되고, 그러면 이 임시 소리는 쓰이지 않는다.
    // 톤 원칙(CLAUDE.md): 놀래키는 큰 소리 금지 → 전부 낮고 작은 소리로 만든다.
    public static class Gim007Sounds
    {
        const int Rate = 44100;
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        delegate float Sample(float t, System.Random rng);

        static AudioClip Make(string key, float seconds, Sample f)
        {
            AudioClip clip;
            if (cache.TryGetValue(key, out clip) && clip != null) return clip;
            int n = Mathf.Max(1, (int)(Rate * seconds));
            var data = new float[n];
            var rng = new System.Random(key.GetHashCode());
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f((float)i / Rate, rng), -1f, 1f);
            clip = AudioClip.Create("Gim007_" + key, n, 1, Rate, false);
            clip.SetData(data, 0);
            cache[key] = clip;
            return clip;
        }

        static float Noise(System.Random rng) { return (float)(rng.NextDouble() * 2.0 - 1.0); }

        // 무거운 가구가 바닥을 긁는 낮은 소리 (성공 1칸)
        public static AudioClip Scrape()
        {
            float lp = 0f, lp2 = 0f;
            return Make("scrape", 0.85f, (t, r) =>
            {
                lp += (Noise(r) - lp) * 0.04f;            // 낮은 대역만 남긴 잡음
                lp2 += (lp - lp2) * 0.08f;
                float grit = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 38f + Mathf.Sin(t * 7f) * 3f));   // 걸렸다 미끄러지는 느낌
                float env = Mathf.Clamp01(t * 12f) * Mathf.Clamp01((0.85f - t) * 5f);
                return lp2 * grit * env * 9f;
            });
        }

        // 거친 숨소리 두 번 (숨고르기)
        public static AudioClip Breath()
        {
            float lp = 0f, hp = 0f, prev = 0f;
            return Make("breath", 1.6f, (t, r) =>
            {
                float x = Noise(r);
                lp += (x - lp) * 0.25f;                     // 대역 통과(쉿 소리 대역)
                hp = 0.97f * (hp + lp - prev); prev = lp;
                float b1 = Mathf.Sin(Mathf.Clamp01(t / 0.7f) * Mathf.PI);
                float b2 = Mathf.Sin(Mathf.Clamp01((t - 0.8f) / 0.75f) * Mathf.PI) * 0.8f;
                return hp * (b1 * b1 + b2 * b2) * 0.5f;
            });
        }

        // 혼자 힘쓰는 "읏" 소리 (혼자 밀 때)
        public static AudioClip Strain()
        {
            float lp = 0f;
            return Make("strain", 0.75f, (t, r) =>
            {
                float f = 120f - 25f * t;
                float saw = 2f * (t * f - Mathf.Floor(t * f + 0.5f));
                lp += (saw * 0.6f + Noise(r) * 0.4f - lp) * 0.12f;
                float env = Mathf.Clamp01(t * 10f) * Mathf.Clamp01((0.75f - t) * 4f);
                return lp * env * 0.55f;
            });
        }

        // 금속 장치가 걸리는 "철컥" (발판 완료, 문 걸쇠)
        public static AudioClip Clank()
        {
            return Make("clank", 0.6f, (t, r) =>
            {
                float metal = Mathf.Sin(2f * Mathf.PI * 820f * t) + 0.7f * Mathf.Sin(2f * Mathf.PI * 1310f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 2170f * t);
                float hit = Noise(r) * Mathf.Exp(-t * 90f);
                float second = t > 0.09f ? Mathf.Sin(2f * Mathf.PI * 640f * t) * Mathf.Exp(-(t - 0.09f) * 22f) : 0f;   // 철-컥 두 번
                return (metal * Mathf.Exp(-t * 28f) * 0.18f + hit * 0.35f + second * 0.2f);
            });
        }

        // 사람 무게로 발판이 덜컥거리다 마는 소리 (작고 짧게)
        public static AudioClip Rattle()
        {
            return Make("rattle", 0.45f, (t, r) =>
            {
                float taps = 0f;
                for (int k = 0; k < 3; k++)
                {
                    float tk = t - k * 0.11f;
                    if (tk > 0f) taps += Mathf.Sin(2f * Mathf.PI * (520f - k * 60f) * tk) * Mathf.Exp(-tk * 45f) + Noise(r) * Mathf.Exp(-tk * 120f) * 0.6f;
                }
                return taps * 0.16f;
            });
        }

        // 집 안 어딘가에서 나무가 삐걱이는 소리 (숨고르기 중 앰비언트, 아주 작게)
        public static AudioClip Creak()
        {
            float phase = 0f;
            return Make("creak", 1.3f, (t, r) =>
            {
                float f = 190f - 70f * t / 1.3f;
                phase += f / Rate;
                float stick = Mathf.Pow(Mathf.Abs(Mathf.Sin(t * 2f * Mathf.PI * 14f)), 6f);   // 끼익끼익 끊기는 마찰
                float env = Mathf.Clamp01(t * 4f) * Mathf.Clamp01((1.3f - t) * 3f);
                return Mathf.Sin(2f * Mathf.PI * phase) * stick * env * 0.22f;
            });
        }

        // 비밀문이 천천히 열리는 소리 (낮은 삐걱임 길게)
        public static AudioClip DoorOpen()
        {
            float phase = 0f;
            return Make("dooropen", 2.4f, (t, r) =>
            {
                float f = 140f + 30f * Mathf.Sin(t * 1.7f);
                phase += f / Rate;
                float stick = 0.4f + 0.6f * Mathf.Pow(Mathf.Abs(Mathf.Sin(t * 2f * Mathf.PI * 6f)), 3f);
                float env = Mathf.Clamp01(t * 2f) * Mathf.Clamp01((2.4f - t) * 1.5f);
                return Mathf.Sin(2f * Mathf.PI * phase) * stick * env * 0.25f;
            });
        }

        // 다른 사람이 실패했을 때 등 짧은 둔탁한 소리 (가구가 덜컹 멈춤)
        public static AudioClip Thud()
        {
            float lp = 0f;
            return Make("thud", 0.4f, (t, r) =>
            {
                lp += (Noise(r) - lp) * 0.05f;
                return (Mathf.Sin(2f * Mathf.PI * 70f * t) * 0.5f + lp * 4f) * Mathf.Exp(-t * 14f) * 0.5f;
            });
        }
    }
}
