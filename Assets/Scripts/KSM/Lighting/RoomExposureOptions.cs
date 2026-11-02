using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CoopDemo
{
    // 방 전용 노출에도 기존 옵션의 밝기(EV)를 더한다. 공유 VolumeProfile 에셋은 수정하지 않는다.
    [RequireComponent(typeof(Volume))]
    public class RoomExposureOptions : MonoBehaviour
    {
        public float baseExposureEV = 0.75f;
        Volume volume;
        VolumeProfile runtimeProfile;
        ColorAdjustments color;

        void OnEnable()
        {
            volume = GetComponent<Volume>();
            if (volume.sharedProfile == null) return;
            // Volume.profile은 프로필뿐 아니라 각 VolumeComponent도 복제한다.
            runtimeProfile = volume.profile;
            runtimeProfile.TryGet(out color);
            PlayerOptions.EnsureLoaded();
            PlayerOptions.Changed += Apply;
            Apply(PlayerOptions.Current);
        }

        void Apply(PlayerOptionsData options)
        {
            if (color != null) color.postExposure.Override(baseExposureEV + options.brightness);
        }

        void OnDisable()
        {
            PlayerOptions.Changed -= Apply;
            if (volume != null) volume.profile = null;
            if (runtimeProfile != null)
            {
                foreach (var component in runtimeProfile.components)
                    if (component != null) Destroy(component);
                Destroy(runtimeProfile);
            }
            runtimeProfile = null;
            color = null;
        }
    }
}
