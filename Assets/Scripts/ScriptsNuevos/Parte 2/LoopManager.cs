using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Maneja los loops de los pads ("hold"): mientras un pad está en loop, vuelve a disparar
/// su sonido una y otra vez (el RITMO de repetición — no cambia el tono del audio).
///
/// Cada loop pertenece a un BANCO y a una posición (slot 0-7) de ese banco: en otro banco los
/// pads responden normal y no muestran el loop; al volver a su banco se ve de nuevo repitiéndose.
/// Hay como máximo un loop por (banco, slot), ruteado al mismo canal del Mixer que el pad. El
/// sonido se lee del banco en cada repetición: si el audio cambia el loop lo sigue, y si se quita
/// (o se borra el banco) el loop se detiene.
///
/// DOS MODOS (SetSyncEnabled):
///  - LIBRE: cada loop va con su propia velocidad (deslizar vertical = más rápido / más lento).
///  - SINCRONIZADO: modo "batería". Hay UN tempo común (BPM) y un reloj de pulsos compartido.
///    Cada pad repite según una DIVISIÓN del pulso (1 cada 4 tiempos, 1 cada 2, negras, corcheas,
///    tresillos, semicorcheas...). Un loop nuevo entra en el siguiente pulso, así que cae justo sobre
///    los demás, y si cambia el tempo todos se adaptan juntos sin perder la alineación, pero cada
///    uno conserva su propia división. Deslizar vertical sobre un pad cambia SU división; el
///    tempo común se cambia con LoopTempoControl (slider / TAP / ±).
///
/// Opcionales en el Inspector:
///  - Botón "detener todos": solo se ve mientras haya algún loop activo.
///  - HUD: panel que se muestra mientras se ajusta un pad (texto + barra vertical).
///    Dejar Raycast Target desactivado en sus Image.
/// </summary>
[DefaultExecutionOrder(-150)]
public class LoopManager : MonoBehaviour
{
    public static LoopManager Instance { get; private set; }

    /// <summary>(banco, slot, activo): un loop empezó o terminó.</summary>
    public static event Action<SoundBankSO, int, bool> OnLoopStateChanged;

    /// <summary>(banco, slot, voz): el loop acaba de disparar el sonido (para el resalte del pad).</summary>
    public static event Action<SoundBankSO, int, AudioSource> OnLoopHit;

    /// <summary>true = loops sincronizados, false = independientes.</summary>
    public static event Action<bool> OnSyncChanged;

    /// <summary>El tempo común cambió (valor en BPM).</summary>
    public static event Action<float> OnTempoChanged;

    /// <summary>"Primer toque espera el pulso" se activó o desactivó.</summary>
    public static event Action<bool> OnQuantizeChanged;

    /// <summary>
    /// Cada pulso del reloj: posición dentro del compás (0 = el "1", hasta BeatsPerBar - 1).
    /// -1 = ya no hay reloj (no hay loops sincronizados sonando).
    /// </summary>
    public static event Action<int> OnBeat;

    public const int BeatsPerBar = 4;

    public const int SlotCount = 8;

    // Divisiones disponibles en modo sincronizado: golpes por pulso (el pulso = una negra).
    private static readonly float[] DivisionHitsPerBeat = { 0.25f, 0.5f, 1f, 2f, 3f, 4f, 6f };
    private static readonly string[] DivisionNames =
    {
        "1 cada 4 tiempos", "1 cada 2 tiempos", "Negras (1/4)", "Corcheas (1/8)",
        "Tresillo (1/8T)", "Semicorcheas (1/16)", "Tresillo (1/16T)"
    };
    private const int DefaultDivisionIndex = 2; // negras: un golpe por pulso

    [Header("Modo libre: velocidad (intervalo entre disparos)")]
    [Tooltip("Intervalo más corto (loop más rápido), en segundos.")]
    [SerializeField] private float minIntervalSeconds = 0.08f;
    [Tooltip("Intervalo más largo (loop más lento), en segundos.")]
    [SerializeField] private float maxIntervalSeconds = 2f;
    [Tooltip("Intervalo con el que arranca un loop nuevo.")]
    [SerializeField] private float defaultIntervalSeconds = 0.5f;

