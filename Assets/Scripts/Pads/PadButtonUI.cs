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
/// ARTE: cada pad usa UN solo sprite completo (fondo + ícono ya resuelto por
/// el diseñador) para cada estado — "apagado" (PadSoundData.iconOff) o
/// "encendido" (PadSoundData.iconOn). Por eso este componente YA NO tiñe el
/// Image del pad con theme.padActive/padIdle: esos sprites ya traen su color
/// final, y aplicarles un tinte adicional los oscurece (Unity multiplica
/// color * sprite). RepaintBaseColor() de la versión anterior se eliminó por
/// este motivo — este componente ya no implementa IThemeable.
///
/// Mientras el audio se reproduce, se agrega un breve resalte (feedbackFlash)
/// — un overlay blanco semitransparente que se ENCIENDE/APAGA (enabled =
/// true/false), no que cambia de color con el tema. Su color base se deja
/// fijo (blanco semitransparente) desde el Editor, y el script solo
/// prende/apaga su visibilidad.
///
/// IMPORTANTE: Awake() fuerza feedbackFlash.enabled = false al iniciar, para que
/// cualquier color que haya quedado puesto a mano en el Editor (al armar el
/// prefab) no quede visible todo el tiempo por error — solo se ve mientras
/// ShowFeedbackWhilePlaying() lo activa.
/// </summary>
[RequireComponent(typeof(Image))]
public class PadButtonUI : MonoBehaviour, IPointerDownHandler
{
    [Header("Referencias UI")]
    [Tooltip("La Image que muestra el sprite COMPLETO del pad (fondo + ícono, " +
             "según el estado). Si se deja null, usa el Image de este GameObject.")]
    [SerializeField] private Image background;
    [SerializeField] private Text label;             // Cambiar a TMP_Text si el proyecto usa TextMeshPro
    [SerializeField] private Image feedbackFlash;    // overlay blanco semitransparente que aparece al tocar

    private PadSoundData padData;
    private Coroutine feedbackRoutine;
    private bool hasSound;
    private int slotIndex; // 0-7, fijo para este pad — define a qué Channel del Mixer rutea

    private void Awake()
    {
        if (background == null) background = GetComponent<Image>();

        // Asegura que el Image del pad se vea tal cual el sprite, sin ningún
        // tinte de color encima (blanco = sin modificar el sprite original).
        background.color = Color.white;

        // Fuerza apagado al iniciar: evita que un color puesto a mano en el
        // Editor (para poder verlo mientras se arma el prefab) quede visible
        // permanentemente en vez de solo durante el feedback.
        if (feedbackFlash != null) feedbackFlash.enabled = false;
    }

    /// <summary>
    /// Asigna el sonido que le corresponde a este pad (llamado por BankManager).
    /// slotIndex (0-7) es la posición física del pad en la grilla — se usa para
    /// rutear al canal Channel(slotIndex+1) del AudioMixer, así el fader de ese
    /// número siempre controla "lo que sea que esté en este pad ahora", sin
    /// importar qué banco esté cargado.
    /// </summary>
    public void Setup(PadSoundData data, int slotIndex)
    {
        padData = data;
        this.slotIndex = slotIndex;
        hasSound = data != null && data.clip != null;

        if (label != null) label.text = hasSound ? data.displayName : string.Empty;

        RepaintSprite();
    }

    /// <summary>Elige el sprite completo (apagado/encendido) según si hay sonido asignado.</summary>
    private void RepaintSprite()
    {
        if (background == null) return;

        Sprite target = null;
        if (padData != null)
        {
            target = hasSound ? padData.iconOn : padData.iconOff;
        }

        background.sprite = target;
        background.enabled = target != null;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        TriggerPad();
    }

    private void TriggerPad()
    {
        if (!hasSound) return;
        if (AudioManager.Instance == null) return;

        AudioSource voice = AudioManager.Instance.PlaySound(padData.clip, slotIndex);

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
