using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Control del tempo común del modo sincronizado ("batería"): un slider de BPM, un botón TAP
/// (se toca al ritmo deseado y calcula el tempo) y botones − / + opcionales.
/// Todos los campos son opcionales: usa los que quieras.
///
/// Si se asigna Root, el control solo se muestra mientras el modo sincronizado está activo.
/// </summary>
public class LoopTempoControl : MonoBehaviour
{
    [Tooltip("Opcional: panel que se muestra solo en modo sincronizado. Si se deja vacío siempre está visible " +
             "(no debe ser el mismo GameObject que lleva este script).")]
    [SerializeField] private GameObject root;

    [SerializeField] private Slider tempoSlider;
    [SerializeField] private Text bpmLabel;
    [SerializeField] private Button tapButton;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;
    [SerializeField] private float stepBpm = 2f;

    [Tooltip("Si pasan más de estos segundos entre dos toques de TAP, se empieza a medir de nuevo.")]
    [SerializeField] private float tapResetSeconds = 2f;

    private const int MaxTaps = 5;
    private readonly float[] tapTimes = new float[MaxTaps];
    private int tapCount;
    private bool wired;

    private void Awake()
    {
        if (wired) return;
        wired = true;

        tempoSlider?.onValueChanged.AddListener(OnSliderChanged);
        tapButton?.onClick.AddListener(OnTap);
        minusButton?.onClick.AddListener(() => Nudge(-stepBpm));
        plusButton?.onClick.AddListener(() => Nudge(+stepBpm));
    }

    private void OnEnable()
    {
        LoopManager.OnTempoChanged += HandleTempoChanged;
        LoopManager.OnSyncChanged += HandleSyncChanged;

        LoopManager manager = LoopManager.Instance;
        if (manager != null && tempoSlider != null)
        {
            tempoSlider.wholeNumbers = true;
            tempoSlider.minValue = manager.MinBpm;
            tempoSlider.maxValue = manager.MaxBpm;
        }
        Refresh();
    }

    private void OnDisable()
    {
        LoopManager.OnTempoChanged -= HandleTempoChanged;
        LoopManager.OnSyncChanged -= HandleSyncChanged;
    }

    private void OnSliderChanged(float bpm)
    {
        if (LoopManager.Instance != null) LoopManager.Instance.SetTempoBpm(bpm);
    }

    private void Nudge(float delta)
    {
        LoopManager manager = LoopManager.Instance;
        if (manager != null) manager.SetTempoBpm(Mathf.Round(manager.Bpm) + delta);
    }

    private void OnTap()
    {
        LoopManager manager = LoopManager.Instance;
        if (manager == null) return;

        float now = Time.unscaledTime;
        if (tapCount > 0 && now - tapTimes[tapCount - 1] > tapResetSeconds) tapCount = 0;

        if (tapCount == MaxTaps)
        {
            for (int i = 1; i < MaxTaps; i++) tapTimes[i - 1] = tapTimes[i];
            tapCount--;
        }
        tapTimes[tapCount++] = now;

        if (tapCount < 2) return;

        float averageInterval = (tapTimes[tapCount - 1] - tapTimes[0]) / (tapCount - 1);
        if (averageInterval > 0.01f) manager.SetTempoBpm(60f / averageInterval);
    }

    private void HandleTempoChanged(float bpm) => Refresh();
    private void HandleSyncChanged(bool synced) => Refresh();

    private void Refresh()
    {
        LoopManager manager = LoopManager.Instance;
        if (manager == null) return;

        if (root != null) root.SetActive(manager.SyncEnabled);

        float bpm = manager.Bpm;
        if (tempoSlider != null) tempoSlider.SetValueWithoutNotify(Mathf.Round(bpm));
        if (bpmLabel != null) bpmLabel.text = $"{Mathf.RoundToInt(bpm)} BPM";
    }
}
