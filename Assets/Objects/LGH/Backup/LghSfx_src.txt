using UnityEngine;

namespace LGH
{
    // Small helpers to play a one-shot sound. 3D sounds fade out linearly so they stay local to a room.
    public static class LghSfx
    {
        public static LghSoundBank Bank { get { return LghSoundBank.Get; } }

        // 3D sound at a point (heard by whoever is near). maxDistance in metres.
        public static AudioSource At(AudioClip clip, Vector3 position, float volume = 0.8f, float maxDistance = 14f, float pitch = 1f)
        {
            if (clip == null || !Application.isPlaying) return null;
            var go = new GameObject("Sfx_" + clip.name);
            go.transform.position = position;
            var s = go.AddComponent<AudioSource>();
            Setup3D(s, maxDistance);
            s.clip = clip;
            s.volume = volume;
            s.pitch = pitch;
            s.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.2f);
            return s;
        }

        // Sound in your own head (UI-like): no distance, only the local player hears it.
        public static AudioSource Local(AudioClip clip, float volume = 0.8f)
        {
            if (clip == null || !Application.isPlaying) return null;
            var go = new GameObject("Sfx2D_" + clip.name);
            Object.DontDestroyOnLoad(go);
            var s = go.AddComponent<AudioSource>();
            s.spatialBlend = 0f;
            s.playOnAwake = false;
            s.clip = clip;
            s.volume = volume;
            s.Play();
            Object.Destroy(go, clip.length + 0.2f);
            return s;
        }

        public static void Setup3D(AudioSource s, float maxDistance)
        {
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = Mathf.Min(1.5f, maxDistance * 0.25f);
            s.maxDistance = maxDistance;
            s.dopplerLevel = 0f;
        }
    }
}
