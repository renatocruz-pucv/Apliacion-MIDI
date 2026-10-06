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
///
/// IMPORTANTE: MixerScreen puede arrancar con SetActive(false) (el panel de
/// Mezcla oculto hasta que el usuario toca el botón para entrar). Si este script
/// se suscribiera a BankManager.OnBankLoaded solo en Start(), se perdería el
/// aviso del banco inicial — porque PadsScreen ya cargó el banco 0 ANTES de que
/// el usuario activara el panel de Mezcla por primera vez. Por eso OnEnable()
/// pide el banco actual directamente (bankManager.CurrentBank) cada vez que el
/// panel se activa, en vez de depender únicamente de haber escuchado el evento.
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

    [Tooltip("Volumen (0-1) con el que arranca cada uno de los 8 canales al abrir la app. " +
             "Sin esto, cada Slider parte donde haya quedado puesto en el Editor (típicamente " +
             "0), así que algunos pads suenan mudos hasta que alguien suba el fader a mano.")]
    [SerializeField] [Range(0f, 1f)] private float initialChannelVolume01 = 0.8f;

    private bool slidersInitialized;

    private void Start()
    {
        InitializeSlidersOnce();

        if (bankManager != null)
        {
            bankManager.OnBankLoaded += UpdateChannelLabels;
        }
    }

    private void OnEnable()
    {
        // Cubre el caso de MixerScreen arrancando desactivado: Start() de este
        // componente puede no haber corrido aún la primera vez que se activa
        // (Unity llama Awake/OnEnable de un objeto inactivo recién cuando se
        // activa, y Start inmediatamente después) — y aunque corra, el banco
        // ya pudo haberse cargado antes. Preguntar el estado actual es más
        // confiable que depender solo del evento.
        if (bankManager != null && bankManager.CurrentBank != null)
        {
            UpdateChannelLabels(bankManager.CurrentBank);
        }
    }

    private void OnDestroy()
    {
        if (bankManager != null)
        {
            bankManager.OnBankLoaded -= UpdateChannelLabels;
        }
    }

    private void InitializeSlidersOnce()
    {
        if (slidersInitialized) return;
        slidersInitialized = true;

        for (int i = 0; i < channelSliders.Length; i++)
        {
            int index = i; // captura para el closure
            if (channelSliders[i] == null) continue;

            // Aplica el volumen inicial tanto al Mixer como a la posición visual
            // del Slider, para que abran ya audibles y la perilla no quede
            // desincronizada con lo que realmente se está escuchando.
            channelSliders[i].SetValueWithoutNotify(initialChannelVolume01);
            SetChannelVolume(index, initialChannelVolume01);

            channelSliders[i].onValueChanged.AddListener(value => SetChannelVolume(index, value));
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
