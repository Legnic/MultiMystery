using UnityEngine;

namespace CoopDemo
{
    // 파랑이/주황이 모델 한 개에 붙는 "움직임 담당" 컴포넌트.
    //
    //  - 걷기/뛰기/점프: 애니메이션 클립 없이 코드로 뼈를 돌린다 (절차적 애니메이션).
    //    몸이 실제로 움직인 거리(위치 변화)만 보기 때문에 네트워크로 위치만 오는 상대 플레이어에게도 똑같이 동작한다.
    //  - 상호작용(손 뻗기)은 Animator가 맡는다. Animator는 왼팔·가슴 뼈만 움직이고, 그 위에 절차적 흔들림을 덧입힌다.
    //  - 입: MouthMesh의 블렌드 셰이프 "MouthClose"(100 = 다문 입). 말하는 동안만 열었다 닫는다.
    //
    // 뼈 방향이 제각각이라 "뼈의 로컬 축"이 아니라 "캐릭터 기준 월드 축"으로 돌린다 (Pose 함수).
    [DisallowMultipleComponent]
    public class CharacterAvatarModel : MonoBehaviour
    {
        [Header("뼈 (이름으로 자동 연결)")]
        public Transform hips, spine, spine1, spine2, neck, head;
        public Transform leftUpLeg, leftLeg, leftFoot, rightUpLeg, rightLeg, rightFoot;
        public Transform leftArm, leftForeArm, rightArm, rightForeArm, rightHand, leftHand;

        [Header("손에 든 촛불 (상대에게 보이는 쪽)")]
        [Tooltip("오른손 위치 기준, 모델 좌표(오른쪽, 위, 앞)로 얼마나 띄울지 (접시 바닥이 손바닥에 오게)")]
        public Vector3 handCandleOffset = new Vector3(0.0f, 0.10f, 0.10f);
        [Tooltip("모델이 아니라 \"월드\"를 기준으로 촛불을 똑바로 세운다 (팔이 돌아가도 촛불은 기울지 않는다)")]
        public bool handCandleUpright = true;

        [Header("얼굴 앞 감정표현 손 (EmoteHand, 상대에게 보이는 쪽)")]
        [Tooltip("머리 위치 기준, 모델 좌표(오른쪽, 위, 앞)로 얼마나 띄울지")]
        public Vector3 faceItemOffset = new Vector3(0.0f, -0.05f, 0.30f);
        [Tooltip("감정표현 손이 머리 기울기를 따라갈지(false) 월드 기준으로 세울지(true)")]
        public bool faceItemUpright = false;

        [Header("왼손에 든 공용 물건 (인형·배경카드·화분 등, 상대에게 보이는 쪽)")]
        [Tooltip("왼손 위치 기준, 모델 좌표(오른쪽, 위, 앞)로 얼마나 띄울지")]
        public Vector3 handItemOffset = new Vector3(0.0f, 0.08f, 0.10f);
        [Tooltip("손에 든 물건이 손 자체 기울기를 따라갈지(false, 자연스럽게 들린 느낌) 월드 기준으로 세울지(true)")]
        public bool handItemUpright = true;   // 손 뼈 방향을 따르면 인형이 옆으로 눕는다 — 월드 기준으로 세워서 든다
        [Tooltip("왼손에 물건을 든 자세로 바뀌는 속도(초당)")]
        public float holdPoseBlendSpeed = 8f;

        [Header("웅크리기 (상대에게 보이는 쪽)")]
        [Tooltip("웅크릴 때 허벅지를 앞으로 드는 각도(도). 클수록 더 깊이 쪼그린다")]
        public float crouchTuckAngle = 62f;
        [Tooltip("웅크릴 때 무릎을 굽히는 각도(도)")]
        public float crouchKneeAngle = 105f;
        [Tooltip("웅크릴 때 골반/등을 앞으로 숙이는 각도(도)")]
        public float crouchHipsAngle = 10f;
        public float crouchSpineAngle = 14f;
        [Tooltip("웅크림 상태 전환 속도(초당)")]
        public float crouchBlendSpeed = 8f;

        [Header("그림")]
        public SkinnedMeshRenderer body;
        public Animator animator;

