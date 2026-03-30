using UnityEngine;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Unity;
using System.Collections;

public class PoseDetectionManager : MonoBehaviour
{
    public static PoseDetectionManager Instance;

    // Latest normalized landmark positions (0-1 range)
    public static Vector2 Nose        = Vector2.zero;
    public static Vector2 RightWrist  = Vector2.zero;
    public static Vector2 LeftShoulder  = Vector2.zero;
    public static Vector2 RightShoulder = Vector2.zero;
    public static Vector2 LeftHip      = Vector2.zero;
    public static Vector2 RightHip     = Vector2.zero;
    public static bool    IsTracking    = false;

    private PoseLandmarker _landmarker;
    private Texture2D _cameraFrameTexture;
    private Color32[] _pixelBuffer;
    private int _frameSkip = 0;
    [SerializeField] private int processEveryNthFrame = 2; // Performance: skip frames

    void Awake() { Instance = this; }

    IEnumerator Start()
    {
        // Wait for camera to be ready
        yield return new WaitUntil(() => CameraInputManager.Instance.IsReady());
        InitMediaPipe();
    }

    void InitMediaPipe()
    {
        var modelPath = System.IO.Path.Combine(Application.streamingAssetsPath, "pose_landmarker_lite.task");
        var options = new PoseLandmarkerOptions(
            new BaseOptions(modelAssetPath: modelPath),
            runningMode: Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM,
            resultCallback: OnPoseLandmarkerResult
        );
        _landmarker = PoseLandmarker.CreateFromOptions(options);
        Debug.Log("PoseLandmarker initialized.");
    }

    void Update()
    {
        if (_landmarker == null) return;
        if (!CameraInputManager.Instance.IsReady()) return;

        _frameSkip++;
        if (_frameSkip < processEveryNthFrame) return;
        _frameSkip = 0;

        var tex = CameraInputManager.Instance.webCamTexture;
        if (tex == null || tex.width <= 16 || tex.height <= 16) return;

        EnsureCameraFrameTexture(tex.width, tex.height);
        tex.GetPixels32(_pixelBuffer);
        _cameraFrameTexture.SetPixels32(_pixelBuffer);
        _cameraFrameTexture.Apply(false);

        using var image = new Mediapipe.Image(_cameraFrameTexture);
        _landmarker.DetectAsync(image, (long)(Time.time * 1000));
    }

    void OnPoseLandmarkerResult(PoseLandmarkerResult result, Mediapipe.Image img, long timestamp)
    {
        if (result.poseLandmarks == null || result.poseLandmarks.Count == 0)
        {
            IsTracking = false;
            return;
        }
        IsTracking = true;
        var lm = result.poseLandmarks[0].landmarks;

        // MediaPipe landmark indices
        Nose            = new Vector2(lm[0].x,  lm[0].y);
        LeftShoulder    = new Vector2(lm[11].x, lm[11].y);
        RightShoulder   = new Vector2(lm[12].x, lm[12].y);
        LeftHip         = new Vector2(lm[23].x, lm[23].y);
        RightHip        = new Vector2(lm[24].x, lm[24].y);
        RightWrist      = new Vector2(lm[16].x, lm[16].y);
    }

    void EnsureCameraFrameTexture(int width, int height)
    {
        if (_cameraFrameTexture != null && _cameraFrameTexture.width == width && _cameraFrameTexture.height == height)
        {
            return;
        }

        if (_cameraFrameTexture != null)
        {
            Destroy(_cameraFrameTexture);
        }

        _cameraFrameTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        _pixelBuffer = new Color32[width * height];
    }

    void OnDestroy()
    {
        _landmarker?.Close();
        if (_cameraFrameTexture != null)
        {
            Destroy(_cameraFrameTexture);
        }
    }
}