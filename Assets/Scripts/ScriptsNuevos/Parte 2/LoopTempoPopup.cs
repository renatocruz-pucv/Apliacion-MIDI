using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reemplaza el panel de tempo siempre visible por un solo botón pequeño que muestra el tempo
/// ("100 BPM") y que, al tocarlo, abre el panel completo (slider, TAP, − / +) flotando junto al botón.
/// El panel se cierra solo unos segundos después del último cambio de tempo, o al volver a tocar el botón.
/// El botón solo aparece en modo SYNC.
///
/// Montaje: el panel (con el componente LoopTempoControl en ÉL MISMO, sin Root) debe ser un hijo directo del
/// Canvas (o de un panel a pantalla completa) para poder flotar sobre el resto de la UI.
/// </summary>
public class LoopTempoPopup : MonoBehaviour
{
    [Tooltip("Botón pequeño que abre y cierra el panel.")]
    [SerializeField] private Button toggleButton;

    [Tooltip("Texto del botón: muestra el tempo actual.")]
    [SerializeField] private Text buttonLabel;

    [Tooltip("El panel completo de tempo (el GameObject que lleva LoopTempoControl).")]
    [SerializeField] private GameObject panel;

    [Tooltip("Qué se oculta cuando el modo SYNC está apagado. Si se deja vacío se oculta el propio botón.")]
    [SerializeField] private GameObject visibleOnlyInSync;

    [Tooltip("Dónde se ancla el panel. Si se deja vacío, el botón.")]
    [SerializeField] private RectTransform anchorTarget;

    [SerializeField] private bool preferAbove = true;
    [SerializeField] private float gap = 10f;

    [Tooltip("Segundos sin tocar el tempo para que el panel se cierre solo (0 = no se cierra solo).")]
    [SerializeField] private float autoCloseSeconds = 4f;

    private float closeAt;
    private bool wired;

    private void Awake()
    {
        if (wired) return;
        wired = true;

        if (anchorTarget == null && toggleButton != null) anchorTarget = toggleButton.transform as RectTransform;
        toggleButton?.onClick.AddListener(Toggle);
        if (panel != null) panel.SetActive(false);
    }

    private void OnEnable()
    {
        LoopManager.OnTempoChanged += HandleTempoChanged;
        LoopManager.OnSyncChanged += HandleSyncChanged;
        Refresh();
    }

    private void OnDisable()
    {
        LoopManager.OnTempoChanged -= HandleTempoChanged;
        LoopManager.OnSyncChanged -= HandleSyncChanged;
    }

    private void Update()
    {
        if (panel != null && panel.activeSelf && autoCloseSeconds > 0f && Time.unscaledTime >= closeAt) Close();
    }

    public void Toggle()
    {
        if (panel != null && panel.activeSelf) Close();
        else Open();
    }

    public void Open()
    {
        if (panel == null) return;

        panel.SetActive(true);
        UIPlacement.PlaceNear(panel.transform as RectTransform, anchorTarget, gap, preferAbove);
        closeAt = Time.unscaledTime + autoCloseSeconds;
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void HandleTempoChanged(float bpm)
    {
        RefreshLabel();
        // Mientras se está moviendo el tempo, el panel no se cierra.
        closeAt = Time.unscaledTime + autoCloseSeconds;
    }

    private void HandleSyncChanged(bool synced) => Refresh();

    private void Refresh()
    {
        LoopManager manager = LoopManager.Instance;
        bool synced = manager != null && manager.SyncEnabled;

        GameObject target = visibleOnlyInSync != null ? visibleOnlyInSync
                          : (toggleButton != null ? toggleButton.gameObject : null);
        if (target != null && target != gameObject) target.SetActive(synced);
        if (!synced) Close();

        RefreshLabel();
    }

    private void RefreshLabel()
    {
        LoopManager manager = LoopManager.Instance;
        if (buttonLabel != null && manager != null) buttonLabel.text = $"{Mathf.RoundToInt(manager.Bpm)} BPM";
    }
}
