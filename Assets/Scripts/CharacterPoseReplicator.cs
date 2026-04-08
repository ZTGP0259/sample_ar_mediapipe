using UnityEngine;
using UnityEngine.UI;

public class CharacterPoseReplicator : MonoBehaviour
{
    [Header("IK Targets")]
    [SerializeField] private Transform rightHandTarget;
    [SerializeField] private Transform leftHandTarget;
    [SerializeField] private Transform rightElbowHint;
    [SerializeField] private Transform leftElbowHint;

    [Header("Character")]
    [SerializeField] private Transform characterRoot;
    [SerializeField] private Transform spine;
    [SerializeField] private Transform head;
    [SerializeField] private Transform neck;

    [Header("Calibrate Button")]
    [SerializeField] private Button calibrateButton;

    [Header("Tuning")]
    [SerializeField] private float ikScale             = 1.0f;
    [SerializeField] private float smoothSpeed         = 12f;
    [SerializeField] private float headSmooth          = 7f;
    // Shoulder height in character LOCAL space (above root). Tune to match your model.
    [SerializeField] private float characterShoulderHeight = 1.35f;
    // Real-world shoulder separation (metres). Drives movement scale from calibration.
    [SerializeField] private float realShoulderWidth   = 0.35f;

    // ── internal state ──────────────────────────────────────
    private bool      _restCaptured  = false;
    private bool      _calibrated    = false;

    // Calibration data
    private float     _calibShoulderWidth = 0.25f;
    private float     _calibShoulderY     = 0.35f;
    private Vector3   _charWorldOrigin;

    // Rest bone rotations
    private Quaternion _restSpine, _restHead, _restNeck;

    // IK rest world positions (arms at sides)
    private Vector3 _ikRestRight, _ikRestLeft;
    private Vector3 _ikHintRestRight, _ikHintRestLeft;

    // Smoothed positions
    private Vector3 _smoothRight, _smoothLeft;
    private Vector3 _smoothHintR, _smoothHintL;

    // Head smoothing
    private float _hTurn, _hNod, _lean;

    // ── lifecycle ───────────────────────────────────────────

    void Start()
    {
        if (calibrateButton != null)
            calibrateButton.onClick.AddListener(OnCalibratePressed);
    }

    void LateUpdate()
    {
        // Step 1: capture rest AFTER animator runs first frame
        if (!_restCaptured)
        {
            CaptureRestPose();
            return;
        }

        // Step 2: auto-calibrate once pose is first detected
        if (!_calibrated && PoseDetectionManager.IsTracking)
        {
            TryCalibrate();
        }

        // Step 3: only drive IK when calibrated + tracking
        if (!_calibrated || !PoseDetectionManager.IsTracking)
        {
            // Keep IK at rest so character doesn't collapse
            HoldRestPose();
            return;
        }

        DriveIK();
        DriveSpine();
        DriveHead();
    }

    // ── REST CAPTURE ────────────────────────────────────────

