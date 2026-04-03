// using UnityEngine;

// public class CharacterPoseReplicator : MonoBehaviour
// {
//     [Header("Animator")]
//     [SerializeField] private Animator animator;

//     [Header("Bone References")]
//     [SerializeField] private Transform rightUpperArm;
//     [SerializeField] private Transform leftUpperArm;
//     [SerializeField] private Transform rightForeArm;
//     [SerializeField] private Transform leftForeArm;
//     [SerializeField] private Transform spine;
//     [SerializeField] private Transform head;

//     [Header("Arm Settings")]
//     [SerializeField] private float armRaiseScale = 150f;
//     [SerializeField] private float armForwardScale = 80f;
//     [SerializeField] private Vector3 rightArmRaiseAxis = new Vector3(0, 0, 1);
//     [SerializeField] private Vector3 rightArmForwardAxis = new Vector3(1, 0, 0);
//     [SerializeField] private Vector3 leftArmRaiseAxis = new Vector3(0, 0, -1);
//     [SerializeField] private Vector3 leftArmForwardAxis = new Vector3(1, 0, 0);

//     [Header("Body Settings")]
//     [SerializeField] private float leanScale = 25f;
//     [SerializeField] private float headTurnScale = 40f;
//     [SerializeField] private float headNodScale = 30f;

//     [Header("Smoothing")]
//     [SerializeField] private float smoothing = 10f;
//     [SerializeField, Range(0f, 1f)] private float visibilityThreshold = 0.3f;

//     // Original rotations
//     private Quaternion _origRight, _origLeft, _origRightFore, _origLeftFore;
//     private Quaternion _origSpine, _origHead;

//     // Smoothed values
//     private float _sRightRaise, _sRightFwd;
//     private float _sLeftRaise,  _sLeftFwd;
//     private float _sLean, _sHeadTurn, _sHeadNod;

//     // Previous shoulder center for head tracking
//     private float _prevShoulderCenterX = 0.5f;

//     void Start()
//     {
//         if (rightUpperArm) _origRight     = rightUpperArm.localRotation;
//         if (leftUpperArm)  _origLeft      = leftUpperArm.localRotation;
//         if (rightForeArm)  _origRightFore = rightForeArm.localRotation;
//         if (leftForeArm)   _origLeftFore  = leftForeArm.localRotation;
//         if (spine)         _origSpine     = spine.localRotation;
//         if (head)          _origHead      = head.localRotation;
//     }

//     void LateUpdate()
//     {
//         if (!PoseDetectionManager.IsTracking) return;

//         ApplyRightArm();
//         ApplyLeftArm();
//         ApplySpine();
//         ApplyHead();
//     }

//     void ApplyRightArm()
//     {
//         if (!rightUpperArm) return;

//         bool wristOk    = IsReliable(PoseDetectionManager.PoseLandmarkIndex.RightWrist);
//         bool shoulderOk = IsReliable(PoseDetectionManager.PoseLandmarkIndex.RightShoulder);
//         if (!shoulderOk) return;

//         float shoulderY = PoseDetectionManager.RightShoulder.y;
//         float shoulderX = PoseDetectionManager.RightShoulder.x;

//         // Raise: how high wrist is above shoulder (Y axis, inverted in MediaPipe)
//         float raiseTarget = 0f;
//         float fwdTarget   = 0f;

//         if (wristOk)
//         {
//             float wristY = PoseDetectionManager.RightWrist.y;
//             float wristX = PoseDetectionManager.RightWrist.x;

//             // Clamp wrist to valid range
//             if (wristX >= 0f && wristX <= 1f && wristY >= 0f && wristY <= 1f)
//             {
//                 raiseTarget = Mathf.Clamp01(shoulderY - wristY) * armRaiseScale;
//                 // Forward: wrist moves toward center of body = arm goes forward
//                 float lateralDiff = shoulderX - wristX; // positive = wrist crossed toward center
//                 fwdTarget = Mathf.Clamp(lateralDiff * armForwardScale, 0f, 90f);
//             }
//         }

//         _sRightRaise = Mathf.Lerp(_sRightRaise, raiseTarget, Time.deltaTime * smoothing);
//         _sRightFwd   = Mathf.Lerp(_sRightFwd,   fwdTarget,   Time.deltaTime * smoothing);

//         rightUpperArm.localRotation = _origRight
//             * Quaternion.AngleAxis(_sRightRaise, rightArmRaiseAxis)
//             * Quaternion.AngleAxis(_sRightFwd,   rightArmForwardAxis);
//     }

//     void ApplyLeftArm()
//     {
//         if (!leftUpperArm) return;