        [Header("걷기/뛰기")]
        [Tooltip("한 걸음(반 주기)에 걸리는 이동 거리(m)")] public float strideLength = 0.5f;
        public float legSwing = 36f;
        public float kneeBend = 38f;
        public float armSwing = 28f;
        [Tooltip("몸을 앞으로 기울이는 정도(도, 최고 속도일 때)")] public float lean = 7f;
        [Tooltip("이 속도(m/s)에서 걷기 흔들림이 최대가 된다")] public float fullSpeed = 3.2f;

        [Header("오른손 (촛불을 든 자세)")]
        public float holdArmAngle = -34f;
        public float holdForeArmAngle = -26f;

        [Header("왼손 (물건을 들었을 때 자세 — 오른손과 같은 방식)")]
        public float leftHoldArmAngle = -34f;
        public float leftHoldForeArmAngle = -26f;

        [Header("점프")]
        public float airLegAngle = -28f;
        public float airKneeAngle = 46f;
        public float airArmAngle = -62f;
        public float landCrouchSeconds = 0.16f;

        [Header("입")]
        public string mouthShapeName = "MouthClose";
        public float mouthSpeed = 14f;

        // 정지 상태에서의 뼈 회전 (프리팹에 저장된 자세 = 기준)
        struct Bone { public Transform t; public Quaternion rest; public bool animated; }
        Bone[] bones;

        Vector3 lastPos;
        float speed01, phase, airWeight, landTimer, airTimer;
        Vector3 swingAxis = Vector3.right;
        bool wasAir, grounded = true;
        float headPitch;
        int mouthIndex = -1;
        float mouthOpen;
        bool speaking;
        static readonly int InteractHash = Animator.StringToHash("Interact");

        Transform handCandleSocket;
        Transform handItemSocket;
        Transform faceItemSocket;
        bool wantCrouch;
        float crouchAmount;
        float landCrouchNow;   // 이번 프레임의 착지 굽힘(0~1)
        float footRest = float.NaN;   // 서 있는 기본 자세에서 발 뼈의 (모델 뿌리 기준) 높이 — 웅크려도 발이 이 높이에 머물게 모델을 내린다
        bool holdingLeftItem;
        float leftHoldAmount;

        // 상대 화면에서 촛불을 손에 붙인다. 촛불 오브젝트 하나를 1인칭 카메라 앞에서 손 소켓으로 옮긴다.
        public void AttachHandCandle(Transform candle)
        {
            if (candle == null || rightHand == null) return;
            if (handCandleSocket == null)
            {
                var go = new GameObject("HandCandleSocket");
                handCandleSocket = go.transform;
                handCandleSocket.SetParent(transform, false);
            }
            // 모델이 1.5배로 커져 있어도 촛불은 원래 크기(1인칭 때와 같은 크기)로 보이게 소켓을 되돌려 놓는다
            float s = Mathf.Max(0.0001f, transform.lossyScale.x);
            handCandleSocket.localScale = Vector3.one / s;
            candle.SetParent(handCandleSocket, false);
            candle.localPosition = Vector3.zero;
            candle.localRotation = Quaternion.identity;
            UpdateHandCandle();
        }

        void UpdateHandCandle()
        {
            if (handCandleSocket == null || rightHand == null) return;
            handCandleSocket.position = rightHand.position + transform.TransformVector(handCandleOffset);
            handCandleSocket.rotation = handCandleUpright ? Quaternion.Euler(0f, transform.eulerAngles.y, 0f) : transform.rotation;
        }

        // 공용 들기 시스템(HoldableItem)이 왼손에 든 물건을 상대 화면에서 왼손에 붙인다 (HoldableNetworkSync가 호출).
        // AttachHandCandle과 같은 패턴 — 들고 있는 동안 걷기 팔 흔들림을 그대로 따라가서 "손이 움직이는" 게 보인다.
        public void AttachLeftHandItem(Transform item)
        {
            if (item == null || leftHand == null) return;
            if (handItemSocket == null)
            {
                var go = new GameObject("LeftHandItemSocket");
                handItemSocket = go.transform;
                handItemSocket.SetParent(transform, false);
            }
            float s = Mathf.Max(0.0001f, transform.lossyScale.x);
            handItemSocket.localScale = Vector3.one / s;
            item.SetParent(handItemSocket, false);
            item.localPosition = Vector3.zero;
            item.localRotation = Quaternion.identity;
            holdingLeftItem = true;   // 오른팔처럼 왼팔도 "드는 자세"로 굽힌다 (차렷 자세로 손목에 박혀 보이는 문제 방지)
            UpdateHandItem();
        }

