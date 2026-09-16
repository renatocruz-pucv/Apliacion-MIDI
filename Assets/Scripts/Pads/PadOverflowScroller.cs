using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Solo se usa en el layout de teléfono. La grilla de 8 pads se dibuja en una fila
/// más ancha que la pantalla; por defecto se ven los primeros 6, y este componente
/// permite deslizar (o tocar la flechita del borde) para revolucionar los 2 restantes,
/// SIN cambiar de banco — es un scroll interno de la sección actual.
///
/// Requiere que el contenido (los 8 pads) esté dentro de un RectTransform hijo
/// ("content") más ancho que el viewport visible.
/// </summary>
public class PadOverflowScroller : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private RectTransform content;     // contiene los 8 pads en fila
    [SerializeField] private float collapsedOffsetX = 0f;   // posición mostrando pads 1-6
    [SerializeField] private float expandedOffsetX = -220f; // posición mostrando pads 7-8 (ajustar al ancho real de 2 pads)
    [SerializeField] private float snapDuration = 0.2f;

    private bool isExpanded;
    private Vector2 dragStartPos;
    private float dragStartOffsetX;
    private Coroutine snapRoutine;

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragStartPos = eventData.position;
        dragStartOffsetX = content.anchoredPosition.x;
        if (snapRoutine != null) StopCoroutine(snapRoutine);
    }

    public void OnDrag(PointerEventData eventData)
    {
        float deltaX = eventData.position.x - dragStartPos.x;
        float newX = Mathf.Clamp(dragStartOffsetX + deltaX, expandedOffsetX, collapsedOffsetX);
        content.anchoredPosition = new Vector2(newX, content.anchoredPosition.y);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Snap al estado más cercano (mostrar 6 o mostrar los 2 extra), como un carrete.
        float midPoint = (collapsedOffsetX + expandedOffsetX) / 2f;
        bool goExpanded = content.anchoredPosition.x < midPoint;
        SnapTo(goExpanded);
    }

    /// <summary>Para conectar la flechita lateral del prototipo (toque simple).</summary>
    public void ToggleViaArrow() => SnapTo(!isExpanded);

    private void SnapTo(bool expanded)
    {
        isExpanded = expanded;
        float target = expanded ? expandedOffsetX : collapsedOffsetX;
        if (snapRoutine != null) StopCoroutine(snapRoutine);
        snapRoutine = StartCoroutine(SnapRoutine(target));
    }

    private IEnumerator SnapRoutine(float targetX)
    {
        float startX = content.anchoredPosition.x;
        float t = 0f;
        while (t < snapDuration)
        {
            t += Time.deltaTime;
            float x = Mathf.Lerp(startX, targetX, t / snapDuration);
            content.anchoredPosition = new Vector2(x, content.anchoredPosition.y);
            yield return null;
        }
        content.anchoredPosition = new Vector2(targetX, content.anchoredPosition.y);
    }
}