    void CaptureRestPose()
    {
        // Cache bone rest rotations
        if (spine) _restSpine = spine.localRotation;
        if (head)  _restHead  = head.localRotation;
        if (neck)  _restNeck  = neck.localRotation;

        // Get character's actual world position
        float charX = characterRoot ? characterRoot.position.x : 0f;
        float charY = characterRoot ? characterRoot.position.y : 0f;
        float charZ = characterRoot ? characterRoot.position.z : 0f;

        // OLD APPROACH (world space, direction-dependent): 
        // Arms at sides, offset from character world position
        // This breaks if character rotates or moves unexpectedly
        // _ikRestRight = new Vector3(charX + 0.22f, charY + 0.95f, charZ + 0.1f);

        // NEW APPROACH (character-local space, rotation-invariant):
        // Character has Y:180 rotation → local -Z is toward camera (in front of chest).
        // Hands z < 0 = in front of body. Hints z > 0 = behind body (natural elbow bend).
        float restY = characterShoulderHeight - 0.40f; // ≈ hip/side-hang height
        float hintY = characterShoulderHeight - 0.25f; // ≈ elbow height
        Vector3 restRightLocal = new Vector3( 0.22f, restY, -0.1f);
        Vector3 restLeftLocal  = new Vector3(-0.22f, restY, -0.1f);
        Vector3 hintRightLocal = new Vector3( 0.35f, hintY,  0.2f);
        Vector3 hintLeftLocal  = new Vector3(-0.35f, hintY,  0.2f);

        if (characterRoot)
        {
            _ikRestRight     = characterRoot.TransformPoint(restRightLocal);
            _ikRestLeft      = characterRoot.TransformPoint(restLeftLocal);
            _ikHintRestRight = characterRoot.TransformPoint(hintRightLocal);
            _ikHintRestLeft  = characterRoot.TransformPoint(hintLeftLocal);
        }
        else
        {
            // Fallback to world space if no characterRoot
            _ikRestRight     = new Vector3(charX + restRightLocal.x, charY + restRightLocal.y, charZ + restRightLocal.z);
            _ikRestLeft      = new Vector3(charX + restLeftLocal.x,  charY + restLeftLocal.y,  charZ + restLeftLocal.z);
            _ikHintRestRight = new Vector3(charX + hintRightLocal.x, charY + hintRightLocal.y, charZ + hintRightLocal.z);
            _ikHintRestLeft  = new Vector3(charX + hintLeftLocal.x,  charY + hintLeftLocal.y,  charZ + hintLeftLocal.z);
        }

        // Initialize smoothed values
        _smoothRight  = _ikRestRight;
        _smoothLeft   = _ikRestLeft;
        _smoothHintR  = _ikHintRestRight;
        _smoothHintL  = _ikHintRestLeft;

        // Place IK targets at rest
        if (rightHandTarget) rightHandTarget.position = _ikRestRight;
        if (leftHandTarget)  leftHandTarget.position  = _ikRestLeft;
        if (rightElbowHint)  rightElbowHint.position  = _ikHintRestRight;
        if (leftElbowHint)   leftElbowHint.position   = _ikHintRestLeft;

        _restCaptured = true;
        Debug.Log($"[Pose] charRoot={characterRoot.name}, pos={characterRoot.position}");
        Debug.Log($"[Pose] Rest. R={_ikRestRight} L={_ikRestLeft} Hint_R={_ikHintRestRight}");
    }

    void HoldRestPose()
    {
        // Smoothly return to rest when not tracking
        float t = Time.deltaTime * smoothSpeed;
        _smoothRight = Vector3.Lerp(_smoothRight, _ikRestRight, t);
        _smoothLeft  = Vector3.Lerp(_smoothLeft,  _ikRestLeft,  t);
        _smoothHintR = Vector3.Lerp(_smoothHintR, _ikHintRestRight, t);
        _smoothHintL = Vector3.Lerp(_smoothHintL, _ikHintRestLeft,  t);

        if (rightHandTarget) rightHandTarget.position = _smoothRight;
        if (leftHandTarget)  leftHandTarget.position  = _smoothLeft;
        if (rightElbowHint)  rightElbowHint.position  = _smoothHintR;
        if (leftElbowHint)   leftElbowHint.position   = _smoothHintL;
    }

    // ── CALIBRATION ─────────────────────────────────────────

    void TryCalibrate()
    {
        if (!OK(11) || !OK(12)) return; // need both shoulders visible

        _calibShoulderWidth = Mathf.Abs(Get(12).x - Get(11).x);
        if (_calibShoulderWidth < 0.05f) return; // too small = bad frame

        _calibShoulderY  = (Get(11).y + Get(12).y) * 0.5f;
        _charWorldOrigin = characterRoot
                         ? characterRoot.position
                         : Vector3.zero;
        _calibrated      = true;

        Debug.Log($"[Pose] Calibrated! shoulderW={_calibShoulderWidth:F3} shoulderY={_calibShoulderY:F3}");
    }

    public void OnCalibratePressed()
    {
        _calibrated = false; // force recalibrate next frame
        Debug.Log("[Pose] Manual recalibrate triggered.");
    }

    // ── IK DRIVING ──────────────────────────────────────────

