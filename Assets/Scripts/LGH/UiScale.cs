using UnityEngine;

namespace LGH
{
    // Scales all OnGUI screens so they look the same size on any resolution.
    // UI is laid out for a virtual screen ReferenceHeight pixels tall; Apply() returns the virtual size to lay out in.
    public static class UiScale
    {
        public const float ReferenceHeight = 640f;
        public static float UserScale = 1f;   // extra multiplier if the UI should be bigger/smaller overall

        public static float Factor { get { return Mathf.Clamp(Screen.height / ReferenceHeight, 0.75f, 4f) * UserScale; } }

        public static Vector2 Apply()
        {
            float s = Factor;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            return new Vector2(Screen.width / s, Screen.height / s);
        }
    }
}
