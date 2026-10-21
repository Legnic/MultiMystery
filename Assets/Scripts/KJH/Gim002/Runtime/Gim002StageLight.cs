using UnityEngine;

namespace Gim002
{
    // 무대 조명. 2층 무대에 태엽 열쇠를 쓰면(stageWound) 서서히 켜진다.
    public class Gim002StageLight : MonoBehaviour
    {
        [SerializeField] Light stageLight;
        [Tooltip("켜졌을 때 밝기")]
        [SerializeField] float onIntensity = 3f;
        [Tooltip("켜지는 데 걸리는 시간(초)")]
        [SerializeField] float fadeSeconds = 0.8f;

        void Awake()
        {
            if (stageLight == null) stageLight = GetComponentInChildren<Light>(true);
            if (stageLight != null) { stageLight.intensity = 0f; stageLight.enabled = false; }
        }

        void Update()
        {
            var c = Gim002Controller.Instance;
            if (c == null || stageLight == null) return;
            float target = c.State.stageWound ? onIntensity : 0f;
            stageLight.intensity = Mathf.MoveTowards(stageLight.intensity, target, onIntensity * Time.deltaTime / Mathf.Max(0.01f, fadeSeconds));
            stageLight.enabled = stageLight.intensity > 0.001f;
        }
    }
}
