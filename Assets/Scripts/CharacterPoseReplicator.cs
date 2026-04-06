using UnityEngine;

public class CharacterPoseReplicator : MonoBehaviour
{
    [Header("Bone References")]
    [SerializeField] private Transform rightUpperArm;
    [SerializeField] private Transform leftUpperArm;
    [SerializeField] private Transform rightForeArm;
    [SerializeField] private Transform leftForeArm;
    [SerializeField] private Transform rightHand;
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform spine;
    [SerializeField] private Transform chest;      // spine_02 or spine_03
    [SerializeField] private Transform neck;
    [SerializeField] private Transform head;
    [SerializeField] private Transform leftShoulder;   // collar bone
    [SerializeField] private Transform rightShoulder;  // collar bone

    [Header("Arm Tuning")]
    [SerializeField] private float armRaiseScale    = 160f;
    [SerializeField] private float armSideScale     = 80f;
    [SerializeField] private float forearmBendScale = 130f;

    [Header("Body Tuning")]
    [SerializeField] private float leanScale        = 18f;
    [SerializeField] private float twistScale       = 12f;
    [SerializeField] private float headTurnScale    = 35f;
    [SerializeField] private float headNodScale     = 25f;
    [SerializeField] private float neckTurnScale    = 15f;
    [SerializeField] private float shoulderShrug    = 20f;

    [Header("Smoothing")]
    [SerializeField] private float smoothSpeed      = 8f;
    [SerializeField] private float deadZone         = 0.025f;

    // Rest poses
    private Quaternion _restRightArm,  _restLeftArm;
    private Quaternion _restRightFore, _restLeftFore;
    private Quaternion _restRightHand, _restLeftHand;
    private Quaternion _restSpine,     _restChest;
    private Quaternion _restNeck,      _restHead;
    private Quaternion _restRightShoulder, _restLeftShoulder;

    // Smoothed values — right arm
    private float _rRaise, _rSide, _rTwist;
    // Smoothed values — left arm
    private float _lRaise, _lSide, _lTwist;
    // Smoothed values — forearms
    private float _rElbow, _lElbow;
    // Smoothed values — body
    private float _lean, _bodyTwist;
    // Smoothed values — head/neck
    private float _hTurn, _hNod, _nTurn;
    // Smoothed values — shoulder shrug
    private float _rShrug, _lShrug;

    void Start()
    {
        Cache(rightUpperArm,   ref _restRightArm);
        Cache(leftUpperArm,    ref _restLeftArm);
        Cache(rightForeArm,    ref _restRightFore);
        Cache(leftForeArm,     ref _restLeftFore);
        Cache(rightHand,       ref _restRightHand);
        Cache(leftHand,        ref _restLeftHand);
        Cache(spine,           ref _restSpine);
        Cache(chest,           ref _restChest);
        Cache(neck,            ref _restNeck);
        Cache(head,            ref _restHead);
        Cache(rightShoulder,   ref _restRightShoulder);
        Cache(leftShoulder,    ref _restLeftShoulder);
    }

    void Cache(Transform t, ref Quaternion q)
    {
        if (t != null) q = t.localRotation;
    }

    void LateUpdate()
    {
        if (!PoseDetectionManager.IsTracking) return;
        UpdateShoulders();
        UpdateUpperArms();
        UpdateForearms();
        UpdateSpineAndChest();
        UpdateNeckAndHead();
    }

    // ── COLLAR BONES (shrug detection) ──────────────────────

    void UpdateShoulders()
    {
        bool lShOk = Visible(PDM.PoseLandmarkIndex.LeftShoulder);
        bool rShOk = Visible(PDM.PoseLandmarkIndex.RightShoulder);

        if (rightShoulder != null && rShOk)
        {
            bool lHipOk = Visible(PDM.PoseLandmarkIndex.LeftHip);
            bool rHipOk = Visible(PDM.PoseLandmarkIndex.RightHip);

            if (lHipOk && rHipOk)
            {
                float hipY      = (PDM.LeftHip.y + PDM.RightHip.y) * 0.5f;
                float rShY      = PDM.RightShoulder.y;
                // Smaller diff = shoulders raised (shrug)
                float shrug     = Mathf.Clamp((0.35f - (hipY - rShY)) * shoulderShrug, -10f, 20f);
                _rShrug         = Lerp(_rShrug, shrug);
                rightShoulder.localRotation = _restRightShoulder * Quaternion.Euler(_rShrug, 0, 0);
            }
        }

        if (leftShoulder != null && lShOk)
        {
            bool lHipOk = Visible(PDM.PoseLandmarkIndex.LeftHip);
            bool rHipOk = Visible(PDM.PoseLandmarkIndex.RightHip);

            if (lHipOk && rHipOk)
            {
                float hipY      = (PDM.LeftHip.y + PDM.RightHip.y) * 0.5f;
                float lShY      = PDM.LeftShoulder.y;
                float shrug     = Mathf.Clamp((0.35f - (hipY - lShY)) * shoulderShrug, -10f, 20f);
                _lShrug         = Lerp(_lShrug, shrug);
                leftShoulder.localRotation = _restLeftShoulder * Quaternion.Euler(_lShrug, 0, 0);
            }
        }
    }

