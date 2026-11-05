using System;
using UnityEngine;

namespace CoopDemo
{
    // 플레이어별 로컬 설정값 한 묶음. 네트워크로 전송되지 않는다 (각자 PC에만 적용/저장).
    [Serializable]
    public struct PlayerOptionsData
    {
        [Range(0f, 1f)] public float masterVolume;
        [Range(0f, 1f)] public float bgmVolume;
        [Range(0f, 1f)] public float sfxVolume;
        [Range(0f, 1f)] public float voiceVolume;   // 상대방 음성 음량 (0~1, Vivox 로컬 볼륨 조정값으로 환산)
        public bool micMuted;
        [Range(0.1f, 5f)] public float mouseSensitivity;
        [Range(-1f, 1f)] public float brightness;   // 화면 노출(EV) 보정값. 0 = 아트 기준값 그대로
        public bool fullscreen;
        public int resolutionWidth;
        public int resolutionHeight;
        public string micDeviceId;      // Vivox VivoxInputDevice.DeviceID. 비어있으면 시스템 기본 장치
        public string speakerDeviceId;  // Vivox VivoxOutputDevice.DeviceID. 비어있으면 시스템 기본 장치
        [Range(0.5f, 2.5f)] public float crosshairSize;   // 기준 픽셀 크기(32px)에 곱하는 배율
        public Color crosshairColor;
        public int crosshairIndex;   // LGH.PlayerInteractor.crosshairOptions / OptionsMenuController.crosshairTextures의 인덱스
        public int graphicsQuality;  // GraphicsQuality: -1 자동, 0 낮음, 1 높음
        public int optionsVersion;   // 저장 형식 버전. 2부터 전체 음량 70%가 예전 100%(=그대로의 소리 크기)와 같다 (아래 MasterVolumeReference)

        public const int CurrentVersion = 2;

        // 전체 음량 슬라이더에서 "예전 최대 소리 크기"가 되는 값. 슬라이더 70% = AudioListener.volume 1.0(지금까지의 소리 크기),
        // 100% = 약 1.43배로 더 크게. 기본값은 70%.
        public const float MasterVolumeReference = 0.7f;

        public static PlayerOptionsData Default()
        {
            var r = Screen.currentResolution;
            return new PlayerOptionsData
            {
                masterVolume = MasterVolumeReference,
                bgmVolume = 1f,
                sfxVolume = 1f,
                voiceVolume = 1f,
                micMuted = false,
                mouseSensitivity = 2f,   // LGH.FirstPersonController의 기본값과 동일
                brightness = 0f,
                fullscreen = true,
                resolutionWidth = r.width,
                resolutionHeight = r.height,
                micDeviceId = "",
                speakerDeviceId = "",
                crosshairSize = 1f,
                crosshairColor = Color.white,
                crosshairIndex = 1,   // 배열 순서상 Crosshair_04 (심플한 십자형) — 처음 고른 기본 모양
                graphicsQuality = GraphicsQuality.Auto,
                optionsVersion = CurrentVersion,
            };
        }
    }

    // 저장/로드 + "지금 미리보기 중인 값(Current)"을 들고 있는 곳. 실제 반영은 OptionsManager가 한다.
    public static class PlayerOptions
    {
        const string Key = "KSM_PlayerOptions_v1";

        public static PlayerOptionsData Current { get; private set; } = PlayerOptionsData.Default();

        // Current가 바뀔 때마다 (미리보기 포함) 울린다. OptionsManager가 구독해서 실제 시스템에 반영한다.
        public static event Action<PlayerOptionsData> Changed;

        static bool loaded;

        public static void EnsureLoaded()
        {
            if (loaded) return;
            Load();
        }

        public static void Load()
        {
            loaded = true;
            Current = LoadFromDisk();
            Changed?.Invoke(Current);
        }

        // 기본값 위에 저장된 JSON을 "덮어쓰기"로 읽는다(FromJsonOverwrite) — 나중에 필드가 추가됐는데
        // 예전에 저장된 JSON에는 그 필드가 없는 경우, 0으로 비는 대신 Default() 값을 그대로 쓰게 하기 위함
        // (예: crosshairSize가 0이 되면 조준점이 아예 안 보이는 등 "없는 필드=0"이 진짜 문제가 됐던 적이 있음).
        static PlayerOptionsData LoadFromDisk()
        {
            if (!PlayerPrefs.HasKey(Key)) return PlayerOptionsData.Default();
            // 버전 필드가 없는 예전 저장값을 알아보려고 0에서 시작해 덮어쓴다 (새 저장값에는 2가 들어 있다)
            var start = PlayerOptionsData.Default();
            start.optionsVersion = 0;
            object d = start;
            try { JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(Key), d); }
            catch (Exception e)
            {
                Debug.LogWarning("[PlayerOptions] 저장된 설정을 읽지 못해 기본값을 사용합니다: " + e.Message);
                return PlayerOptionsData.Default();
            }
            var data = (PlayerOptionsData)d;
            if (data.optionsVersion < 2)
            {
                // 예전에는 슬라이더 100%가 지금의 70%와 같은 소리 크기였다 → 같은 소리 크기가 되도록 옮긴다 (예: 100% → 70%)
                data.masterVolume = Mathf.Clamp01(data.masterVolume * PlayerOptionsData.MasterVolumeReference);
                data.optionsVersion = PlayerOptionsData.CurrentVersion;
            }
            return data;
        }

        // 슬라이더를 움직이는 동안 호출 — 미리보기만 하고 디스크에는 쓰지 않는다.
        public static void Preview(PlayerOptionsData data)
        {
            Current = data;
            Changed?.Invoke(Current);
        }

        // [적용 및 저장]
        public static void Save(PlayerOptionsData data)
        {
            Current = data;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            Changed?.Invoke(Current);
        }

        // [취소] — 마지막으로 저장된 값으로 되돌린다 (디스크에 있는 값 다시 읽기)
        public static PlayerOptionsData RevertToSaved()
        {
            var d = LoadFromDisk();
            Current = d;
            Changed?.Invoke(Current);
            return d;
        }
    }
}
