using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// Conecta los 8 Sliders de canal de la pestaña de Mezcla a los parámetros
/// expuestos del AudioMixer — uno por cada posición de pad en la grilla (0-7).
///
/// El fader maestro y el de Bass NO se manejan acá — usan MasterVolumeController
/// y BassBoostController respectivamente, cada uno en su propio Slider. Lo que
/// SÍ maneja este script, además del volumen, son 3 extras pedidos para el
/// Mixer (solo acá — el fader maestro de la pantalla principal de Pads nunca
/// pasa por este componente, así que no se ve afectado):
///
///   1) Al mover un fader de canal, suena el pad correspondiente (para que el
///      usuario escuche el efecto del cambio al instante). Al mover Master o
///      Bass (ver MasterVolumeController/BassBoostController) debería sonar un
///      clip de REFERENCIA propio en vez de un pad — eso se maneja con
///      MixerReferenceSoundController en esos dos sliders.
///   2) Si el fader queda en 0, se muestra un indicador visual de "silenciado"
///      (muteIndicators[i], ej. una X roja) superpuesto al fader. El indicador
///      tiene raycastTarget=false por diseño (ver el prefab) para no bloquear
///      el arrastre del Slider — este script solo lo prende/apaga.
///   3) El texto bajo cada fader (channelLabels) normalmente muestra el nombre
///      del pad, pero al mover el fader cambia por unos segundos a mostrar el
///      porcentaje de volumen, y luego vuelve a mostrar el nombre.
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

    [Tooltip("Indicador visual de 'silenciado' (ej. una X roja) por cada canal, " +
             "superpuesto al fader. Debe tener Raycast Target DESACTIVADO en su " +
             "Image para no bloquear el arrastre del Slider que tiene debajo — " +
             "es solo un overlay visual, nunca capta el toque.")]
    [SerializeField] private GameObject[] muteIndicators = new GameObject[8];

    [Tooltip("Volumen (0-1) con el que arranca cada uno de los 8 canales al abrir la app. " +
             "Sin esto, cada Slider parte donde haya quedado puesto en el Editor (típicamente " +
             "0), así que algunos pads suenan mudos hasta que alguien suba el fader a mano.")]
    [SerializeField] [Range(0f, 1f)] private float initialChannelVolume01 = 0.8f;

    [Header("Feedback de audio al mover el fader")]
    [Tooltip("Si está activo, mover un fader de canal reproduce el sonido de su " +
             "pad correspondiente (ruteado por el mismo canal del Mixer que ese " +
             "fader controla), para que el cambio de volumen se escuche al instante.")]
    [SerializeField] private bool playPadSoundOnDrag = true;

    [Header("Texto temporal con el % de volumen")]
    [Tooltip("Segundos que el label muestra el porcentaje antes de volver a mostrar el nombre del pad.")]
    [SerializeField] private float percentLabelDuration = 1.5f;

    private bool slidersInitialized;
    private Coroutine[] percentLabelRoutines;
    private string[] lastPadNames; // lo que channelLabels[i] debe mostrar cuando NO está mostrando el %

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

        percentLabelRoutines = new Coroutine[channelSliders.Length];
        lastPadNames = new string[channelSliders.Length];

        for (int i = 0; i < channelSliders.Length; i++)
        {
            int index = i; // captura para el closure
            if (channelSliders[i] == null) continue;

            // Aplica el volumen inicial tanto al Mixer como a la posición visual
            // del Slider, para que abran ya audibles y la perilla no quede
            // desincronizada con lo que realmente se está escuchando.
            channelSliders[i].SetValueWithoutNotify(initialChannelVolume01);
            SetChannelVolume(index, initialChannelVolume01);
            UpdateMuteIndicator(index, initialChannelVolume01);

            channelSliders[i].onValueChanged.AddListener(value => OnChannelSliderChanged(index, value));
        }
    }

    private void OnChannelSliderChanged(int index, float value01)
    {
        SetChannelVolume(index, value01);
        UpdateMuteIndicator(index, value01);
        ShowPercentLabelTemporarily(index, value01);

        if (playPadSoundOnDrag)
        {
            PlayPadPreviewSound(index);
        }
    }

    /// <summary>Reproduce el sonido asignado ahora mismo al pad de este slot, ruteado por el mismo canal.</summary>
    private void PlayPadPreviewSound(int slotIndex)
    {
        if (bankManager == null || bankManager.CurrentBank == null) return;
        if (AudioManager.Instance == null) return;

        PadSoundData pad = bankManager.CurrentBank.GetPad(slotIndex);
        if (pad == null || pad.clip == null) return;

        AudioManager.Instance.PlaySound(pad.clip, slotIndex);
    }

    /// <summary>Muestra/oculta el indicador de silenciado (ej. X roja) según si el fader quedó en 0.</summary>
    private void UpdateMuteIndicator(int index, float value01)
    {
        if (muteIndicators == null || index >= muteIndicators.Length) return;
        if (muteIndicators[index] == null) return;

        bool isMuted = value01 <= 0.001f;
        muteIndicators[index].SetActive(isMuted);
    }

    /// <summary>Refresca el texto bajo cada fader con el nombre del sonido actual en ese slot.</summary>
    private void UpdateChannelLabels(SoundBankSO bank)
    {
        for (int i = 0; i < channelLabels.Length; i++)
        {
            if (channelLabels[i] == null) continue;
            PadSoundData pad = bank.GetPad(i);
            string name = (pad != null && pad.clip != null) ? pad.displayName : string.Empty;

            lastPadNames[i] = name;

            // Si justo se está mostrando el % en este label, no lo pises —
            // la corrutina lo restaurará sola al terminar.
            if (percentLabelRoutines == null || percentLabelRoutines[i] == null)
            {
                channelLabels[i].text = name;
            }
        }
    }

    /// <summary>Muestra el % de volumen en el label por unos segundos y luego vuelve al nombre del pad.</summary>
    private void ShowPercentLabelTemporarily(int index, float value01)
    {
        if (channelLabels == null || index >= channelLabels.Length || channelLabels[index] == null) return;

        if (percentLabelRoutines[index] != null)
        {
            StopCoroutine(percentLabelRoutines[index]);
        }
        percentLabelRoutines[index] = StartCoroutine(PercentLabelRoutine(index, value01));
    }

    private IEnumerator PercentLabelRoutine(int index, float value01)
    {
        int percent = Mathf.RoundToInt(value01 * 100f);
        channelLabels[index].text = $"{percent}%";

        yield return new WaitForSeconds(percentLabelDuration);

        channelLabels[index].text = (lastPadNames != null && lastPadNames[index] != null) ? lastPadNames[index] : string.Empty;
        percentLabelRoutines[index] = null;
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
