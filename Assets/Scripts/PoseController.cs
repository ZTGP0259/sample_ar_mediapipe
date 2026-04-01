using UnityEngine;
using TMPro;

public class PoseController : MonoBehaviour
{
    private const float ScreenCenterX = 0.5f;

    [Header("References")]
    [SerializeField] private Transform playerCube;
    [SerializeField] private TextMeshProUGUI debugText;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed    = 5f;
    [SerializeField] private float leanThreshold = 0.08f; // How much lean triggers move
    [SerializeField] private float lerpSmooth   = 6f;
    [SerializeField] private float moveBounds = 4f;
    [SerializeField] private bool mirrorFrontCameraMovement = true;
    [SerializeField] private float leanSmoothing = 8f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce    = 7f;
    [SerializeField] private float jumpCooldown = 0.8f;
    [SerializeField] private float wristAboveNoseOffset = 0.05f;
    [SerializeField] private int jumpFramesRequired = 2;

    private Rigidbody _rb;
    private float     _lastJumpTime = -999f;
    private bool      _isGrounded   = true;
    private float     _targetX      = 0f;
    private float     _smoothedLean = 0f;
    private int       _jumpPoseFrames = 0;

    void Start()
    {
        _rb = playerCube.GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (PoseDetectionManager.InitializationFailed)
        {
            UpdateDebugUI(PoseDetectionManager.StatusMessage);
            return;
        }

        if (!PoseDetectionManager.IsTracking)
        {
            _jumpPoseFrames = 0;
            UpdateDebugUI(PoseDetectionManager.StatusMessage);
            return;
        }

        if (IsJumpPose())
        {
            _jumpPoseFrames++;
            if (_jumpPoseFrames >= jumpFramesRequired && _isGrounded && Time.time - _lastJumpTime > jumpCooldown)
            {
                Jump();
                _jumpPoseFrames = 0;
            }
        }
        else
        {
            _jumpPoseFrames = 0;
        }

        float bodyCenterX = ScreenCenterX;
        float lean = 0f;
        bool hasReliableBodyCenter = TryGetLean(out lean, out bodyCenterX);
        _smoothedLean = Mathf.Lerp(_smoothedLean, lean, Time.deltaTime * leanSmoothing);

        if (hasReliableBodyCenter && _smoothedLean > leanThreshold)
        {
            MoveHorizontal(mirrorFrontCameraMovement ? -1f : 1f);
            Debug.Log("MoveLeft");
        }
        else if (hasReliableBodyCenter && _smoothedLean < -leanThreshold)
        {
            MoveHorizontal(mirrorFrontCameraMovement ? 1f : -1f);
            Debug.Log("MoveRight");
        }

        // Smooth return to target position
        Vector3 pos = playerCube.position;
        pos.x = Mathf.Lerp(pos.x, _targetX, Time.deltaTime * lerpSmooth);
        playerCube.position = pos;

        // Update debug UI
        UpdateDebugUI(
            $"Nose: ({PoseDetectionManager.Nose.x:F2}, {PoseDetectionManager.Nose.y:F2})\n" +
            $"RWrist: ({PoseDetectionManager.RightWrist.x:F2}, {PoseDetectionManager.RightWrist.y:F2})\n" +
            $"Body X: {bodyCenterX:F2} | Lean: {_smoothedLean:F2}\n" +
            $"Jump Pose Frames: {_jumpPoseFrames}\n" +
            $"Grounded: {_isGrounded}"
        );
    }

    void MoveHorizontal(float direction)
    {
        _targetX = Mathf.Clamp(_targetX + direction * moveSpeed * Time.deltaTime, -moveBounds, moveBounds);
    }

    bool IsJumpPose()
{
    // Reject if wrist is outside camera frame (negative or >1.0)
    if (PoseDetectionManager.RightWrist.x < 0f || PoseDetectionManager.RightWrist.x > 1f ||
        PoseDetectionManager.RightWrist.y < 0f || PoseDetectionManager.RightWrist.y > 1f)
        return false;

    if (!PoseDetectionManager.IsLandmarkReliable(PoseDetectionManager.PoseLandmarkIndex.RightWrist) ||
        !PoseDetectionManager.IsLandmarkReliable(PoseDetectionManager.PoseLandmarkIndex.Nose))
        return false;

    return PoseDetectionManager.RightWrist.y < PoseDetectionManager.Nose.y - wristAboveNoseOffset;
}

    bool TryGetLean(out float lean, out float bodyCenterX)
    {
        if (!PoseDetectionManager.TryGetBodyCenter(out var bodyCenter))
        {
            lean = 0f;
            bodyCenterX = ScreenCenterX;
            return false;
        }

        bodyCenterX = bodyCenter.x;
        lean = bodyCenterX - ScreenCenterX;
        return true;
    }

    void Jump()
    {
        if (!_isGrounded) return;
        _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        _lastJumpTime = Time.time;
        _isGrounded = false;
        Debug.Log("JUMP triggered!");
        // Optional: scale bounce animation
        StartCoroutine(JumpScaleAnim());
    }

    System.Collections.IEnumerator JumpScaleAnim()
    {
        Vector3 orig = playerCube.localScale;
        playerCube.localScale = orig * 1.3f;
        yield return new WaitForSeconds(0.12f);
        playerCube.localScale = orig;
    }

    void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.CompareTag("Ground")) _isGrounded = true;
    }

    void UpdateDebugUI(string msg)
    {
        if (debugText != null) debugText.text = msg;
    }
}