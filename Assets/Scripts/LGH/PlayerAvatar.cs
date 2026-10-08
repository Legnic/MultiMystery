using UnityEngine;

namespace LGH
{
    // Temporary blocky character for the network player. Swings arms and legs from how fast the body moves
    // (works for the remote player too, since only the position is synced) and tints the coat per player slot.
    public class PlayerAvatar : MonoBehaviour
    {
        public Transform legL, legR, armL, armR;
        public Renderer[] coat;
        public Color[] slotColors = { new Color(0.40f, 0.06f, 0.07f), new Color(0.08f, 0.15f, 0.34f) };
        public float swing = 32f, stepRate = 9f, holdAngle = -68f;

        Vector3 last;
        float phase, speed;
        MaterialPropertyBlock mpb;

        void OnEnable() { last = transform.position; }

        public void SetSlot(int slot)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            Color c = slotColors[Mathf.Abs(slot) % slotColors.Length];
            foreach (var r in coat)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", c);
                r.SetPropertyBlock(mpb);
            }
        }

        void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 p = transform.position, d = p - last; d.y = 0f; last = p;
            float target = Mathf.Clamp01(d.magnitude / dt / 3f);
            speed = Mathf.Lerp(speed, target, 10f * dt);
            phase += dt * stepRate * (0.4f + speed);
            float a = Mathf.Sin(phase) * swing * speed;
            if (legL) legL.localRotation = Quaternion.Euler(a, 0f, 0f);
            if (legR) legR.localRotation = Quaternion.Euler(-a, 0f, 0f);
            if (armL) armL.localRotation = Quaternion.Euler(-a * 0.8f, 0f, 0f);
            if (armR) armR.localRotation = Quaternion.Euler(holdAngle + a * 0.15f, 0f, 0f);   // right hand holds the candle
        }
    }
}
