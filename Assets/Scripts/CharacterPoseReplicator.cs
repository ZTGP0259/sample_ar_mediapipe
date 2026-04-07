using UnityEngine;

public class CharacterPoseReplicator : MonoBehaviour
{
    [Header("IK Targets — assign the empty GameObjects")]
    [SerializeField] private Transform rightHandTarget;
    [SerializeField] private Transform leftHandTarget;
    [SerializeField] private Transform rightElbowHint;
    [SerializeField] private Transform leftElbowHint;
    [SerializeField] private Transform headTarget;

    [Header("Character Root (for world space conversion)")]
    [SerializeField] private Transform characterRoot;

    [Header("Spine / Head bones (direct rotation)")]
    [SerializeField] private Transform spine;
    [SerializeField] private Transform head;
    [SerializeField] private Transform neck;

    [Header("Calibration — press C to calibrate T-pose")]
    [SerializeField] private KeyCode calibrateKey = KeyCode.C;

    [Header("Scale — how far IK targets move in world units")]
    [SerializeField] private float ikScale = 1.4f;

    [Header("Smoothing")]
    [SerializeField] private float smoothSpeed = 12f;
    [SerializeField] private float headSmooth  = 8f;

    // Calibration data
    private bool  _calibrated = false;
    private float _calibShoulderWidth;
    private float _calibShoulderY;
    private Vector3 _charOrigin;

    // Rest rotations
    private Quaternion _restHead, _restNeck, _restSpine;
    private bool _restCaptured = false;

    // Smoothed IK target positions
    private Vector3 _rHandSmooth, _lHandSmooth;
    private Vector3 _rElbowSmooth, _lElbowSmooth;
    private float _hTurn, _hNod, _lean;

    void LateUpdate()
    {
        if (!_restCaptured) { CaptureRest(); _restCaptured = true; return; }

        if (!_calibrated && PoseDetectionManager.IsTracking && IsGoodFrame())
            Calibrate();

        if (Input.GetKeyDown(calibrateKey) && PoseDetectionManager.IsTracking)
            Calibrate();

        if (!PoseDetectionManager.IsTracking || !_calibrated) return;

        MoveIKTargets();
        RotateHead();
        RotateSpine();
    }

    // ── CALIBRATION ──────────────────────────────────────────

    void Calibrate()
    {
        _calibShoulderWidth = Mathf.Abs(Get(12).x - Get(11).x);
        _calibShoulderY     = (Get(11).y + Get(12).y) * 0.5f;
        _charOrigin         = characterRoot != null
                            ? characterRoot.position
                            : transform.position;

        _rHandSmooth = LandmarkToWorld(Get(16));
        _lHandSmooth = LandmarkToWorld(Get(15));

        _calibrated = true;
        Debug.Log($"[PoseReplicator] Calibrated! ShoulderW={_calibShoulderWidth:F3} ShoulderY={_calibShoulderY:F3}");
    }

    // ── IK TARGET MOVEMENT ───────────────────────────────────

    void MoveIKTargets()
    {
        if (rightHandTarget != null && OK(16) && InFrame(Get(16)))
        {
            Vector3 targetWorld = LandmarkToWorld(Get(16));
            _rHandSmooth        = Vector3.Lerp(_rHandSmooth, targetWorld, Time.deltaTime * smoothSpeed);
            rightHandTarget.position = _rHandSmooth;
        }

        if (leftHandTarget != null && OK(15) && InFrame(Get(15)))
        {
            Vector3 targetWorld = LandmarkToWorld(Get(15));
            _lHandSmooth        = Vector3.Lerp(_lHandSmooth, targetWorld, Time.deltaTime * smoothSpeed);
            leftHandTarget.position = _lHandSmooth;
        }

        if (rightElbowHint != null && OK(14) && InFrame(Get(14)))
        {
            Vector3 elbowWorld  = LandmarkToWorld(Get(14));
            elbowWorld         += (characterRoot != null ? characterRoot.forward : Vector3.forward) * -0.3f;
            _rElbowSmooth       = Vector3.Lerp(_rElbowSmooth, elbowWorld, Time.deltaTime * smoothSpeed);
            rightElbowHint.position = _rElbowSmooth;
        }

        if (leftElbowHint != null && OK(13) && InFrame(Get(13)))
        {
            Vector3 elbowWorld  = LandmarkToWorld(Get(13));
            elbowWorld         += (characterRoot != null ? characterRoot.forward : Vector3.forward) * -0.3f;
            _lElbowSmooth       = Vector3.Lerp(_lElbowSmooth, elbowWorld, Time.deltaTime * smoothSpeed);
            leftElbowHint.position = _lElbowSmooth;
        }
    }

