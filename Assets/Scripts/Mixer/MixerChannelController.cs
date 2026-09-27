using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// Conecta los 8 Sliders de canal de la pestaña de Mezcla a los parámetros
/// expuestos del AudioMixer — uno por cada posición de pad en la grilla (0-7).
///
/// El fader maestro y el de Bass NO se manejan acá — usan MasterVolumeController
/// y BassBoostController respectivamente, cada uno en su propio Slider.
///
/// Las etiquetas (channelLabels) muestran el nombre del sonido que hay AHORA en
/// cada slot, y se actualizan solas cada vez que BankManager cambia de banco —
/// por eso este script necesita una referencia a BankManager, no solo al Mixer.
/// </summary>
public class MixerChannelController : MonoBehaviour
{
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private BankManager bankManager;

    [Header("Canales 1-8 (uno por pad)")]
    [SerializeField] private Slider[] channelSliders = new Slider[8];
    [SerializeField] private Text[] channelLabels = new Text[8]; // texto bajo cada fader; cambiar a TMP_Text si aplica
    [SerializeField] private string[] channelParamNames = new string[8]
    {
        "Channel1Vol", "Channel2Vol", "Channel3Vol", "Channel4Vol",
        "Channel5Vol", "Channel6Vol", "Channel7Vol", "Channel8Vol"
    };

    private void Start()
    {
        for (int i = 0; i < channelSliders.Length; i++)
        {
            int index = i; // captura para el closure
            if (channelSliders[i] == null) continue;
            channelSliders[i].onValueChanged.AddListener(value => SetChannelVolume(index, value));
        }

        if (bankManager != null)
        {
            bankManager.OnBankLoaded += UpdateChannelLabels;
        }
    }

    private void OnDestroy()
    {
        if (bankManager != null)
        {
            bankManager.OnBankLoaded -= UpdateChannelLabels;
        }
    }

    /// <summary>Refresca el texto bajo cada fader con el nombre del sonido actual en ese slot.</summary>
    private void UpdateChannelLabels(SoundBankSO bank)
    {
        for (int i = 0; i < channelLabels.Length; i++)
        {
            if (channelLabels[i] == null) continue;
            PadSoundData pad = bank.GetPad(i);
            channelLabels[i].text = (pad != null && pad.clip != null) ? pad.displayName : string.Empty;
        }
    }

    private void SetChannelVolume(int channelIndex, float linear01)
    {
        float db = LinearToDecibel(linear01);
        audioMixer.SetFloat(channelParamNames[channelIndex], db);
    }

    private static float LinearToDecibel(float linear01)
    {
        float clamped = Mathf.Clamp(linear01, 0.0001f, 1f);
        return Mathf.Log10(clamped) * 20f;
    }
}
