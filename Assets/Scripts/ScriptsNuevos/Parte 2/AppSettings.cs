using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Guarda las preferencias de la persona (modo claro/oscuro y volúmenes) en un JSON
/// dentro de Application.persistentDataPath/app_settings.json, y las devuelve al
/// abrir la app de nuevo. Es una clase estática: no hay que ponerla en ninguna escena.
///
/// Los setters solo marcan "hay cambios"; un pequeño componente invisible
/// (AppSettingsSaver, se crea solo) escribe el archivo cuando el valor deja de
/// cambiar por un instante — así arrastrar un fader no escribe en disco en cada frame —
/// y también al pausar o cerrar la app.
/// </summary>
public static class AppSettings
{
    public const int ChannelCount = 8;
    private const string FileName = "app_settings.json";

    [Serializable]
    private class Data
    {
        public int version = 1;
        public bool hasDarkMode;
        public bool darkMode;
        public float master = -1f;   // -1 = nunca guardado
        public float bass = -1f;
        public float[] channels = NewChannelArray();
    }

    private static Data data;
    private static bool dirty;

    private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    private static float[] NewChannelArray()
    {
        var arr = new float[ChannelCount];
        for (int i = 0; i < arr.Length; i++) arr[i] = -1f;
        return arr;
    }

    private static Data Current
    {
        get
        {
            if (data == null) Load();
            return data;
        }
    }

    // ------------------------------------------------------------------
    // Lectura (TryGet devuelve false si nunca se guardó nada: usar el valor por defecto)
    // ------------------------------------------------------------------

    public static bool TryGetDarkMode(out bool dark)
    {
        dark = Current.darkMode;
        return Current.hasDarkMode;
    }

    public static bool TryGetMasterVolume(out float value01) => TryGet(Current.master, out value01);
    public static bool TryGetBassAmount(out float value01) => TryGet(Current.bass, out value01);

    public static bool TryGetChannelVolume(int channel, out float value01)
    {
        value01 = 0f;
        if (channel < 0 || channel >= ChannelCount) return false;
        return TryGet(Current.channels[channel], out value01);
    }

    private static bool TryGet(float stored, out float value01)
    {
        value01 = Mathf.Clamp01(stored);
        return stored >= 0f;
    }

    // ------------------------------------------------------------------
    // Escritura
    // ------------------------------------------------------------------

    public static void SetDarkMode(bool dark)
    {
        if (Current.hasDarkMode && Current.darkMode == dark) return;
        Current.hasDarkMode = true;
        Current.darkMode = dark;
        MarkDirty();
    }

    public static void SetMasterVolume(float value01) => SetIfChanged(ref Current.master, value01);
    public static void SetBassAmount(float value01) => SetIfChanged(ref Current.bass, value01);

    public static void SetChannelVolume(int channel, float value01)
    {
        if (channel < 0 || channel >= ChannelCount) return;
        SetIfChanged(ref Current.channels[channel], value01);
    }

    private static void SetIfChanged(ref float field, float value01)
    {
        float v = Mathf.Clamp01(value01);
        if (Mathf.Approximately(field, v)) return;
        field = v;
        MarkDirty();
    }

    // ------------------------------------------------------------------
    // Disco
    // ------------------------------------------------------------------

    private static void MarkDirty()
    {
        dirty = true;
        AppSettingsSaver.EnsureExists();
    }

    /// <summary>Escribe el archivo si hay cambios pendientes.</summary>
    public static void Flush()
    {
        if (!dirty || data == null) return;
        dirty = false;

        try
        {
            string path = FilePath;
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(data));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
        catch (Exception e)
        {
            dirty = true; // se reintenta en el próximo flush
            Debug.LogWarning($"[AppSettings] No se pudo guardar: {e.Message}");
        }
    }

    private static void Load()
    {
        data = new Data();

        try
        {
            if (!File.Exists(FilePath)) return;

            Data loaded = JsonUtility.FromJson<Data>(File.ReadAllText(FilePath));
            if (loaded == null) return;

            // Un archivo viejo o editado a mano podría traer el arreglo con otro largo.
            float[] channels = NewChannelArray();
            if (loaded.channels != null)
            {
                for (int i = 0; i < channels.Length && i < loaded.channels.Length; i++)
                    channels[i] = loaded.channels[i];
            }
            loaded.channels = channels;
            data = loaded;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AppSettings] Archivo de ajustes ilegible, se usan los valores por defecto: {e.Message}");
            data = new Data();
        }
    }
}

/// <summary>
/// Componente invisible que escribe AppSettings en disco: poco después del último cambio
/// y siempre al pausar/cerrar la app (en Android al salir con "Inicio" solo llega la pausa).
/// Se crea solo la primera vez que algo cambia.
/// </summary>
public class AppSettingsSaver : MonoBehaviour
{
    private const float SaveDelaySeconds = 1f;

    private static AppSettingsSaver instance;
    private float lastChangeTime;

    public static void EnsureExists()
    {
        if (instance == null)
        {
            var go = new GameObject("AppSettingsSaver");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AppSettingsSaver>();
        }
        instance.lastChangeTime = Time.unscaledTime;
    }

    private void Update()
    {
        if (Time.unscaledTime - lastChangeTime >= SaveDelaySeconds) AppSettings.Flush();
    }

    private void OnApplicationPause(bool paused) { if (paused) AppSettings.Flush(); }
    private void OnApplicationQuit() => AppSettings.Flush();
}
