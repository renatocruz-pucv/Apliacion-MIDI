using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Botón pequeño que alterna entre loops SINCRONIZADOS (comparten ritmo y entran juntos
/// en el pulso) y loops LIBRES (cada uno va a su aire, como antes). Va junto a los demás
/// controles de loop en PadsScreen. Se agrega a un GameObject con Button.
///
/// Muestra el estado con dos sprites (sincronizado / libre). Si no se asignan, usa un
/// cambio de color (sobre un fondo sin arte) y/o un texto opcional.
/// </summary>
[RequireComponent(typeof(Button))]
public class LoopSyncToggle : MonoBehaviour
{
    [Tooltip("Image del botón. Si se deja vacío usa la del mismo GameObject.")]
    [SerializeField] private Image icon;

    [Tooltip("Sprite cuando los loops están sincronizados (opcional).")]
    [SerializeField] private Sprite syncedSprite;

    [Tooltip("Sprite cuando los loops son libres (opcional).")]
    [SerializeField] private Sprite freeSprite;

    [Tooltip("Opcional: texto del botón (ej. 'SYNC' / 'LIBRE').")]
    [SerializeField] private Text label;
    [SerializeField] private string syncedText = "SYNC";
    [SerializeField] private string freeText = "LIBRE";

    [Tooltip("Solo se usan si NO hay sprites asignados (con arte del diseñador no se tiñe).")]
    [SerializeField] private Color syncedColor = new Color(0.1f, 0.8f, 0.7f, 1f);
    [SerializeField] private Color freeColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (icon == null) icon = GetComponent<Image>();
        button.onClick.AddListener(OnClicked);
    }

    private void OnEnable()
    {
        LoopManager.OnSyncChanged += HandleSyncChanged;
        Refresh();
    }

    private void OnDisable() => LoopManager.OnSyncChanged -= HandleSyncChanged;

    private void OnClicked()
    {
        if (LoopManager.Instance != null) LoopManager.Instance.ToggleSync();
    }

    private void HandleSyncChanged(bool synced) => Refresh();

    private void Refresh()
    {
        bool synced = LoopManager.Instance != null && LoopManager.Instance.SyncEnabled;

        if (icon != null)
        {
            Sprite sprite = synced ? syncedSprite : freeSprite;
            if (sprite != null) icon.sprite = sprite;
            else icon.color = synced ? syncedColor : freeColor;
        }
        if (label != null) label.text = synced ? syncedText : freeText;
    }
}
