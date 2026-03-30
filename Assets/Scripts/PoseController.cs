using UnityEngine;
using TMPro;

public class PoseController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerCube;
    [SerializeField] private TextMeshProUGUI debugText;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed    = 5f;
    [SerializeField] private float jumpForce    = 7f;
    [SerializeField] private float leanThreshold = 0.08f; // How much lean triggers move
    [SerializeField] private float lerpSmooth   = 6f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpCooldown = 0.8f;

    private Rigidbody _rb;
    private float     _lastJumpTime = -999f;
    private bool      _isGrounded   = true;
    private float     _targetX      = 0f;

    void Start()
    {
        _rb = playerCube.GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (!PoseDetectionManager.IsTracking)
        {
            UpdateDebugUI("No pose detected — stand in front of camera");
            return;
        }

        // --- JUMP: right wrist Y above nose Y ---
        // Note: MediaPipe Y=0 is top, Y=1 is bottom (inverted!)
        float wristY = PoseDetectionManager.RightWrist.y;
        float noseY  = PoseDetectionManager.Nose.y;
        if (wristY < noseY - 0.05f && _isGrounded && Time.time - _lastJumpTime > jumpCooldown)
        {
            Jump();
        }

        // --- LEAN: body center X shift ---
        float shoulderCenterX = (PoseDetectionManager.LeftShoulder.x + PoseDetectionManager.RightShoulder.x) / 2f;
        float hipCenterX      = (PoseDetectionManager.LeftHip.x     + PoseDetectionManager.RightHip.x)     / 2f;
        float bodyCenterX     = (shoulderCenterX + hipCenterX) / 2f;

        // bodyCenterX: 0=left edge, 0.5=center, 1=right edge (mirrored in front cam)
        float lean = bodyCenterX - 0.5f; // negative = leaning RIGHT on screen = move left in mirror

        if (lean > leanThreshold)
        {
            MoveHorizontal(-1f); // screen-right = move left (front cam mirror)
            Debug.Log("MoveLeft");
        }
        else if (lean < -leanThreshold)
        {
            MoveHorizontal(1f);
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
            $"Body X: {bodyCenterX:F2} | Lean: {lean:F2}\n" +
            $"Grounded: {_isGrounded}"
        );
    }

    void MoveHorizontal(float direction)
    {
        _targetX = Mathf.Clamp(_targetX + direction * moveSpeed * Time.deltaTime, -4f, 4f);
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