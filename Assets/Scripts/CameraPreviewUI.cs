using UnityEngine;
using UnityEngine.UI;

public class CameraPreviewUI : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private RawImage previewImage;
    // Assign a RawImage UI element in the bottom-left corner of your Canvas

    void Update()
    {
        if (previewImage == null) return;
        if (CameraInputManager.Instance == null) return;
        if (!CameraInputManager.Instance.IsReady()) return;

        if (previewImage.texture != CameraInputManager.Instance.webCamTexture)
            previewImage.texture = CameraInputManager.Instance.webCamTexture;

        // Fix rotation on Android (front camera is often rotated)
        var tex = CameraInputManager.Instance.webCamTexture;
        float angle = -tex.videoRotationAngle;
        bool mirrored = tex.videoVerticallyMirrored;
        previewImage.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
        previewImage.uvRect = mirrored
            ? new Rect(0, 1, 1, -1)
            : new Rect(0, 0, 1, 1);
    }
}