using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
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
///   1) Al SOLTAR un fader de canal, suena el pad correspondiente (para que el
///      usuario escuche el efecto del cambio). No suena en cada frame mientras
///      se arrastra — eso generaba un sonido molesto y repetitivo — sino una
///      sola vez, al levantar el dedo/soltar el mouse (IPointerUpHandler).
///      Al mover Master o Bass (ver MasterVolumeController/BassBoostController)
///      debería sonar un clip de REFERENCIA propio en vez de un pad — eso se
///      maneja con MixerReferenceSoundController en esos dos sliders, con el
///      mismo comportamiento de "solo al soltar".
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
    /// <summary>
    /// Detecta cuándo se SUELTA un Slider específico (PointerUp), para disparar
    /// el sonido de feedback solo en ese momento en vez de en cada frame de
    /// arrastre. Se agrega automáticamente sobre cada channelSlider[i] desde
    /// InitializeSlidersOnce() — no hace falta ponerlo a mano en el Editor.
    /// </summary>
    private class SliderReleaseNotifier : MonoBehaviour, IPointerUpHandler
    {
        public System.Action OnReleased;
        public void OnPointerUp(PointerEventData eventData) => OnReleased?.Invoke();
    }


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
    private float[] lastSliderValues; // último valor conocido de cada slider, para sonar con el valor correcto al soltar

    /// <summary>
    /// Crea los arreglos de estado interno si todavía no existen. Se llama desde
    /// cualquier método que los use, porque Unity ejecuta OnEnable ANTES que
    /// Start: si estos arreglos se crearan solo en Start(), un OnEnable temprano
    /// (por ejemplo al activar MixerScreen por primera vez) los encontraría en
    /// null y fallaría con NullReferenceException, dejando los labels vacíos.
    /// </summary>
    private void EnsureStateArrays()
    {
        if (lastPadNames != null) return;

        int size = Mathf.Max(channelSliders.Length, channelLabels.Length);
        percentLabelRoutines = new Coroutine[size];
        lastPadNames = new string[size];
        lastSliderValues = new float[size];
    }

    private void OnEnable()
    {
        EnsureStateArrays();
        InitializeSlidersOnce();

        if (bankManager == null) return;

        // Se suscribe acá (no en Start) para que funcione sin importar si este
        // componente vive en un objeto que arranca activo o desactivado, y se
        // des-suscribe en OnDisable para no acumular suscripciones al reactivar.
        bankManager.OnBankLoaded += UpdateChannelLabels;

        // Pregunta el banco actual directamente en vez de depender de haber
        // escuchado el evento: el banco ya pudo cargarse antes de que este
        // componente se activara. Leer SoundBankSO no requiere que los labels
        // estén visibles, así que los nombres quedan puestos desde antes de
        // que el usuario abra la pantalla.
        if (bankManager.CurrentBank != null)
        {
            UpdateChannelLabels(bankManager.CurrentBank);
        }
    }

    private void OnDisable()
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

        EnsureStateArrays();

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
            lastSliderValues[index] = initialChannelVolume01;

            channelSliders[i].onValueChanged.AddListener(value => OnChannelSliderChanged(index, value));

            // El sonido de feedback NO va en onValueChanged (dispara en cada
            // frame mientras se arrastra — eso era el sonido molesto). Se
            // agrega un listener de PointerUp sobre el mismo GameObject del
            // Slider para que suene una sola vez, al soltar.
            SliderReleaseNotifier releaseNotifier = channelSliders[i].gameObject.GetComponent<SliderReleaseNotifier>();
            if (releaseNotifier == null)
            {
                releaseNotifier = channelSliders[i].gameObject.AddComponent<SliderReleaseNotifier>();
            }
            releaseNotifier.OnReleased += () => OnChannelSliderReleased(index);
        }
    }

    /// <summary>Se llama en CADA frame de arrastre: actualiza volumen real, X de mute y % del label.</summary>
    private void OnChannelSliderChanged(int index, float value01)
    {
        SetChannelVolume(index, value01);
        UpdateMuteIndicator(index, value01);
        ShowPercentLabelTemporarily(index, value01);
        lastSliderValues[index] = value01;
    }

    /// <summary>Se llama UNA VEZ al soltar el fader: acá va el sonido de feedback, para no repetirlo en cada frame.</summary>
    private void OnChannelSliderReleased(int index)
    {
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
        if (bank == null) return;
        EnsureStateArrays();

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
