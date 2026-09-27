using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Maneja toda la reproducción de audio de la aplicación:
///  - Precarga TODOS los clips de TODOS los bancos al abrir la app (sin delay al presionar pads).
///  - Mantiene un pool de AudioSources para que varios sonidos puedan sonar a la vez
///    sin cortarse entre sí (requerimiento "Reproducción Simultánea").
///  - Expone el volumen general y el potenciador de graves (Bass Boost) vía un AudioMixer.
///
/// Requiere, del lado del Editor (esto no se puede automatizar sin un Editor abierto):
///  1. Un Audio Mixer llamado "MasterMixer" con al menos:
///       - Grupo "SFX" (donde se reproducen los pads).
///       - Parámetro expuesto "MasterVolume" (controla el Volume del grupo Master, en dB).
///       - Un efecto ParamEQ (o Lowpass) en el grupo SFX con su ganancia de graves expuesta
///         como "BassGain", para poder animarla entre "apagado" y "potenciado".
///  2. Todos los AudioClip importados con "Load Type" = Decompress On Load
///     (así LoadAudioData() realmente los deja listos en memoria).
/// </summary>
[DefaultExecutionOrder(-100)] // se inicializa antes que los pads/bancos
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer")]
    [SerializeField] private AudioMixer masterMixer;
    [SerializeField] private AudioMixerGroup sfxMixerGroup; // fallback si un pad no tiene canal asignado
    [SerializeField] private string masterVolumeParam = "MasterVolume";
    [SerializeField] private string bassGainParam = "BassGain";

    [Header("Canales 1-8 (uno por posición de pad, deben calzar en orden con MixerChannelController)")]
    [Tooltip("Arrastra acá los grupos Channel1...Channel8 del AudioMixer, EN ORDEN " +
             "(elemento 0 = Channel1, elemento 7 = Channel8). Cada pad rutea según SU POSICIÓN " +
             "en la grilla (slot 0-7), no según qué sonido tenga — así el fader de cada canal " +
             "siempre controla 'lo que sea que esté en ese pad ahora', incluso al cambiar de banco.")]
    [SerializeField] private AudioMixerGroup[] channelGroups = new AudioMixerGroup[8];

    [Header("Polifonía")]
    [Tooltip("Cantidad de sonidos que pueden sonar exactamente al mismo tiempo.")]
    [SerializeField] private int voicePoolSize = 16;

    [Header("Precarga")]
    [Tooltip("Todos los bancos de sonido de la app. Se recorren al iniciar para precargar cada clip.")]
    [SerializeField] private List<SoundBankSO> allBanks;

    [Header("Bass Boost")]
    [SerializeField] private float bassGainOffDb = 0f;
    [SerializeField] private float bassGainOnDb = 12f;

    private readonly List<AudioSource> voicePool = new List<AudioSource>();
    private int nextVoiceIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildVoicePool();
        PreloadAllClips();
        SetMasterVolume(initialVolume01);
    }

    private void BuildVoicePool()
    {
        for (int i = 0; i < voicePoolSize; i++)
        {
            var voiceObj = new GameObject($"Voice_{i}");
            voiceObj.transform.SetParent(transform);
            var source = voiceObj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = sfxMixerGroup;
            voicePool.Add(source);
        }
    }

    /// <summary>
    /// Fuerza la decodificación en memoria de todos los clips usados en todos los bancos,
    /// para que el primer toque de cada pad sea instantáneo (sin stutter de carga).
    /// </summary>
    private void PreloadAllClips()
    {
        if (allBanks == null) return;

        foreach (var bank in allBanks)
        {
            if (bank == null) continue;
            for (int i = 0; i < bank.PadCount; i++)
            {
                var pad = bank.GetPad(i);
                if (pad != null && pad.clip != null)
                {
                    pad.clip.LoadAudioData();
                }
            }
        }
    }

    /// <summary>
    /// Reproduce un clip SIN rutear a ningún canal específico (sale por SFX).
    /// Úsala solo para sonidos que no vienen de un pad de la grilla (por ejemplo
    /// un efecto de UI). Los pads deben usar la sobrecarga con slotIndex.
    /// </summary>
    public AudioSource PlaySound(AudioClip clip)
    {
        if (clip == null) return null;

        AudioSource source = GetFreeVoice();
        source.outputAudioMixerGroup = sfxMixerGroup;
        source.clip = clip;
        source.Play();
        return source;
    }

    /// <summary>
    /// Reproduce un clip ruteado al canal Channel(slotIndex+1) del Mixer, según la
    /// POSICIÓN física del pad que lo disparó (0-7), no según el sonido en sí.
    /// Así el fader de ese canal en la pestaña de Mezcla controla lo que sea que
    /// esté en ese pad ahora mismo, sin importar qué banco esté cargado. Esta es
    /// la que debe usar PadButtonUI.
    /// </summary>
    public AudioSource PlaySound(AudioClip clip, int slotIndex)
    {
        if (clip == null) return null;

        AudioSource source = GetFreeVoice();
        source.outputAudioMixerGroup = GetChannelGroup(slotIndex);
        source.clip = clip;
        source.Play();
        return source;
    }

    private AudioMixerGroup GetChannelGroup(int slotIndex0To7)
    {
        if (slotIndex0To7 < 0 || slotIndex0To7 >= channelGroups.Length || channelGroups[slotIndex0To7] == null)
        {
            Debug.LogWarning(
                $"[AudioManager] No hay AudioMixerGroup asignado para el pad en el slot {slotIndex0To7} " +
                "(Channel Groups) — este sonido va a salir por SFX sin pasar por ningún canal " +
                "individual, así que su fader en Mezcla no le hará nada.");
            return sfxMixerGroup;
        }
        return channelGroups[slotIndex0To7];
    }

    private AudioSource GetFreeVoice()
    {
        // Primero buscamos una voz completamente libre.
        for (int i = 0; i < voicePool.Count; i++)
        {
            if (!voicePool[i].isPlaying) return voicePool[i];
        }

        // Si todas están ocupadas (caso límite), reciclamos la más antigua (round-robin)
        // en vez de no reproducir el sonido nuevo.
        var voice = voicePool[nextVoiceIndex];
        nextVoiceIndex = (nextVoiceIndex + 1) % voicePool.Count;
        return voice;
    }

    // ---------------- Volumen general ----------------

    [Header("Volumen General")]
    [Tooltip("Umbral por debajo del cual se considera 'arrastrado al mínimo' y se mutea explícitamente.")]
    [SerializeField] private float muteThreshold01 = 0.01f;
    [SerializeField] private float mutedDb = -80f; // silencio real para el AudioMixer
    [SerializeField] [Range(0f, 1f)] private float initialVolume01 = 1f;

    public bool IsMuted { get; private set; }
    public float CurrentVolume01 { get; private set; }

    /// <summary>
    /// Se dispara cada vez que el volumen cambia, sin importar qué slider lo originó.
    /// Lo usan los MasterVolumeController de cada pantalla (Pads y Mezcla) para
    /// mantener sus perillas sincronizadas entre sí.
    /// </summary>
    public event System.Action<float> OnMasterVolumeChanged;

    /// <summary>value entre 0 (silencio) y 1 (máximo). Al llegar al mínimo mutea explícitamente
    /// en vez de solo acercarse a -80dB por la curva logarítmica.</summary>
    public void SetMasterVolume(float value01)
    {
        CurrentVolume01 = Mathf.Clamp01(value01);
        IsMuted = CurrentVolume01 <= muteThreshold01;

        if (IsMuted)
        {
            masterMixer.SetFloat(masterVolumeParam, mutedDb);
        }
        else
        {
            float clamped = Mathf.Clamp(CurrentVolume01, 0.0001f, 1f);
            float db = Mathf.Log10(clamped) * 20f;
            masterMixer.SetFloat(masterVolumeParam, db);
        }

        OnMasterVolumeChanged?.Invoke(CurrentVolume01);
    }

    // ---------------- Bass Boost ----------------

    public float CurrentBassAmount01 { get; private set; }

    /// <summary>value entre 0 (sin potenciar) y 1 (máximo potenciado). Interpola
    /// linealmente entre bassGainOffDb y bassGainOnDb — es un fader continuo,
    /// no un simple on/off.</summary>
    public void SetBassBoostAmount(float value01)
    {
        CurrentBassAmount01 = Mathf.Clamp01(value01);
        float db = Mathf.Lerp(bassGainOffDb, bassGainOnDb, CurrentBassAmount01);
        masterMixer.SetFloat(bassGainParam, db);
    }

    // Compatibilidad por si en algún lado se quiere un simple on/off en vez del fader.
    public void SetBassBoost(bool enabled) => SetBassBoostAmount(enabled ? 1f : 0f);
    public void ToggleBassBoost() => SetBassBoost(CurrentBassAmount01 < 0.5f);
}
