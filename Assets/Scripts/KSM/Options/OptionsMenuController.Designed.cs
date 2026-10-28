using System.Collections.Generic;
using UnityEngine;

namespace CoopDemo
{
    // 옵션 창 — 디자이너 이미지 버전 (KJH/Options, Resources/OptionsDesignTheme).
    // 좌표는 전부 디자인 이미지(1536x864) 픽셀 기준. 로비 화면(LGH.MultiplayerMenu.DrawDesignedLobby)과 같은 방식으로
    // 이미지를 화면에 꽉 차게(넘치는 쪽은 잘림) 깔고 GUI.matrix로 같은 배율을 걸어 이미지 속 칸 위에 컨트롤을 얹는다.
    // 기능(미리보기/저장/해상도 되돌리기 타이머 등)은 예전 IMGUI 화면과 같은 필드·메서드를 그대로 쓴다.
    public partial class OptionsMenuController
    {
        const float DW = 1536f, DH = 864f;

        static readonly Rect[] DTabs =
        {
            Rect.MinMaxRect(426, 206, 642, 253), Rect.MinMaxRect(661, 206, 878, 253), Rect.MinMaxRect(896, 206, 1110, 253)
        };
        static readonly Rect DBtnDefault = Rect.MinMaxRect(427, 693, 642, 747);
        static readonly Rect DBtnSave = Rect.MinMaxRect(661, 693, 878, 747);
        static readonly Rect DBtnClose = Rect.MinMaxRect(896, 693, 1111, 747);
        static readonly Rect DToast = Rect.MinMaxRect(427, 752, 1111, 790);

        // 안쪽 칸의 다섯 줄 (가운데 높이). 구분선은 줄 사이(353, 426, 499, 572)
        static readonly float[] DRowY = { 318f, 391f, 463f, 536f, 609f };
        static readonly float[] DRowLineY = { 353f, 426f, 499f, 572f };
        const float DLabelX = 460f, DLineX0 = 458f, DLineX1 = 1078f;
        const float DTrackX0 = 677f, DTrackX1 = 992f;     // 이미지 속 슬라이더 막대
        const float DValueRight = 1078f;
        const float DCtrlX0 = 677f, DCtrlX1 = 1077f;      // 드롭다운 칸 / 오른쪽 컨트롤 영역
        static readonly Rect[] DDropBox = { Rect.MinMaxRect(677, 515, 1077, 557), Rect.MinMaxRect(677, 588, 1077, 630) };
        const float DDropItemH = 36f;
        const int DDropMaxVisible = 5;

        // 빌드에서 IMGUI 색이 진하게 나오는 문제 보정 (MeetYouGuiSkin.GuiColor 참고)
        static Color DCream => MeetYouGuiSkin.GuiColor(new Color(0.96f, 0.90f, 0.76f));
        static Color DGold => MeetYouGuiSkin.GuiColor(new Color(0.98f, 0.85f, 0.62f));
        static readonly Color DTrackColor = new Color(0.98f, 0.85f, 0.64f);   // DrawSolid에서 보정
        static Color DDim => MeetYouGuiSkin.GuiColor(new Color(0.96f, 0.90f, 0.76f, 0.45f));