    [Header("Modo sincronizado: tempo")]
    [SerializeField] private float minBpm = 40f;
    [SerializeField] private float maxBpm = 240f;
    [Tooltip("Tempo la primera vez que se abre la app (después se recuerda el último).")]
    [SerializeField] private float defaultBpm = 100f;
    [Tooltip("Estado inicial la primera vez que se abre la app (después se recuerda lo que eligió la persona).")]
    [SerializeField] private bool syncEnabledAtStart = false;
    [Tooltip("Estado inicial de 'el primer toque espera el pulso' (después se recuerda lo que eligió la persona).")]
    [SerializeField] private bool quantizeFirstTapAtStart = false;

    [Header("Bancos")]
    [Tooltip("Para saber qué banco se está mostrando. Si se deja vacío se busca solo en la escena.")]
    [SerializeField] private BankManager bankManager;

    [Tooltip("Activado: un loop sigue sonando aunque cambies a otro banco (ej. dejar un ritmo de base). " +
             "Desactivado: el loop se pausa al salir de su banco y se reanuda al volver.")]
    [SerializeField] private bool keepPlayingInOtherBanks = true;

    [Header("Opcional: detener todos")]
    [SerializeField] private Button stopAllButton;

    [Header("Opcional: HUD al ajustar un pad")]
    [SerializeField] private GameObject speedHudRoot;
    [SerializeField] private Text speedHudLabel;
    [Tooltip("Image con Image Type = Filled (Vertical) que sube al acelerar.")]
    [SerializeField] private Image speedHudFill;
    [Tooltip("El HUD aparece junto al pad que se está ajustando (encima o debajo, sin salirse de pantalla). " +
             "Debe ser hijo directo del Canvas (o de un panel a pantalla completa), fuera de Layout Groups.")]
    [SerializeField] private bool speedHudFollowsPad = true;
    [SerializeField] private float speedHudGap = 12f;

    private class LoopState
    {
        public SoundBankSO bank;
        public int slot;
        public bool needsRealign;      // estuvo pausado (otro banco) con la sincronización activa

        // Modo libre (tiempo en segundos)
        public float interval;
        public double lastHitTime;
        public double nextHitTime;

        // Modo sincronizado (posición en pulsos)
        public int divisionIndex = DefaultDivisionIndex;
        public double originBeat;      // pulso (entero) en que este loop "entró"; su fase queda anclada ahí
        public double nextBeat;        // pulso del próximo disparo
        public double coveredBeat = double.NegativeInfinity; // pulso ya sonado por el toque inicial cuantizado
    }

    // Toque que espera el pulso para sonar (modo sincronizado + "primer toque espera el pulso").
    private class PendingHit
    {
        public SoundBankSO bank;
        public int slot;
        public AudioClip clip;
        public double beat;
    }

    private readonly System.Collections.Generic.List<LoopState> loops = new System.Collections.Generic.List<LoopState>();
    private readonly System.Collections.Generic.List<PendingHit> pendingHits = new System.Collections.Generic.List<PendingHit>();
    private readonly float[] lastIntervals = new float[SlotCount];
    private readonly int[] lastDivisionIndex = new int[SlotCount];

    // Reloj compartido: pulso(t) = beatsAtRef + (t - refTime) / beatInterval.
    private bool syncEnabled;
    private bool hasClock;
    private double refTime;
    private double beatsAtRef;
    private float beatInterval;   // segundos por pulso (60 / BPM); también vale sin reloj activo
    private double lastBeatIndex = double.NaN;
    private bool quantizeFirstTap;

    public bool SyncEnabled => syncEnabled;
    public bool QuantizeFirstTap => quantizeFirstTap;
    public float Bpm => 60f / beatInterval;
    public float MinBpm => minBpm;
    public float MaxBpm => maxBpm;

    // ------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        for (int i = 0; i < SlotCount; i++)
        {
            lastIntervals[i] = defaultIntervalSeconds;
            lastDivisionIndex[i] = DefaultDivisionIndex;
        }

        if (bankManager == null) bankManager = FindFirstObjectByType<BankManager>();

        syncEnabled = AppSettings.TryGetLoopSync(out bool savedSync) ? savedSync : syncEnabledAtStart;
        quantizeFirstTap = AppSettings.TryGetQuantizeFirstTap(out bool savedQuantize) ? savedQuantize : quantizeFirstTapAtStart;
        float bpm = AppSettings.TryGetTempoBpm(out float savedBpm) ? savedBpm : defaultBpm;
        beatInterval = 60f / Mathf.Clamp(bpm, minBpm, maxBpm);

        if (stopAllButton != null) stopAllButton.onClick.AddListener(StopAll);
        if (speedHudRoot != null) speedHudRoot.SetActive(false);
        RefreshStopAllButton();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private SoundBankSO CurrentBank => bankManager != null ? bankManager.CurrentBank : null;

