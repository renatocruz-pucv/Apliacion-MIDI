using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Controla un pad individual de la grilla.
///
/// Usa IPointerDownHandler (no OnClick de Button) porque el requerimiento pide
/// "Toque rápido: ... reproducir los sonidos al instante, sin esperar a que el
/// usuario retire el dedo de la pantalla" — OnClick de Unity solo dispara al
/// soltar, PointerDown dispara al tocar.
/// </summary>
[RequireComponent(typeof(Image))]
public class PadButtonUI : MonoBehaviour, IPointerDownHandler
{
    [Header("Referencias UI")]
    [SerializeField] private Text label;           // Cambiar a TMP_Text si el proyecto usa TextMeshPro
    [SerializeField] private Image icon;
    [SerializeField] private Image feedbackLight;   // Indicador que se enciende mientras suena

    private PadSoundData padData;
    private Coroutine feedbackRoutine;
    private Color idleColor = Color.white;

    private void Awake()
    {
        if (feedbackLight != null)
        {
            idleColor = feedbackLight.color;
            SetIndicator(false, idleColor);
        }
    }

    /// <summary>Asigna el sonido que le corresponde a este pad (llamado por BankManager).</summary>
    public void Setup(PadSoundData data)
    {
        padData = data;

        bool hasSound = data != null && data.clip != null;

        if (label != null) label.text = hasSound ? data.displayName : string.Empty;
        if (icon != null) icon.sprite = hasSound ? data.icon : null;
        if (icon != null) icon.enabled = hasSound && data.icon != null;

        SetIndicator(false, idleColor);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        TriggerPad();
    }

    private void TriggerPad()
    {
        if (padData == null || padData.clip == null) return;
        if (AudioManager.Instance == null) return;

        AudioSource voice = AudioManager.Instance.PlaySound(padData.clip);

        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        feedbackRoutine = StartCoroutine(ShowFeedbackWhilePlaying(voice));
    }

    /// <summary>
    /// Enciende la luz indicadora inmediatamente y la apaga cuando el AudioSource
    /// termina. Si el sonido es largo, el pad se mantiene encendido hasta que
    /// el audio termina (requerimiento de la sección 6 del GDD).
    /// </summary>
    private IEnumerator ShowFeedbackWhilePlaying(AudioSource voice)
    {
        SetIndicator(true, padData.feedbackColor);

        // Esperamos mientras el AudioSource siga reproduciendo ESTE clip en particular.
        // (si el pool reasigna la voz a otro sonido antes de que termine, cortamos el feedback igual)
        AudioClip playingClip = padData.clip;
        while (voice != null && voice.isPlaying && voice.clip == playingClip)
        {
            yield return null;
        }

        SetIndicator(false, idleColor);
        feedbackRoutine = null;
    }

    private void SetIndicator(bool active, Color color)
    {
        if (feedbackLight == null) return;
        feedbackLight.color = color;
        feedbackLight.enabled = active || color == idleColor; // el aro puede quedar visible apagado si se prefiere
    }
}
