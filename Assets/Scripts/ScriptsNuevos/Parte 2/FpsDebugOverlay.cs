using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// Medidor para diagnosticar lentitud EN EL TELÉFONO. Solo existe en Development Build
/// (Build Settings > Development Build); en una build normal no hace nada ni se crea.
///
/// Muestra arriba a la izquierda: FPS promedio y mínimo, tiempo del peor frame, memoria
/// usada y basura (GC) generada por segundo. Cada 5 s escribe lo mismo en el log
/// (adb logcat -s Unity) para poder pegarlo aquí sin tomar capturas.
/// </summary>
public class FpsDebugOverlay : MonoBehaviour
{
    private const float WindowSeconds = 1f;
    private const float LogEverySeconds = 5f;

    private float windowTime, worstFrame, minFps = float.MaxValue, nextLog;
    private int frames;
    private long lastGcBytes;
    private string text = "";
    private GUIStyle style;
    private readonly StringBuilder sb = new StringBuilder(128);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (!Debug.isDebugBuild) return;
        var go = new GameObject("FpsDebugOverlay");
        DontDestroyOnLoad(go);
        go.AddComponent<FpsDebugOverlay>();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        windowTime += dt;
        frames++;
        if (dt > worstFrame) worstFrame = dt;
        if (dt > 0f) minFps = Mathf.Min(minFps, 1f / dt);

        if (windowTime < WindowSeconds) return;

        long gc = System.GC.GetTotalMemory(false);
        float gcPerSec = Mathf.Max(0, gc - lastGcBytes) / 1024f / windowTime;
        lastGcBytes = gc;

        sb.Clear();
        sb.Append(Mathf.RoundToInt(frames / windowTime)).Append(" fps (min ").Append(Mathf.RoundToInt(minFps))
          .Append(") | peor frame ").Append(Mathf.RoundToInt(worstFrame * 1000f)).Append(" ms\n")
          .Append("Mem ").Append(Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024)).Append(" MB | GC ")
          .Append(Mathf.RoundToInt(gcPerSec)).Append(" KB/s");
        text = sb.ToString();

        if (Time.unscaledTime >= nextLog)
        {
            nextLog = Time.unscaledTime + LogEverySeconds;
            Debug.Log("[Perf] " + text.Replace("\n", " "));
        }

        windowTime = 0f; frames = 0; worstFrame = 0f; minFps = float.MaxValue;
    }

    private void OnGUI()
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(18, Screen.height / 40) };
            style.normal.textColor = Color.yellow;
        }
        GUI.Label(new Rect(20, 10, Screen.width, Screen.height / 6f), text, style);
    }
}