    // ── UPPER ARMS ──────────────────────────────────────────

    void UpdateUpperArms()
    {
        // RIGHT
        if (rightUpperArm != null)
        {
            float raise = 0, side = 0, twist = 0;
            bool rSh = Visible(PDM.PoseLandmarkIndex.RightShoulder);
            bool rEl = Visible(PDM.PoseLandmarkIndex.RightElbow);

            if (rSh && rEl)
            {
                var sh = PDM.RightShoulder;
                var el = PDM.RightElbow;

                if (InFrame(el))
                {
                    // Raise: elbow Y above shoulder Y
                    float rawRaise = sh.y - el.y;
                    if (Mathf.Abs(rawRaise) > deadZone)
                        raise = Mathf.Clamp(rawRaise * armRaiseScale, 0f, 175f);

                    // Side: elbow X distance from shoulder
                    float rawSide = sh.x - el.x;
                    if (Mathf.Abs(rawSide) > deadZone)
                        side = Mathf.Clamp(rawSide * armSideScale, -70f, 70f);

                    // Twist: use wrist to determine forearm plane rotation
                    bool rWr = Visible(PDM.PoseLandmarkIndex.RightWrist);
                    if (rWr && InFrame(PDM.RightWrist))
                    {
                        float wristElbowDY = el.y - PDM.RightWrist.y;
                        float wristElbowDX = el.x - PDM.RightWrist.x;
                        twist = Mathf.Clamp(
                            Mathf.Atan2(wristElbowDY, wristElbowDX) * Mathf.Rad2Deg * 0.3f,
                            -45f, 45f);
                    }
                }
            }

            _rRaise = Lerp(_rRaise, raise);
            _rSide  = Lerp(_rSide,  side);
            _rTwist = Lerp(_rTwist, twist);

            rightUpperArm.localRotation = _restRightArm
                * Quaternion.Euler(_rSide, _rTwist, -_rRaise);
        }

        // LEFT
        if (leftUpperArm != null)
        {
            float raise = 0, side = 0, twist = 0;
            bool lSh = Visible(PDM.PoseLandmarkIndex.LeftShoulder);
            bool lEl = Visible(PDM.PoseLandmarkIndex.LeftElbow);

            if (lSh && lEl)
            {
                var sh = PDM.LeftShoulder;
                var el = PDM.LeftElbow;

                if (InFrame(el))
                {
                    float rawRaise = sh.y - el.y;
                    if (Mathf.Abs(rawRaise) > deadZone)
                        raise = Mathf.Clamp(rawRaise * armRaiseScale, 0f, 175f);

                    float rawSide = el.x - sh.x;
                    if (Mathf.Abs(rawSide) > deadZone)
                        side = Mathf.Clamp(rawSide * armSideScale, -70f, 70f);

                    bool lWr = Visible(PDM.PoseLandmarkIndex.LeftWrist);
                    if (lWr && InFrame(PDM.LeftWrist))
                    {
                        float wristElbowDY = el.y - PDM.LeftWrist.y;
                        float wristElbowDX = PDM.LeftWrist.x - el.x;
                        twist = Mathf.Clamp(
                            Mathf.Atan2(wristElbowDY, wristElbowDX) * Mathf.Rad2Deg * 0.3f,
                            -45f, 45f);
                    }
                }
            }

            _lRaise = Lerp(_lRaise, raise);
            _lSide  = Lerp(_lSide,  side);
            _lTwist = Lerp(_lTwist, twist);

            leftUpperArm.localRotation = _restLeftArm
                * Quaternion.Euler(-_lSide, -_lTwist, _lRaise);
        }
    }

