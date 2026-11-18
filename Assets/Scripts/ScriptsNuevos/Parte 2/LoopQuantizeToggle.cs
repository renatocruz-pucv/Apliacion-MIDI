using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Botón pequeño: "el primer toque espera el pulso". Activado, en modo SYNC un pad tocado
/// suena en el siguiente pulso del reloj (cae siempre en tiempo); desactivado, suena al instante
/// (con el pequeño desfase que eso implica). Solo tiene efecto mientras haya loops sincronizados
/// sonando (es lo que crea el reloj).
///
/// Se agrega a un GameObject con Button. Dos sprites (activado / desactivado), o texto y color.
/// </summary>
[RequireComponent(typeof(Button))]
public class LoopQuantizeToggle : MonoBehaviour
{
    [Tooltip("Image del botón. Si se deja vacío usa la del mismo GameObject.")]
    [SerializeField] private Image icon;
    [SerializeField] private Sprite onSprite;
    [SerializeField] private Sprite offSprite;

    [Tooltip("Opcional: texto del botón.")]
    [SerializeField] private Text label;
    [SerializeField] private string onText = "AL PULSO";
    [SerializeField] private string offText = "AL INSTANTE";

    [Tooltip("Solo se usan si NO hay sprites asignados.")]
    [SerializeField] private Color onColor = new Color(0.1f, 0.8f, 0.7f, 1f);
    [SerializeField] private Color offColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (icon == null) icon = GetComponent<Image>();
        button.onClick.AddListener(() =>
        {
            if (LoopManager.Instance != null) LoopManager.Instance.ToggleQuantizeFirstTap();
        });
    }

    private void OnEnable()
    {
        LoopManager.OnQuantizeChanged += HandleChanged;
        Refresh();
    }

    private void OnDisable() => LoopManager.OnQuantizeChanged -= HandleChanged;

    private void HandleChanged(bool on) => Refresh();

    private void Refresh()
    {
        bool on = LoopManager.Instance != null && LoopManager.Instance.QuantizeFirstTap;

        if (icon != null)
        {
            Sprite sprite = on ? onSprite : offSprite;
            if (sprite != null) icon.sprite = sprite;
            else icon.color = on ? onColor : offColor;
        }
        if (label != null) label.text = on ? onText : offText;
    }
}
