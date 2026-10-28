using UnityEngine;

// LCW 사운드를 재생하는 공용 도구. 효과음은 전부 "이 컴퓨터에서만" 재생한다 — 상대 컴퓨터의 소리는 상대 컴퓨터가 같은 상태 변화를 보고 직접 낸다.
// 3D 소리는 AudioSource 풀 한 곳에서 돌려 쓴다(씬에 오브젝트를 따로 만들 필요 없음).
public static class LcwSfx
{
    static LcwSoundBank bank;
    static AudioSource[] pool;
    static Transform root;
    static int next;
    const int PoolSize = 24;

    static readonly AnimationCurve RolloffCurve = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(0.08f, 0.7f), new Keyframe(0.25f, 0.28f), new Keyframe(0.5f, 0.09f), new Keyframe(1f, 0f));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { bank = null; pool = null; root = null; next = 0; }

    public static LcwSoundBank Bank
    {
        get
        {
            if (bank == null) bank = Resources.Load<LcwSoundBank>("KSM/LcwSoundBank");
            return bank;
        }
    }

    static void Ensure()
    {
        if (pool != null && root != null) return;
        var go = new GameObject("LcwSfxPool");
        Object.DontDestroyOnLoad(go);
        root = go.transform;
        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var a = new GameObject("Sfx" + i).AddComponent<AudioSource>();
            a.transform.SetParent(root, false);
            a.playOnAwake = false;
            a.loop = false;
            a.spatialBlend = 1f;
            // 로그 감쇠는 최대 거리 밖에서도 완전히 꺼지지 않아서 다른 층 소리가 계속 작게 들린다 → 최대 거리에서 0이 되는 곡선을 쓴다
            // (가로축 = 거리 / maxDistance, 세로축 = 음량)
            a.rolloffMode = AudioRolloffMode.Custom;
            a.SetCustomCurve(AudioSourceCurveType.CustomRolloff, RolloffCurve);
            a.dopplerLevel = 0f;
            pool[i] = a;
        }
    }

    static AudioSource Take()
    {
        Ensure();
        for (int k = 0; k < PoolSize; k++)
        {
            var a = pool[(next + k) % PoolSize];
            if (!a.isPlaying) { next = (next + k + 1) % PoolSize; return a; }
        }
        var oldest = pool[next];   // 전부 재생 중이면 가장 오래된 것을 끊고 쓴다
        next = (next + 1) % PoolSize;
        return oldest;
    }

    // 월드의 한 지점에서 나는 소리 (가까울수록 크게, maxDistance 밖에서는 들리지 않음)
    public static void Play(AudioClip clip, Vector3 position, float volume = 1f, float maxDistance = 20f, float minDistance = 2f, float pitchRandom = 0.03f)
    {
        if (clip == null) return;
        var a = Take();
        a.transform.position = position;
        a.clip = clip;
        a.volume = Mathf.Clamp01(volume);
        a.spatialBlend = 1f;
        a.minDistance = minDistance;
        a.maxDistance = maxDistance;
        a.pitch = 1f + Random.Range(-pitchRandom, pitchRandom);
        a.Play();
    }

    // 거리와 상관없이 내 귀에 바로 들리는 소리 (내가 한 행동, 화면 연출 등)
    public static void Play2D(AudioClip clip, float volume = 1f, float pitchRandom = 0.0f)
    {
        if (clip == null) return;
        var a = Take();
        a.transform.position = Vector3.zero;
        a.clip = clip;
        a.volume = Mathf.Clamp01(volume);
        a.spatialBlend = 0f;
        a.pitch = 1f + Random.Range(-pitchRandom, pitchRandom);
        a.Play();
    }
}
