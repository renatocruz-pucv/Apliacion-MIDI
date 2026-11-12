using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Guarda, carga y administra los bancos CREADOS POR LA PERSONA (los bancos de
/// fábrica siguen siendo SoundBankSO del proyecto, de solo lectura).
///
/// Cómo funciona:
///  - Los datos (nombres de bancos y pads, y qué archivo de audio usa cada pad) se
///    guardan en persistentDataPath/custom_banks.json.
///  - Cada audio que la persona elige se COPIA a persistentDataPath/CustomAudio, así
///    sigue funcionando aunque el archivo original se mueva o se borre.
///  - Al iniciar, todos los audios se cargan completos y descomprimidos en memoria
///    (no en streaming), para que los pads suenen al instante, igual que los de fábrica.
///  - Por cada banco se mantiene un SoundBankSO creado en ejecución. Así BankManager,
///    PadButtonUI y el Mixer los tratan igual que a un banco de fábrica, sin saber que
///    vienen de aquí.
///
/// Se agrega a UN GameObject que arranque activo (por ejemplo "CustomBankLibrary"
/// bajo el Canvas o como objeto vacío en la raíz).
/// </summary>
[DefaultExecutionOrder(-150)] // antes que BankManager, para que ya exista su lista de bancos
public class CustomBankLibrary : MonoBehaviour
{
    public const int PadsPerBank = 8;
    private const string JsonFileName = "custom_banks.json";
    private const string AudioFolderName = "CustomAudio";

    [Header("Arte de los pads personalizados")]
    [Tooltip("Sprite del pad apagado/vacío. Se usa en TODOS los pads de bancos creados por la persona " +
             "(los bancos de fábrica traen el suyo en cada PadSoundData). Si queda vacío, el pad no se ve.")]
    [SerializeField] private Sprite padIconOff;

    [Tooltip("Sprite del pad con audio asignado.")]
    [SerializeField] private Sprite padIconOn;

    [Tooltip("Imagen (ej. una X) que se dibuja ENCIMA de los pads sin audio de bancos propios en PadsScreen, " +
             "sin reemplazar el cuadrado de fondo. Requiere el campo 'Empty Overlay' de cada PadButtonUI. " +
             "Si queda vacío no se muestra nada.")]
    [SerializeField] private Sprite emptyPadOverlaySprite;

    [Tooltip("Opcional (recomendado): un banco de fábrica del cual copiar los sprites. Si se asigna, " +
             "los pads propios usan EXACTAMENTE los mismos Icon Off / Icon On que sus pads (se toma el " +
             "primer pad que los tenga), y los dos campos de arriba se ignoran. Así se ven y reaccionan " +
             "igual que los de fábrica, incluido el destello al tocar.")]
    [SerializeField] private SoundBankSO iconSourceBank;

    [Header("Límites")]
    [SerializeField] private int maxBanks = 10;
    [SerializeField] private int maxBankNameLength = 20;
    [SerializeField] private int maxPadNameLength = 12;
    [Tooltip("Un audio más largo que esto se rechaza: se guarda descomprimido en memoria " +
             "(120 s en estéreo ocupan unos 20 MB).")]
    [SerializeField] private float maxClipSeconds = 120f;

    /// <summary>Se dispara cuando cambia la lista de bancos o el contenido de alguno (también al terminar de cargar audios).</summary>
    public event Action OnLibraryChanged;

    // Tope de peso del archivo original. Un audio largo se descomprime a cientos de MB en
    // memoria y en Android eso cierra la app; mejor rechazarlo antes de decodificarlo.
    private const long MaxSourceFileBytes = 25L * 1024 * 1024;

    // Hasta este peso (mp3/ogg/m4a) el audio se deja descomprimido en memoria para que suene
    // al instante; los más pesados quedan comprimidos en memoria (ahorra RAM, el costo
    // es una mínima demora al reproducir). Los WAV ya son PCM, así que no cambian.
    private const long MaxBytesToDecodeFully = 800L * 1024;

    public Sprite PadIconOff => padIconOff;
    public Sprite PadIconOn => padIconOn;
    public int MaxBanks => maxBanks;
    public int MaxBankNameLength => maxBankNameLength;
    public int MaxPadNameLength => maxPadNameLength;
    public int BankCount => data.banks.Count;

    /// <summary>Los bancos como SoundBankSO, en el mismo orden que la lista guardada.</summary>
    public IReadOnlyList<SoundBankSO> Banks => runtimeBanks;

    private CustomBankLibraryData data = new CustomBankLibraryData();
    private readonly List<SoundBankSO> runtimeBanks = new List<SoundBankSO>();
    private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();

    private string JsonPath => Path.Combine(Application.persistentDataPath, JsonFileName);
    private string AudioFolder => Path.Combine(Application.persistentDataPath, AudioFolderName);

    // ------------------------------------------------------------------
    // Ciclo de vida
    // ------------------------------------------------------------------

    private void Awake()
    {
        CopyIconsFromSourceBank();

        if (padIconOff == null || padIconOn == null)
        {
            Debug.LogWarning("[CustomBankLibrary] Falta asignar Pad Icon Off / On: los pads de los bancos " +
                             "personalizados no se verían.");
        }

        LoadFromDisk();

        for (int i = 0; i < data.banks.Count; i++)
        {
            runtimeBanks.Add(ScriptableObject.CreateInstance<SoundBankSO>());
            RefreshRuntimeBank(i);
        }

        StartCoroutine(LoadAllClipsRoutine());
    }

    private void OnDestroy()
    {
        foreach (AudioClip clip in clipCache.Values)
        {
            if (clip != null) Destroy(clip);
        }
        clipCache.Clear();

        foreach (SoundBankSO bank in runtimeBanks)
        {
            if (bank != null) Destroy(bank);
        }
        runtimeBanks.Clear();
    }

    private void CopyIconsFromSourceBank()
    {
        if (iconSourceBank == null) return;

        Sprite off = null, on = null;
        for (int i = 0; i < iconSourceBank.PadCount; i++)
        {
            PadSoundData pad = iconSourceBank.GetPad(i);
            if (pad == null) continue;
            if (off == null) off = pad.iconOff;
            if (on == null) on = pad.iconOn;
            if (off != null && on != null) break;
        }

        if (off != null) padIconOff = off;
        if (on != null) padIconOn = on;
    }

    // ------------------------------------------------------------------
    // Consulta
    // ------------------------------------------------------------------

    public SoundBankSO GetBank(int bankIndex)
        => (bankIndex >= 0 && bankIndex < runtimeBanks.Count) ? runtimeBanks[bankIndex] : null;

    public CustomBankData GetBankData(int bankIndex)
        => (bankIndex >= 0 && bankIndex < data.banks.Count) ? data.banks[bankIndex] : null;

    public CustomPadData GetPadData(int bankIndex, int padIndex)
    {
        CustomBankData bank = GetBankData(bankIndex);
        if (bank == null || padIndex < 0 || padIndex >= bank.pads.Count) return null;
        return bank.pads[padIndex];
    }

    // ------------------------------------------------------------------
    // Edición de bancos
    // ------------------------------------------------------------------

    /// <summary>Crea un banco vacío. Devuelve su índice, o -1 si ya se llegó al máximo.</summary>
    public int CreateBank(string desiredName = null)
    {
        if (data.banks.Count >= maxBanks) return -1;

        var bank = new CustomBankData
        {
            id = Guid.NewGuid().ToString("N"),
            name = ClampName(desiredName, maxBankNameLength, $"Mi banco {data.banks.Count + 1}")
        };
        NormalizeBank(bank);

        data.banks.Add(bank);
        runtimeBanks.Add(ScriptableObject.CreateInstance<SoundBankSO>());
        int index = data.banks.Count - 1;
        RefreshRuntimeBank(index);

        SaveToDisk();
        OnLibraryChanged?.Invoke();
        return index;
    }

    /// <summary>Elimina el banco y todos los audios que se habían copiado para él.</summary>
    public void DeleteBank(int bankIndex)
    {
        CustomBankData bank = GetBankData(bankIndex);
        if (bank == null) return;

        for (int i = 0; i < bank.pads.Count; i++)
        {
            if (bank.pads[i].HasAudio) ReleaseAudio(bank.pads[i].audioFileName);
        }

        SoundBankSO runtime = runtimeBanks[bankIndex];
        data.banks.RemoveAt(bankIndex);
        runtimeBanks.RemoveAt(bankIndex);
        if (runtime != null) Destroy(runtime);

        SaveToDisk();
        OnLibraryChanged?.Invoke();
    }

    /// <summary>Cambia el nombre del banco. Un nombre vacío se ignora. Devuelve true si hubo cambio.</summary>
    public bool RenameBank(int bankIndex, string newName)
    {
        CustomBankData bank = GetBankData(bankIndex);
        if (bank == null) return false;

        string clean = ClampName(newName, maxBankNameLength, null);
        if (string.IsNullOrEmpty(clean) || clean == bank.name) return false;

        bank.name = clean;
        RefreshRuntimeBank(bankIndex);
        SaveToDisk();
        OnLibraryChanged?.Invoke();
        return true;
    }

    // ------------------------------------------------------------------
    // Edición de pads
    // ------------------------------------------------------------------

    /// <summary>Cambia el nombre de un pad. Devuelve true si hubo cambio.</summary>
    public bool RenamePad(int bankIndex, int padIndex, string newName)
    {
        CustomPadData pad = GetPadData(bankIndex, padIndex);
        if (pad == null) return false;

        string clean = ClampName(newName, maxPadNameLength, "");
        if (clean == pad.displayName) return false;

        pad.displayName = clean;
        RefreshRuntimeBank(bankIndex);
        SaveToDisk();
        OnLibraryChanged?.Invoke();
        return true;
    }

    /// <summary>Quita el audio del pad (y su nombre), y borra el archivo copiado.</summary>
    public void ClearPadAudio(int bankIndex, int padIndex)
    {
        CustomPadData pad = GetPadData(bankIndex, padIndex);
        if (pad == null || !pad.HasAudio) return;

        ReleaseAudio(pad.audioFileName);
        pad.audioFileName = "";
        pad.displayName = "";

        RefreshRuntimeBank(bankIndex);
        SaveToDisk();
        OnLibraryChanged?.Invoke();
    }

    /// <summary>
    /// Asigna (o reemplaza) el audio de un pad con un archivo del dispositivo: lo copia,
    /// lo carga en memoria y recién entonces actualiza el pad. Si algo falla, el pad queda
    /// como estaba. onDone recibe (éxito, mensaje para mostrar a la persona).
    /// </summary>
    public void SetPadAudio(int bankIndex, int padIndex, string sourcePath, Action<bool, string> onDone)
    {
        CustomBankData bank = GetBankData(bankIndex);
        if (bank == null || padIndex < 0 || padIndex >= bank.pads.Count)
        {
            onDone?.Invoke(false, "El banco ya no existe.");
            return;
        }

        StartCoroutine(SetPadAudioRoutine(bank.id, padIndex, sourcePath, onDone));
    }

    private IEnumerator SetPadAudioRoutine(string bankId, int padIndex, string sourcePath, Action<bool, string> onDone)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
        {
            onDone?.Invoke(false, "No se pudo leer el archivo elegido.");
            yield break;
        }

        long sourceBytes = 0;
        try { sourceBytes = new FileInfo(sourcePath).Length; } catch { }
        if (sourceBytes > MaxSourceFileBytes)
        {
            onDone?.Invoke(false, $"El archivo pesa más de {MaxSourceFileBytes / (1024 * 1024)} MB.");
            yield break;
        }

        string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext)) ext = ".audio";

        string newFileName = Guid.NewGuid().ToString("N") + ext;
        string destPath = Path.Combine(AudioFolder, newFileName);

        bool copied = false;
        try
        {
            Directory.CreateDirectory(AudioFolder);
            File.Copy(sourcePath, destPath, true);
            copied = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CustomBankLibrary] No se pudo copiar el audio: {e.Message}");
        }

        if (!copied)
        {
            onDone?.Invoke(false, "No se pudo copiar el archivo.");
            yield break;
        }

        AudioClip clip = null;
        yield return LoadClipRoutine(destPath, c => clip = c);

        if (clip == null)
        {
            TryDeleteFile(destPath);
            onDone?.Invoke(false, "No se pudo cargar el audio. Prueba con un archivo WAV, MP3 u OGG.");
            yield break;
        }

        if (clip.length > maxClipSeconds)
        {
            Destroy(clip);
            TryDeleteFile(destPath);
            onDone?.Invoke(false, $"El audio dura más de {Mathf.RoundToInt(maxClipSeconds)} segundos.");
            yield break;
        }

        // El banco pudo borrarse o cambiar de posición mientras el audio cargaba.
        int bankIndex = FindBankIndex(bankId);
        if (bankIndex < 0)
        {
            Destroy(clip);
            TryDeleteFile(destPath);
            onDone?.Invoke(false, "El banco ya no existe.");
            yield break;
        }

        CustomPadData pad = data.banks[bankIndex].pads[padIndex];
        string oldFile = pad.audioFileName;

        pad.audioFileName = newFileName;
        if (string.IsNullOrWhiteSpace(pad.displayName))
        {
            pad.displayName = ClampName(Path.GetFileNameWithoutExtension(sourcePath), maxPadNameLength, "");
        }

        clipCache[newFileName] = clip;
        if (!string.IsNullOrEmpty(oldFile)) ReleaseAudio(oldFile);

        RefreshRuntimeBank(bankIndex);
        SaveToDisk();
        OnLibraryChanged?.Invoke();
        onDone?.Invoke(true, "Audio guardado.");
    }

    // ------------------------------------------------------------------
    // Carga de audios
    // ------------------------------------------------------------------

    private IEnumerator LoadAllClipsRoutine()
    {
        // Copia de la lista: la persona puede editar mientras esto carga.
        var snapshot = new List<CustomBankData>(data.banks);

        foreach (CustomBankData bank in snapshot)
        {
            bool anyLoaded = false;

            for (int p = 0; p < bank.pads.Count; p++)
            {
                string fileName = bank.pads[p].audioFileName;
                if (string.IsNullOrEmpty(fileName) || clipCache.ContainsKey(fileName)) continue;

                string path = Path.Combine(AudioFolder, fileName);
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[CustomBankLibrary] Falta el archivo de audio '{fileName}'.");
                    continue;
                }

                AudioClip loaded = null;
                yield return LoadClipRoutine(path, c => loaded = c);

                if (loaded != null)
                {
                    clipCache[fileName] = loaded;
                    anyLoaded = true;
                }
            }

            int index = FindBankIndex(bank.id);
            if (index >= 0 && anyLoaded)
            {
                RefreshRuntimeBank(index);
                OnLibraryChanged?.Invoke();
            }
        }
    }

    private IEnumerator LoadClipRoutine(string fullPath, Action<AudioClip> onDone)
    {
        string uri = new Uri(fullPath).AbsoluteUri;

        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(uri, GetAudioType(fullPath)))
        {
            var handler = (DownloadHandlerAudioClip)request.downloadHandler;
            handler.streamAudio = false; // cargar todo antes de reproducir
            // Descomprimido solo si es liviano (reproducción instantánea); si no, comprimido en memoria.
            long bytes = 0;
            try { bytes = new FileInfo(fullPath).Length; } catch { }
            handler.compressed = bytes > MaxBytesToDecodeFully;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[CustomBankLibrary] No se pudo cargar '{fullPath}': {request.error}");
                onDone?.Invoke(null);
                yield break;
            }

            AudioClip clip = null;
            try
            {
                clip = DownloadHandlerAudioClip.GetContent(request);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CustomBankLibrary] Audio no válido '{fullPath}': {e.Message}");
            }

            onDone?.Invoke(clip);
        }
    }

    private static AudioType GetAudioType(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".wav": return AudioType.WAV;
            case ".mp3": return AudioType.MPEG;
            case ".ogg": return AudioType.OGGVORBIS;
            case ".m4a":
            case ".aac": return AudioType.ACC;
            default: return AudioType.UNKNOWN;
        }
    }

    // ------------------------------------------------------------------
    // Utilidades internas
    // ------------------------------------------------------------------

    /// <summary>Reconstruye el SoundBankSO de ejecución de un banco a partir de sus datos guardados.</summary>
    private void RefreshRuntimeBank(int bankIndex)
    {
        CustomBankData bank = data.banks[bankIndex];
        SoundBankSO runtime = runtimeBanks[bankIndex];
        runtime.bankName = bank.name;

        for (int i = 0; i < PadsPerBank; i++)
        {
            runtime.SetPad(i, BuildPadSoundData(bank.pads[i], i));
        }
    }

    private PadSoundData BuildPadSoundData(CustomPadData pad, int padIndex)
    {
        AudioClip clip = null;
        if (pad.HasAudio) clipCache.TryGetValue(pad.audioFileName, out clip);

        string name = pad.displayName;
        if (string.IsNullOrWhiteSpace(name) && pad.HasAudio) name = $"Pad {padIndex + 1}";

        return new PadSoundData
        {
            displayName = name ?? "",
            clip = clip,
            iconOff = padIconOff,
            emptyOverlay = emptyPadOverlaySprite,
            iconOn = padIconOn
        };
    }

    private int FindBankIndex(string bankId)
    {
        for (int i = 0; i < data.banks.Count; i++)
        {
            if (data.banks[i].id == bankId) return i;
        }
        return -1;
    }

    /// <summary>Libera el clip en memoria y borra el archivo copiado.</summary>
    private void ReleaseAudio(string fileName)
    {
        if (clipCache.TryGetValue(fileName, out AudioClip clip))
        {
            if (clip != null) Destroy(clip);
            clipCache.Remove(fileName);
        }
        TryDeleteFile(Path.Combine(AudioFolder, fileName));
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception e) { Debug.LogWarning($"[CustomBankLibrary] No se pudo borrar '{path}': {e.Message}"); }
    }

    private static string ClampName(string raw, int maxLength, string fallback)
    {
        string text = (raw ?? "").Trim();
        if (text.Length > maxLength) text = text.Substring(0, maxLength).TrimEnd();
        return text.Length == 0 ? fallback : text;
    }

    /// <summary>Asegura id, nombre y exactamente 8 pads sin nulos (por si el JSON viene incompleto).</summary>
    private void NormalizeBank(CustomBankData bank)
    {
        if (string.IsNullOrEmpty(bank.id)) bank.id = Guid.NewGuid().ToString("N");
        bank.name = ClampName(bank.name, maxBankNameLength, "Mi banco");
        if (bank.pads == null) bank.pads = new List<CustomPadData>();

        for (int i = 0; i < bank.pads.Count; i++)
        {
            if (bank.pads[i] == null) bank.pads[i] = new CustomPadData();
        }
        while (bank.pads.Count < PadsPerBank) bank.pads.Add(new CustomPadData());
        if (bank.pads.Count > PadsPerBank) bank.pads.RemoveRange(PadsPerBank, bank.pads.Count - PadsPerBank);
    }

    // ------------------------------------------------------------------
    // Persistencia
    // ------------------------------------------------------------------

    private void LoadFromDisk()
    {
        try
        {
            if (File.Exists(JsonPath))
            {
                var loaded = JsonUtility.FromJson<CustomBankLibraryData>(File.ReadAllText(JsonPath));
                if (loaded != null && loaded.banks != null) data = loaded;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CustomBankLibrary] custom_banks.json ilegible, se guarda una copia y se empieza vacío: {e.Message}");
            try { File.Copy(JsonPath, JsonPath + ".bak", true); } catch { /* sin copia de respaldo */ }
            data = new CustomBankLibraryData();
        }

        foreach (CustomBankData bank in data.banks) NormalizeBank(bank);
    }

    private void SaveToDisk()
    {
        try
        {
            string tempPath = JsonPath + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));
            if (File.Exists(JsonPath)) File.Delete(JsonPath);
            File.Move(tempPath, JsonPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[CustomBankLibrary] No se pudo guardar custom_banks.json: {e.Message}");
        }
    }
}
