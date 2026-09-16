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
    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    [SerializeField] private string masterVolumeParam = "MasterVolume";
    [SerializeField] private string bassGainParam = "BassGain";

    [Header("Polifonía")]
    [Tooltip("Cantidad de sonidos que pueden sonar exactamente al mismo tiempo.")]
    [SerializeField] private int voicePoolSize = 16;

    [Header("Precarga")]
    [Tooltip("Todos los bancos de sonido de la app. Se recorren al iniciar para precargar cada clip.")]
    [SerializeField] private List<SoundBankSO> allBanks;

    [Header("Bass Boost")]
    [SerializeField] private float bassGainOffDb = 0f;
    [SerializeField] private float bassGainOnDb = 12f;
    private bool bassBoostEnabled;

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
    /// Reproduce un clip usando la próxima voz libre del pool (round-robin sobre las
    /// que no están sonando). Devuelve el AudioSource usado para que quien llamó
    /// (por ejemplo un pad) pueda saber cuándo termina y apagar su luz indicadora.
    /// </summary>
    public AudioSource PlaySound(AudioClip clip)
    {
        if (clip == null) return null;

        AudioSource source = GetFreeVoice();
        source.clip = clip;
        source.Play();
        return source;
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

    /// <summary>value entre 0 (silencio) y 1 (máximo).</summary>
    public void SetMasterVolume(float value01)
    {
        float clamped = Mathf.Clamp(value01, 0.0001f, 1f);
        float db = Mathf.Log10(clamped) * 20f;
        masterMixer.SetFloat(masterVolumeParam, db);
    }

    // ---------------- Bass Boost ----------------

    public bool BassBoostEnabled => bassBoostEnabled;

    public void SetBassBoost(bool enabled)
    {
        bassBoostEnabled = enabled;
        float target = enabled ? bassGainOnDb : bassGainOffDb;
        masterMixer.SetFloat(bassGainParam, target);
    }

    public void ToggleBassBoost() => SetBassBoost(!bassBoostEnabled);
}
