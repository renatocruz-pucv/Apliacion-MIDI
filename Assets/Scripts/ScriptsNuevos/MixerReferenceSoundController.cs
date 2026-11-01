using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Versión de los "extras" del Mixer (sonido de feedback, X roja de mute, label
/// temporal con %) para los faders de MASTER y BASS dentro de MixerScreen —
/// que no controlan un pad específico, así que no tiene sentido reproducir un
/// sonido de pad al moverlos. En su lugar reproducen un AudioClip de
/// REFERENCIA fijo (asignado en el Inspector) para que el usuario pueda
/// escuchar el efecto del cambio igual que en los canales 1-8.
///
/// El sonido de referencia suena solo al SOLTAR el fader (IPointerUpHandler),
/// no en cada frame de arrastre — igual que en MixerChannelController, para
/// evitar el sonido repetitivo y molesto de dispararlo en cada onValueChanged.
///
/// IMPORTANTE: este componente se agrega SOLO en los Sliders de Master/Bass
/// dentro de MixerScreen. El fader maestro de la pantalla principal de Pads
/// (MasterSlider en PadsScreen) NO debe llevar este componente ni ningún
/// equivalente — ese debe quedar mudo, tal como se pidió.
///
/// El propio cambio de volumen (Master → AudioManager.SetMasterVolume, Bass →
/// AudioManager.SetBassBoostAmount) sigue a cargo de MasterVolumeController /
/// BassBoostController, cada uno ya enganchado al mismo Slider — este
/// componente NO reemplaza a esos, solo agrega el sonido + la X + el label.
/// </summary>
[RequireComponent(typeof(Slider))]
public class MixerReferenceSoundController : MonoBehaviour, IPointerUpHandler
{
    [Tooltip("Sonido de referencia a reproducir al SOLTAR este fader " +
             "(distinto para Master y para Bass — asignar un clip propio en cada uno).")]
    [SerializeField] private AudioClip referenceClip;

    [Tooltip("Indicador visual de 'silenciado' (ej. una X roja), superpuesto al " +
             "fader. Debe tener Raycast Target DESACTIVADO para no bloquear el " +
             "arrastre del Slider — es solo un overlay visual.")]
    [SerializeField] private GameObject muteIndicator;

    [Tooltip("Texto bajo el fader (normalmente 'MASTER' o 'BASS') que se reemplaza " +
             "temporalmente por el porcentaje al mover el Slider.")]
    [SerializeField] private Text label;

    [Tooltip("Texto fijo a restaurar en el label cuando termina de mostrarse el %.")]
    [SerializeField] private string idleLabelText = "MASTER";

    [SerializeField] private float percentLabelDuration = 1.5f;

    private Slider slider;
    private Coroutine percentLabelRoutine;
    private float lastValue;
    private float lastIndicatorValue = -1f; // valor con el que se calculó la X por última vez

    private void Awake() => slider = GetComponent<Slider>();

    private void OnEnable()
    {
        slider.onValueChanged.AddListener(HandleValueChanged);
        lastValue = slider.value;
        UpdateMuteIndicator(slider.value);
        if (label != null && percentLabelRoutine == null) label.text = idleLabelText;
    }

    private void OnDisable() => slider.onValueChanged.RemoveListener(HandleValueChanged);

    /// <summary>
    /// Revisa la X de mute cada frame. MasterVolumeController y BassBoostController
    /// posicionan el slider con SetValueWithoutNotify (a propósito, para no
    /// reaplicar el valor al mixer), y eso NO dispara onValueChanged. Sin esta
    /// revisión, la X quedaba calculada con el valor que el slider traía de la
    /// escena (0) y se mostraba aunque el volumen real fuera otro, sobre todo
    /// cuando MixerScreen arranca desactivado. LateUpdate corre después de los
    /// OnEnable/Start de los demás controladores, así que ya ven el valor final.
    /// </summary>
    private void LateUpdate()
    {
        if (!Mathf.Approximately(slider.value, lastIndicatorValue))
        {
            UpdateMuteIndicator(slider.value);
        }
    }

    /// <summary>Se llama en CADA frame de arrastre: actualiza la X de mute y el % del label, sin sonido.</summary>
    private void HandleValueChanged(float value01)
    {
        lastValue = value01;
        UpdateMuteIndicator(value01);
        ShowPercentLabelTemporarily(value01);
    }

    /// <summary>Se llama UNA VEZ al soltar el fader (clic/touch levantado): acá va el sonido de referencia.</summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (referenceClip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound(referenceClip);
        }
    }

    private void UpdateMuteIndicator(float value01)
    {
        lastIndicatorValue = value01;
        if (muteIndicator == null) return;
        muteIndicator.SetActive(value01 <= 0.001f);
    }

    private void ShowPercentLabelTemporarily(float value01)
    {
        if (label == null) return;

        if (percentLabelRoutine != null) StopCoroutine(percentLabelRoutine);
        percentLabelRoutine = StartCoroutine(PercentLabelRoutine(value01));
    }

    private IEnumerator PercentLabelRoutine(float value01)
    {
        int percent = Mathf.RoundToInt(value01 * 100f);
        label.text = $"{percent}%";

        yield return new WaitForSeconds(percentLabelDuration);

        label.text = idleLabelText;
        percentLabelRoutine = null;
    }
}
