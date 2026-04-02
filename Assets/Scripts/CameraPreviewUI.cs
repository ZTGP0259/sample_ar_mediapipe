using UnityEngine;
using UnityEngine.UI;

public class CameraPreviewUI : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private RawImage previewImage;
    // Assign a RawImage UI element in the bottom-left corner of your Canvas

    private Vector2 _savedAnchoredPosition;
    private Vector2 _savedSizeDelta;
    private bool _pivotFixed;

    void Start()
    {
        if (previewImage != null)
            TryFixPivotToCenter();
    }

    void Update()
    {
        if (previewImage == null) return;
        if (CameraInputManager.Instance == null) return;
        if (!CameraInputManager.Instance.IsReady()) return;

        // Retry pivot fix if Canvas rect wasn't ready in Start
        if (!_pivotFixed)
            TryFixPivotToCenter();

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

        // Restore editor-set position and size — rotation around a non-center pivot
        // visually shifts the element on device where videoRotationAngle is non-zero
        previewImage.rectTransform.anchoredPosition = _savedAnchoredPosition;
        previewImage.rectTransform.sizeDelta = _savedSizeDelta;
    }

    // Moves the pivot to center (0.5, 0.5) without changing the element's visual position,
    // then saves the resulting anchoredPosition and sizeDelta.
    private void TryFixPivotToCenter()
    {
        var rt = previewImage.rectTransform;
        Vector2 size = rt.rect.size;
        if (size.sqrMagnitude < 0.001f) return; // layout not built yet, retry next frame

        Vector2 deltaPivot = new Vector2(0.5f, 0.5f) - rt.pivot;
        rt.anchoredPosition += new Vector2(deltaPivot.x * size.x, deltaPivot.y * size.y);
        rt.pivot = new Vector2(0.5f, 0.5f);

        _savedAnchoredPosition = rt.anchoredPosition;
        _savedSizeDelta = rt.sizeDelta;
        _pivotFixed = true;
    }
}