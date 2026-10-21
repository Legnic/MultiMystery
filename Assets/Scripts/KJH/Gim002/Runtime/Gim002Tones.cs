using System.Collections.Generic;
using UnityEngine;

namespace Gim002
{
    // 사운드 에셋이 아직 없을 때 쓰는 임시 소리 생성기. 코드로 사인파를 만들어 AudioClip으로 돌려준다.
    // 실제 사운드가 나오면 각 컴포넌트의 AudioClip 칸에 끼우면 되고, 그러면 이 임시 소리는 쓰이지 않는다.
    public static class Gim002Tones
    {
        const int Rate = 44100;
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        // 피아노 건반 대신 쓸 단음 (주파수 Hz). 짧게 울리고 서서히 작아진다.
        public static AudioClip Note(float frequency, float seconds = 0.9f)
        {
            return Chord("note" + frequency, seconds, frequency);
        }

        // 여러 음을 겹친 화음 / 연속음. notes를 동시에 울린다.
        public static AudioClip Chord(string key, float seconds, params float[] notes)
        {
            AudioClip clip;
            if (cache.TryGetValue(key, out clip) && clip != null) return clip;

            int n = Mathf.Max(1, (int)(Rate * seconds));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-3f * t / seconds) * Mathf.Clamp01(t * 200f);   // 빠른 시작, 서서히 감쇠
                float v = 0f;
                foreach (var f in notes) v += Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * f * t);
                data[i] = v / (notes.Length * 1.3f) * env * 0.6f;
            }
            clip = AudioClip.Create("Gim002_" + key, n, 1, Rate, false);
            clip.SetData(data, 0);
            cache[key] = clip;
            return clip;
        }

        // 여러 음을 차례로 울리는 짧은 멜로디 (성공음 등)
        public static AudioClip Arpeggio(string key, float step, params float[] notes)
        {
            AudioClip clip;
            if (cache.TryGetValue(key, out clip) && clip != null) return clip;

            float total = step * notes.Length + 0.6f;
            int n = (int)(Rate * total);
            var data = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = (int)(Rate * step * k);
                for (int i = start; i < n; i++)
                {
                    float t = (float)(i - start) / Rate;
                    float env = Mathf.Exp(-4f * t) * Mathf.Clamp01(t * 200f);
                    data[i] += Mathf.Sin(2f * Mathf.PI * notes[k] * t) * env * 0.35f;
                }
            }
            clip = AudioClip.Create("Gim002_" + key, n, 1, Rate, false);
            clip.SetData(data, 0);
            cache[key] = clip;
            return clip;
        }

        // 기본 임시 소리들
        public static AudioClip Warning() { return Chord("warning", 0.6f, 110f, 116.5f); }               // 낮고 거친 경고음
        public static AudioClip WrongChord() { return Chord("wrongChord", 0.8f, 261.6f, 277.2f, 370f); }  // 틀린 화음
        public static AudioClip Open() { return Arpeggio("open", 0.08f, 392f, 523.3f); }                   // 반주판 열림
        public static AudioClip Success() { return Arpeggio("success", 0.12f, 523.3f, 659.3f, 784f); }     // 성공음
        public static AudioClip Complete() { return Arpeggio("complete", 0.15f, 523.3f, 659.3f, 784f, 1046.5f); }
        public static AudioClip Click() { return Chord("click", 0.12f, 1200f); }                           // 꽂히는 소리
        public static AudioClip Wind() { return Chord("wind", 0.15f, 700f, 900f); }                        // 태엽·손잡이 딸깍
    }
}