        // Resources/OptionsDesignTheme (필요한 칸이 다 차 있을 때만 사용)
        static OptionsDesignTheme designTheme;
        static bool designThemeLoaded;
        static OptionsDesignTheme DesignTheme
        {
            get
            {
                if (!designThemeLoaded)
                {
                    designThemeLoaded = true;
                    designTheme = Resources.Load<OptionsDesignTheme>("OptionsDesignTheme");
                    if (designTheme != null && !designTheme.IsComplete) designTheme = null;
                }
                return designTheme;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetDesignStatics()
        {
            designTheme = null;
            designThemeLoaded = false;
        }

        int openDropdown = -1;          // -1 = 닫힘, 0 = 마이크 장치, 1 = 오디오(스피커) 장치
        float dropScroll;
        float micKnob = -1f;            // 토글 동그라미 위치 (0 = 왼쪽/꺼짐, 1 = 오른쪽/켜짐), 부드럽게 움직인다
        float fullscreenKnob = -1f;
        float toastUntil;

        GUIStyle dLabel, dNumber, dMicText, dBody, dBodyCenter, dSmall, dItem, dOutline, dHit, dArrow;
        Texture2D dSolid;
        Font dStyleNumberFont;

        struct DeviceEntry { public string name, id; }
        readonly List<DeviceEntry> deviceBuffer = new List<DeviceEntry>();

        void DesignStyles(OptionsDesignTheme t)
        {
            if (dLabel != null && dStyleNumberFont == t.numberFont && dOutline.normal.background == t.hoverOutline) return;
            dStyleNumberFont = t.numberFont;
            dLabel = new GUIStyle { font = t.numberFont, fontSize = 26, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            dLabel.normal.textColor = DCream;
            dNumber = new GUIStyle(dLabel) { fontSize = 25, alignment = TextAnchor.MiddleRight };
            dMicText = new GUIStyle(dLabel) { fontSize = 24 };
            dBody = new GUIStyle { font = t.bodyFont, fontSize = 22, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            dBody.normal.textColor = DCream;
            dBodyCenter = new GUIStyle(dBody) { alignment = TextAnchor.MiddleCenter };
            dSmall = new GUIStyle(dBody) { fontSize = 19, wordWrap = true };
            dItem = new GUIStyle(dBody) { fontSize = 21, padding = new RectOffset(16, 16, 0, 0) };
            dArrow = new GUIStyle(dBodyCenter) { fontSize = 26 };
            dOutline = new GUIStyle { border = new RectOffset(28, 28, 28, 28) };
            dOutline.normal.background = t.hoverOutline;
            dHit = new GUIStyle();
            if (dSolid == null)
            {
                dSolid = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                dSolid.SetPixel(0, 0, Color.white);
                dSolid.Apply();
            }
        }

        void DrawDesigned()
        {
            var t = DesignTheme;
            DesignStyles(t);

            float s = Mathf.Max(Screen.width / DW, Screen.height / DH);
            var offset = new Vector3((Screen.width - DW * s) * 0.5f, (Screen.height - DH * s) * 0.5f, 0f);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(s, s, 1f));

            // 펼친 장치 목록이 있으면 클릭/휠을 그 목록이 먼저 처리한다 (뒤에 깔린 버튼이 같이 눌리지 않게)
            if (openDropdown >= 0) HandleDropdownEvents();

            var e = Event.current;
            if (e.type == EventType.Repaint)
            {
                // 옆이 잘리는 화면 비율이어도 빈 곳이 보이지 않게 먼저 바탕을 깐다
                DrawSolid(new Rect(-4000, -4000, DW + 8000, DH + 8000), new Color(0.01f, 0.03f, 0.05f, 1f));
                var bg = tab == 0 ? t.audioBackground
                       : tab == 1 && t.graphicsBackground != null ? t.graphicsBackground
                       : tab == 2 && t.controlsBackground != null ? t.controlsBackground
                       : t.plainBackground;
                GUI.DrawTexture(new Rect(0, 0, DW, DH), bg);
            }

            // 탭 (선택 = 금색 테두리, 마우스 올림 = 하늘색 테두리)
            for (int i = 0; i < 3; i++)
            {
                var r = DTabs[i];
                var tex = tab == i ? t.tabSelected[i] : t.tabNormal[i];
                if (e.type == EventType.Repaint)
                    GUI.DrawTexture(new Rect(r.x - 2, 204, tex.width, tex.height), tex);
                if (DesignButton(r, tab != i))
                {
                    tab = i;
                    openDropdown = -1;
                }
            }

            switch (tab)
            {
                case 0: DrawDesignedAudio(t); break;
                case 1: DrawDesignedGraphics(t); break;
                default: DrawDesignedControls(t); break;
            }

            // 아래 버튼 (글자는 배경 이미지에 그려져 있다)
            if (DesignButton(DBtnDefault, true)) { openDropdown = -1; ApplyPreview(PlayerOptionsData.Default()); }
            if (DesignButton(DBtnSave, true))
            {
                openDropdown = -1;
                PlayerOptions.Save(staged);
                toastUntil = Time.unscaledTime + 2f;
            }
            if (DesignButton(DBtnClose, true)) { openDropdown = -1; Close(false); }

            if (Time.unscaledTime < toastUntil && e.type == EventType.Repaint)
            {
                var c = DCream;
                c.a = Mathf.Clamp01((toastUntil - Time.unscaledTime) / 0.5f);
                var prev = dBodyCenter.normal.textColor;
                dBodyCenter.normal.textColor = c;
                dBodyCenter.Draw(DToast, "설정을 저장했습니다", false, false, false, false);
                dBodyCenter.normal.textColor = prev;
            }

            // 장치 목록은 맨 위에 그린다
            if (openDropdown >= 0) DrawDropdownList();
        }

        // ---------------- 오디오 탭 ----------------
        // 줄 이름·구분선·드롭다운 칸은 배경 이미지에 그려져 있고, 움직이는 부분만 여기서 그린다.

        void DrawDesignedAudio(OptionsDesignTheme t)
        {
            // 전체 음량 / 상대방 음성: 마름모를 잡고 좌우로 끌기 (막대를 눌러도 그 자리로 옮겨진 뒤 끌 수 있다)
            float v = DesignSlider(DRowY[0], staged.masterVolume, 0f, 1f, drawTrack: false);
            if (!Mathf.Approximately(v, staged.masterVolume)) { staged.masterVolume = v; PlayerOptions.Preview(staged); }
            DrawValue(DRowY[0], Mathf.RoundToInt(staged.masterVolume * 100f).ToString());

            v = DesignSlider(DRowY[1], staged.voiceVolume, 0f, 1f, drawTrack: false);
            if (!Mathf.Approximately(v, staged.voiceVolume)) { staged.voiceVolume = v; PlayerOptions.Preview(staged); }
            DrawValue(DRowY[1], Mathf.RoundToInt(staged.voiceVolume * 100f).ToString());

            // 내 마이크: 아이콘 + 상태 글자 + 켜짐/꺼짐 토글
            bool micOn = !staged.micMuted;
            float cy = DRowY[2];
            if (Event.current.type == EventType.Repaint)
            {
                var icon = micOn ? t.micOn : t.micMuted;
                GUI.DrawTexture(new Rect(DCtrlX0, cy - icon.height / 2f, icon.width, icon.height), icon);
            }
            GUI.Label(new Rect(DCtrlX0 + t.micOn.width + 14f, cy - 20f, 260f, 40f), micOn ? "마이크 켜짐" : "마이크 꺼짐", dMicText);
            bool next = DesignToggle(cy, micOn, ref micKnob);
            if (next != micOn) { staged.micMuted = !next; PlayerOptions.Preview(staged); }

            // 마이크 장치 / 오디오 장치: 드롭다운 (펼친 목록은 DrawDesigned 끝에서 맨 위에 그린다)
            // 목록은 음성 채팅(Vivox) 연결과 상관없이 이 컴퓨터의 장치를 직접 읽는다 (SystemAudioDevices).
            for (int i = 0; i < 2; i++)
            {
                var box = DDropBox[i];
                var textRect = new Rect(box.x + 16f, box.y, box.width - 64f, box.height);
                GUI.Label(textRect, Ellipsize(CurrentDeviceLabel(i == 0), dBody, textRect.width), dBody);
                if (DesignButton(box, openDropdown != i))
                {
                    openDropdown = i;
                    dropScroll = 0f;
                    deviceRefreshAt = 0f;   // 펼칠 때마다 새로 읽는다 (그 사이 꽂거나 뺀 장치 반영)
                }
            }
        }

        // 장치 목록 캐시 (Windows 장치 조회는 매 프레임 할 필요가 없다)
        string[] micNames = System.Array.Empty<string>();
        List<string> speakerNames = new List<string>();
        string speakerDefaultName;
        float deviceRefreshAt;

        void RefreshDevicesIfNeeded()
        {
            if (Time.unscaledTime < deviceRefreshAt) return;
            deviceRefreshAt = Time.unscaledTime + 3f;
            micNames = SystemAudioDevices.Microphones();
            speakerNames = SystemAudioDevices.Speakers(out speakerDefaultName);
        }

        string CurrentDeviceLabel(bool input)
        {
            RefreshDevicesIfNeeded();
            string id = input ? staged.micDeviceId : staged.speakerDeviceId;
            if (string.IsNullOrEmpty(id))
            {
                if (input) return micNames.Length > 0 ? "기본 장치" : "연결된 마이크가 없습니다";
                return string.IsNullOrEmpty(speakerDefaultName) ? "Windows 기본 장치" : speakerDefaultName;
            }
            bool present = input ? System.Array.IndexOf(micNames, id) >= 0 : speakerNames.Contains(id);
            return present ? id : id + " (연결 안 됨)";
        }

        // 맨 위 "기본 장치"(id = 빈 문자열) + 실제 장치들(id = 장치 이름)
        List<DeviceEntry> CollectDevices(bool input, out string activeId)
        {
            RefreshDevicesIfNeeded();
            deviceBuffer.Clear();
            activeId = (input ? staged.micDeviceId : staged.speakerDeviceId) ?? "";
            deviceBuffer.Add(new DeviceEntry { name = input ? "기본 장치" : "Windows 기본 장치", id = "" });
            if (input) foreach (var n in micNames) deviceBuffer.Add(new DeviceEntry { name = n, id = n });
            else foreach (var n in speakerNames) deviceBuffer.Add(new DeviceEntry { name = n, id = n });
            return deviceBuffer;
        }

        Rect DropdownListRect(int count)
        {
            var box = DDropBox[openDropdown];
            int visible = Mathf.Clamp(count, 1, DDropMaxVisible);
            return new Rect(box.x, box.yMax + 4f, box.width, visible * DDropItemH + 8f);
        }

        void HandleDropdownEvents()
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown && e.type != EventType.ScrollWheel) return;
            bool input = openDropdown == 0;
            var list = CollectDevices(input, out _);
            var r = DropdownListRect(list.Count);
            float maxScroll = Mathf.Max(0f, list.Count * DDropItemH - (r.height - 8f));

            if (e.type == EventType.ScrollWheel)
            {
                if (r.Contains(e.mousePosition)) { dropScroll = Mathf.Clamp(dropScroll + e.delta.y * 12f, 0f, maxScroll); e.Use(); }
                return;
            }

            if (r.Contains(e.mousePosition))
            {
                int index = Mathf.FloorToInt((e.mousePosition.y - (r.y + 4f) + dropScroll) / DDropItemH);
                if (index >= 0 && index < list.Count)
                {
                    var d = list[index];
                    // 고른 장치는 옵션 값에 저장해 두고, 음성이 연결돼 있으면 OptionsManager가 바로 같은 이름의 Vivox 장치로 맞춘다
                    if (input) staged.micDeviceId = d.id;
                    else staged.speakerDeviceId = d.id;
                    PlayerOptions.Preview(staged);
                    openDropdown = -1;
                }
            }
            else
            {
                openDropdown = -1;   // 바깥(자기 칸 포함)을 누르면 접기만 하고, 그 클릭은 다른 버튼에 넘기지 않는다
            }
            e.Use();
        }

        void DrawDropdownList()
        {
            if (Event.current.type != EventType.Repaint) return;
            bool input = openDropdown == 0;
            var list = CollectDevices(input, out string activeId);
            var r = DropdownListRect(list.Count);

            // 칸 안쪽과 같은 어두운 청록 바탕 + 금색 테두리 (원래 디자인의 드롭다운 칸 색에 맞춤)
            DrawSolid(new Rect(r.x + 3f, r.y + 5f, r.width, r.height), new Color(0f, 0f, 0f, 0.45f));   // 그림자
            DrawSolid(r, new Color(0.012f, 0.078f, 0.098f, 0.98f));
            DrawFrame(r, new Color(0.86f, 0.72f, 0.50f, 1f), 1.5f);
            DrawFrame(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), new Color(0.86f, 0.72f, 0.50f, 0.25f), 1f);

            if (list.Count == 0)
            {
                GUI.Label(new Rect(r.x, r.y + 4f, r.width, DDropItemH), "  사용할 수 있는 장치가 없습니다", dItem);
                return;
            }

            var inner = new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height - 8f);
            GUI.BeginGroup(inner);
            var mouse = Event.current.mousePosition;
            for (int i = 0; i < list.Count; i++)
            {
                var row = new Rect(0f, i * DDropItemH - dropScroll, inner.width, DDropItemH);
                if (row.yMax < 0f || row.y > inner.height) continue;
                bool active = list[i].id == activeId;
                if (row.Contains(mouse)) DrawSolid(row, new Color(0.08f, 0.75f, 0.88f, 0.14f));
                else if (active) DrawSolid(row, new Color(0.98f, 0.85f, 0.62f, 0.08f));
                if (i > 0) DrawSolid(new Rect(row.x + 10f, row.y, row.width - 20f, 1f), new Color(0.86f, 0.72f, 0.50f, 0.18f));
                var prev = dItem.normal.textColor;
                dItem.normal.textColor = active ? DGold : (row.Contains(mouse) ? Color.white : DCream);
                string text = (active ? "◆  " : "     ") + list[i].name;
                dItem.Draw(row, Ellipsize(text, dItem, row.width - dItem.padding.horizontal - 6f), false, false, false, false);
                dItem.normal.textColor = prev;
            }
            GUI.EndGroup();

