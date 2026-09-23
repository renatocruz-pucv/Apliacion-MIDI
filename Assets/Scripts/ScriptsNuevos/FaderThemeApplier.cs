using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pinta los 3 sub-elementos visuales de un Slider de Unity UI (Background,
/// Fill, Handle) según el tema activo. Se agrega directo sobre el mismo
/// GameObject que tiene el componente Slider, uno por cada fader de la
/// pestaña de Mezcla (los 9 canales + el maestro).
/// </summary>
public class FaderThemeApplier : MonoBehaviour, IThemeable
{
    [SerializeField] private Image track;   // "Background" del Slider
    [SerializeField] private Image fill;    // "Fill" del Slider (la barra turquesa)
    [SerializeField] private Image handle;  // "Handle" del Slider

    private void OnEnable() => ThemeManager.Instance?.Register(this);
    private void OnDisable() => ThemeManager.Instance?.Unregister(this);

    public void ApplyTheme(UIThemeSO theme)
    {
        if (theme == null) return;
        if (track != null) track.color = theme.faderTrack;
        if (fill != null) fill.color = theme.faderFill;
        if (handle != null) handle.color = theme.faderHandle;
    }
}
