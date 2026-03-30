using UnityEngine;

public class CameraInputManager : MonoBehaviour
{
    public static CameraInputManager Instance;
    public WebCamTexture webCamTexture;
    [SerializeField] private int targetFPS = 30;
    [SerializeField] private int camWidth = 640;
    [SerializeField] private int camHeight = 480;

    void Awake() { Instance = this; }

    void Start()
    {
        // Request camera permission on Android
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            StartCoroutine(RequestCameraPermission());
        else
            InitCamera();
    }

    System.Collections.IEnumerator RequestCameraPermission()
    {
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (Application.HasUserAuthorization(UserAuthorization.WebCam))
            InitCamera();
        else
            Debug.LogError("Camera permission denied!");
    }

    void InitCamera()
    {
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0) { Debug.LogError("No camera found!"); return; }

        // Prefer front camera for body pose
        string camName = devices[0].name;
        foreach (var d in devices)
            if (d.isFrontFacing) { camName = d.name; break; }

        webCamTexture = new WebCamTexture(camName, camWidth, camHeight, targetFPS);
        webCamTexture.Play();
        Debug.Log($"Camera started: {camName}");
    }

    public bool IsReady() => webCamTexture != null && webCamTexture.isPlaying && webCamTexture.width > 16;

    void OnDestroy() { if (webCamTexture != null) webCamTexture.Stop(); }
}