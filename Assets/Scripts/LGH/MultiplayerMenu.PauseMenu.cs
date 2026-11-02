using UnityEngine;

namespace LGH
{
    // 플레이 중 ESC를 누르면 뜨는 메뉴 — 디자이너 이미지 버전 (KJH/Options/Menu_Panel.png, Resources/OptionsDesignTheme).
    // 좌표는 디자인 이미지(1536x864) 기준. 패널 바깥은 투명이라 게임 화면 위에 패널만 띄우고,
    // 화면 비율이 달라도 패널이 잘리지 않게 "전부 들어가게(contain)" 배율을 쓴다.
    // 기능은 예전 일시정지 패널과 같다: 계속하기(커서 잠금) / 옵션 / 방 나가기. 닉네임은 로비 화면에서 바꾼다.
    public partial class MultiplayerMenu
    {
        static readonly Rect PmPanel = new Rect(379, 44, 780, 775);
        static readonly Rect PmRoomInfo = Rect.MinMaxRect(431, 190, 1104, 232);
        static readonly Rect PmCode = Rect.MinMaxRect(574, 245, 913, 289);
        static readonly Rect PmCopy = Rect.MinMaxRect(930, 243, 1105, 290);
        static readonly Rect PmMe = Rect.MinMaxRect(476, 314, 1062, 366);
        static readonly Rect PmOther = Rect.MinMaxRect(476, 371, 1062, 423);
        static readonly Rect PmVoiceStatus = Rect.MinMaxRect(476, 458, 1062, 496);
        const float PmMicY = 524f;                  // 마이크 켜짐/꺼짐 줄 (가운데 높이)
        const float PmSensY = 585f;                 // 마이크 감도 줄
        const float PmTrackX0 = 660f, PmTrackX1 = 993f;
        const float PmRight = 1062f;                // 칸 안쪽 오른쪽 끝 (구분선 끝)
        static readonly Rect PmContinue = Rect.MinMaxRect(428, 651, 1106, 704);
        static readonly Rect PmOptions = Rect.MinMaxRect(430, 719, 752, 769);
        static readonly Rect PmLeave = Rect.MinMaxRect(785, 719, 1106, 769);

        GUIStyle pmCenter, pmLeft, pmMic, pmNumber, pmVoice;
        Font pmBodyFont;
        float pmMicKnob = -1f;
        float pmCopiedUntil;

        // 그렸으면 true. 리소스가 없으면 false를 돌려줘서 예전 패널을 그리게 한다.
        bool DrawDesignedPause()
        {
            var t = CoopDemo.OptionsDesignTheme.Load();
            if (t == null || t.menuPanel == null || t.bodyFont == null || t.numberFont == null ||
                t.sliderThumb == null || t.micOn == null || t.micMuted == null || t.toggleTrack == null || t.toggleKnob == null)
                return false;

            LobbyStyles();   // 버튼 호버 테두리(LobbyButton)
            PauseStyles(t);

            const float W = 1536f, H = 864f;
            float s = Mathf.Min(Screen.width / W, Screen.height / H);
            var offset = new Vector3((Screen.width - W * s) * 0.5f, (Screen.height - H * s) * 0.5f, 0f);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(s, s, 1f));

            var e = Event.current;
            if (e.type == EventType.Repaint) GUI.DrawTexture(PmPanel, t.menuPanel);

            // 방 이름 · 인원
            if (gHasSession) GUI.Label(PmRoomInfo, gSessionName + " · " + gPlayerCount + "/" + gMaxPlayers + "명", pmCenter);

            // 참가 코드 (읽기 전용) + 코드 복사
            bool copied = Time.unscaledTime < pmCopiedUntil;
            GUI.Label(PmCode, copied ? "복사했습니다" : gSessionCode, pmCenter);
            if (LobbyButton(PmCopy, gHasSession && !string.IsNullOrEmpty(gSessionCode)))
            {
                GUIUtility.systemCopyBuffer = gSessionCode;
                pmCopiedUntil = Time.unscaledTime + 1.5f;
            }

            // 나 / 상대
            PlayerNamePair(out string me, out string other);
            GUI.Label(PmMe, "나 · " + (me ?? Nickname), pmLeft);
            var prevColor = pmLeft.normal.textColor;
            if (other == null) pmLeft.normal.textColor = new Color(LbCream.r, LbCream.g, LbCream.b, 0.5f);
            GUI.Label(PmOther, "상대 · " + (other ?? "기다리는 중..."), pmLeft);
            pmLeft.normal.textColor = prevColor;

            // 음성 상태 + 마이크 켜짐/꺼짐 (옵션-오디오의 '내 마이크'와 같은 모양)
            var voice = CoopDemo.VoiceChatManager.Instance;
            bool voiceReady = voice != null && voice.IsReady;
            string status = voice != null ? voice.Status.Replace("음성:", "음성 :") : "음성 : 아직 시작 안 됨";
            if (voice != null && voice.IsInChannel) status += " (상대 " + voice.RemoteCount + "명)";
            GUI.Label(PmVoiceStatus, status, pmVoice);

