using UnityEngine;

namespace LGH
{
    // 대기방(방 만들기/참가 직후, GameFlowSync.Stage == Lobby) — 디자이너 이미지 버전
    // (KJH/Lobby/Lobby_WaitingRoom.png, Resources/OptionsDesignTheme.waitingRoomBackground). 글자는 전부 LevelUp(bodyFont).
    // 좌표는 디자인 이미지(1536x864) 기준. 뒤에 LobbyCanvas(불투명)가 깔려 있으므로 로비 화면처럼 꽉 차게(cover) 그린다.
    // 기능은 예전 대기방과 같다: 준비 완료/준비 취소(2명일 때만), 둘 다 준비하면 카운트다운, 옵션, 방 나가기. 닉네임은 로비 화면에서 바꾼다.
    public partial class MultiplayerMenu
    {
        static readonly Rect WrRoomName = Rect.MinMaxRect(431, 220, 1104, 258);   // 제목 구분선과 참가 코드 줄 사이
        static readonly Rect WrCode = Rect.MinMaxRect(570, 263, 913, 309);
        static readonly Rect WrCopy = Rect.MinMaxRect(928, 262, 1102, 310);
        static readonly Rect WrMeName = Rect.MinMaxRect(470, 377, 835, 425);
        static readonly Rect WrOtherName = Rect.MinMaxRect(470, 474, 835, 522);
        static readonly Rect WrMeState = Rect.MinMaxRect(851, 377, 1069, 425);    // 이미지 속 오른쪽 둥근 칸
        static readonly Rect WrOtherState = Rect.MinMaxRect(851, 474, 1069, 522);
        static readonly Rect WrReady = Rect.MinMaxRect(425, 587, 1103, 642);
        static readonly Rect WrOptions = Rect.MinMaxRect(426, 656, 749, 706);
        static readonly Rect WrLeave = Rect.MinMaxRect(782, 656, 1102, 706);

        GUIStyle wrCenter, wrLeft, wrButton;
        Font wrFont;
        Texture2D wrReadyFill;
        float wrCopiedUntil;

        static Color WrGold => CoopDemo.MeetYouGuiSkin.GuiColor(new Color(0.98f, 0.82f, 0.50f));

        // 그렸으면 true. 리소스가 없으면 false를 돌려줘서 예전 패널을 그리게 한다.
        bool DrawDesignedWaitingRoom(CoopDemo.GameFlowSync flow)
        {
            var t = CoopDemo.OptionsDesignTheme.Load();
            if (t == null || t.waitingRoomBackground == null || t.bodyFont == null) return false;

            LobbyStyles();   // LobbyButton(호버 테두리 / 비활성 어둡게)
            WaitingRoomStyles(t);

            const float W = 1536f, H = 864f;
            float s = Mathf.Max(Screen.width / W, Screen.height / H);
            var offset = new Vector3((Screen.width - W * s) * 0.5f, (Screen.height - H * s) * 0.5f, 0f);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(s, s, 1f));

            var e = Event.current;
            if (e.type == EventType.Repaint) GUI.DrawTexture(new Rect(0, 0, W, H), t.waitingRoomBackground);

            // 방 이름(방 번호) · 인원
            if (gHasSession) GUI.Label(WrRoomName, gSessionName + "  ·  " + gPlayerCount + "/" + gMaxPlayers + "명", wrCenter);

            // 참가 코드 (읽기 전용) + 코드 복사
            bool copied = Time.unscaledTime < wrCopiedUntil;
            GUI.Label(WrCode, copied ? "복사했습니다" : gSessionCode, wrCenter);
            if (LobbyButton(WrCopy, gHasSession && !string.IsNullOrEmpty(gSessionCode)))
            {
                GUIUtility.systemCopyBuffer = gSessionCode;
                wrCopiedUntil = Time.unscaledTime + 1.5f;
            }

            // 나 / 상대 + 오른쪽 칸에 준비 상태
            PlayerNamePair(out string me, out string other);
            bool twoPlayers = gTwoPlayers;
            GUI.Label(WrMeName, "나 : " + (me ?? Nickname), wrLeft);
            DrawReadyState(WrMeState, gHasFlow && gLocalReady);

            var prev = wrLeft.normal.textColor;
            if (other == null) wrLeft.normal.textColor = new Color(LbCream.r, LbCream.g, LbCream.b, 0.5f);
            GUI.Label(WrOtherName, "상대 : " + (other ?? "기다리는 중..."), wrLeft);
            wrLeft.normal.textColor = prev;
            if (other != null) DrawReadyState(WrOtherState, gHasFlow && gOtherReady);

            // 준비 완료 / 준비 취소 (상대가 들어오기 전엔 비활성) — 둘 다 준비하면 카운트다운 문구로 바뀐다
            if (gHasFlow && flow != null)
            {
                if (gCountdown >= 0)
                {
                    GUI.Label(WrReady, "모두 준비 완료!  " + Mathf.CeilToInt((float)gCountdown) + "초 후 시작합니다", wrButton);
                }
                else
                {
                    bool can = twoPlayers;
                    var c = wrButton.normal.textColor;
                    if (!can) wrButton.normal.textColor = new Color(c.r, c.g, c.b, 0.55f);
                    GUI.Label(WrReady, gLocalReady ? "준비 취소" : "준비 완료", wrButton);
                    wrButton.normal.textColor = c;
                    if (LobbyButton(WrReady, can)) flow.RequestLobbyReady(!gLocalReady);
                }
            }

            // 옵션 / 방 나가기 (글자는 이미지에 그려져 있다)
            if (LobbyButton(WrOptions, true)) CoopDemo.OptionsMenuController.Open();
            if (LobbyButton(WrLeave, !gBusy)) LeaveRoom();
            return true;
        }

        // 오른쪽 둥근 칸: 준비 완료 = 금색 글자 + 옅은 금색 바탕, 준비 중 = 흐린 글자
        void DrawReadyState(Rect r, bool isReady)
        {
            if (Event.current.type == EventType.Repaint && isReady)
                GUI.DrawTexture(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), wrReadyFill);
            var c = wrCenter.normal.textColor;
            wrCenter.normal.textColor = isReady ? WrGold : new Color(LbCream.r, LbCream.g, LbCream.b, 0.6f);
            GUI.Label(r, isReady ? "준비 완료" : "준비 중", wrCenter);
            wrCenter.normal.textColor = c;
        }

        void WaitingRoomStyles(CoopDemo.OptionsDesignTheme t)
        {
            if (wrCenter != null && wrFont == t.bodyFont && wrReadyFill != null) return;
            wrFont = t.bodyFont;
            wrCenter = new GUIStyle { font = t.bodyFont, fontSize = 26, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            wrCenter.normal.textColor = LbCream;
            wrLeft = new GUIStyle(wrCenter) { alignment = TextAnchor.MiddleLeft };
            wrButton = new GUIStyle(wrCenter) { fontSize = 29 };
            if (wrReadyFill == null) wrReadyFill = MakeTex(new Color(0.98f, 0.82f, 0.50f, 0.12f));
        }
    }
}