    private bool IsSynced => syncEnabled && hasClock;

    private void Update()
    {
        double now = Time.unscaledTimeAsDouble;

        if (pendingHits.Count > 0) ProcessPendingHits(now);
        if (IsSynced) TrackBeat(now);

        if (loops.Count == 0) return;

        SoundBankSO current = CurrentBank;

        // De atrás hacia adelante: RemoveLoop saca elementos de la lista.
        for (int i = loops.Count - 1; i >= 0; i--)
        {
            LoopState loop = loops[i];

            // El banco pudo borrarse, o se quitó el audio de ese pad.
            AudioClip clip = ClipOf(loop);
            if (loop.bank == null || clip == null) { RemoveLoop(i); continue; }

            // Fuera de su banco y con "seguir sonando" apagado: pausado.
            if (!keepPlayingInOtherBanks && current != null && loop.bank != current)
            {
                if (IsSynced)
                {
                    // El reloj sigue corriendo: al volver el loop se realinea al próximo golpe de SU rejilla.
                    loop.needsRealign = true;
                }
                else
                {
                    // Modo libre: se corre el calendario lo que dura la pausa para reanudar igual.
                    double dt = Time.unscaledDeltaTime;
                    loop.lastHitTime += dt;
                    loop.nextHitTime += dt;
                }
                continue;
            }

            if (loop.needsRealign)
            {
                loop.needsRealign = false;
                if (IsSynced) AlignNextBeat(loop, now);
            }

            if (IsSynced) UpdateSyncedLoop(loop, clip, now);
            else UpdateFreeLoop(loop, clip, now);
        }
    }

    private void UpdateFreeLoop(LoopState loop, AudioClip clip, double now)
    {
        if (now < loop.nextHitTime) return;

        Fire(loop, clip);

        loop.lastHitTime = loop.nextHitTime;
        loop.nextHitTime = loop.lastHitTime + loop.interval;

        // Si hubo un tirón (pausa, frame largo), no se "recupera" disparando en ráfaga.
        if (loop.nextHitTime < now) loop.nextHitTime = now + loop.interval;
    }

    private void UpdateSyncedLoop(LoopState loop, AudioClip clip, double now)
    {
        if (now < BeatToTime(loop.nextBeat)) return;

        Fire(loop, clip);

        loop.nextBeat += Period(loop);

        // Tirón largo: se vuelve a la rejilla en vez de disparar en ráfaga.
        if (BeatToTime(loop.nextBeat) < now) AlignNextBeat(loop, now);
    }

    /// <summary>Avisa de cada pulso nuevo (para el indicador visual).</summary>
    private void TrackBeat(double now)
    {
        double current = Math.Floor(BeatAt(now));
        if (current < 0 || current == lastBeatIndex) return;

        lastBeatIndex = current; // si un frame largo se saltó pulsos, se avisa solo del actual
        int inBar = (int)(((long)current % BeatsPerBar + BeatsPerBar) % BeatsPerBar);
        OnBeat?.Invoke(inBar);
    }

    private void ProcessPendingHits(double now)
    {
        for (int i = pendingHits.Count - 1; i >= 0; i--)
        {
            PendingHit hit = pendingHits[i];

            // Si el reloj desapareció (se detuvieron todos los loops) no hay pulso que esperar: suena ya.
            if (hasClock && now < BeatToTime(hit.beat)) continue;

            pendingHits.RemoveAt(i);
            if (hit.bank == null || hit.clip == null) continue;

            AudioSource voice = AudioManager.Instance != null
                ? AudioManager.Instance.PlaySound(hit.clip, hit.slot)
                : null;
            OnLoopHit?.Invoke(hit.bank, hit.slot, voice);
        }
    }

    private void ClearClock()
    {
        hasClock = false;
        lastBeatIndex = double.NaN;
        OnBeat?.Invoke(-1);
    }

    private void Fire(LoopState loop, AudioClip clip)
    {
        AudioSource voice = AudioManager.Instance != null
            ? AudioManager.Instance.PlaySound(clip, loop.slot)
            : null;
        OnLoopHit?.Invoke(loop.bank, loop.slot, voice);
    }

    // ------------------------------------------------------------------
    // API de loops
    // ------------------------------------------------------------------

    public bool IsLooping(SoundBankSO bank, int slot) => Find(bank, slot) != null;

    public bool AnyLooping => loops.Count > 0;

