using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Solo se usa en el layout de teléfono. Según el ADD, la grilla de teléfono
/// es de 3 columnas x 2 filas (6 pads fijos visibles). El ADD no dibuja dónde
/// quedan los 2 pads restantes del banco (que sí existe, son 8 por banco) —
/// se agregó este botón/interacción propia: un botón "Ver más" despliega una
/// tercera fila hacia abajo con los 2 pads que faltan (2 de 3 slots usados).
///
/// Requiere:
///  - Un GameObject "ExtraRow" con los pads 7 y 8 (más un slot vacío o solo 2),
///    puesto en la fila de abajo de la grilla, inicialmente colapsado (altura 0
///    o fuera de pantalla).
///  - Un botón visible ("▾ Ver más" / "▴ Ver menos") que llama a Toggle().
/// </summary>
public class PadOverflowScroller : MonoBehaviour
{
    [SerializeField] private RectTransform extraRow;   // contiene los pads 7 y 8
    [SerializeField] private Text toggleButtonLabel;    // opcional: cambia el texto/ícono del botón
    [SerializeField] private string collapsedLabel = "\u25be Ver m\u00e1s"; // ▾
    [SerializeField] private string expandedLabel = "\u25b4 Ver menos";    // ▴
    [SerializeField] private float collapsedHeight = 0f;
    [SerializeField] private float expandedHeight = 220f; // alto real de una fila de pads
    [SerializeField] private float animDuration = 0.2f;

    private LayoutElement extraRowLayout;
    private bool isExpanded;
    private Coroutine animRoutine;

    private void Awake()
    {
        extraRowLayout = extraRow.GetComponent<LayoutElement>();
        if (extraRowLayout == null) extraRowLayout = extraRow.gameObject.AddComponent<LayoutElement>();
        extraRowLayout.preferredHeight = collapsedHeight;
        UpdateLabel();
    }

    /// <summary>Conectar al OnClick() del botón "Ver más / Ver menos".</summary>
    public void Toggle()
    {
        isExpanded = !isExpanded;
        float target = isExpanded ? expandedHeight : collapsedHeight;

        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(AnimateHeight(target));
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (toggleButtonLabel != null)
        {
            toggleButtonLabel.text = isExpanded ? expandedLabel : collapsedLabel;
        }
    }

    private IEnumerator AnimateHeight(float targetHeight)
    {
        float startHeight = extraRowLayout.preferredHeight;
        float t = 0f;
        while (t < animDuration)
        {
            t += Time.deltaTime;
            extraRowLayout.preferredHeight = Mathf.Lerp(startHeight, targetHeight, t / animDuration);
            yield return null;
        }
        extraRowLayout.preferredHeight = targetHeight;
    }
}
