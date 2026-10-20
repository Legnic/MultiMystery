using System;
using UnityEngine;

// 스펙트럼 선이 소리에 "얼마나 예민하게" 반응할지 정하는 값 묶음.
// 선의 모양(색·굵기·번짐)은 건드리지 않고, 출렁임의 크기·속도·반응 방식만 정한다.
// SpectrumOverlay가 기본값으로 하나를 들고 있고, 사물마다 다른 반응을 원하면
// 아래 SpectrumResponseProfile 에셋(잔잔 / 보통 / 예민 등)을 ObjectEchoTarget에 연결한다.
[Serializable]
public class SpectrumResponse
{
    [Header("세기 계산")]
    [Tooltip("분석 값 → 선 높이 배율. 자동 음량 맞춤을 켜면 영향이 작아진다(조용한 소리의 바탕 크기 정도).")]
    public float gain = 14f;

    [Tooltip("켜면 소리 세기를 dB(로그) 단위로 바꿔서 보여준다. 사람 귀처럼 작은 소리의 변화가 잘 보인다.")]
    public bool useDecibelScale = true;

    [Tooltip("dB 단위일 때 '선이 평평해지는' 바닥 값. -36이면 가장 큰 소리보다 36dB 작은 소리는 안 보인다. 더 낮추면(-45) 작은 소리까지 보이지만 잡음도 보인다.")]
    [Range(-60f, -10f)]
    public float decibelFloor = -36f;

    [Tooltip("높은음 보정 (옥타브당 dB). 대부분의 소리는 저음 에너지가 훨씬 커서 선의 왼쪽(저음)만 움직이는데, " +
             "높은음을 조금씩 키워 선 전체가 고르게 출렁이게 한다. 0이면 보정 없음.")]
    [Range(0f, 6f)]
    public float highFrequencyTilt = 2f;

    [Header("자동 음량 맞춤")]
    [Tooltip("켜면 최근 몇 초의 가장 큰 소리를 기준으로 나눠서, 소리가 크든 작든 비슷한 폭으로 출렁이게 한다.")]
    public bool autoNormalize = true;

    [Tooltip("자동 음량 맞춤 후 가장 큰 순간의 선 높이 (0~1). 1이면 영역 끝까지 닿는다.")]
    [Range(0.1f, 1f)]
    public float normalizeTarget = 0.9f;

    [Tooltip("기준(최근 최대값)이 줄어드는 시간 (초). 짧으면 조용해질 때 금방 다시 커지고, 길면 큰 소리 뒤의 작은 소리가 작게 남는다.")]
    public float normalizeRelease = 1.5f;

    [Tooltip("기준 값의 최소치. 거의 무음일 때 잡음을 크게 키우지 않도록 막는다.")]
    public float minReference = 0.12f;

    [Header("움직임")]
    [Tooltip("선이 커질 때 따라가는 속도. 클수록 소리에 즉각 반응한다.")]
    public float riseSpeed = 16f;

    [Tooltip("선이 작아질 때 따라가는 속도. 클수록 출렁임이 또렷하고, 작을수록 여운이 길다.")]
    public float fallSpeed = 6f;

    [Tooltip("선 전체에 들어가는 물결 수.")]
    public float waveCycles = 9f;

    [Tooltip("물결이 옆으로 흐르는 속도 (라디안/초).")]
    public float waveFlowSpeed = 1.4f;

    [Tooltip("선 양 끝에서 출렁임이 0으로 모이는 구간 비율 (0~0.5). 0.5면 가운데만 크게 출렁이는 산 모양, " +
             "작을수록 끝 가까이까지 출렁인다. 저음(왼쪽)이 강한 소리는 작게 둬야 잘 보인다.")]
    [Range(0.05f, 0.5f)]
    public float edgeFalloff = 0.2f;

    [Header("실제 파형 섞기")]
    [Tooltip("실제 소리의 진동 모양(시간 파형)을 섞는 비율. 0 = 주파수 세기만, 1 = 실제 진동만. 두드림 같은 순간 반응이 살아난다.")]
    [Range(0f, 1f)]
    public float waveformBlend = 0.3f;

    [Tooltip("실제 파형 부분이 따라가는 속도. 너무 크면 선이 지글거리고, 작으면 부드럽다.")]
    public float waveformSmoothing = 20f;

    // 기본값과 같은 새 묶음을 만든다 (코드에서 프리셋을 만들 때 출발점).
    public SpectrumResponse Clone() => (SpectrumResponse)MemberwiseClone();
}

// 반응 값 묶음을 에셋 파일로 저장해 여러 사물이 같이 쓰게 하는 프리셋.
// 만들기: Project 창 우클릭 → Create → KJH → Spectrum Response Profile
// 기본 제공: Assets/Scripts/KJH/SoundCue/Presets/ (Calm=잔잔, Normal=보통, Sensitive=예민)
[CreateAssetMenu(fileName = "SpectrumResponse_", menuName = "KJH/Spectrum Response Profile")]
public class SpectrumResponseProfile : ScriptableObject
{
    [Tooltip("이 프리셋의 반응 값.")]
    public SpectrumResponse response = new SpectrumResponse();
}
