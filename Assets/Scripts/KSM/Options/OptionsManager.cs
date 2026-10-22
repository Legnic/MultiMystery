using Unity.Netcode;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CoopDemo
{
    // 옵션 값을 실제 게임 시스템에 적용하는 곳. 씬에 하나만 두고 DontDestroyOnLoad로 유지한다.
    // 네트워크와는 무관하다 — 전부 "이 컴퓨터"에만 영향을 주는 로컬 적용이다.
    public class OptionsManager : MonoBehaviour
    {
        public static OptionsManager Instance { get; private set; }

        [Header("오디오 믹서 (없으면 BGM/SFX 슬라이더는 조용히 무시됨)")]
        [Tooltip("Assets/Sounds/KJH/KJH_AudioMixer.mixer. BGM/SFX 그룹과 노출 파라미터(BGMVolume/SFXVolume)는 " +
                 "Audio Mixer 창에서 팀원이 직접 추가해야 한다 (스크립트로는 그룹 생성이 불가능함).")]
        [SerializeField] AudioMixer mixer;
        const string BgmParam = "BGMVolume";
        const string SfxParam = "SFXVolume";

        [Header("화면 밝기 (URP ColorAdjustments.postExposure)")]
        [Tooltip("비워두면 씬의 전역(Global) Volume 중 ColorAdjustments가 있는 것을 자동으로 찾는다.")]
        [SerializeField] Volume brightnessVolume;
        float baseExposure;       // 아트 기준값 (씬에 이미 들어있는 postExposure) — 그 위에 밝기 슬라이더를 더한다
        ColorAdjustments colorAdjustments;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (brightnessVolume == null)
            {
                foreach (var v in FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (v.isGlobal && v.profile != null && v.profile.TryGet(out colorAdjustments)) { brightnessVolume = v; break; }
                }
            }
            else
            {
                brightnessVolume.profile.TryGet(out colorAdjustments);
            }
            if (colorAdjustments != null) baseExposure = colorAdjustments.postExposure.value;

            PlayerOptions.EnsureLoaded();
            PlayerOptions.Changed += Apply;
            Apply(PlayerOptions.Current);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            PlayerOptions.Changed -= Apply;
        }

        bool voiceDevicesApplied;

        void Update()
        {
            // FirstPersonController는 네트워크 스폰 시점에 생기므로, 로컬 플레이어가 나타나면 그때 감도를 먹인다.
            var local = LGH.NetworkPlayer.Local;
            if (local != null)
            {
                var fpc = local.GetComponent<LGH.FirstPersonController>();
                if (fpc != null && !Mathf.Approximately(fpc.mouseSensitivity, PlayerOptions.Current.mouseSensitivity))
                    fpc.mouseSensitivity = PlayerOptions.Current.mouseSensitivity;
            }

            // 마이크/스피커 장치는 Vivox 로그인이 끝난 뒤에만 고를 수 있어서, 준비되는 순간 한 번 적용한다.
            if (!voiceDevicesApplied && VoiceChatManager.Instance != null && VoiceChatManager.Instance.IsReady)
            {
                voiceDevicesApplied = true;
                ApplyVoiceDevices(PlayerOptions.Current);
            }
        }

        void Apply(PlayerOptionsData d)
        {
            // 전체 음량: 믹서 라우팅 여부와 상관없이 이 컴퓨터에서 나가는 모든 소리에 적용된다.
            AudioListener.volume = d.masterVolume;

            if (mixer != null)
            {
                SetMixerVolume(BgmParam, d.bgmVolume);
                SetMixerVolume(SfxParam, d.sfxVolume);
            }

            // 상대방 음성 음량: Vivox 참가자별 LocalVolume에 곱해진다 (거리 감쇠와 별개로 곱산됨).
            if (VoiceChatManager.Instance != null)
            {
                VoiceChatManager.Instance.SetRemoteVolumeScale(d.voiceVolume);
                VoiceChatManager.Instance.SetMicMuted(d.micMuted);
            }

            if (colorAdjustments != null)
            {
                // d.brightness: -1~1 EV 보정. 아트 기준값(baseExposure) 위에 더한다.
                colorAdjustments.postExposure.overrideState = true;
                colorAdjustments.postExposure.value = baseExposure + d.brightness;
            }

            Screen.fullScreenMode = d.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (d.resolutionWidth > 0 && d.resolutionHeight > 0 &&
                (Screen.width != d.resolutionWidth || Screen.height != d.resolutionHeight))
            {
                Screen.SetResolution(d.resolutionWidth, d.resolutionHeight, Screen.fullScreenMode);
            }

            var local = LGH.NetworkPlayer.Local;
            if (local != null)
            {
                var fpc = local.GetComponent<LGH.FirstPersonController>();
                if (fpc != null) fpc.mouseSensitivity = d.mouseSensitivity;
            }

            if (VoiceChatManager.Instance != null && VoiceChatManager.Instance.IsReady)
                ApplyVoiceDevices(d);
        }

        // 저장된 장치 ID로 Vivox의 활성 입력/출력 장치를 맞춘다. 장치가 안 보이면(뽑힘 등) 조용히 건너뛴다.
        async void ApplyVoiceDevices(PlayerOptionsData d)
        {
            try
            {
                if (!string.IsNullOrEmpty(d.micDeviceId))
                {
                    foreach (var dev in VivoxService.Instance.AvailableInputDevices)
                        if (dev.DeviceID == d.micDeviceId) { await dev.SetActiveDeviceAsync(); break; }
                }
                if (!string.IsNullOrEmpty(d.speakerDeviceId))
                {
                    foreach (var dev in VivoxService.Instance.AvailableOutputDevices)
                        if (dev.DeviceID == d.speakerDeviceId) { await dev.SetActiveDeviceAsync(); break; }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[OptionsManager] 마이크/스피커 장치 적용 실패: " + e.Message);
            }
        }

        // 0~1 슬라이더 값을 믹서 데시벨로 바꿔서 쓴다 (0 = -80dB(사실상 무음), 1 = 0dB).
        void SetMixerVolume(string param, float linear01)
        {
            float dB = linear01 <= 0.0001f ? -80f : Mathf.Log10(Mathf.Clamp01(linear01)) * 20f;
            if (!mixer.SetFloat(param, dB))
                Debug.LogWarning($"[OptionsManager] 믹서에 '{param}' 노출 파라미터가 없습니다. Audio Mixer 창에서 해당 그룹 볼륨을 " +
                                  $"우클릭 → Expose to script 로 추가하고 이름을 '{param}'으로 지어주세요.");
        }
    }
}
