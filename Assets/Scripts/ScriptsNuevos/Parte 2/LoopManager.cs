using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Maneja los loops de los pads ("hold"): mientras un slot está en loop, vuelve a
/// disparar su sonido cada cierto intervalo (el RITMO de repetición — no cambia
/// el tono del audio).
///
/// Cada loop pertenece a un BANCO y a una posición (slot 0-7) de ese banco: en otro
/// banco los pads responden normal y no muestran el loop; al volver a su banco se ve
/// de nuevo repitiéndose. Hay como máximo un loop por (banco, slot), ruteado al mismo
/// canal del Mixer que el pad. El sonido se lee del banco en cada repetición, así que
/// si el audio del pad cambia el loop lo sigue, y si se quita (o se borra el banco) se detiene.
///
/// Opcionales en el Inspector:
///  - Botón "detener todos": solo se ve mientras haya algún loop activo.
///  - HUD de velocidad: panel que se muestra mientras la persona ajusta la velocidad
///    deslizando (texto + barra vertical). Dejar Raycast Target desactivado en sus Image.
/// </summary>
[DefaultExecutionOrder(-150)]
public class LoopManager : MonoBehaviour
{
    public static LoopManager Instance { get; private set; }

    /// <summary>(banco, slot, activo): un loop empezó o terminó.</summary>
    public static event Action<SoundBankSO, int, bool> OnLoopStateChanged;

    /// <summary>(banco, slot, voz): el loop acaba de disparar el sonido (para el resalte del pad).</summary>
    public static event Action<SoundBankSO, int, AudioSource> OnLoopHit;

    public const int SlotCount = 8;

    [Header("Velocidad (intervalo entre disparos)")]
    [Tooltip("Intervalo más corto (loop más rápido), en segundos.")]
    [SerializeField] private float minIntervalSeconds = 0.08f;
    [Tooltip("Intervalo más largo (loop más lento), en segundos.")]
    [SerializeField] private float maxIntervalSeconds = 2f;
    [Tooltip("Intervalo con el que arranca un loop nuevo.")]
    [SerializeField] private float defaultIntervalSeconds = 0.5f;

    [Header("Bancos")]
    [Tooltip("Para saber qué banco se está mostrando. Si se deja vacío se busca solo en la escena.")]
    [SerializeField] private BankManager bankManager;

    [Tooltip("Activado: un loop sigue sonando aunque cambies a otro banco (ej. dejar un ritmo de base). " +
             "Desactivado: el loop se pausa al salir de su banco y se reanuda al volver.")]
    [SerializeField] private bool keepPlayingInOtherBanks = true;

    [Header("Opcional: detener todos")]
    [SerializeField] private Button stopAllButton;

    [Header("Opcional: HUD al ajustar la velocidad")]
    [SerializeField] private GameObject speedHudRoot;
    [SerializeField] private Text speedHudLabel;
    [Tooltip("Image con Image Type = Filled (Vertical) que sube al acelerar.")]
    [SerializeField] private Image speedHudFill;

    private class LoopState
    {
        public SoundBankSO bank;
        public int slot;
        public float interval;
        public double lastHitTime;
        public double nextHitTime;
    }

    private readonly System.Collections.Generic.List<LoopState> loops = new System.Collections.Generic.List<LoopState>();
    private readonly float[] lastIntervals = new float[SlotCount];

    // ------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        for (int i = 0; i < lastIntervals.Length; i++) lastIntervals[i] = defaultIntervalSeconds;

        if (bankManager == null) bankManager = FindFirstObjectByType<BankManager>();

        if (stopAllButton != null) stopAllButton.onClick.AddListener(StopAll);
        if (speedHudRoot != null) speedHudRoot.SetActive(false);
        RefreshStopAllButton();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private SoundBankSO CurrentBank => bankManager != null ? bankManager.CurrentBank : null;