    /// <summary>
    /// Valor 0-1 que controla el gesto de deslizar sobre un pad: en modo libre es la velocidad
    /// (0 = lenta, 1 = rápida); en modo sincronizado es la división del pulso (0 = la más espaciada).
    /// </summary>
    public float GetSpeed01(SoundBankSO bank, int slot)
    {
        if (syncEnabled)
        {
            LoopState loop = Find(bank, slot);
            int index = loop != null ? loop.divisionIndex : (InRange(slot) ? lastDivisionIndex[slot] : DefaultDivisionIndex);
            return index / (float)(DivisionHitsPerBeat.Length - 1);
        }
        return IntervalToSpeed01(CurrentFreeInterval(bank, slot));
    }

    /// <summary>
    /// Inicia el loop de un pad. firstHitTime es el momento (Time.unscaledTimeAsDouble) del disparo
    /// inicial ya reproducido por el toque.
    /// </summary>
    public void StartLoop(SoundBankSO bank, int slot, double firstHitTime, double quantizedBeat = double.NaN)
    {
        if (bank == null || !InRange(slot)) return;
        PadSoundData pad = bank.GetPad(slot);
        if (pad == null || pad.clip == null) return;

        double now = Time.unscaledTimeAsDouble;
        LoopState loop = Find(bank, slot);
        bool isNew = loop == null;
        if (isNew) loop = new LoopState { bank = bank, slot = slot };

        if (syncEnabled)
        {
            loop.divisionIndex = lastDivisionIndex[slot];

            if (!hasClock)
            {
                // Es el primero: su toque original es el pulso 0 del reloj.
                refTime = firstHitTime;
                beatsAtRef = 0;
                hasClock = true;
                loop.originBeat = 0;
            }
            else
            {
                // Entra en el pulso donde sonó su toque (si esperó el pulso) o, si no, en el siguiente
                // pulso del reloj compartido: así cae justo sobre los demás.
                loop.originBeat = double.IsNaN(quantizedBeat) ? Math.Ceiling(BeatAt(now) - 1e-6) : quantizedBeat;
            }

            // Ese pulso ya lo suena el toque inicial: el loop empieza después, sin duplicarlo.
            loop.coveredBeat = double.IsNaN(quantizedBeat) ? double.NegativeInfinity : quantizedBeat;
            AlignNextBeat(loop, now);
        }
        else
        {
            float interval = lastIntervals[slot];
            double next = firstHitTime + interval;
            if (next <= now) next = firstHitTime + interval * Math.Ceiling((now - firstHitTime) / interval);

            loop.interval = interval;
            loop.lastHitTime = firstHitTime;
            loop.nextHitTime = next;
        }

        if (isNew)
        {
            loops.Add(loop);
            RefreshStopAllButton();
        }
        OnLoopStateChanged?.Invoke(bank, slot, true);
    }

    /// <summary>
    /// Aplica el valor 0-1 del gesto de deslizar. Modo libre: cambia la velocidad de ese loop.
    /// Modo sincronizado: cambia la división del pulso de ese loop (el tempo común no se toca).
    /// </summary>
    public void SetSpeed01(SoundBankSO bank, int slot, float value01)
    {
        if (!InRange(slot)) return;

        if (syncEnabled)
        {
            int index = Mathf.RoundToInt(Mathf.Clamp01(value01) * (DivisionHitsPerBeat.Length - 1));
            lastDivisionIndex[slot] = index;

            LoopState synced = Find(bank, slot);
            if (synced != null && synced.divisionIndex != index)
            {
                synced.divisionIndex = index;
                if (hasClock) AlignNextBeat(synced, Time.unscaledTimeAsDouble);
            }
            return;
        }

        float interval = Speed01ToInterval(value01);
        lastIntervals[slot] = interval;

        LoopState loop = Find(bank, slot);
        if (loop == null) return;

        loop.interval = interval;
        // El próximo disparo se reubica con el intervalo nuevo (sin pasarse del actual si se acelera).
        loop.nextHitTime = Math.Max(loop.lastHitTime + interval, Time.unscaledTimeAsDouble);
    }

    public void StopLoop(SoundBankSO bank, int slot)
    {
        int index = IndexOf(bank, slot);
        if (index >= 0) RemoveLoop(index);
    }

    /// <summary>Detiene los loops de TODOS los bancos.</summary>
    public void StopAll()
    {
        for (int i = loops.Count - 1; i >= 0; i--) RemoveLoop(i);
    }