            // 넘치는 목록이면 오른쪽에 얇은 스크롤 표시
            float contentH = list.Count * DDropItemH;
            if (contentH > inner.height)
            {
                float barH = Mathf.Max(20f, inner.height * inner.height / contentH);
                float barY = inner.y + (inner.height - barH) * (dropScroll / (contentH - inner.height));
                DrawSolid(new Rect(inner.xMax - 4f, barY, 3f, barH), new Color(0.86f, 0.72f, 0.50f, 0.6f));
            }
        }

        // 칸보다 긴 글자는 끝을 "…"로 줄인다 (장치 이름이 긴 경우가 많다)
        static string Ellipsize(string text, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(text) || style.CalcSize(new GUIContent(text)).x <= width) return text;
            int len = text.Length;
            while (len > 1 && style.CalcSize(new GUIContent(text.Substring(0, len) + "…")).x > width) len--;
            return text.Substring(0, len).TrimEnd() + "…";
        }

        // ---------------- 그래픽 탭 ----------------

        void DrawDesignedGraphics(OptionsDesignTheme t)
        {
            // 그래픽 전용 배경이 있으면 줄 이름·구분선 3개·밝기 막대는 이미지에 그려져 있다
            bool baked = t.graphicsBackground != null;
            if (!baked) DrawRowLines(t, resolutionPending ? 4 : 3);
            else if (resolutionPending) DrawRowLine(t, 3);

            if (!baked) RowLabel(0, "전체 화면");
            GUI.Label(new Rect(DCtrlX0, DRowY[0] - 20f, 300f, 40f), staged.fullscreen ? "켜짐 (테두리 없음)" : "꺼짐 (창 모드)", dBody);
            bool fs = DesignToggle(DRowY[0], staged.fullscreen, ref fullscreenKnob);
            if (fs != staged.fullscreen) { staged.fullscreen = fs; PlayerOptions.Preview(staged); }

            if (!baked) RowLabel(1, "화면 밝기");
            float v = DesignSlider(DRowY[1], staged.brightness, -1f, 1f, drawTrack: !baked);
            if (!Mathf.Approximately(v, staged.brightness)) { staged.brightness = v; PlayerOptions.Preview(staged); }
            DrawValue(DRowY[1], Mathf.RoundToInt(Mathf.InverseLerp(-1f, 1f, staged.brightness) * 100f).ToString());

            if (!baked) RowLabel(2, "해상도");
            EnsureResolutionOptions();
            int current = CurrentResolutionIndex();
            float cy = DRowY[2];
            var left = new Rect(DCtrlX0, cy - 21f, 52f, 42f);
            var right = new Rect(DCtrlX1 - 52f, cy - 21f, 52f, 42f);
            bool canLeft = !resolutionPending && current > 0;
            bool canRight = !resolutionPending && current < resolutionOptions.Count - 1;
            ArrowLabel(left, "◀", canLeft);
            ArrowLabel(right, "▶", canRight);
            // 좌우 화살표는 하늘색 호버 테두리 없이 누르기만 받는다
            if (canLeft && GUI.Button(left, GUIContent.none, dHit)) TryChangeResolution(resolutionOptions[current - 1]);
            if (canRight && GUI.Button(right, GUIContent.none, dHit)) TryChangeResolution(resolutionOptions[current + 1]);
            GUI.Label(new Rect(left.xMax, cy - 20f, right.x - left.xMax, 40f),
                $"{resolutionOptions[current].w} x {resolutionOptions[current].h}", dBodyCenter);

            if (resolutionPending)
            {
                int secs = Mathf.CeilToInt(resolutionRevertAt - Time.unscaledTime);
                var prev = dBody.normal.textColor;
                dBody.normal.textColor = MeetYouGuiSkin.GuiColor(new Color(1f, 0.78f, 0.45f));
                GUI.Label(new Rect(DLabelX, DRowY[3] - 20f, DLineX1 - DLabelX, 40f), $"이 해상도를 유지할까요? {secs}초 후 자동으로 되돌아갑니다.", dBody);
                dBody.normal.textColor = prev;
                var keep = new Rect(DCtrlX0, DRowY[4] - 21f, 190f, 42f);
                var undo = new Rect(DCtrlX1 - 190f, DRowY[4] - 21f, 190f, 42f);
                DrawFrame(keep, new Color(0.86f, 0.72f, 0.50f, 0.8f), 1.5f);
                DrawFrame(undo, new Color(0.86f, 0.72f, 0.50f, 0.8f), 1.5f);
                GUI.Label(keep, "유지하기", dBodyCenter);
                GUI.Label(undo, "지금 되돌리기", dBodyCenter);
                if (DesignButton(keep, true)) ConfirmResolution();
                if (DesignButton(undo, true)) RevertResolution();
            }
            else
            {
                // 문장마다 한 줄씩 (줄바꿈 자동 접기 없이 3줄)
                var prevWrap = dSmall.wordWrap;
                dSmall.wordWrap = false;
                GUI.Label(new Rect(DLabelX, DRowLineY[2] + 18f, DLineX1 - DLabelX, 100f),
                    "밝기는 보기 편하게만 보정합니다.\n" +
                    "어두운 곳의 단서는 원래 보이도록 만들어져 있습니다.\n" +
                    "해상도를 바꾸면 15초 안에 [유지하기]를 눌러야 그대로 남습니다.", dSmall);
                dSmall.wordWrap = prevWrap;
            }
        }

        // ---------------- 조작 탭 (조작 + 조준점) ----------------

        void DrawDesignedControls(OptionsDesignTheme t)
        {
            if (t.controlsBackground != null) { DrawDesignedControlsBaked(); return; }
            DrawRowLines(t, 4);

            RowLabel(0, "마우스 감도");
            float v = DesignSlider(DRowY[0], staged.mouseSensitivity, 0.2f, 5f, drawTrack: true);
            if (!Mathf.Approximately(v, staged.mouseSensitivity)) { staged.mouseSensitivity = v; PlayerOptions.Preview(staged); }
            DrawValue(DRowY[0], Mathf.RoundToInt(Mathf.InverseLerp(0.2f, 5f, staged.mouseSensitivity) * 100f).ToString());

            // 조준점 모양: ◀ [미리보기] ▶ — 지금 고른 모양을 실제 크기/색 그대로 보여준다
            RowLabel(1, "조준점 모양");
            bool hasShapes = crosshairTextures != null && crosshairTextures.Length > 0;
            int shapeIndex = hasShapes ? Mathf.Clamp(staged.crosshairIndex, 0, crosshairTextures.Length - 1) : 0;
            float cy = DRowY[1];
            var left = new Rect(DCtrlX0, cy - 21f, 52f, 42f);
            var preview = new Rect(left.xMax + 12f, cy - 30f, 76f, 60f);
            var right = new Rect(preview.xMax + 12f, cy - 21f, 52f, 42f);
            bool canLeft = hasShapes && shapeIndex > 0;
            bool canRight = hasShapes && shapeIndex < crosshairTextures.Length - 1;
            ArrowLabel(left, "◀", canLeft);
            ArrowLabel(right, "▶", canRight);
            if (DesignButton(left, canLeft)) ChangeCrosshairShape(shapeIndex - 1);
            if (DesignButton(right, canRight)) ChangeCrosshairShape(shapeIndex + 1);
            if (Event.current.type == EventType.Repaint)
            {
                DrawSolid(preview, new Color(0f, 0f, 0f, 0.55f));
                DrawFrame(preview, new Color(0.86f, 0.72f, 0.50f, 0.5f), 1f);
                if (hasShapes && crosshairTextures[shapeIndex] != null)
                {
                    float size = Mathf.Min(54f, CrosshairBaseSize * staged.crosshairSize);
                    var old = GUI.color;
                    GUI.color = staged.crosshairColor;
                    GUI.DrawTexture(new Rect(preview.center.x - size / 2f, preview.center.y - size / 2f, size, size), crosshairTextures[shapeIndex]);
                    GUI.color = old;
                }
            }

            RowLabel(2, "조준점 크기");
            v = DesignSlider(DRowY[2], staged.crosshairSize, 0.5f, 2.5f, drawTrack: true);
            if (!Mathf.Approximately(v, staged.crosshairSize)) { staged.crosshairSize = v; PlayerOptions.Preview(staged); }
            DrawValue(DRowY[2], Mathf.RoundToInt(Mathf.InverseLerp(0.5f, 2.5f, staged.crosshairSize) * 100f).ToString());

            // 조준점 색상: 원형 색 견본 (선택된 색은 금색 고리)
            RowLabel(3, "조준점 색상");
            DrawCrosshairSwatches(DRowY[3]);

            RowLabel(4, "조작 키");
            var prevSize = dSmall.fontSize;
            dSmall.fontSize = 17;
            GUI.Label(new Rect(DCtrlX0, DRowLineY[3] + 6f, DLineX1 - DCtrlX0, 74f),
                "WASD 이동 · 스페이스 점프 · [E] 상호작용\n[I] 소지품 · [F] 읽기 · [G] 내려놓기\n[M] 마이크 · [ESC] 일시정지",
                dSmall);
            dSmall.fontSize = prevSize;
        }

        // 조작 탭 디자인 이미지 버전: 줄 이름·구분선·슬라이더 막대·조작 키 안내는 배경에 그려져 있다.
        // 줄 순서 = 마우스 감도 / 조준점 크기 / 조준점 색상 / 조작 키(안내 그림만, 바꾸는 기능 없음)
        void DrawDesignedControlsBaked()
        {
            float v = DesignSlider(DRowY[0], staged.mouseSensitivity, 0.2f, 5f, drawTrack: false);
            if (!Mathf.Approximately(v, staged.mouseSensitivity)) { staged.mouseSensitivity = v; PlayerOptions.Preview(staged); }
            DrawValue(DRowY[0], Mathf.RoundToInt(Mathf.InverseLerp(0.2f, 5f, staged.mouseSensitivity) * 100f).ToString());

            v = DesignSlider(DRowY[1], staged.crosshairSize, 0.5f, 2.5f, drawTrack: false);
            if (!Mathf.Approximately(v, staged.crosshairSize)) { staged.crosshairSize = v; PlayerOptions.Preview(staged); }
            DrawValue(DRowY[1], Mathf.RoundToInt(Mathf.InverseLerp(0.5f, 2.5f, staged.crosshairSize) * 100f).ToString());

            // 조준점 색상: 원형 색 견본 (기존과 동일)
            DrawCrosshairSwatches(DRowY[2]);
            // 조준점 모양 고르기는 쓰지 않는다 (저장된 모양 값은 그대로 유지)
        }

        void DrawCrosshairSwatches(float cy)
        {
            if (circleTex == null) circleTex = MakeCircle(64);
            for (int i = 0; i < CrosshairPresets.Length; i++)
            {
                var c = CrosshairPresets[i];
                bool isActive = staged.crosshairColor == c;
                var cr = new Rect(DCtrlX0 + i * 52f, cy - 20f, 40f, 40f);
                if (Event.current.type == EventType.Repaint)
                {
                    var old = GUI.color;
                    if (isActive) { GUI.color = DGold; GUI.DrawTexture(cr, circleTex); }
                    else if (cr.Contains(Event.current.mousePosition)) { GUI.color = new Color(0.55f, 0.95f, 1f, 0.55f); GUI.DrawTexture(cr, circleTex); }
                    GUI.color = c;
                    float pad = isActive ? 5f : 4f;
                    GUI.DrawTexture(new Rect(cr.x + pad, cr.y + pad, cr.width - pad * 2f, cr.height - pad * 2f), circleTex);
                    GUI.color = old;
                }
                if (GUI.Button(cr, GUIContent.none, dHit)) { staged.crosshairColor = c; PlayerOptions.Preview(staged); }
            }
        }

        // ---------------- 공용 부품 ----------------

        void RowLabel(int row, string text)
        {
            GUI.Label(new Rect(DLabelX, DRowY[row] - 22f, DCtrlX0 - DLabelX - 10f, 44f), text, dLabel);
        }

        // 위에서부터 count개의 줄 사이 구분선
        void DrawRowLines(OptionsDesignTheme t, int count)
        {
            for (int i = 0; i < count && i < DRowLineY.Length; i++) DrawRowLine(t, i);
        }

        void DrawRowLine(OptionsDesignTheme t, int i)
        {
            if (Event.current.type != EventType.Repaint) return;
            float y = DRowLineY[i];
            if (t.rowLine != null) GUI.DrawTexture(new Rect(DLineX0, y - 1f, DLineX1 - DLineX0, 3f), t.rowLine);
            else DrawSolid(new Rect(DLineX0, y, DLineX1 - DLineX0, 1f), new Color(0.5f, 0.52f, 0.48f, 1f));
        }

        void DrawValue(float cy, string text)
        {
            GUI.Label(new Rect(DValueRight - 80f, cy - 20f, 80f, 40f), text, dNumber);
        }

        void ArrowLabel(Rect r, string text, bool enabled)
        {
            var prev = dArrow.normal.textColor;
            dArrow.normal.textColor = enabled ? DCream : DDim;
            GUI.Label(r, text, dArrow);
            dArrow.normal.textColor = prev;
        }

        // 투명 버튼 + 마우스 올림 하늘색 테두리 (로비 버튼과 같은 모양)
        bool DesignButton(Rect r, bool enabled)
        {
            if (!enabled) return false;
            bool clicked = GUI.Button(r, GUIContent.none, dHit);
            if (Event.current.type == EventType.Repaint && openDropdown < 0 && r.Contains(Event.current.mousePosition))
                dOutline.Draw(new Rect(r.x - 12, r.y - 12, r.width + 24, r.height + 24), false, false, false, false);
            return clicked;
        }

        // 마름모를 잡고 좌우로 끄는 슬라이더. 막대 위 아무 곳을 눌러도 그 자리로 옮겨진 뒤 계속 끌 수 있다.
        // drawTrack: 오디오 탭은 막대가 배경 이미지에 그려져 있어 마름모만 그리고, 다른 탭은 막대도 그린다.
        float DesignSlider(float cy, float value, float min, float max, bool drawTrack)
        {
            var t = DesignTheme;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            float t01 = Mathf.InverseLerp(min, max, value);
            float x = Mathf.Lerp(DTrackX0, DTrackX1, t01);
            var thumbRect = new Rect(x - t.sliderThumb.width / 2f, cy - t.sliderThumb.height / 2f + 0.5f, t.sliderThumb.width, t.sliderThumb.height);
            var hit = new Rect(DTrackX0 - 12f, cy - 16f, DTrackX1 - DTrackX0 + 24f, 32f);
            var e = Event.current;

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && openDropdown < 0 && (hit.Contains(e.mousePosition) || thumbRect.Contains(e.mousePosition)))
                    {
                        GUIUtility.hotControl = id;
                        value = Mathf.Lerp(min, max, Mathf.InverseLerp(DTrackX0, DTrackX1, e.mousePosition.x));
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        value = Mathf.Lerp(min, max, Mathf.InverseLerp(DTrackX0, DTrackX1, e.mousePosition.x));
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
                    break;
                case EventType.Repaint:
                    if (drawTrack) DrawSolid(new Rect(DTrackX0, cy - 1f, DTrackX1 - DTrackX0, 2.5f), DTrackColor);
                    bool hot = GUIUtility.hotControl == id || (openDropdown < 0 && thumbRect.Contains(e.mousePosition));
                    var old = GUI.color;
                    if (hot) GUI.color = new Color(1.15f, 1.15f, 1.15f, 1f);
                    GUI.DrawTexture(thumbRect, t.sliderThumb);
                    GUI.color = old;
                    break;
            }
            return value;
        }

        // 켜짐/꺼짐 슬라이드 토글 (오른쪽 끝 정렬). 동그라미가 오른쪽 = 켜짐, 왼쪽 = 꺼짐. 누를 때마다 바뀐다.
        bool DesignToggle(float cy, bool on, ref float knob)
        {
            var t = DesignTheme;
            var track = new Rect(DCtrlX1 - t.toggleTrack.width, cy - t.toggleTrack.height / 2f, t.toggleTrack.width, t.toggleTrack.height);
            // 토글은 하늘색 호버 테두리 없이 누르기만 받는다 (동그라미가 움직이는 것으로 충분히 보인다)
            if (GUI.Button(track, GUIContent.none, dHit)) on = !on;
            if (Event.current.type == EventType.Repaint)
            {
                float target = on ? 1f : 0f;
                if (knob < 0f) knob = target;
                knob = Mathf.MoveTowards(knob, target, Time.unscaledDeltaTime * 8f);
                GUI.DrawTexture(track, t.toggleTrack);
                float pad = (track.height - t.toggleKnob.height) / 2f;
                float kx = Mathf.Lerp(track.x + pad + 2f, track.xMax - pad - 2f - t.toggleKnob.width, knob);
                GUI.DrawTexture(new Rect(kx, track.y + pad, t.toggleKnob.width, t.toggleKnob.height), t.toggleKnob);
            }
            return on;
        }

        void DrawSolid(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (dSolid == null)
            {
                dSolid = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                dSolid.SetPixel(0, 0, Color.white);
                dSolid.Apply();
            }
            var old = GUI.color;
            GUI.color = MeetYouGuiSkin.GuiColor(c);
            GUI.DrawTexture(r, dSolid);
            GUI.color = old;
        }

        void DrawFrame(Rect r, Color c, float w)
        {
            DrawSolid(new Rect(r.x, r.y, r.width, w), c);
            DrawSolid(new Rect(r.x, r.yMax - w, r.width, w), c);
            DrawSolid(new Rect(r.x, r.y, w, r.height), c);
            DrawSolid(new Rect(r.xMax - w, r.y, w, r.height), c);
        }
    }
}
