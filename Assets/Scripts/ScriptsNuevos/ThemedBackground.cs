using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pinta el color de fondo de cualquier panel (el Canvas raíz, un panel de
/// pestaña, etc.) según el tema activo. Agregar sobre el Image de fondo de
/// cada pantalla (Pads, Mezcla).
/// </summary>
[RequireComponent(typeof(Image))]
public class ThemedBackground : MonoBehaviour, IThemeable
{
    private Image image;

    private void Awake() => image = GetComponent<Image>();
    private void OnEnable() => ThemeManager.Instance?.Register(this);
    private void OnDisable() => ThemeManager.Instance?.Unregister(this);

    public void ApplyTheme(UIThemeSO theme)
    {
        if (theme == null || image == null) return;
        image.color = theme.background;
    }
}