    private void RemoveLoop(int index)
    {
        LoopState loop = loops[index];
        loops.RemoveAt(index);
        if (loops.Count == 0 && hasClock) ClearClock(); // el próximo loop vuelve a definir el reloj
        RefreshStopAllButton();
        OnLoopStateChanged?.Invoke(loop.bank, loop.slot, false);
    }

    // ------------------------------------------------------------------
    // Modo sincronizado y tempo
    // ------------------------------------------------------------------

    /// <summary>Activa o desactiva el modo sincronizado (se recuerda entre sesiones).</summary>
    public void SetSyncEnabled(bool enabled)
    {
        if (syncEnabled == enabled) return;

        syncEnabled = enabled;
        AppSettings.SetLoopSync(enabled);

        if (enabled) EnterSyncMode();
        else ExitSyncMode();

        OnSyncChanged?.Invoke(syncEnabled);
    }

    public void ToggleSync() => SetSyncEnabled(!syncEnabled);

    /// <summary>Activa o desactiva que, en modo sincronizado, el primer toque espere el siguiente pulso para sonar.</summary>
    public void SetQuantizeFirstTap(bool enabled)
    {
        if (quantizeFirstTap == enabled) return;

        quantizeFirstTap = enabled;
        AppSettings.SetQuantizeFirstTap(enabled);
        OnQuantizeChanged?.Invoke(enabled);
    }

    public void ToggleQuantizeFirstTap() => SetQuantizeFirstTap(!quantizeFirstTap);

    /// <summary>
    /// Si corresponde (modo sincronizado, opción activada y ya hay un reloj corriendo), programa el sonido del
    /// toque para el siguiente pulso y devuelve true junto con ese pulso. Si devuelve false, el pad debe sonar al instante.
    /// </summary>
    public bool TryScheduleOnNextBeat(SoundBankSO bank, int slot, AudioClip clip, out double beat)
    {
        beat = double.NaN;
        if (!syncEnabled || !quantizeFirstTap || !hasClock || bank == null || clip == null || !InRange(slot)) return false;

        beat = Math.Ceiling(BeatAt(Time.unscaledTimeAsDouble) - 1e-6);
        pendingHits.Add(new PendingHit { bank = bank, slot = slot, clip = clip, beat = beat });
        return true;
    }

    /// <summary>Cambia el tempo común (BPM). Los loops sincronizados se adaptan sin perder la alineación.</summary>
    public void SetTempoBpm(float bpm)
    {
        bpm = Mathf.Clamp(bpm, minBpm, maxBpm);
        if (Mathf.Approximately(bpm, Bpm)) return;

        if (hasClock)
        {
            // Se re-ancla el reloj en el pulso actual: la posición en pulsos no salta, solo cambia su velocidad.
            double now = Time.unscaledTimeAsDouble;
            beatsAtRef = BeatAt(now);
            refTime = now;
        }
        beatInterval = 60f / bpm;

        AppSettings.SetTempoBpm(bpm);
        OnTempoChanged?.Invoke(bpm);
    }

    /// <summary>Con loops ya sonando: el más antiguo manda el tempo; todos pasan a negras y entran juntos en el pulso 0.</summary>
    private void EnterSyncMode()
    {
        if (loops.Count == 0) { if (hasClock) ClearClock(); return; }

        LoopState reference = loops[0];
        float interval = Mathf.Clamp(reference.interval, 60f / maxBpm, 60f / minBpm);
        beatInterval = interval;
        AppSettings.SetTempoBpm(Bpm);
        OnTempoChanged?.Invoke(Bpm);

        // Pulso 0 = el próximo disparo del loop de referencia.
        refTime = Math.Max(reference.nextHitTime, Time.unscaledTimeAsDouble);
        beatsAtRef = 0;
        hasClock = true;

        double now = Time.unscaledTimeAsDouble;
        for (int i = 0; i < loops.Count; i++)
        {
            loops[i].divisionIndex = DefaultDivisionIndex;
            loops[i].originBeat = 0;
            AlignNextBeat(loops[i], now);
        }
    }

    /// <summary>Al volver a modo libre cada loop sigue con el ritmo que tenía en ese momento.</summary>
    private void ExitSyncMode()
    {
        if (hasClock)
        {
            for (int i = 0; i < loops.Count; i++)
            {
                LoopState loop = loops[i];
                loop.interval = (float)(beatInterval * Period(loop));
                loop.nextHitTime = BeatToTime(loop.nextBeat);
                loop.lastHitTime = loop.nextHitTime - loop.interval;
            }
        }
        if (hasClock) ClearClock();
    }

