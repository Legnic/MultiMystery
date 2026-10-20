using UnityEngine;

namespace KSM
{
    // Procedural walk cycle for the Toy Figure rig. No Animator/animation clips -
    // each limb joint is rotated directly from a shared phase driven by how fast the
    // body is moving (position delta only, same trick as LGH's PlayerAvatar), so it
    // works for remote players too since only position is networked.
    public class ProceduralToyAnimator : MonoBehaviour
    {
        [Header("Hip joints (thigh root)")]
        public Transform leftUpLeg, rightUpLeg;
        [Header("Knee joints")]
        public Transform leftLeg, rightLeg;
        [Header("Shoulder joints (upper arm root)")]
        public Transform leftArm, rightArm;
        [Header("Elbow joints")]
        public Transform leftForeArm, rightForeArm;
        [Header("Torso (bottom to top)")]
        public Transform spine, spine1, spine2;
        [Header("Head stabilization")]
        public Transform head;

        [Header("Gait")]
        public float stepRate = 9f;
        public float hipSwing = 28f;
        public float kneeBend = 45f;
        public float armSwing = 22f;
        public float elbowBend = 18f;
        public float torsoSway = 6f;
        public float torsoBounce = 2.5f;
        public float headStabilize = 0.5f;

        [Header("Idle")]
        public float idleBreatheRate = 1.2f;
        public float idleBreatheAmount = 1.5f;

        Vector3 lastPos;
        float phase, speed;

        Quaternion leftUpLegRest, rightUpLegRest, leftLegRest, rightLegRest;
        Quaternion leftArmRest, rightArmRest, leftForeArmRest, rightForeArmRest;
        Quaternion spineRest, spine1Rest, spine2Rest, headRest;

        void OnEnable()
        {
            lastPos = transform.position;
            CaptureRest(leftUpLeg, out leftUpLegRest);
            CaptureRest(rightUpLeg, out rightUpLegRest);
            CaptureRest(leftLeg, out leftLegRest);
            CaptureRest(rightLeg, out rightLegRest);
            CaptureRest(leftArm, out leftArmRest);
            CaptureRest(rightArm, out rightArmRest);
            CaptureRest(leftForeArm, out leftForeArmRest);
            CaptureRest(rightForeArm, out rightForeArmRest);
            CaptureRest(spine, out spineRest);
            CaptureRest(spine1, out spine1Rest);
            CaptureRest(spine2, out spine2Rest);
            CaptureRest(head, out headRest);
        }

        static void CaptureRest(Transform t, out Quaternion rest) { rest = t != null ? t.localRotation : Quaternion.identity; }

        void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 p = transform.position, d = p - lastPos; d.y = 0f; lastPos = p;
            float target = Mathf.Clamp01(d.magnitude / dt / 3f);
            speed = Mathf.Lerp(speed, target, 10f * dt);
            phase += dt * stepRate * (0.4f + speed);

            float s = Mathf.Sin(phase);
            float hipL = s * hipSwing * speed;
            float hipR = -s * hipSwing * speed;
            // knee only bends on the forward half of each leg's swing (a lifted leg bends, a planted leg stays straight)
            float kneeL = Mathf.Max(0f, Mathf.Sin(phase + Mathf.PI * 0.5f)) * kneeBend * speed;
            float kneeR = Mathf.Max(0f, Mathf.Sin(phase - Mathf.PI * 0.5f)) * kneeBend * speed;

            Apply(leftUpLeg, leftUpLegRest, hipL, 0f, 0f);
            Apply(rightUpLeg, rightUpLegRest, hipR, 0f, 0f);
            Apply(leftLeg, leftLegRest, -kneeL, 0f, 0f);
            Apply(rightLeg, rightLegRest, -kneeR, 0f, 0f);

            // arms counter-swing against the opposite leg, elbows bend a touch while swinging
            float armL = -hipL * (armSwing / Mathf.Max(hipSwing, 0.01f));
            float armR = -hipR * (armSwing / Mathf.Max(hipSwing, 0.01f));
            Apply(leftArm, leftArmRest, armL, 0f, 0f);
            Apply(rightArm, rightArmRest, armR, 0f, 0f);
            Apply(leftForeArm, leftForeArmRest, Mathf.Abs(armL) * (elbowBend / Mathf.Max(armSwing, 0.01f)), 0f, 0f);
            Apply(rightForeArm, rightForeArmRest, Mathf.Abs(armR) * (elbowBend / Mathf.Max(armSwing, 0.01f)), 0f, 0f);

            // torso: side-to-side sway plus a double-time up/down bounce, both fading in with speed
            float sway = Mathf.Sin(phase) * torsoSway * speed;
            float bounceTwist = Mathf.Abs(Mathf.Sin(phase)) * torsoBounce * speed;
            Apply(spine, spineRest, 0f, 0f, sway * 0.3f);
            Apply(spine1, spine1Rest, bounceTwist, 0f, sway * 0.5f);
            Apply(spine2, spine2Rest, -bounceTwist * 0.5f, 0f, sway * 0.2f);

            // idle breathing when mostly still, head stays level against torso sway
            float idle = (1f - speed) * Mathf.Sin(Time.time * idleBreatheRate) * idleBreatheAmount;
            Apply(head, headRest, idle - bounceTwist * headStabilize, 0f, -sway * headStabilize);
        }

        static void Apply(Transform t, Quaternion rest, float x, float y, float z)
        {
            if (t == null) return;
            t.localRotation = rest * Quaternion.Euler(x, y, z);
        }
    }
}