        // 왼손이 비면 다시 평소 걷기 흔들림으로 돌아간다 (HoldableNetworkSync가 놓을 때 호출)
        public void DetachLeftHandItem()
        {
            holdingLeftItem = false;
        }

        // 이 모델이 사라지기 직전(상대가 나감 등) 왼손에 붙여 둔 물건을 떼어 낸다 — 안 그러면 모델과 같이 파괴되어
        // 그 물건(GIM-002 인형·열쇠, 다락 황금상 등)이 남은 사람 화면에서 영영 사라진다. 제자리 복귀는 HoldableNetworkSync가 한다.
        public void ReleaseLeftHandItems()
        {
            holdingLeftItem = false;
            if (handItemSocket == null) return;
            for (int i = handItemSocket.childCount - 1; i >= 0; i--)
                handItemSocket.GetChild(i).SetParent(null, true);
        }

        void UpdateHandItem()
        {
            if (handItemSocket == null || leftHand == null) return;
            handItemSocket.position = leftHand.position + transform.TransformVector(handItemOffset);
            handItemSocket.rotation = handItemUpright ? Quaternion.Euler(0f, transform.eulerAngles.y, 0f) : leftHand.rotation;
        }

        // 촛불 잡느라 바쁜 오른손 대신 쓰는 감정표현 손(EmoteHand)을 상대 화면에서 얼굴 앞에 붙인다.
        // AttachHandCandle과 같은 패턴 - 같은 오브젝트를 그대로 옮겨 붙이므로 모양이 바뀔 때마다 다시 할 필요가 없다.
        public void AttachFaceItem(Transform item)
        {
            if (item == null || head == null) return;
            if (faceItemSocket == null)
            {
                var go = new GameObject("FaceItemSocket");
                faceItemSocket = go.transform;
                faceItemSocket.SetParent(transform, false);
            }
            float s = Mathf.Max(0.0001f, transform.lossyScale.x);
            faceItemSocket.localScale = Vector3.one / s;
            item.SetParent(faceItemSocket, false);
            item.localPosition = Vector3.zero;
            item.localRotation = Quaternion.identity;
            UpdateFaceItem();
        }

        void UpdateFaceItem()
        {
            if (faceItemSocket == null || head == null) return;
            faceItemSocket.position = head.position + transform.TransformVector(faceItemOffset);
            faceItemSocket.rotation = faceItemUpright ? Quaternion.Euler(0f, transform.eulerAngles.y, 0f) : head.rotation;
        }

        // ---- 바깥에서 부르는 것 ----
        public void SetSpeaking(bool value) { speaking = value; }
        public void SetHeadPitch(float degrees) { headPitch = Mathf.Clamp(degrees, -50f, 50f); }
        public void SetCrouching(bool value) { wantCrouch = value; }
        public void TriggerInteract() { if (animator != null && animator.isActiveAndEnabled) animator.SetTrigger(InteractHash); }

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (body == null) body = GetComponentInChildren<SkinnedMeshRenderer>(true);
            AutoBind();
            CaptureRest();
            CaptureFootRest();
            if (body != null && body.sharedMesh != null) mouthIndex = body.sharedMesh.GetBlendShapeIndex(mouthShapeName);
            if (animator != null) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            lastPos = transform.position;
        }

        void OnEnable() { lastPos = transform.position; }

