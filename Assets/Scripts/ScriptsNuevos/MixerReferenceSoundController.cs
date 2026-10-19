using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Versión de los "extras" del Mixer (sonido de feedback, X roja de mute, label
/// temporal con %) para los faders de MASTER y BASS dentro de MixerScreen —
/// que no controlan un pad específico, así que no tiene sentido reproducir un
/// sonido de pad al moverlos. En su lugar reproducen un AudioClip de
/// REFERENCIA fijo (asignado en el Inspector) para que el usuario pueda
/// escuchar el efecto del cambio igual que en los canales 1-8.
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
public class MixerReferenceSoundController : MonoBehaviour
{
    [Tooltip("Sonido de referencia a reproducir cada vez que se mueve este fader " +
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

    private void Awake() => slider = GetComponent<Slider>();

    private void OnEnable()
    {
        slider.onValueChanged.AddListener(HandleValueChanged);
        UpdateMuteIndicator(slider.value);
        if (label != null && percentLabelRoutine == null) label.text = idleLabelText;
    }

    private void OnDisable() => slider.onValueChanged.RemoveListener(HandleValueChanged);

    private void HandleValueChanged(float value01)
    {
        UpdateMuteIndicator(value01);
        ShowPercentLabelTemporarily(value01);

        if (referenceClip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound(referenceClip);
        }
    }

    private void UpdateMuteIndicator(float value01)
    {
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
