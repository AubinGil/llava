using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

[Serializable] class LLaVAResponse { public string text; public int latency_ms; public string model; public string error; }

public class QuestLLaVAClient : MonoBehaviour
{
    [Header("Server")]
    public string serverUri = "ws://192.168.2.29:8765/llava";   // set your Mac IP

    // Put this inside the QuestLLaVAClient class
    public void SendPrompt()                  // text-only, uses promptInput
    {
        SendPromptWithImageBytes(null, null);
    }

    public void SendPrompt(string prompt)     // text-only, explicit prompt
    {
        SendPromptWithImageBytes(null, prompt);
    }

    [Header("UI")]
    public TMP_InputField promptInput;           // optional
    public TextMeshProUGUI responseText;         // required (displays reply)

    /// <summary>Send prompt + optional JPG bytes to the LLaVA WS server and print reply.</summary>
    public async void SendPromptWithImageBytes(byte[] jpg, string promptOverride = null)
    {
        string prompt = string.IsNullOrWhiteSpace(promptOverride)
            ? (!string.IsNullOrWhiteSpace(promptInput?.text) ? promptInput.text : "Describe the scene briefly.")
            : promptOverride;

        try
        {
            using var ws = new ClientWebSocket();
            ws.Options.SetRequestHeader("Origin", "http://localhost"); // avoids 403 on some servers
            await ws.ConnectAsync(new Uri(serverUri), CancellationToken.None);

            // 1) header (binary): {"prompt":"...", "image_len":N}
            var headerObj = new { prompt = prompt, image_len = jpg?.Length ?? 0 };
            byte[] headerBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(headerObj));
            await ws.SendAsync(new ArraySegment<byte>(headerBytes), WebSocketMessageType.Binary, true, CancellationToken.None);

            // 2) optional image bytes (binary)
            if (jpg != null && jpg.Length > 0)
                await ws.SendAsync(new ArraySegment<byte>(jpg), WebSocketMessageType.Binary, true, CancellationToken.None);

            // 3) read one text reply
            var buf = new byte[128 * 1024];
            int total = 0;
            WebSocketReceiveResult res;
            do
            {
                res = await ws.ReceiveAsync(new ArraySegment<byte>(buf, total, buf.Length - total), CancellationToken.None);
                total += res.Count;
                if (total >= buf.Length) break;
            } while (!res.EndOfMessage);

            string msg = Encoding.UTF8.GetString(buf, 0, total);
            var parsed = JsonUtility.FromJson<LLaVAResponse>(msg);
            string text = !string.IsNullOrEmpty(parsed?.text) ? parsed.text :
                          !string.IsNullOrEmpty(parsed?.error) ? $"Error: {parsed.error}" : msg;
            if (responseText) responseText.text = text;

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
        }
        catch (Exception e)
        {
            if (responseText) responseText.text = $"Send failed: {e.Message}";
        }
    }
}