        // 컴포넌트를 붙일 때/빌더가 호출: 이름으로 뼈를 찾는다
        public void AutoBind()
        {
            Transform Find(string n)
            {
                foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
                return null;
            }
            hips = hips ? hips : Find("Hips");
            spine = spine ? spine : Find("Spine");
            spine1 = spine1 ? spine1 : Find("Spine1");
            spine2 = spine2 ? spine2 : Find("Spine2");
            neck = neck ? neck : Find("Neck");
            head = head ? head : Find("Head");
            leftUpLeg = leftUpLeg ? leftUpLeg : Find("LeftUpLeg");
            leftLeg = leftLeg ? leftLeg : Find("LeftLeg");
            leftFoot = leftFoot ? leftFoot : Find("LeftFoot");
            rightUpLeg = rightUpLeg ? rightUpLeg : Find("RightUpLeg");
            rightLeg = rightLeg ? rightLeg : Find("RightLeg");
            rightFoot = rightFoot ? rightFoot : Find("RightFoot");
            leftArm = leftArm ? leftArm : Find("LeftArm");
            leftForeArm = leftForeArm ? leftForeArm : Find("LeftForeArm");
            rightArm = rightArm ? rightArm : Find("RightArm");
            rightForeArm = rightForeArm ? rightForeArm : Find("RightForeArm");
            rightHand = rightHand ? rightHand : Find("RightHand");
            leftHand = leftHand ? leftHand : Find("LeftHand");
        }

        void CaptureRest()
        {
            // Animator가 직접 움직이는 뼈: 이 뼈들은 "Animator가 쓴 값"에서 시작해 흔들림을 얹는다.
            var list = new System.Collections.Generic.List<Bone>();
            bool hasAnimator = animator != null && animator.runtimeAnimatorController != null;
            void Add(Transform t, bool animated) { if (t != null) list.Add(new Bone { t = t, rest = t.localRotation, animated = animated && hasAnimator }); }
            Add(hips, false); Add(spine, false); Add(spine1, true); Add(spine2, false); Add(neck, false); Add(head, false);
            Add(leftUpLeg, false); Add(leftLeg, false); Add(leftFoot, false);
            Add(rightUpLeg, false); Add(rightLeg, false); Add(rightFoot, false);
            // 상호작용은 왼팔로 한다 (오른팔은 계속 촛불을 들고 있다)
            Add(leftArm, true); Add(leftForeArm, true);
            Add(rightArm, false); Add(rightForeArm, false);
            bones = list.ToArray();
        }

        // 발 뼈의 가장 낮은 높이(모델 뿌리 기준). 기본 자세 그대로일 때 한 번만 잰다.
        float LowestFootLocalY()
        {
            if (leftFoot == null && rightFoot == null) return float.NaN;
            float y = float.MaxValue;
            if (leftFoot != null) y = Mathf.Min(y, leftFoot.position.y);
            if (rightFoot != null) y = Mathf.Min(y, rightFoot.position.y);
            // 모델은 만들어진 뒤에 1.5배 같은 배율이 씌워지므로, 배율로 나눠서 "모델 자체 크기 기준" 값으로 저장/비교한다
            return (y - transform.position.y) / Mathf.Max(0.0001f, transform.lossyScale.y);
        }

        void CaptureFootRest() { footRest = LowestFootLocalY(); }

        Quaternion BaseOf(Transform t)
        {
            foreach (var b in bones) if (b.t == t) return b.animated ? t.localRotation : b.rest;
            return t.localRotation;
        }

        // 뼈를 "월드 축 기준"으로 angle도 돌린다. base는 돌리기 전의 로컬 회전.
        static Quaternion Rot(Transform bone, Quaternion baseLocal, Vector3 axisWorld, float angle)
        {
            Quaternion pw = bone.parent != null ? bone.parent.rotation : Quaternion.identity;
            return Quaternion.Inverse(pw) * Quaternion.AngleAxis(angle, axisWorld) * pw * baseLocal;
        }

        // 한 뼈에 여러 회전을 차례로 얹는다 (위 뼈부터 호출해야 아래 뼈가 따라온다)
        void Pose(Transform bone, params (Vector3 axis, float angle)[] ops)
        {
            if (bone == null) return;
            Quaternion q = BaseOf(bone);
            bone.localRotation = q;   // 기준 자세로 먼저 되돌린다 (뼈마다 부모의 최신 회전을 쓰기 위해)
            foreach (var op in ops)
            {
                if (Mathf.Abs(op.angle) < 0.01f) continue;
                q = Rot(bone, q, op.axis, op.angle);
                bone.localRotation = q;
            }
        }