    // ── LANDMARK → WORLD SPACE CONVERSION ────────────────────

    Vector3 LandmarkToWorld(Vector2 lm)
    {
        // Flip X for front-facing camera mirror
        float nx = 1f - lm.x;
        float ny = lm.y;

        float scale  = ikScale / Mathf.Max(_calibShoulderWidth, 0.1f);
        float worldX = (nx - 0.5f) * scale * _calibShoulderWidth;
        float worldY = (_calibShoulderY - ny) * scale * _calibShoulderWidth + 1.4f;
        float worldZ = 0f;

        if (characterRoot != null)
            return characterRoot.TransformPoint(new Vector3(worldX, worldY, worldZ));

        return _charOrigin + new Vector3(worldX, worldY, worldZ);
    }

    // ── HEAD ROTATION ─────────────────────────────────────────

    void RotateHead()
    {
        if (!OK(0)) return;

        var   nose  = Get(0);
        float shCX  = (OK(11) && OK(12)) ? (Get(11).x + Get(12).x) * 0.5f : 0.5f;
        float shCY  = (OK(11) && OK(12)) ? (Get(11).y + Get(12).y) * 0.5f : _calibShoulderY;

        float turn = (shCX - nose.x) * 60f;
        float nod  = Mathf.Clamp((shCY - nose.y - 0.17f) * 40f, -25f, 20f);

        _hTurn = Mathf.Lerp(_hTurn, turn, Time.deltaTime * headSmooth);
        _hNod  = Mathf.Lerp(_hNod,  nod,  Time.deltaTime * headSmooth);

        if (neck != null)
            neck.localRotation = _restNeck * Quaternion.Euler(_hNod * 0.4f, _hTurn * 0.4f, 0f);
        if (head != null)
            head.localRotation = _restHead * Quaternion.Euler(_hNod * 0.6f, _hTurn * 0.6f, 0f);
    }

    // ── SPINE LEAN ────────────────────────────────────────────

    void RotateSpine()
    {
        if (!OK(11) || !OK(12)) return;

        float shCX  = (Get(11).x + Get(12).x) * 0.5f;
        float hipCX = (OK(23) && OK(24)) ? (Get(23).x + Get(24).x) * 0.5f : 0.5f;

        float lean  = (shCX - hipCX) * 20f;
        _lean       = Mathf.Lerp(_lean, lean, Time.deltaTime * smoothSpeed);

        if (spine != null)
            spine.localRotation = _restSpine * Quaternion.Euler(0f, 0f, -_lean);
    }

    // ── HELPERS ───────────────────────────────────────────────

    void CaptureRest()
    {
        if (spine) _restSpine = spine.localRotation;
        if (head)  _restHead  = head.localRotation;
        if (neck)  _restNeck  = neck.localRotation;
    }

    bool IsGoodFrame() =>
        OK(11) && OK(12) && OK(15) && OK(16) && _calibShoulderWidth < 0.01f;

    bool OK(int i) =>
        i < PoseDetectionManager.CurrentVisibility.Length &&
        PoseDetectionManager.CurrentVisibility[i] >= 0.4f &&
        PoseDetectionManager.CurrentPresence[i]   >= 0.4f;

    Vector2 Get(int i) => PoseDetectionManager.CurrentLandmarks[i];

    bool InFrame(Vector2 p) =>
        p.x > 0.02f && p.x < 0.98f && p.y > 0.02f && p.y < 0.98f;
}
