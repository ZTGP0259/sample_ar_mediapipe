using UnityEngine;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Unity;
using System.Collections;

public class PoseDetectionManager : MonoBehaviour
{
    public enum PoseLandmarkIndex
    {
        Nose = 0,
        LeftShoulder = 11,
        RightShoulder = 12,
        LeftElbow = 13,
        RightElbow = 14,
        LeftWrist = 15,
        RightWrist = 16,
        LeftHip = 23,
        RightHip = 24,
        LeftKnee = 25,
        RightKnee = 26,
        LeftAnkle = 27,
        RightAnkle = 28,
    }

    private const int PoseLandmarkCount = 33;

    public static PoseDetectionManager Instance;

    public static readonly Vector2[] CurrentLandmarks = new Vector2[PoseLandmarkCount];
    public static readonly float[] CurrentVisibility = new float[PoseLandmarkCount];
    public static readonly float[] CurrentPresence = new float[PoseLandmarkCount];
    public static bool IsTracking = false;
    public static bool InitializationFailed { get; private set; }
    public static string StatusMessage { get; private set; } = "Waiting for camera...";

    public static Vector2 Nose => GetLandmark(PoseLandmarkIndex.Nose);
    public static Vector2 LeftShoulder => GetLandmark(PoseLandmarkIndex.LeftShoulder);
    public static Vector2 RightShoulder => GetLandmark(PoseLandmarkIndex.RightShoulder);
    public static Vector2 LeftHip => GetLandmark(PoseLandmarkIndex.LeftHip);
    public static Vector2 RightHip => GetLandmark(PoseLandmarkIndex.RightHip);
    public static Vector2 LeftWrist => GetLandmark(PoseLandmarkIndex.LeftWrist);
    public static Vector2 RightWrist => GetLandmark(PoseLandmarkIndex.RightWrist);

    private PoseLandmarker _landmarker;
    private Texture2D _cameraFrameTexture;
    private Color32[] _pixelBuffer;
    private int _frameSkip = 0;

    [Header("Tracking Settings")]
    [SerializeField] private int processEveryNthFrame = 2; // Performance: skip frames
    [SerializeField, Range(0f, 1f)] private float landmarkSmoothing = 0.35f;
    [SerializeField, Range(0f, 1f)] private float minimumVisibility = 0.5f;
    [SerializeField, Range(0f, 1f)] private float minimumPresence = 0.5f;

    void Awake() { Instance = this; }

    IEnumerator Start()
    {
        // Wait for camera to be ready
        yield return new WaitUntil(() => CameraInputManager.Instance.IsReady());
        InitMediaPipe();
    }

    void InitMediaPipe()
    {
        try
        {
            var modelPath = System.IO.Path.Combine(Application.streamingAssetsPath, "pose_landmarker_lite.task");
            var options = new PoseLandmarkerOptions(
                new BaseOptions(modelAssetPath: modelPath),
                runningMode: Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM,
                resultCallback: OnPoseLandmarkerResult
            );
            _landmarker = PoseLandmarker.CreateFromOptions(options);
            InitializationFailed = false;
            StatusMessage = "PoseLandmarker initialized.";
            Debug.Log(StatusMessage);
        }
        catch (System.DllNotFoundException ex)
        {
            InitializationFailed = true;
            StatusMessage = BuildNativePluginErrorMessage();
            Debug.LogError($"{StatusMessage}\n{ex}");
        }
        catch (System.Exception ex)
        {
            InitializationFailed = true;
            StatusMessage = $"MediaPipe init failed: {ex.GetType().Name}";
            Debug.LogError($"{StatusMessage}\n{ex}");
        }
    }

    void Update()
    {
        if (InitializationFailed) return;
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
            ResetTrackingState();
            StatusMessage = "No pose detected. Stand where your upper body is visible to the camera.";
            return;
        }

        IsTracking = true;
        StatusMessage = "Pose detected.";
        var lm = result.poseLandmarks[0].landmarks;

        if (lm == null || lm.Count < PoseLandmarkCount)
        {
            ResetTrackingState();
            return;
        }

        for (int i = 0; i < PoseLandmarkCount; i++)
        {
            UpdateLandmark(i, lm[i]);
        }
    }

    public static Vector2 GetLandmark(PoseLandmarkIndex index) => CurrentLandmarks[(int)index];

    public static float GetVisibility(PoseLandmarkIndex index) => CurrentVisibility[(int)index];

    public static float GetPresence(PoseLandmarkIndex index) => CurrentPresence[(int)index];

    public static bool IsLandmarkReliable(PoseLandmarkIndex index)
    {
        if (Instance == null)
        {
            return false;
        }

        var landmarkIndex = (int)index;
        return CurrentVisibility[landmarkIndex] >= Instance.minimumVisibility &&
               CurrentPresence[landmarkIndex] >= Instance.minimumPresence;
    }

    public static bool TryGetBodyCenter(out Vector2 bodyCenter)
    {
        if (!IsLandmarkReliable(PoseLandmarkIndex.LeftShoulder) ||
            !IsLandmarkReliable(PoseLandmarkIndex.RightShoulder) ||
            !IsLandmarkReliable(PoseLandmarkIndex.LeftHip) ||
            !IsLandmarkReliable(PoseLandmarkIndex.RightHip))
        {
            bodyCenter = Vector2.zero;
            return false;
        }

        var shoulderCenter = (LeftShoulder + RightShoulder) * 0.5f;
        var hipCenter = (LeftHip + RightHip) * 0.5f;
        bodyCenter = (shoulderCenter + hipCenter) * 0.5f;
        return true;
    }

    private void UpdateLandmark(int index, NormalizedLandmark landmark)
    {
        var targetPosition = new Vector2(landmark.x, landmark.y);
        var smoothedPosition = Vector2.Lerp(CurrentLandmarks[index], targetPosition, 1f - landmarkSmoothing);

        CurrentLandmarks[index] = smoothedPosition;
        CurrentVisibility[index] = landmark.visibility ?? 0f;
        CurrentPresence[index] = landmark.presence ?? 0f;
    }

    private void ResetTrackingState()
    {
        IsTracking = false;

        for (int i = 0; i < PoseLandmarkCount; i++)
        {
            CurrentVisibility[i] = 0f;
            CurrentPresence[i] = 0f;
        }
    }

    private string BuildNativePluginErrorMessage()
    {
        if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
        {
            return "MediaPipe native plugin is missing for macOS. This repo can capture camera in Editor, but pose detection will not run on your Mac until a macOS mediapipe_c library is added. Build and test on Android, or add the macOS native plugin.";
        }

        return "MediaPipe native plugin could not be loaded for this platform.";
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
        ResetTrackingState();
        _landmarker?.Close();
        if (_cameraFrameTexture != null)
        {
            Destroy(_cameraFrameTexture);
        }
    }
}