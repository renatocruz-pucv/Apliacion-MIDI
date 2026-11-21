using UnityEngine;

/// <summary>
/// Utilidades para colocar un panel flotante cerca de otro elemento de la UI (por ejemplo el panel
/// de velocidad junto al pad que se está ajustando), sin salirse de la pantalla.
/// </summary>
public static class UIPlacement
{
    private static readonly Vector3[] corners = new Vector3[4];

    /// <summary>
    /// Pone <paramref name="panel"/> encima (o debajo) de <paramref name="target"/>, centrado en él y pegado a
    /// su borde, y lo ajusta para que quede dentro de su padre. Si no cabe arriba, lo pone abajo (y al revés).
    /// El panel debe ser hijo directo de un RectTransform grande (típicamente el Canvas o un panel a pantalla completa)
    /// y NO estar dentro de un Layout Group.
    /// </summary>
    public static void PlaceNear(RectTransform panel, RectTransform target, float gap = 12f, bool preferAbove = true)
    {
        if (panel == null || target == null) return;
        RectTransform parent = panel.parent as RectTransform;
        if (parent == null) return;

        // Tamaño actual del panel (se conserva aunque se cambien los anclajes).
        Vector2 size = Vector2.Scale(panel.rect.size, panel.localScale);
        Vector2 sizeDelta = panel.rect.size;

        target.GetWorldCorners(corners); // 0 = abajo-izq, 1 = arriba-izq, 2 = arriba-der, 3 = abajo-der
        Vector2 topCenter = parent.InverseTransformPoint((corners[1] + corners[2]) * 0.5f);
        Vector2 bottomCenter = parent.InverseTransformPoint((corners[0] + corners[3]) * 0.5f);

        Rect bounds = parent.rect;
        float roomAbove = bounds.yMax - (topCenter.y + gap);
        float roomBelow = (bottomCenter.y - gap) - bounds.yMin;

        bool above = preferAbove;
        if (above && size.y > roomAbove && roomBelow > roomAbove) above = false;
        else if (!above && size.y > roomBelow && roomAbove > roomBelow) above = true;

        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = above ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f);
        panel.sizeDelta = sizeDelta;

        Vector2 position = above
            ? new Vector2(topCenter.x, topCenter.y + gap)
            : new Vector2(bottomCenter.x, bottomCenter.y - gap);

        float halfWidth = size.x * 0.5f;
        position.x = Mathf.Clamp(position.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth);
        position.y = above
            ? Mathf.Min(position.y, bounds.yMax - size.y)
            : Mathf.Max(position.y, bounds.yMin + size.y);

        // Con anclajes en el centro del padre, anchoredPosition es relativa al centro de su rectángulo.
        panel.anchoredPosition = position - bounds.center;
    }
}
