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
///
/// Colores según el ADD: el pad se pinta turquesa (theme.padActive) si tiene un
/// sonido asignado, y gris (theme.padIdle) si el slot está vacío — es un estado
/// permanente, no la animación de "sonando ahora". Encima de eso, mientras el
/// audio se reproduce, se agrega un breve resalte (feedbackFlash) que ya existía
/// en la versión anterior para el requerimiento de feedback visual del GDD.
/// </summary>
[RequireComponent(typeof(Image))]
public class PadButtonUI : MonoBehaviour, IPointerDownHandler, IThemeable
{
    [Header("Referencias UI")]
    [SerializeField] private Image background;      // el propio pad; si se deja null, usa el Image de este GameObject
    [SerializeField] private Text label;             // Cambiar a TMP_Text si el proyecto usa TextMeshPro
    [SerializeField] private Image icon;
    [SerializeField] private Image feedbackFlash;    // overlay blanco semitransparente que aparece al tocar

    private PadSoundData padData;
    private UIThemeSO theme;
    private Coroutine feedbackRoutine;
    private bool hasSound;

    private void Awake()
    {
        if (background == null) background = GetComponent<Image>();
        if (feedbackFlash != null) feedbackFlash.enabled = false;
    }

    private void OnEnable() => ThemeManager.Instance?.Register(this);
    private void OnDisable() => ThemeManager.Instance?.Unregister(this);

    /// <summary>Asigna el sonido que le corresponde a este pad (llamado por BankManager).</summary>
    public void Setup(PadSoundData data)
    {
        padData = data;
        hasSound = data != null && data.clip != null;

        if (label != null) label.text = hasSound ? data.displayName : string.Empty;
        if (icon != null) icon.sprite = hasSound ? data.icon : null;
        if (icon != null) icon.enabled = hasSound && data.icon != null;

        RepaintBaseColor();
    }

    public void ApplyTheme(UIThemeSO newTheme)
    {
        theme = newTheme;
        RepaintBaseColor();
    }

    private void RepaintBaseColor()
    {
        if (theme == null || background == null) return;
        background.color = hasSound ? theme.padActive : theme.padIdle;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        TriggerPad();
    }

    private void TriggerPad()
    {
        if (!hasSound) return;
        if (AudioManager.Instance == null) return;

        AudioSource voice = AudioManager.Instance.PlaySound(padData.clip);

        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        feedbackRoutine = StartCoroutine(ShowFeedbackWhilePlaying(voice));
    }

    /// <summary>
    /// Muestra el resalte mientras el AudioSource sigue reproduciendo ESTE clip.
    /// Si el sonido es largo, se mantiene hasta que el audio termina
    /// (requerimiento de la sección 6 del GDD).
    /// </summary>
    private IEnumerator ShowFeedbackWhilePlaying(AudioSource voice)
    {
        if (feedbackFlash != null) feedbackFlash.enabled = true;

        AudioClip playingClip = padData.clip;
        while (voice != null && voice.isPlaying && voice.clip == playingClip)
        {
            yield return null;
        }

        if (feedbackFlash != null) feedbackFlash.enabled = false;
        feedbackRoutine = null;
    }
}
