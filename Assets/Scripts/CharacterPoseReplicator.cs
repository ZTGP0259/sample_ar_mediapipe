using UnityEngine;

public class CharacterPoseReplicator : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Bone References - assign in Inspector")]
    [SerializeField] private Transform rightUpperArm;
    [SerializeField] private Transform leftUpperArm;
    [SerializeField] private Transform rightForeArm;
    [SerializeField] private Transform leftForeArm;
    [SerializeField] private Transform spine;
    [SerializeField] private Transform head;

    [Header("Settings")]
    [SerializeField] private float armRotationScale = 120f;
    [SerializeField] private float leanScale = 30f;
    [SerializeField] private float smoothing = 8f;

    // Cached original rotations
    private Quaternion _origRightUpperArm;
    private Quaternion _origLeftUpperArm;
    private Quaternion _origRightForeArm;
    private Quaternion _origLeftForeArm;
    private Quaternion _origSpine;

    // Smoothed targets
    private float _smoothRightArmAngle;
    private float _smoothLeftArmAngle;
    private float _smoothLean;

    void Start()
    {
        if (rightUpperArm) _origRightUpperArm = rightUpperArm.localRotation;
        if (leftUpperArm)  _origLeftUpperArm  = leftUpperArm.localRotation;
        if (rightForeArm)  _origRightForeArm  = rightForeArm.localRotation;
        if (leftForeArm)   _origLeftForeArm   = leftForeArm.localRotation;
        if (spine)         _origSpine         = spine.localRotation;
    }

    void LateUpdate()  // LateUpdate so we apply AFTER animator
    {
        if (!PoseDetectionManager.IsTracking) return;

        ApplyRightArm();
        ApplyLeftArm();
        ApplySpineLean();
    }

    void ApplyRightArm()
    {
        if (!rightUpperArm) return;
        if (!PoseDetectionManager.IsLandmarkReliable(PoseDetectionManager.PoseLandmarkIndex.RightWrist) ||
            !PoseDetectionManager.IsLandmarkReliable(PoseDetectionManager.PoseLandmarkIndex.RightShoulder))
            return;

        // Y in MediaPipe: 0=top, 1=bottom. Convert to arm raise angle.
        float shoulderY = PoseDetectionManager.RightShoulder.y;
        float wristY    = PoseDetectionManager.RightWrist.y;
        float raise     = Mathf.Clamp01(shoulderY - wristY); // 0=down, 1=fully raised

        float targetAngle = raise * armRotationScale;
        _smoothRightArmAngle = Mathf.Lerp(_smoothRightArmAngle, targetAngle, Time.deltaTime * smoothing);

        // Rotate forward (Z axis for most humanoid rigs — adjust if needed)
        rightUpperArm.localRotation = _origRightUpperArm * Quaternion.Euler(0, 0, _smoothRightArmAngle);
    }

    void ApplyLeftArm()
    {
        if (!leftUpperArm) return;
        if (!PoseDetectionManager.IsLandmarkReliable(PoseDetectionManager.PoseLandmarkIndex.LeftWrist) ||
            !PoseDetectionManager.IsLandmarkReliable(PoseDetectionManager.PoseLandmarkIndex.LeftShoulder))
            return;

        float shoulderY = PoseDetectionManager.LeftShoulder.y;
        float wristY    = PoseDetectionManager.LeftWrist.y;
        float raise     = Mathf.Clamp01(shoulderY - wristY);

        float targetAngle = raise * armRotationScale;
        _smoothLeftArmAngle = Mathf.Lerp(_smoothLeftArmAngle, targetAngle, Time.deltaTime * smoothing);

        leftUpperArm.localRotation = _origLeftUpperArm * Quaternion.Euler(0, 0, -_smoothLeftArmAngle);
    }

    void ApplySpineLean()
    {
        if (!spine) return;
        if (!PoseDetectionManager.TryGetBodyCenter(out var bodyCenter)) return;

        float lean = (bodyCenter.x - 0.5f) * leanScale;
        _smoothLean = Mathf.Lerp(_smoothLean, lean, Time.deltaTime * smoothing);

        spine.localRotation = _origSpine * Quaternion.Euler(0, 0, -_smoothLean);
    }
}