    private void Update()
    {
        if (loops.Count == 0) return;

        double now = Time.unscaledTimeAsDouble;
        SoundBankSO current = CurrentBank;

        // De atrás hacia adelante: StopLoop saca elementos de la lista.
        for (int i = loops.Count - 1; i >= 0; i--)
        {
            LoopState loop = loops[i];

            // El banco pudo borrarse, o se quitó el audio de ese pad.
            AudioClip clip = ClipOf(loop);
            if (loop.bank == null || clip == null) { RemoveLoop(i); continue; }

            // Fuera de su banco y con "seguir sonando" apagado: pausado. Se corre el calendario
            // el tiempo que dura la pausa, para reanudar con el mismo ritmo.
            if (!keepPlayingInOtherBanks && current != null && loop.bank != current)
            {
                double dt = Time.unscaledDeltaTime;
                loop.lastHitTime += dt;
                loop.nextHitTime += dt;
                continue;
            }

            if (now < loop.nextHitTime) continue;

            AudioSource voice = AudioManager.Instance != null
                ? AudioManager.Instance.PlaySound(clip, loop.slot)
                : null;
            OnLoopHit?.Invoke(loop.bank, loop.slot, voice);

            loop.lastHitTime = loop.nextHitTime;
            loop.nextHitTime = loop.lastHitTime + loop.interval;

            // Si hubo un tirón (pausa, frame largo), no se "recupera" disparando en ráfaga.
            if (loop.nextHitTime < now) loop.nextHitTime = now + loop.interval;
        }
    }

    // ------------------------------------------------------------------
    // API
    // ------------------------------------------------------------------

    public bool IsLooping(SoundBankSO bank, int slot) => Find(bank, slot) != null;

    public bool AnyLooping => loops.Count > 0;

    /// <summary>Velocidad 0-1 (0 = más lenta, 1 = más rápida) del loop, o la última usada en ese slot.</summary>
    public float GetSpeed01(SoundBankSO bank, int slot) => IntervalToSpeed01(CurrentInterval(bank, slot));

    /// <summary>
    /// Inicia el loop de un pad. firstHitTime es el momento (Time.unscaledTimeAsDouble) del
    /// disparo inicial ya reproducido por el toque: los siguientes quedan alineados con él.
    /// </summary>
    public void StartLoop(SoundBankSO bank, int slot, double firstHitTime)
    {
        if (bank == null || !InRange(slot)) return;
        PadSoundData pad = bank.GetPad(slot);
        if (pad == null || pad.clip == null) return;

        float interval = lastIntervals[slot];
        double now = Time.unscaledTimeAsDouble;

        // Primer disparo del loop = el siguiente "múltiplo" del intervalo desde el toque original.
        double next = firstHitTime + interval;
        if (next <= now) next = firstHitTime + interval * Math.Ceiling((now - firstHitTime) / interval);

        LoopState existing = Find(bank, slot);
        if (existing != null)
        {
            existing.interval = interval;
            existing.lastHitTime = firstHitTime;
            existing.nextHitTime = next;
        }
        else
        {
            loops.Add(new LoopState
            {
                bank = bank,
                slot = slot,
                interval = interval,
                lastHitTime = firstHitTime,
                nextHitTime = next
            });
            RefreshStopAllButton();
        }

        OnLoopStateChanged?.Invoke(bank, slot, true);
    }

    /// <summary>Cambia la velocidad (0 = lenta, 1 = rápida) de un loop activo.</summary>
    public void SetSpeed01(SoundBankSO bank, int slot, float speed01)
    {
        if (!InRange(slot)) return;

        float interval = Speed01ToInterval(speed01);
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
        RefreshStopAllButton();
        OnLoopStateChanged?.Invoke(loop.bank, loop.slot, false);
    }

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
    // HUD de velocidad
    // ------------------------------------------------------------------

    public void ShowSpeedHud(SoundBankSO bank, int slot)
    {
        if (speedHudRoot != null) speedHudRoot.SetActive(true);
        UpdateSpeedHud(bank, slot);
    }

    public void UpdateSpeedHud(SoundBankSO bank, int slot)
    {
        float interval = CurrentInterval(bank, slot);

        if (speedHudLabel != null)
        {
            int perMinute = Mathf.RoundToInt(60f / interval);
            speedHudLabel.text = $"Pad {slot + 1}\n{perMinute} / min";
        }
        if (speedHudFill != null) speedHudFill.fillAmount = IntervalToSpeed01(interval);
    }

    public void HideSpeedHud()
    {
        if (speedHudRoot != null) speedHudRoot.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Utilidades
    // ------------------------------------------------------------------

    private float CurrentInterval(SoundBankSO bank, int slot)
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