//         bool wristOk    = IsReliable(PoseDetectionManager.PoseLandmarkIndex.LeftWrist);
//         bool shoulderOk = IsReliable(PoseDetectionManager.PoseLandmarkIndex.LeftShoulder);
//         if (!shoulderOk) return;

//         float shoulderY = PoseDetectionManager.LeftShoulder.y;
//         float shoulderX = PoseDetectionManager.LeftShoulder.x;

//         float raiseTarget = 0f;
//         float fwdTarget   = 0f;

//         if (wristOk)
//         {
//             float wristY = PoseDetectionManager.LeftWrist.y;
//             float wristX = PoseDetectionManager.LeftWrist.x;

//             if (wristX >= 0f && wristX <= 1f && wristY >= 0f && wristY <= 1f)
//             {
//                 raiseTarget = Mathf.Clamp01(shoulderY - wristY) * armRaiseScale;
//                 float lateralDiff = wristX - shoulderX;
//                 fwdTarget = Mathf.Clamp(lateralDiff * armForwardScale, 0f, 90f);
//             }
//         }

//         _sLeftRaise = Mathf.Lerp(_sLeftRaise, raiseTarget, Time.deltaTime * smoothing);
//         _sLeftFwd   = Mathf.Lerp(_sLeftFwd,   fwdTarget,   Time.deltaTime * smoothing);

//         leftUpperArm.localRotation = _origLeft
//             * Quaternion.AngleAxis(_sLeftRaise, leftArmRaiseAxis)
//             * Quaternion.AngleAxis(_sLeftFwd,   leftArmForwardAxis);
//     }

//     void ApplySpine()
//     {
//         if (!spine) return;
//         if (!PoseDetectionManager.TryGetBodyCenter(out var bodyCenter)) return;

//         float leanTarget = (bodyCenter.x - 0.5f) * leanScale;
//         _sLean = Mathf.Lerp(_sLean, leanTarget, Time.deltaTime * smoothing);

//         spine.localRotation = _origSpine * Quaternion.Euler(0, 0, -_sLean);
//     }

//     void ApplyHead()
//     {
//         if (!head) return;

//         // Head turn: follow nose X position relative to shoulder center
//         bool noseOk = IsReliable(PoseDetectionManager.PoseLandmarkIndex.Nose);
//         if (!noseOk) return;

//         float noseX = PoseDetectionManager.Nose.x;
//         float noseY = PoseDetectionManager.Nose.y;

//         // Turn: nose shifts left/right of center
//         float turnTarget = (0.5f - noseX) * headTurnScale;  // mirror for front cam

//         // Nod: nose Y relative to shoulders
//         float nodTarget = 0f;
//         bool shoulderLOk = IsReliable(PoseDetectionManager.PoseLandmarkIndex.LeftShoulder);
//         bool shoulderROk = IsReliable(PoseDetectionManager.PoseLandmarkIndex.RightShoulder);
//         if (shoulderLOk && shoulderROk)
//         {
//             float shoulderY = (PoseDetectionManager.LeftShoulder.y + PoseDetectionManager.RightShoulder.y) * 0.5f;
//             // noseY < shoulderY means nose is above shoulders (looking up in MediaPipe coords)
//             nodTarget = Mathf.Clamp((shoulderY - noseY - 0.15f) * headNodScale, -30f, 20f);
//         }

//         _sHeadTurn = Mathf.Lerp(_sHeadTurn, turnTarget, Time.deltaTime * smoothing);
//         _sHeadNod  = Mathf.Lerp(_sHeadNod,  nodTarget,  Time.deltaTime * smoothing);

//         head.localRotation = _origHead * Quaternion.Euler(_sHeadNod, _sHeadTurn, 0);
//     }

//     bool IsReliable(PoseDetectionManager.PoseLandmarkIndex index)
//     {
//         if (Instance == null) return false;
//         int i = (int)index;
//         return PoseDetectionManager.CurrentVisibility[i] >= visibilityThreshold &&
//                PoseDetectionManager.CurrentPresence[i]  >= visibilityThreshold;
//     }

//     // Shortcut so we don't need Instance reference from PoseDetectionManager
//     private PoseDetectionManager Instance => PoseDetectionManager.Instance;
// }


using UnityEngine;

public class CharacterPoseReplicator : MonoBehaviour
{
    [Header("Bone References")]
    [SerializeField] private Transform rightUpperArm;
    [SerializeField] private Transform leftUpperArm;
    [SerializeField] private Transform spine;
    [SerializeField] private Transform head;

    [Header("Arm Settings")]
    [SerializeField] private float armRaiseScale = 90f;
    [SerializeField] private float armForwardScale = 50f;