    void DriveIK()
    {
        float t = Time.deltaTime * smoothSpeed;

        // RIGHT HAND
        if (rightHandTarget != null)
        {
            Vector3 target = _ikRestRight;
            if (OK(16) && InFrame(Get(16)))
                target = ToWorld(Get(16), 0f);
            _smoothRight = Vector3.Lerp(_smoothRight, target, t);
            rightHandTarget.position = _smoothRight;
        }

        // LEFT HAND
        if (leftHandTarget != null)
        {
            Vector3 target = _ikRestLeft;
            if (OK(15) && InFrame(Get(15)))
                target = ToWorld(Get(15), 0f);
            _smoothLeft = Vector3.Lerp(_smoothLeft, target, t);
            leftHandTarget.position = _smoothLeft;
        }

        // RIGHT ELBOW HINT
        if (rightElbowHint != null)
        {
            Vector3 hint = _ikHintRestRight;
            if (OK(14) && InFrame(Get(14)))
            {
                hint = ToWorld(Get(14), 0.25f); // +zOffset → behind body (local +Z)
            }
            _smoothHintR = Vector3.Lerp(_smoothHintR, hint, t);
            rightElbowHint.position = _smoothHintR;
        }

        // LEFT ELBOW HINT
        if (leftElbowHint != null)
        {
            Vector3 hint = _ikHintRestLeft;
            if (OK(13) && InFrame(Get(13)))
            {
                hint = ToWorld(Get(13), 0.25f);
            }
            _smoothHintL = Vector3.Lerp(_smoothHintL, hint, t);
            leftElbowHint.position = _smoothHintL;
        }
    }

    // ── LANDMARK → WORLD SPACE ──────────────────────────────

    Vector3 ToWorld(Vector2 lm, float zOffset)
    {
        // Front camera: flip X. Character Y:180 re-flips it → correct non-mirror tracking.
        float nx = 1f - lm.x;
        float ny = lm.y;

        // Scale derived from calibrated shoulder width — adapts to user's distance from camera.
        // realShoulderWidth / calibShoulderWidth gives world-units per screen-unit.
        float scaleXY = (realShoulderWidth * ikScale) / Mathf.Max(_calibShoulderWidth, 0.08f);

        // X: relative to screen centre
        float wx = (nx - 0.5f) * scaleXY;

        // Y: shoulders act as reference. MediaPipe Y=0 is top, Y=1 is bottom, so invert.
        float wy = (_calibShoulderY - ny) * scaleXY + characterShoulderHeight;

        // Z: character has Y:180 rotation → local -Z faces camera (in front of chest).
        // Hands use wz = -0.1 (in front). Elbow hints passed +zOffset land behind the body.
        float wz = -0.1f + zOffset;

        if (characterRoot)
            return characterRoot.TransformPoint(wx, wy, wz);
        else
            return _charWorldOrigin + new Vector3(wx, wy, wz);
    }

    // ── SPINE LEAN ──────────────────────────────────────────

    void DriveSpine()
    {
        if (spine == null || !OK(11) || !OK(12)) return;

        float shCX  = (Get(11).x + Get(12).x) * 0.5f;
        float hipCX = (OK(23) && OK(24))
                    ? (Get(23).x + Get(24).x) * 0.5f
                    : 0.5f;

        float leanTarget = (shCX - hipCX) * 22f;
        _lean = Mathf.Lerp(_lean, leanTarget, Time.deltaTime * smoothSpeed);
        spine.localRotation = _restSpine * Quaternion.Euler(0f, 0f, -_lean);
    }

    // ── HEAD ────────────────────────────────────────────────

    void DriveHead()
    {
        if (!OK(0)) return;

        var   nose  = Get(0);
        float shCX  = (OK(11) && OK(12))
                    ? (Get(11).x + Get(12).x) * 0.5f : 0.5f;
        float shCY  = (OK(11) && OK(12))
                    ? (Get(11).y + Get(12).y) * 0.5f : _calibShoulderY;

        float turn = Mathf.Clamp((shCX - nose.x) * 55f, -45f, 45f);
        float nod  = Mathf.Clamp((shCY - nose.y - 0.17f) * 38f, -25f, 20f);

        _hTurn = Mathf.Lerp(_hTurn, turn, Time.deltaTime * headSmooth);
        _hNod  = Mathf.Lerp(_hNod,  nod,  Time.deltaTime * headSmooth);

        if (neck != null)
            neck.localRotation = _restNeck
                * Quaternion.Euler(_hNod * 0.4f, _hTurn * 0.35f, 0f);
        if (head != null)
            head.localRotation = _restHead
                * Quaternion.Euler(_hNod * 0.6f, _hTurn * 0.65f, 0f);
    }

    // ── HELPERS ─────────────────────────────────────────────

    bool OK(int i) =>
        i < PoseDetectionManager.CurrentVisibility.Length &&
        PoseDetectionManager.CurrentVisibility[i] >= 0.4f &&
        PoseDetectionManager.CurrentPresence[i]   >= 0.4f;

    Vector2 Get(int i) => PoseDetectionManager.CurrentLandmarks[i];

    bool InFrame(Vector2 p) =>
        p.x > 0.02f && p.x < 0.98f && p.y > 0.02f && p.y < 0.98f;
}
