using UnityEngine;

// 플레이어 걸음걸이 소리를 재생하는 스크립트. 이동 코드는 전혀 건드리지 않고 "얼마나 움직였는가"만 관찰한다.
// PlayerMovement든 네트워크용 1인칭 컨트롤러든 상관없이 Player 오브젝트에 붙이기만 하면 동작한다.
// (CharacterController.velocity 대신 위치 변화량을 쓰므로, NetworkTransform으로만 움직이는 상대 플레이어에서도 발소리가 난다.)
public class FootstepSound : MonoBehaviour
{
    [Header("소리")]
    [Tooltip("발걸음 소리 클립들. 여러 개를 넣으면 매번 랜덤으로 골라 재생해서 덜 단조롭다.")]
    [SerializeField] private AudioClip[] footstepClips;
    [Tooltip("소리를 낼 AudioSource. 비워두면 이 오브젝트에 자동으로 만든다.")]
    [SerializeField] private AudioSource audioSource;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.6f;
    [Tooltip("재생할 때마다 피치를 이 범위 안에서 무작위로 흔든다 (±값).")]
    [SerializeField] private float pitchVariation = 0.08f;

    [Header("걸음 간격")]
    [Tooltip("이 거리(m)만큼 이동할 때마다 한 걸음 소리를 낸다. 이동 속도가 빠를수록 자연히 더 자주 난다.")]
    [SerializeField] private float stepDistance = 1.8f;
    [Tooltip("이 속도(m/s)보다 느리면 걷는 게 아니라고 보고 소리를 내지 않는다 (제자리 흔들림 방지).")]
    [SerializeField] private float minSpeed = 0.5f;
    [Tooltip("이 속도(m/s)보다 빠르면 순간이동(스폰/텔레포트)으로 보고 무시한다.")]
    [SerializeField] private float maxSpeed = 20f;
    [Tooltip("수직으로 이 속도(m/s) 이상 움직이면 공중(낙하/점프)으로 보고 소리를 내지 않는다.")]
    [SerializeField] private float maxVerticalSpeed = 1.5f;

    private Vector3 lastPosition;
    private float distanceAccumulated;
    private int lastClipIndex = -1;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D 소리: 상대 플레이어의 발소리가 방향/거리감을 갖게 한다.
        }
    }

    private void OnEnable()
    {
        // 비활성 → 활성될 때 이전 위치와 비교해 거리가 튀지 않도록 기준을 다시 잡는다.
        lastPosition = transform.position;
        distanceAccumulated = 0f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;

        float verticalSpeed = Mathf.Abs(delta.y) / dt;
        delta.y = 0f;
        float speed = delta.magnitude / dt;

        // 멈춰있거나, 공중이거나, 순간이동한 프레임은 걸음 누적에서 제외한다.
        if (speed < minSpeed || speed > maxSpeed || verticalSpeed > maxVerticalSpeed)
        {
            distanceAccumulated = 0f;
            return;
        }

        distanceAccumulated += delta.magnitude;
        if (distanceAccumulated >= stepDistance)
        {
            distanceAccumulated -= stepDistance;
            PlayStep();
        }
    }

    private void PlayStep()
    {
        if (footstepClips == null || footstepClips.Length == 0 || audioSource == null) return;

        // 같은 소리가 연속으로 두 번 나오지 않게 직전 인덱스는 피해서 고른다.
        int index = Random.Range(0, footstepClips.Length);
        if (footstepClips.Length > 1 && index == lastClipIndex)
            index = (index + 1) % footstepClips.Length;
        lastClipIndex = index;

        audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        audioSource.PlayOneShot(footstepClips[index], volume);
    }
}