    private double BeatAt(double time) => beatsAtRef + (time - refTime) / beatInterval;

    private double BeatToTime(double beat) => refTime + (beat - beatsAtRef) * beatInterval;

    /// <summary>Duración de un golpe del loop, en pulsos (negras = 1, corcheas = 0.5, ...).</summary>
    private static double Period(LoopState loop) => 1.0 / DivisionHitsPerBeat[loop.divisionIndex];

    /// <summary>Pone el próximo disparo en el siguiente punto de la rejilla del loop (anclada en su pulso de entrada).</summary>
    private void AlignNextBeat(LoopState loop, double now)
    {
        double period = Period(loop);
        double steps = Math.Ceiling((BeatAt(now) - loop.originBeat) / period - 1e-9);
        if (steps < 0) steps = 0;
        loop.nextBeat = loop.originBeat + steps * period;

        // El pulso del toque inicial cuantizado ya sonó (o está por sonar): no se repite.
        while (loop.nextBeat <= loop.coveredBeat + 1e-9) loop.nextBeat += period;
    }

    // ------------------------------------------------------------------
    // Búsqueda
    // ------------------------------------------------------------------

    private int IndexOf(SoundBankSO bank, int slot)
    {
        if (bank == null) return -1;
        for (int i = 0; i < loops.Count; i++)
        {
            if (loops[i].bank == bank && loops[i].slot == slot) return i;
        }
        return -1;
    }

    private LoopState Find(SoundBankSO bank, int slot)
    {
        int index = IndexOf(bank, slot);
        return index >= 0 ? loops[index] : null;
    }

    private static AudioClip ClipOf(LoopState loop)
    {
        if (loop.bank == null) return null;
        PadSoundData pad = loop.bank.GetPad(loop.slot);
        return pad != null ? pad.clip : null;
    }

    // ------------------------------------------------------------------
    // HUD
    // ------------------------------------------------------------------

    public void ShowSpeedHud(SoundBankSO bank, int slot, RectTransform padRect = null)
    {
        if (speedHudRoot != null) speedHudRoot.SetActive(true);
        UpdateSpeedHud(bank, slot);

        if (speedHudFollowsPad && speedHudRoot != null && padRect != null)
        {
            var hudRect = speedHudRoot.transform as RectTransform;
            if (hudRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(hudRect); // el texto acaba de cambiar de tamaño
                UIPlacement.PlaceNear(hudRect, padRect, speedHudGap, true);
            }
        }
    }

    public void UpdateSpeedHud(SoundBankSO bank, int slot)
    {
        if (speedHudLabel != null)
        {
            if (syncEnabled)
            {
                LoopState loop = Find(bank, slot);
                int index = loop != null ? loop.divisionIndex : lastDivisionIndex[slot];
                speedHudLabel.text = $"Pad {slot + 1}\n{DivisionNames[index]}\n{Mathf.RoundToInt(Bpm)} BPM";
            }
            else
            {
                int perMinute = Mathf.RoundToInt(60f / CurrentFreeInterval(bank, slot));
                speedHudLabel.text = $"Pad {slot + 1}\n{perMinute} / min";
            }
        }
        if (speedHudFill != null) speedHudFill.fillAmount = GetSpeed01(bank, slot);
    }

    public void HideSpeedHud()
    {
        if (speedHudRoot != null) speedHudRoot.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Utilidades
    // ------------------------------------------------------------------

    private float CurrentFreeInterval(SoundBankSO bank, int slot)
    {
        LoopState loop = Find(bank, slot);
        if (loop != null) return loop.interval;
        return InRange(slot) ? lastIntervals[slot] : defaultIntervalSeconds;
    }

    // Escala logarítmica: así la sensación de "más rápido/más lento" es pareja a lo largo del deslizamiento.
    private float Speed01ToInterval(float speed01)
        => minIntervalSeconds * Mathf.Pow(maxIntervalSeconds / minIntervalSeconds, 1f - Mathf.Clamp01(speed01));

    private float IntervalToSpeed01(float interval)
    {
        float clamped = Mathf.Clamp(interval, minIntervalSeconds, maxIntervalSeconds);
        float n = Mathf.Log(clamped / minIntervalSeconds) / Mathf.Log(maxIntervalSeconds / minIntervalSeconds);
        return 1f - n;
    }

    private void RefreshStopAllButton()
    {
        if (stopAllButton != null) stopAllButton.gameObject.SetActive(loops.Count > 0);
    }

    private static bool InRange(int slot) => slot >= 0 && slot < SlotCount;
}
