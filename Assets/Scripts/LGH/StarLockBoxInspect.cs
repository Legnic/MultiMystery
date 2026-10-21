using UnityEngine;
using UnityEngine.InputSystem;

namespace LGH
{
    // Close-up view of the star chest. Press E on the chest: the camera moves in front of the lock,
    // the mouse is freed and you click the quadrants to turn their numbers, then click the OPEN button.
    // Right click / E / Q / Esc steps back again.
    public partial class StarLockBox
    {
        [Header("Close-up view")]
        [Tooltip("Where the camera looks from, in the chest's own space (front is -z).")]
        public Vector3 viewEye = new Vector3(0.04f, 0.52f, -1.05f);
        [Tooltip("Point the camera looks at, in the chest's own space.")]
        public Vector3 viewTarget = new Vector3(0.04f, 0.14f, -0.08f);
        public float viewFov = 48f;
        [Tooltip("Small warm lamp switched on while looking closely, so the lock can be read in the dark.")]
        public float viewLight = 1.4f;
        Light lamp;

        public static StarLockBox Inspecting { get; private set; }
        public bool IsInspected { get { return Inspecting == this; } }
        static int escFrame = -1;
        // true while the close-up is open (and on the frame Esc closed it) so the pause menu stays away
        public static bool BlocksPause { get { return Inspecting != null || escFrame == Time.frameCount; } }

        FirstPersonController lockedMove;
        Camera viewCam;
        Transform camParent;
        Vector3 camLocalPos, camStartPos;
        Quaternion camLocalRot, camStartRot;
        float camFov, viewT, closeAt = -1f;
        int inspectFrame;
        int hovered = -1;
        GUIStyle hintStyle, tipStyle;

        // IWorldInteractable (the chest body) - StarLockPart calls the same thing
        public bool CanInteract { get { return Inspecting == null || Inspecting == this; } }
        public string InteractPrompt { get { return IsInspected ? "[E] 상자에서 물러나기" : "[E] 별 상자 살펴보기"; } }
        public void Interact(GameObject interactor) { ToggleInspect(interactor); }

        public void ToggleInspect(GameObject interactor)
        {
            if (IsInspected) { if (Time.frameCount != inspectFrame) EndInspect(); return; }
            if (Inspecting != null || Telescope.Viewing) return;
            Camera cam = null;
            if (interactor != null)
            {
                lockedMove = interactor.GetComponentInParent<FirstPersonController>();
                if (lockedMove == null) lockedMove = interactor.GetComponentInChildren<FirstPersonController>();
                cam = interactor.GetComponentInChildren<Camera>();
            }
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            Inspecting = this;
            inspectFrame = Time.frameCount;
            closeAt = -1f;
            if (lockedMove != null) lockedMove.enabled = false;
            viewCam = cam;
            camParent = cam.transform.parent;
            camLocalPos = cam.transform.localPosition;
            camLocalRot = cam.transform.localRotation;
            camStartPos = cam.transform.position;
            camStartRot = cam.transform.rotation;
            camFov = cam.fieldOfView;
            viewT = 0f;
            if (viewLight > 0f)
            {
                var lg = new GameObject("InspectLamp");
                lg.transform.SetParent(transform, false);
                lg.transform.localPosition = viewEye + new Vector3(0.15f, 0.2f, 0.1f);
                lamp = lg.AddComponent<Light>();
                lamp.type = LightType.Point; lamp.range = 2f; lamp.intensity = viewLight; lamp.color = new Color(1f, 0.84f, 0.62f); lamp.shadows = LightShadows.None;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void EndInspect()
        {
            if (Inspecting != this) return;
            Inspecting = null;
            if (viewCam != null)
            {
                viewCam.transform.localPosition = camLocalPos;
                viewCam.transform.localRotation = camLocalRot;
                viewCam.fieldOfView = camFov;
            }
            viewCam = null;
            if (lamp != null) Destroy(lamp.gameObject);
            lamp = null;
            if (lockedMove != null) lockedMove.enabled = true;
            lockedMove = null;
            hovered = -1;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void OnDisable() { EndInspect(); }

        void InspectUpdate()
        {
            if (!IsInspected) return;
            if (viewCam == null) { EndInspect(); return; }
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null && (kb.qKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) { if (kb.escapeKey.wasPressedThisFrame) escFrame = Time.frameCount; EndInspect(); return; }
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) { EndInspect(); return; }
            if (closeAt > 0f && Time.time > closeAt) { EndInspect(); return; }
            // keep the cursor free (other scripts may lock it)
            if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

            hovered = -1;
            if (mouse != null && viewT > 0.9f)
            {
                var ray = viewCam.ScreenPointToRay(mouse.position.ReadValue());
                var hits = Physics.RaycastAll(ray, 3f, ~0, QueryTriggerInteraction.Collide);
                float best = float.MaxValue;
                foreach (var h in hits)
                {
                    var part = h.collider.GetComponent<StarLockPart>();
                    if (part != null && part.box == this && h.distance < best) { best = h.distance; hovered = part.index; }
                }
                if (hovered >= 0 && mouse.leftButton.wasPressedThisFrame && !IsOpen)
                {
                    Press(hovered);
                }
            }
            if (IsOpen && closeAt < 0f) closeAt = Time.time + 2.2f;   // let them watch the lid, then step back
        }

        void LateUpdate()
        {
            if (!IsInspected || viewCam == null) return;
            viewT = Mathf.MoveTowards(viewT, 1f, Time.deltaTime / 0.35f);
            float k = Mathf.SmoothStep(0f, 1f, viewT);
            Vector3 eye = transform.TransformPoint(viewEye);
            Vector3 tgt = transform.TransformPoint(viewTarget);
            Quaternion rot = Quaternion.LookRotation(tgt - eye, Vector3.up);
            viewCam.transform.position = Vector3.Lerp(camStartPos, eye, k);
            viewCam.transform.rotation = Quaternion.Slerp(camStartRot, rot, k);
            viewCam.fieldOfView = Mathf.Lerp(camFov, viewFov, k);
        }

        static readonly string[] QuadNames = { "I", "II", "III", "IV" };

        void OnGUI()
        {
            if (!IsInspected) return;
            GUI.depth = -50;
            var v = UiScale.Apply();
            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
                hintStyle.normal.textColor = new Color(0.85f, 0.8f, 0.7f);
                tipStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                tipStyle.normal.textColor = new Color(1f, 0.88f, 0.6f);
            }
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, v.y - 46f, v.x, 46f), Texture2D.whiteTexture);
            GUI.color = old;
            string hint = IsOpen ? "상자가 열렸다" : "칸을 클릭하면 숫자가 올라갑니다 · OPEN 버튼을 클릭해 열기 · 우클릭 / E / Esc 물러나기";
            GUI.Label(new Rect(0, v.y - 42f, v.x, 38f), hint, hintStyle);

            if (hovered >= 0 && !IsOpen && Mouse.current != null)
            {
                Vector2 mp = Mouse.current.position.ReadValue();
                float s = UiScale.Factor;
                var p = new Vector2(mp.x / s, (Screen.height - mp.y) / s);
                string tip = hovered < 4 ? QuadNames[hovered] + " 칸  (지금 " + Digit(hovered) + ")" : "OPEN";
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(p.x + 14f, p.y + 10f, 130f, 26f), Texture2D.whiteTexture);
                GUI.color = old;
                GUI.Label(new Rect(p.x + 20f, p.y + 10f, 126f, 26f), tip, tipStyle);
            }
        }
    }
}