    [Header("Body Settings")]
    [SerializeField] private float leanScale = 25f;
    [SerializeField] private float headTurnScale = 120f;
    [SerializeField] private float headNodScale = 80f;

    [Header("Smoothing")]
    [SerializeField] private float smoothing = 5f;
    [SerializeField, Range(0f, 1f)] private float visibilityThreshold = 0.3f;

    private Quaternion _origRight, _origLeft, _origSpine, _origHead;

    void Start()
    {
        if (rightUpperArm) _origRight = rightUpperArm.localRotation;
        if (leftUpperArm) _origLeft = leftUpperArm.localRotation;
        if (spine) _origSpine = spine.localRotation;
        if (head) _origHead = head.localRotation;
    }

    void LateUpdate()
    {
        if (!PoseDetectionManager.IsTracking) return;

        ApplyRightArm();
        ApplyLeftArm();
        ApplySpine();
        ApplyHead();
    }

    // ================= ARM LOGIC =================

    void ApplyRightArm()
    {
        if (!rightUpperArm) return;

        if (!IsReliable(PoseDetectionManager.PoseLandmarkIndex.RightShoulder) ||
            !IsReliable(PoseDetectionManager.PoseLandmarkIndex.RightWrist)) return;

        var shoulder = PoseDetectionManager.RightShoulder;
        var wrist    = PoseDetectionManager.RightWrist;

        // Direction vector (natural movement)
        Vector3 dir = new Vector3(
            wrist.x - shoulder.x,
            shoulder.y - wrist.y,
            0f
        );

        Quaternion targetRot = Quaternion.LookRotation(Vector3.forward, dir);

        rightUpperArm.localRotation = Quaternion.Slerp(
            rightUpperArm.localRotation,
            _origRight * targetRot,
            Time.deltaTime * smoothing
        );
    }

    void ApplyLeftArm()
    {
        if (!leftUpperArm) return;

        if (!IsReliable(PoseDetectionManager.PoseLandmarkIndex.LeftShoulder) ||
            !IsReliable(PoseDetectionManager.PoseLandmarkIndex.LeftWrist)) return;

        var shoulder = PoseDetectionManager.LeftShoulder;
        var wrist    = PoseDetectionManager.LeftWrist;

        // Mirror fixed direction
        Vector3 dir = new Vector3(
            shoulder.x - wrist.x,
            shoulder.y - wrist.y,
            0f
        );

        Quaternion targetRot = Quaternion.LookRotation(Vector3.forward, dir);

        leftUpperArm.localRotation = Quaternion.Slerp(
            leftUpperArm.localRotation,
            _origLeft * targetRot,
            Time.deltaTime * smoothing
        );
    }

    // ================= SPINE =================

    void ApplySpine()
    {
        if (!spine) return;
        if (!PoseDetectionManager.TryGetBodyCenter(out var bodyCenter)) return;

        float lean = (bodyCenter.x - 0.5f) * leanScale;

        spine.localRotation = Quaternion.Slerp(
            spine.localRotation,
            _origSpine * Quaternion.Euler(0, 0, -lean),
            Time.deltaTime * smoothing
        );
    }

    // ================= HEAD =================

    void ApplyHead()
    {
        if (!head) return;

        if (!IsReliable(PoseDetectionManager.PoseLandmarkIndex.Nose)) return;

        float noseX = PoseDetectionManager.Nose.x;
        float noseY = PoseDetectionManager.Nose.y;

        float turn = (noseX - 0.5f) * headTurnScale;

        float nod = 0f;

        if (IsReliable(PoseDetectionManager.PoseLandmarkIndex.LeftShoulder) &&
            IsReliable(PoseDetectionManager.PoseLandmarkIndex.RightShoulder))
        {
            float shoulderY = (PoseDetectionManager.LeftShoulder.y + PoseDetectionManager.RightShoulder.y) * 0.5f;
            nod = Mathf.Clamp((shoulderY - noseY - 0.1f) * headNodScale, -40f, 40f);
        }

        head.localRotation = Quaternion.Slerp(
            head.localRotation,
            _origHead * Quaternion.Euler(nod, turn, 0),
            Time.deltaTime * smoothing
        );
    }

    // ================= HELPER =================

    bool IsReliable(PoseDetectionManager.PoseLandmarkIndex index)
    {
        if (PoseDetectionManager.Instance == null) return false;

        int i = (int)index;

        return PoseDetectionManager.CurrentVisibility[i] >= visibilityThreshold &&
               PoseDetectionManager.CurrentPresence[i] >= visibilityThreshold;
    }
}