            bool micOn = !voiceReady || !voice.IsMicMuted;
            if (e.type == EventType.Repaint)
            {
                var icon = micOn ? t.micOn : t.micMuted;
                GUI.DrawTexture(new Rect(476f, PmMicY - icon.height / 2f, icon.width, icon.height), icon);
            }
            GUI.Label(new Rect(476f + t.micOn.width + 14f, PmMicY - 20f, 260f, 40f), micOn ? "마이크 켜짐  [M]" : "마이크 꺼짐  [M]", pmMic);
            bool nextOn = PauseToggle(t, new Rect(PmRight - t.toggleTrack.width, PmMicY - t.toggleTrack.height / 2f, t.toggleTrack.width, t.toggleTrack.height),
                                      micOn, voiceReady);
            if (voiceReady && nextOn != micOn) voice.SetMicMuted(!nextOn);

            // 마이크 감도: -50 ~ 50 (0이 기본)을 0 ~ 100으로 보여준다
            int vol = voice != null ? voice.MicVolume : 0;
            float next = PauseSlider(t, PmSensY, (vol + 50) / 100f, voice != null);
            int nextVol = Mathf.RoundToInt(next * 100f) - 50;
            if (voice != null && nextVol != vol) voice.SetMicVolume(nextVol);
            GUI.Label(new Rect(PmRight - 70f, PmSensY - 20f, 70f, 40f), (vol + 50).ToString(), pmNumber);

            // 버튼 (글자는 이미지에 그려져 있다)
            if (LobbyButton(PmContinue, true)) { GUIUtility.keyboardControl = 0; imeFocus = 0; ShowCursor(false); }
            if (LobbyButton(PmOptions, true)) CoopDemo.OptionsMenuController.Open();
            if (LobbyButton(PmLeave, !gBusy)) LeaveRoom();
            return true;
        }

        void PauseStyles(CoopDemo.OptionsDesignTheme t)
        {
            if (pmCenter != null && pmBodyFont == t.bodyFont) return;
            pmBodyFont = t.bodyFont;
            pmCenter = new GUIStyle { font = t.bodyFont, fontSize = 26, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            pmCenter.normal.textColor = LbCream;
            pmLeft = new GUIStyle(pmCenter) { alignment = TextAnchor.MiddleLeft };
            // 마이크 켜짐/꺼짐 글자 = LevelUp
            pmMic = new GUIStyle { font = t.bodyFont, fontSize = 24, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            pmMic.normal.textColor = LbCream;
            pmNumber = new GUIStyle(pmMic) { font = t.numberFont, fontSize = 25, alignment = TextAnchor.MiddleRight };
            // 음성 상태("음성 : 채널 연결 중" 등) = Song Myung, 흰색
            pmVoice = new GUIStyle(pmLeft) { font = t.numberFont };
            pmVoice.normal.textColor = CoopDemo.MeetYouGuiSkin.GuiColor(Color.white);
        }

        // 나와 상대 닉네임 (없으면 null)
        static void PlayerNamePair(out string me, out string other)
        {
            me = null; other = null;
            foreach (var p in NetworkPlayer.All)
            {
                if (p == null || !p.IsSpawned) continue;
                if (p.IsOwner) me = p.Nickname;
                else other = other == null ? p.Nickname : other + ", " + p.Nickname;
            }
        }

        // 마름모를 잡고 좌우로 끄는 슬라이더 (막대 위를 눌러도 그 자리로 옮겨진 뒤 끌 수 있다). 호버 테두리 없음.
        float PauseSlider(CoopDemo.OptionsDesignTheme t, float cy, float value01, bool enabled)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive);
            float x = Mathf.Lerp(PmTrackX0, PmTrackX1, value01);
            var thumb = new Rect(x - t.sliderThumb.width / 2f, cy - t.sliderThumb.height / 2f + 0.5f, t.sliderThumb.width, t.sliderThumb.height);
            var hit = new Rect(PmTrackX0 - 12f, cy - 16f, PmTrackX1 - PmTrackX0 + 24f, 32f);
            var e = Event.current;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (enabled && e.button == 0 && (hit.Contains(e.mousePosition) || thumb.Contains(e.mousePosition)))
                    {
                        GUIUtility.hotControl = id;
                        value01 = Mathf.InverseLerp(PmTrackX0, PmTrackX1, e.mousePosition.x);
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id) { value01 = Mathf.InverseLerp(PmTrackX0, PmTrackX1, e.mousePosition.x); e.Use(); }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
                    break;
                case EventType.Repaint:
                    GUI.DrawTexture(thumb, t.sliderThumb);
                    break;
            }
            return value01;
        }

        // 켜짐/꺼짐 슬라이드 토글 (동그라미 오른쪽 = 켜짐). 호버 테두리 없음.
        bool PauseToggle(CoopDemo.OptionsDesignTheme t, Rect track, bool on, bool enabled)
        {
            if (enabled && GUI.Button(track, GUIContent.none, lbHit)) on = !on;
            if (Event.current.type == EventType.Repaint)
            {
                float target = on ? 1f : 0f;
                if (pmMicKnob < 0f) pmMicKnob = target;
                pmMicKnob = Mathf.MoveTowards(pmMicKnob, target, Time.unscaledDeltaTime * 8f);
                GUI.DrawTexture(track, t.toggleTrack);
                float pad = (track.height - t.toggleKnob.height) / 2f;
                float kx = Mathf.Lerp(track.x + pad + 2f, track.xMax - pad - 2f - t.toggleKnob.width, pmMicKnob);
                GUI.DrawTexture(new Rect(kx, track.y + pad, t.toggleKnob.width, t.toggleKnob.height), t.toggleKnob);
            }
            return on;
        }
    }
}
