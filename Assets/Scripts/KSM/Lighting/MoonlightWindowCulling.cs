using UnityEngine;

namespace CoopDemo
{
    // 창가 달빛 스포트라이트(MoonlightWindows 아래 자식 전부)를 플레이어와 가까운 것만 켠다.
    // 각 달빛은 창 바깥 8m 뒤에 있어서, 창 위치 = 광원 위치 + 앞쪽 * sourceDistance 이다. 해가 떠 있으면(DayNightCycle의 해 광원이 켜짐) 모두 끈다.
    // 에디터(비실행)에서는 아무것도 하지 않으므로 Scene 뷰에서는 전부 켜진 채로 보인다.
    public class MoonlightWindowCulling : MonoBehaviour
    {
        public float sourceDistance = 8f;
        [Tooltip("플레이어 카메라와 창 사이 거리가 이보다 멀면 끈다(미터).")]
        public float maxDistance = 24f;
        public float interval = 0.25f;

        Light[] lights;
        LGH.DayNightCycle dayNight;
        float next;

        void Awake()
        {
            lights = GetComponentsInChildren<Light>(true);
            dayNight = FindFirstObjectByType<LGH.DayNightCycle>(FindObjectsInactive.Include);
        }

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + interval;
            var cam = Camera.main;
            if (cam == null && LGH.NetworkPlayer.Local != null) cam = LGH.NetworkPlayer.Local.GetComponentInChildren<Camera>();
            bool daylight = dayNight != null && dayNight.sunLight != null && dayNight.sunLight.enabled;
            Vector3 eye = cam != null ? cam.transform.position : Vector3.zero;
            float max2 = maxDistance * maxDistance;
            foreach (var l in lights)
            {
                if (l == null) continue;
                bool on = !daylight && (cam == null || ((l.transform.position + l.transform.forward * sourceDistance) - eye).sqrMagnitude <= max2);
                if (l.enabled != on) l.enabled = on;
            }
        }
    }
}
