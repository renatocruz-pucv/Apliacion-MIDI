using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// Componente genérico reutilizable en la pestaña de Pads y en la de Mezcla:
/// detecta "Deslizamiento horizontal: para cambiar de página" y también
/// puede ser disparado por las flechas laterales que aparecen en ambos prototipos.
///
/// Colocar sobre el panel completo de la sección (el área detrás de los pads o de
/// los faders), cubriendo toda la zona donde se debe poder deslizar.
/// </summary>
public class SwipeNavigator : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [Tooltip("Distancia mínima en píxeles para considerar que fue un swipe y no un toque.")]
    [SerializeField] private float minSwipeDistance = 80f;

    [Header("Eventos")]
    public UnityEvent OnSwipeNext;     // deslizar hacia la izquierda -> siguiente página
    public UnityEvent OnSwipePrevious; // deslizar hacia la derecha -> página anterior

    private Vector2 dragStartPos;

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragStartPos = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        float deltaX = eventData.position.x - dragStartPos.x;

        if (Mathf.Abs(deltaX) < minSwipeDistance) return;

        if (deltaX < 0) OnSwipeNext?.Invoke();
        else OnSwipePrevious?.Invoke();
    }

    /// <summary>Para conectar directamente los botones de flecha (⇦ / ⇨) del prototipo.</summary>
    public void TriggerNext() => OnSwipeNext?.Invoke();
    public void TriggerPrevious() => OnSwipePrevious?.Invoke();
}
