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
public class PadButtonUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Referencias UI")]
    [Tooltip("La Image que muestra el sprite COMPLETO del pad (fondo + ícono, " +
             "según el estado). Si se deja null, usa el Image de este GameObject.")]
    [SerializeField] private Image background;
    [SerializeField] private Text label;             // Cambiar a TMP_Text si el proyecto usa TextMeshPro
    [SerializeField] private Image feedbackFlash;    // overlay blanco semitransparente que aparece al tocar

    [Tooltip("Opcional: Image hija (encima del fondo, con Raycast Target desactivado) que muestra la marca " +
             "de 'pad vacío' (ej. una X) cuando el pad no tiene sonido. Su sprite lo pone el banco " +
             "(PadSoundData.emptyOverlay); acá solo se prende o apaga.")]
    [SerializeField] private Image emptyOverlay;

    [Header("Loop / Hold")]
    [Tooltip("Opcional: imagen (ej. un anillo o el ícono ↻) que se ve mientras este slot está en loop. " +
             "Debe tener Raycast Target desactivado.")]
    [SerializeField] private Image loopIndicator;

    [Tooltip("Segundos manteniendo el dedo quieto para activar el modo loop / ajuste de velocidad.")]
    [SerializeField] private float holdSeconds = 0.4f;

    [Tooltip("Píxeles que puede moverse el dedo durante la espera sin cancelar el hold " +
             "(si se mueve más, se entiende como un deslizamiento normal, p. ej. cambiar de banco).")]
    [SerializeField] private float holdMoveTolerance = 25f;

    [Tooltip("Qué fracción del alto de la pantalla hay que deslizar para recorrer toda la escala de velocidad.")]
    [SerializeField] [Range(0.1f, 0.8f)] private float dragScreenFraction = 0.35f;

    [Tooltip("Píxeles de movimiento vertical antes de empezar a cambiar la velocidad (zona muerta).")]
    [SerializeField] private float dragDeadZone = 12f;

    private PadSoundData padData;
    private Coroutine feedbackRoutine;
    private bool hasSound;
    private SoundBankSO bank; // banco al que pertenece el sonido de este pad ahora mismo
    private int slotIndex; // 0-7, fijo para este pad — define a qué Channel del Mixer rutea

    // ---- Estado del gesto de hold ----
    private enum HoldPhase { None, Waiting, Adjusting }

    /// <summary>
    /// true mientras hay un gesto de loop en curso (y durante el frame en que termina).
    /// SwipeNavigator lo consulta para no cambiar de banco por un deslizamiento que
    /// en realidad era el ajuste de velocidad de un loop.
    /// </summary>
    public static bool IsLoopGestureActive => gestureActive || Time.frameCount <= gestureEndFrame;
    private static bool gestureActive;
    private static int gestureEndFrame = -1;

    private HoldPhase phase = HoldPhase.None;
    private PointerEventData pointer;
    private Vector2 downPosition;
    private double downTime;          // Time.unscaledTimeAsDouble del toque
    private bool wasLoopingAtDown;
    private bool dragged;
    private float startY;
    private float startSpeed01;

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
        if (loopIndicator != null) loopIndicator.enabled = false;
        if (emptyOverlay != null)
        {
            emptyOverlay.raycastTarget = false; // nunca debe bloquear el toque del pad
            emptyOverlay.enabled = false;
        }
    }

    private void OnEnable()
    {
        LoopManager.OnLoopStateChanged += HandleLoopStateChanged;
        LoopManager.OnLoopHit += HandleLoopHit;
        RefreshLoopIndicator();
    }

    private void OnDisable()
    {
        LoopManager.OnLoopStateChanged -= HandleLoopStateChanged;
        LoopManager.OnLoopHit -= HandleLoopHit;
        CancelGesture();
    }

    /// <summary>
    /// Asigna el sonido que le corresponde a este pad (llamado por BankManager).
    /// slotIndex (0-7) es la posición física del pad en la grilla — se usa para
    /// rutear al canal Channel(slotIndex+1) del AudioMixer, así el fader de ese
    /// número siempre controla "lo que sea que esté en este pad ahora", sin
    /// importar qué banco esté cargado.
    /// </summary>
    public void Setup(PadSoundData data, int slotIndex, SoundBankSO bank = null)
    {
        padData = data;
        this.bank = bank;
        this.slotIndex = slotIndex;
        hasSound = data != null && data.clip != null;

        if (label != null) label.text = hasSound ? data.displayName : string.Empty;

        RepaintSprite();
        RefreshLoopIndicator();
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

        // Marca de "vacío" (ej. una X) encima del fondo, solo si el pad no tiene sonido.
        if (emptyOverlay != null)
        {
            Sprite mark = (padData != null && !hasSound) ? padData.emptyOverlay : null;
            emptyOverlay.sprite = mark;
            emptyOverlay.enabled = mark != null;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        LoopManager loops = LoopManager.Instance;
        wasLoopingAtDown = loops != null && loops.IsLooping(bank, slotIndex);
        downTime = Time.unscaledTimeAsDouble;

        // Si el slot ya está en loop, el toque NO dispara sonido (la persona puede querer
        // detenerlo o ajustarlo); si no, suena al instante como siempre.
        if (!wasLoopingAtDown) TriggerPad();

        // El hold solo tiene sentido si hay algo que loopear o algo que detener.
        if (loops == null || (!hasSound && !wasLoopingAtDown)) return;

        pointer = eventData;
        downPosition = eventData.position;
        dragged = false;
        phase = HoldPhase.Waiting;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (phase == HoldPhase.None) return;

        LoopManager loops = LoopManager.Instance;
        bool shouldStop = false;

        if (phase == HoldPhase.Waiting)
        {
            // Toque corto sobre un pad que estaba en loop = detenerlo.
            shouldStop = wasLoopingAtDown;
        }
        else if (phase == HoldPhase.Adjusting)
        {
            // Mantener sobre un loop activo SIN deslizar = detenerlo; si se deslizó, se deja con la nueva velocidad.
            shouldStop = wasLoopingAtDown && !dragged;
        }

        if (shouldStop && loops != null) loops.StopLoop(bank, slotIndex);
        EndGesture();
    }

    private void Update()
    {
        if (phase == HoldPhase.None || pointer == null) return;

        Vector2 pos = pointer.position;

        if (phase == HoldPhase.Waiting)
        {
            // Se movió demasiado antes de que se cumpliera el tiempo: es un deslizamiento, no un hold.
            if ((pos - downPosition).magnitude > holdMoveTolerance)
            {
                phase = HoldPhase.None;
                pointer = null;
                return;
            }

            if (Time.unscaledTimeAsDouble - downTime >= holdSeconds) BeginAdjusting(pos);
            return;
        }

        // Adjusting: deslizar hacia ARRIBA = más rápido, hacia ABAJO = más lento.
        float dy = pos.y - startY;
        if (!dragged && Mathf.Abs(dy) < dragDeadZone) return;
        dragged = true;

        float rangePixels = Mathf.Max(100f, Screen.height * dragScreenFraction);
        float deadZoneOffset = Mathf.Sign(dy) * dragDeadZone;
        float speed01 = Mathf.Clamp01(startSpeed01 + (dy - deadZoneOffset) / rangePixels);

        LoopManager loops = LoopManager.Instance;
        if (loops == null) return;
        loops.SetSpeed01(bank, slotIndex, speed01);
        loops.UpdateSpeedHud(bank, slotIndex);
    }

    private void BeginAdjusting(Vector2 pos)
    {
        LoopManager loops = LoopManager.Instance;
        if (loops == null) { CancelGesture(); return; }

        phase = HoldPhase.Adjusting;
        gestureActive = true;
        startY = pos.y;

        if (!wasLoopingAtDown)
        {
            // Activa el loop con el sonido que acaba de tocarse; los disparos siguientes
            // quedan alineados con ese primer toque.
            loops.StartLoop(bank, slotIndex, downTime);
        }

        startSpeed01 = loops.GetSpeed01(bank, slotIndex);
        loops.ShowSpeedHud(bank, slotIndex);
    }

    private void EndGesture()
    {
        if (phase == HoldPhase.Adjusting)
        {
            gestureActive = false;
            gestureEndFrame = Time.frameCount;
            if (LoopManager.Instance != null) LoopManager.Instance.HideSpeedHud();
        }
        phase = HoldPhase.None;
        pointer = null;
    }

    private void CancelGesture()
    {
        if (phase != HoldPhase.None) EndGesture();
    }

    // ------------------------------------------------------------------
    // Reacciones al LoopManager
    // ------------------------------------------------------------------

    private void HandleLoopStateChanged(SoundBankSO loopBank, int slot, bool active)
    {
        if (loopBank == bank && slot == slotIndex) RefreshLoopIndicator();
    }

    private void HandleLoopHit(SoundBankSO loopBank, int slot, AudioSource voice)
    {
        if (loopBank != bank || slot != slotIndex || voice == null) return;

        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        feedbackRoutine = StartCoroutine(ShowFeedbackWhilePlaying(voice));
    }

    private void RefreshLoopIndicator()
    {
        if (loopIndicator == null) return;
        loopIndicator.enabled = LoopManager.Instance != null && LoopManager.Instance.IsLooping(bank, slotIndex);
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

        AudioClip playingClip = voice != null ? voice.clip : null;
        while (voice != null && voice.isPlaying && voice.clip == playingClip)
        {
            yield return null;
        }

        if (feedbackFlash != null) feedbackFlash.enabled = false;
        feedbackRoutine = null;
    }
}