    // ── FOREARMS (elbow bend) ────────────────────────────────

    void UpdateForearms()
    {
        // RIGHT forearm — elbow bend from shoulder→elbow→wrist angle
        if (rightForeArm != null)
        {
            float bend = 0;
            bool rSh = Visible(PDM.PoseLandmarkIndex.RightShoulder);
            bool rEl = Visible(PDM.PoseLandmarkIndex.RightElbow);
            bool rWr = Visible(PDM.PoseLandmarkIndex.RightWrist);

            if (rSh && rEl && rWr && InFrame(PDM.RightElbow) && InFrame(PDM.RightWrist))
            {
                // Angle at elbow between upper arm and forearm vectors
                Vector2 toShoulder = PDM.RightShoulder - PDM.RightElbow;
                Vector2 toWrist    = PDM.RightWrist    - PDM.RightElbow;
                float angle        = Vector2.Angle(toShoulder, toWrist); // 0=fully bent, 180=straight
                // Convert: 180=straight arm(0 bend), 90=right angle(90 bend), 30=fully bent
                bend = Mathf.Clamp((180f - angle) * 0.85f, 0f, 145f);
            }

            _rElbow = Lerp(_rElbow, bend);
            rightForeArm.localRotation = _restRightFore * Quaternion.Euler(0, -_rElbow, 0);
        }

        // LEFT forearm
        if (leftForeArm != null)
        {
            float bend = 0;
            bool lSh = Visible(PDM.PoseLandmarkIndex.LeftShoulder);
            bool lEl = Visible(PDM.PoseLandmarkIndex.LeftElbow);
            bool lWr = Visible(PDM.PoseLandmarkIndex.LeftWrist);

            if (lSh && lEl && lWr && InFrame(PDM.LeftElbow) && InFrame(PDM.LeftWrist))
            {
                Vector2 toShoulder = PDM.LeftShoulder - PDM.LeftElbow;
                Vector2 toWrist    = PDM.LeftWrist    - PDM.LeftElbow;
                float angle        = Vector2.Angle(toShoulder, toWrist);
                bend               = Mathf.Clamp((180f - angle) * 0.85f, 0f, 145f);
            }

            _lElbow = Lerp(_lElbow, bend);
            leftForeArm.localRotation = _restLeftFore * Quaternion.Euler(0, _lElbow, 0);
        }
    }

    // ── SPINE + CHEST ────────────────────────────────────────

    void UpdateSpineAndChest()
    {
        bool lSh = Visible(PDM.PoseLandmarkIndex.LeftShoulder);
        bool rSh = Visible(PDM.PoseLandmarkIndex.RightShoulder);
        bool lHp = Visible(PDM.PoseLandmarkIndex.LeftHip);
        bool rHp = Visible(PDM.PoseLandmarkIndex.RightHip);

        if (!lSh || !rSh) return;

        // LEAN side to side — from shoulder midpoint vs hip midpoint
        float shCX = (PDM.LeftShoulder.x + PDM.RightShoulder.x) * 0.5f;
        float hipCX = 0.5f;
        if (lHp && rHp) hipCX = (PDM.LeftHip.x + PDM.RightHip.x) * 0.5f;

        float leanRaw = (shCX - hipCX) * leanScale;
        _lean = Lerp(_lean, leanRaw);

        // TWIST — shoulder width perspective change reveals twist
        // When twisting, one shoulder appears closer (smaller X gap)
        float shWidth = Mathf.Abs(PDM.RightShoulder.x - PDM.LeftShoulder.x);
        // baseline shoulder width ~0.25. Deviation from that = twist
        float twistRaw = (0.25f - shWidth) * twistScale * 100f;
        twistRaw = Mathf.Clamp(twistRaw, -25f, 25f);
        _bodyTwist = Lerp(_bodyTwist, twistRaw);

        if (spine != null)
            spine.localRotation = _restSpine
                * Quaternion.Euler(0f, _bodyTwist * 0.4f, -_lean * 0.5f);

        if (chest != null)
            chest.localRotation = _restChest
                * Quaternion.Euler(0f, _bodyTwist * 0.6f, -_lean * 0.5f);
    }

    // ── NECK + HEAD ──────────────────────────────────────────

