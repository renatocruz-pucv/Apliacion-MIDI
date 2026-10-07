using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pinta el Background (track), el Fill y el Handle de un Slider según el tema
/// activo (UIThemeSO.faderTrack / faderFill / faderHandle).
///
/// Según el ADD, el fondo del fader NO es el mismo gris fijo en los dos temas:
/// en modo oscuro el track es gris oscuro, y en modo claro el track pasa a
/// gris claro — sigue al tema igual que ThemedBackground, pero con sus propios
/// colores (no los de fondo general de la pantalla). Por eso hace falta un
/// componente separado en vez de reusar ThemedBackground sobre el Slider.
///
/// Se agrega en el MISMO GameObject que tiene el componente Slider (el padre
/// que contiene a "Background", "Fill Area" y "Handle Slide Area").
/// Si alguna referencia queda vacía (por ejemplo si el fader no tiene Handle
/// visible), simplemente se ignora esa parte.
/// </summary>
[RequireComponent(typeof(Slider))]
public class ThemedFader : MonoBehaviour, IThemeable
{
    [Tooltip("La Image de fondo del slider (el hijo 'Background'). Es el track " +
             "completo detrás del fill — el que cambia de gris oscuro a gris " +
             "claro entre temas según el ADD.")]
    [SerializeField] private Image trackBackground;

    [Tooltip("La Image del 'Fill' (dentro de Fill Area), que marca el nivel " +
             "actual del slider. Normalmente queda turquesa en ambos temas, " +
             "pero se deja configurable por si el ADD lo cambia a futuro.")]
    [SerializeField] private Image fill;

    [Tooltip("La Image del 'Handle' (dentro de Handle Slide Area), la perilla " +
             "que se arrastra.")]
    [SerializeField] private Image handle;

    private void Reset()
    {
        // Autocompletar las referencias más comunes para ahorrar arrastre manual:
        // busca los hijos estándar que deja el Slider por defecto de Unity.
        if (trackBackground == null)
        {
            Transform bg = transform.Find("Background");
            if (bg != null) trackBackground = bg.GetComponent<Image>();
        }
        if (fill == null)
        {
            Transform fillArea = transform.Find("Fill Area");
            if (fillArea != null)
            {
                Transform fillT = fillArea.Find("Fill");
                if (fillT != null) fill = fillT.GetComponent<Image>();
            }
        }
        if (handle == null)
        {
            Transform handleArea = transform.Find("Handle Slide Area");
            if (handleArea != null)
            {
                Transform handleT = handleArea.Find("Handle");
                if (handleT != null) handle = handleT.GetComponent<Image>();
            }
        }
    }

    private void OnEnable() => ThemeManager.Instance?.Register(this);
    private void OnDisable() => ThemeManager.Instance?.Unregister(this);

    public void ApplyTheme(UIThemeSO theme)
    {
        if (theme == null) return;

        if (trackBackground != null) trackBackground.color = theme.faderTrack;
        if (fill != null) fill.color = theme.faderFill;
        if (handle != null) handle.color = theme.faderHandle;
    }
}
