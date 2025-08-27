using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LLaVAHookSimple : MonoBehaviour
{
    [Header("LLaVA Client")]
    [SerializeField] private QuestLLaVAClient llavaClient;         // assign

    [Header("Optional prompt field")]
    [SerializeField] private TMP_InputField promptInput;           // optional

    [Header("Frame source (pick ONE that you have)")]
    [SerializeField] private WebCamTexture webCamTexture;          // if you have a direct WebCamTexture
    [SerializeField] private RawImage sourceRawImage;              // or a UI RawImage showing a WebCamTexture
    [SerializeField] private Renderer sourceRenderer;              // or a Renderer (Mesh/Quad) using a WebCamTexture
    [SerializeField] private Camera captureCamera;                 // or fall back to capturing a Camera
    [SerializeField] private int cameraWidth = 640, cameraHeight = 640;

    [Header("Controls")]
    [SerializeField] private bool sendOnAButton = true;
    [SerializeField] private OVRInput.Button triggerButton = OVRInput.Button.One;

    [Header("Encoding")]
    [Range(1,100)] [SerializeField] private int jpgQuality = 85;

    void Update()
    {
        if (sendOnAButton && OVRInput.GetDown(triggerButton))
            SendOnce();
    }

    /// <summary>Call this from a UI Button if you prefer.</summary>
    public void SendOnce()
    {
        if (llavaClient == null) { Debug.LogWarning("LLaVA client not set."); return; }

        byte[] jpg = CaptureJpg();
        if (jpg == null || jpg.Length == 0) { Debug.LogWarning("No frame available to capture."); return; }

        string prompt = (!string.IsNullOrWhiteSpace(promptInput?.text)) ? promptInput.text : null;
        llavaClient.SendPromptWithImageBytes(jpg, prompt);
    }

    // --- helpers ---

    WebCamTexture FindActiveWebCamTexture()
    {
        if (webCamTexture != null) return webCamTexture;

        if (sourceRawImage != null && sourceRawImage.texture is WebCamTexture w1) return w1;

        if (sourceRenderer != null)
        {
            var tex = sourceRenderer.material != null ? sourceRenderer.material.mainTexture : null;
            if (tex is WebCamTexture w2) return w2;
        }
        return null;
    }

    byte[] CaptureJpg()
    {
        // Prefer a live WebCamTexture if available
        var wct = FindActiveWebCamTexture();
        if (wct != null && wct.isPlaying && wct.width > 16 && wct.height > 16)
            return WebCamToJpg(wct, jpgQuality);

        // Fallback: capture a camera
        if (captureCamera != null)
            return CameraToJpg(captureCamera, cameraWidth, cameraHeight, jpgQuality);

        return null;
    }

    static byte[] WebCamToJpg(WebCamTexture wct, int quality)
    {
        var tex = new Texture2D(wct.width, wct.height, TextureFormat.RGB24, false);
        tex.SetPixels32(wct.GetPixels32());
        tex.Apply();
        byte[] jpg = tex.EncodeToJPG(quality);
        Object.Destroy(tex);
        return jpg;
    }

    static byte[] CameraToJpg(Camera cam, int w, int h, int quality)
    {
        var rt = new RenderTexture(w, h, 24);
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);

        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();

        RenderTexture.active = null;
        cam.targetTexture = null;
        rt.Release();

        byte[] jpg = tex.EncodeToJPG(quality);
        Object.Destroy(tex);
        return jpg;
    }
}