    void UpdateNeckAndHead()
    {
        if (!Visible(PDM.PoseLandmarkIndex.Nose)) return;

        var nose = PDM.Nose;
        bool lSh = Visible(PDM.PoseLandmarkIndex.LeftShoulder);
        bool rSh = Visible(PDM.PoseLandmarkIndex.RightShoulder);

        float shCX = 0.5f;
        float shCY = 0.35f;
        if (lSh && rSh)
        {
            shCX = (PDM.LeftShoulder.x + PDM.RightShoulder.x) * 0.5f;
            shCY = (PDM.LeftShoulder.y + PDM.RightShoulder.y) * 0.5f;
        }

        // Turn: nose X vs shoulder center X (mirrored for front cam)
        float turnRaw = (shCX - nose.x) * headTurnScale;
        turnRaw       = Mathf.Clamp(turnRaw, -50f, 50f);

        // Nod: nose Y vs shoulder Y — typical gap when looking forward ~0.2
        float nodRaw  = Mathf.Clamp((shCY - nose.y - 0.20f) * headNodScale, -30f, 25f);

        _hTurn = Lerp(_hTurn, turnRaw);
        _hNod  = Lerp(_hNod,  nodRaw);
        _nTurn = Lerp(_nTurn, turnRaw * 0.4f); // neck takes 40% of head turn

        if (neck != null)
            neck.localRotation = _restNeck
                * Quaternion.Euler(_hNod * 0.3f, _nTurn, 0f);

        if (head != null)
            head.localRotation = _restHead
                * Quaternion.Euler(_hNod * 0.7f, _hTurn * 0.6f, 0f);
    }

    // ── HELPERS ──────────────────────────────────────────────

    float Lerp(float current, float target) =>
        Mathf.Lerp(current, target, Time.deltaTime * smoothSpeed);

    bool InFrame(Vector2 p) => p.x > 0f && p.x < 1f && p.y > 0f && p.y < 1f;

    bool Visible(PoseDetectionManager.PoseLandmarkIndex idx)
    {
        int i = (int)idx;
        return PoseDetectionManager.CurrentVisibility[i] >= 0.35f
            && PoseDetectionManager.CurrentPresence[i]   >= 0.35f;
    }

    // Shorthand alias
    private static class PDM
    {
        public static Vector2 LeftShoulder  => PoseDetectionManager.LeftShoulder;
        public static Vector2 RightShoulder => PoseDetectionManager.RightShoulder;
        public static Vector2 LeftHip       => PoseDetectionManager.LeftHip;
        public static Vector2 RightHip      => PoseDetectionManager.RightHip;
        public static Vector2 LeftWrist     => PoseDetectionManager.LeftWrist;
        public static Vector2 RightWrist    => PoseDetectionManager.RightWrist;
        public static Vector2 Nose          => PoseDetectionManager.Nose;

        public static Vector2 LeftElbow =>
            PoseDetectionManager.GetLandmark(PoseLandmarkIndex.LeftElbow);
        public static Vector2 RightElbow =>
            PoseDetectionManager.GetLandmark(PoseLandmarkIndex.RightElbow);

        public static class PoseLandmarkIndex
        {
            public const PoseDetectionManager.PoseLandmarkIndex Nose = PoseDetectionManager.PoseLandmarkIndex.Nose;
            public const PoseDetectionManager.PoseLandmarkIndex LeftShoulder = PoseDetectionManager.PoseLandmarkIndex.LeftShoulder;
            public const PoseDetectionManager.PoseLandmarkIndex RightShoulder = PoseDetectionManager.PoseLandmarkIndex.RightShoulder;
            public const PoseDetectionManager.PoseLandmarkIndex LeftElbow = PoseDetectionManager.PoseLandmarkIndex.LeftElbow;
            public const PoseDetectionManager.PoseLandmarkIndex RightElbow = PoseDetectionManager.PoseLandmarkIndex.RightElbow;
            public const PoseDetectionManager.PoseLandmarkIndex LeftWrist = PoseDetectionManager.PoseLandmarkIndex.LeftWrist;
            public const PoseDetectionManager.PoseLandmarkIndex RightWrist = PoseDetectionManager.PoseLandmarkIndex.RightWrist;
            public const PoseDetectionManager.PoseLandmarkIndex LeftHip = PoseDetectionManager.PoseLandmarkIndex.LeftHip;
            public const PoseDetectionManager.PoseLandmarkIndex RightHip = PoseDetectionManager.PoseLandmarkIndex.RightHip;
        }
    }
}