        void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            crouchAmount = Mathf.MoveTowards(crouchAmount, wantCrouch ? 1f : 0f, crouchBlendSpeed * dt);
            leftHoldAmount = Mathf.MoveTowards(leftHoldAmount, holdingLeftItem ? 1f : 0f, holdPoseBlendSpeed * dt);
            UpdateMotionState(dt);
            transform.localPosition = Vector3.zero;
            ApplyPose();
            GroundFeet();
            UpdateHandCandle();   // 뼈를 다 돌린 뒤에 손 위치를 읽는다
            UpdateHandItem();
            UpdateFaceItem();
            UpdateMouth(dt);
        }

        // 다리를 굽히면 골반은 그대로인 채 발이 위로 끌려 올라와 공중에 뜬다. 그만큼만 모델을 내려서 발을 바닥에 붙인다.
        // (고정 값으로 내리면 다리가 덜 굽혀졌을 때 발이 바닥 아래로 가라앉는다 — 굽힌 정도에서 바로 계산한다)
        // 웅크림/착지 굽힘에만 적용한다. 점프 중에는 일부러 발을 들어 올리므로 건드리지 않는다.
        void GroundFeet()
        {
            if (float.IsNaN(footRest)) return;
            float weight = Mathf.Max(crouchAmount, landCrouchNow) * (1f - airWeight);
            if (weight <= 0.0001f) return;
            float lift = (LowestFootLocalY() - footRest) * transform.lossyScale.y;   // 발이 기본 높이보다 얼마나 올라왔나(월드 m)
            if (lift <= 0f) return;
            Transform p = transform.parent;
            float parentScaleY = p != null ? Mathf.Max(0.0001f, p.lossyScale.y) : 1f;
            transform.localPosition = new Vector3(0f, -lift * weight / parentScaleY, 0f);
        }

        void UpdateMotionState(float dt)
        {
            Vector3 p = transform.position;
            Vector3 delta = p - lastPos;
            lastPos = p;
            if (delta.sqrMagnitude > 25f) delta = Vector3.zero;   // 순간이동(스폰/리스폰)은 걸은 것으로 치지 않는다

            Vector3 horiz = new Vector3(delta.x, 0f, delta.z);
            float speed = horiz.magnitude / dt;
            float target = Mathf.Clamp01(speed / fullSpeed);
            speed01 = Mathf.MoveTowards(speed01, target, 8f * dt);

            if (speed > 0.15f)
            {
                var axis = Vector3.Cross(Vector3.up, horiz.normalized);
                if (axis.sqrMagnitude > 0.01f) swingAxis = Vector3.Slerp(swingAxis, axis.normalized, 1f - Mathf.Exp(-12f * dt));
                phase += (horiz.magnitude / Mathf.Max(0.05f, strideLength)) * Mathf.PI;
            }

            // 땅에 닿아 있는가 (발 아래로 짧게 쏜다)
            grounded = Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, 0.7f, ~0, QueryTriggerInteraction.Ignore);
            airTimer = grounded ? 0f : airTimer + dt;
            bool air = airTimer > 0.07f;
            if (wasAir && !air) landTimer = landCrouchSeconds;
            wasAir = air;
            landTimer = Mathf.Max(0f, landTimer - dt);
            airWeight = Mathf.MoveTowards(airWeight, air ? 1f : 0f, 7f * dt);
        }

        void ApplyPose()
        {
            Vector3 right = transform.right;
            Vector3 up = Vector3.up;
            float amp = speed01;
            float s = Mathf.Sin(phase);
            float c = Mathf.Cos(phase);
            float ground = 1f - airWeight;
            float landCrouch = landTimer > 0f ? Mathf.Sin(landTimer / landCrouchSeconds * Mathf.PI) : 0f;
            landCrouchNow = landCrouch;
            // 착지 순간의 짧은 굽힘(가볍게)과 지속적인 웅크림(깊게)은 각도만 다르고 같은 공식을 쓴다 — 둘 중 더 굽은 쪽을 따른다
            float hipsFlex = Mathf.Max(6f * landCrouch, crouchHipsAngle * crouchAmount);
            float spineFlex = Mathf.Max(8f * landCrouch, crouchSpineAngle * crouchAmount);
            float thighFlex = Mathf.Max(26f * landCrouch, crouchTuckAngle * crouchAmount);
            float kneeFlex = Mathf.Max(40f * landCrouch, crouchKneeAngle * crouchAmount);
            float breathe = Mathf.Sin(Time.time * 1.7f) * 1.1f * (1f - amp);

            // 몸통: 살짝 앞으로 기울고, 서 있을 땐 숨쉬듯 흔들린다
            Pose(hips, (swingAxis, lean * 0.4f * amp * ground), (right, hipsFlex));
            Pose(spine, (swingAxis, lean * 0.6f * amp * ground + breathe), (right, spineFlex));
            Pose(spine1, (swingAxis, lean * 0.3f * amp * ground));
            Pose(spine2);
            Pose(neck);
            // 고개: 카메라가 보는 위아래를 따라간다 (상대 플레이어가 어디를 보는지 보인다)
            Pose(head, (right, headPitch * 0.7f - lean * 0.5f * amp * ground));

            // 팔: 왼팔은 다리 반대로 흔들고(물건을 들고 있으면 오른팔처럼 드는 자세로 굽혀서 조금만 흔든다), 오른팔은 촛불을 든 채로 조금만 흔든다
            float armAmp = armSwing * amp * ground;
            float leftArmAmp = Mathf.Lerp(armAmp, armAmp * 0.35f, leftHoldAmount);
            Pose(leftArm, (swingAxis, s * leftArmAmp), (right, airArmAngle * airWeight + leftHoldArmAngle * leftHoldAmount * ground));
            Pose(leftForeArm, (right, -8f * amp * ground * (1f - leftHoldAmount) + airArmAngle * 0.3f * airWeight + leftHoldForeArmAngle * leftHoldAmount * ground));
            Pose(rightArm, (swingAxis, -s * armAmp * 0.35f), (right, holdArmAngle * ground + airArmAngle * airWeight));
            Pose(rightForeArm, (right, holdForeArmAngle * ground + airArmAngle * 0.3f * airWeight));

            // 다리: 걸을 때 번갈아 앞뒤로, 점프 중엔 웅크려 모은다
            float legAmp = legSwing * amp * ground;
            float tuck = airLegAngle * airWeight - thighFlex;
            float knee = airKneeAngle * airWeight + kneeFlex;
            float lUp = -s * legAmp;           // 앞으로 나갈 때 음수
            float rUp = s * legAmp;
            float lKnee = Mathf.Max(0f, c) * kneeBend * amp * ground + knee;
            float rKnee = Mathf.Max(0f, -c) * kneeBend * amp * ground + knee;
            Pose(leftUpLeg, (swingAxis, lUp), (right, tuck));
            Pose(rightUpLeg, (swingAxis, rUp), (right, tuck));
            Pose(leftLeg, (right, lKnee));
            Pose(rightLeg, (right, rKnee));
            // 발은 다리가 꺾인 만큼 반대로 꺾어서 바닥과 평행에 가깝게 둔다
            Pose(leftFoot, (right, -(tuck + lKnee * 0.5f) * 0.6f));
            Pose(rightFoot, (right, -(tuck + rKnee * 0.5f) * 0.6f));
        }

        void UpdateMouth(float dt)
        {
            if (body == null || mouthIndex < 0) return;
            float target = 0f;
            if (speaking)
            {
                // 규칙적으로 벌렸다 닫는 대신 두 박자를 섞어 사람 말처럼 불규칙하게
                float t = Time.time;
                target = 0.25f + 0.75f * Mathf.Abs(Mathf.Sin(t * 11f) * (0.55f + 0.45f * Mathf.Sin(t * 4.7f + 1f)));
            }
            mouthOpen = Mathf.MoveTowards(mouthOpen, target, mouthSpeed * dt);
            body.SetBlendShapeWeight(mouthIndex, 100f * (1f - mouthOpen));   // 100 = 다문 입
        }
    }
}
