using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Singleton de audio: pool de voces para polifonía, ruteo por canal (0-7),
/// volumen maestro con mute explícito, y bass boost continuo (0-1 → dB).
/// </summary>
[DefaultExecutionOrder(-100)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer")]
    [SerializeField] private AudioMixer masterMixer;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    [SerializeField] private AudioMixerGroup[] channelGroups = new AudioMixerGroup[8]; // Channel1..Channel8, en orden

    [Header("Pool de voces")]
    [SerializeField] private int voicePoolSize = 16;
    private List<AudioSource> voicePool;

    [Header("Volumen maestro")]
    [SerializeField] private string masterVolumeParam = "MasterVolume";
    [SerializeField] [Range(0f, 1f)] private float initialVolume01 = 0.8f;
    [SerializeField] private float mutedDb = -80f;
    private const float muteThreshold01 = 0.01f;

    public float CurrentVolume01 { get; private set; }
    public bool IsMuted => CurrentVolume01 <= muteThreshold01;
    public event System.Action<float> OnMasterVolumeChanged;

    [Header("Bass boost")]
    [SerializeField] private string bassGainParam = "BassGain";
    [SerializeField] private float bassGainOffDb = 0f;
    [SerializeField] private float bassGainOnDb = 12f;
    public float CurrentBassAmount01 { get; private set; }

    [Header("Bancos (para precarga)")]
    [SerializeField] private List<SoundBankSO> allBanks;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildVoicePool();
        PreloadAllClips();

        SetMasterVolume(initialVolume01);
        SetBassBoostAmount(0f);
    }

    private void BuildVoicePool()
    {
        voicePool = new List<AudioSource>(voicePoolSize);
        for (int i = 0; i < voicePoolSize; i++)
        {
            GameObject voiceGO = new GameObject($"Voice_{i}");
            voiceGO.transform.SetParent(transform);
            AudioSource src = voiceGO.AddComponent<AudioSource>();
            src.playOnAwake = false;
            voicePool.Add(src);
        }
    }

    private void PreloadAllClips()
    {
        if (allBanks == null) return;
        foreach (var bank in allBanks)
        {
            if (bank == null) continue;
            for (int i = 0; i < bank.PadCount; i++)
            {
                PadSoundData pad = bank.GetPad(i);
                if (pad != null && pad.clip != null)
                {
                    pad.clip.LoadAudioData();
                }
            }
        }
    }

    private AudioSource GetFreeVoice()
    {
        foreach (var voice in voicePool)
        {
            if (!voice.isPlaying) return voice;
        }
        // Si todas están ocupadas, reusa la más antigua (voicePool[0]) en vez de fallar.
        return voicePool[0];
    }

    /// <summary>Reproduce un sonido fuera del ruteo por pad (ej. SFX de UI). Rutea a SFX.</summary>
    public AudioSource PlaySound(AudioClip clip)
    {
        if (clip == null) return null;
        AudioSource voice = GetFreeVoice();
        voice.clip = clip;
        voice.outputAudioMixerGroup = sfxMixerGroup;
        voice.Play();
        return voice;
    }

    /// <summary>Reproduce el sonido de un pad, ruteado al canal del Mixer según su slot (0-7).</summary>
    public AudioSource PlaySound(AudioClip clip, int slotIndex)
    {
        if (clip == null) return null;
        AudioSource voice = GetFreeVoice();
        voice.clip = clip;
        voice.outputAudioMixerGroup = GetChannelGroup(slotIndex);
        voice.Play();
        return voice;
    }

    private AudioMixerGroup GetChannelGroup(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < channelGroups.Length && channelGroups[slotIndex] != null)
        {
            return channelGroups[slotIndex];
        }
        Debug.LogWarning($"AudioManager: no hay AudioMixerGroup asignado para el canal {slotIndex}, usando SFX como respaldo.");
        return sfxMixerGroup;
    }

    public void SetMasterVolume(float value01)
    {
        CurrentVolume01 = Mathf.Clamp01(value01);
        float db = IsMuted ? mutedDb : LinearToDecibel(CurrentVolume01);
        masterMixer.SetFloat(masterVolumeParam, db);
        OnMasterVolumeChanged?.Invoke(CurrentVolume01);
    }

    public void SetBassBoostAmount(float value01)
    {
        CurrentBassAmount01 = Mathf.Clamp01(value01);
        float db = Mathf.Lerp(bassGainOffDb, bassGainOnDb, CurrentBassAmount01);
        masterMixer.SetFloat(bassGainParam, db);
    }

    public void SetBassBoost(bool enabled) => SetBassBoostAmount(enabled ? 1f : 0f);
    public void ToggleBassBoost() => SetBassBoost(CurrentBassAmount01 < 0.5f);

    private static float LinearToDecibel(float linear01)
    {
        float clamped = Mathf.Clamp(linear01, 0.0001f, 1f);
        return Mathf.Log10(clamped) * 20f;
    }
}
