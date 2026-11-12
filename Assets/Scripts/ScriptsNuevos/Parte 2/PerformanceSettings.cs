using UnityEngine;

/// <summary>
/// Ajustes de rendimiento y latencia para móvil. Se ejecuta SOLA al iniciar la app
/// (RuntimeInitializeOnLoadMethod): no hay que ponerla en ninguna escena.
///
///  - Frame rate: en Android Unity limita a 30 fps por defecto salvo que se pida otra cosa.
///    Se fija 60 (o la tasa de la pantalla si es menor) para que fader, destellos y deslizamientos
///    se vean fluidos.
///  - V-Sync apagado: el límite lo pone targetFrameRate (en móvil vSync + targetFrameRate se pisan).
///  - Anti-aliasing apagado: la UI no lo necesita y en GPU móvil cuesta bastante.
///  - Búfer de audio (solo Android): uno más chico baja el retraso entre tocar un pad y oírlo.
///    Si se escuchan chasquidos o cortes en algún teléfono, subir DspBufferSize a 512.
/// </summary>
public static class PerformanceSettings
{
    private const int TargetFps = 60;

    // 256 = baja latencia; 512 = más seguro en teléfonos modestos.
    private const int DspBufferSize = 256;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        QualitySettings.vSyncCount = 0;
        QualitySettings.antiAliasing = 0;

        int refresh = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
        Application.targetFrameRate = refresh > 0 ? Mathf.Min(TargetFps, refresh) : TargetFps;

#if UNITY_ANDROID && !UNITY_EDITOR
        AudioConfiguration config = AudioSettings.GetConfiguration();
        if (config.dspBufferSize != DspBufferSize)
        {
            config.dspBufferSize = DspBufferSize;
            AudioSettings.Reset(config);
        }
#endif
    }
}
