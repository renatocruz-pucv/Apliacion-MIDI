using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pinta un elemento con el color de CONTRASTE del tema (UIThemeSO.surfaceContrast),
/// no con el color de fondo. A diferencia de ThemedBackground (que iguala el
/// fondo del tema), este componente es para superficies que deben leerse como
/// "figura" sobre el fondo — se ven oscuras en tema claro y claras en tema
/// oscuro, para mantener siempre contraste visual, tal como pide el ADD para:
///   - Los botones de navegación (flechas "<-"/"->", el ícono de hamburguesa).
///   - El "cuadro" o panel detrás del slider maestro (MasterFaderPanel).
///
/// Opcionalmente también puede pintar un ícono/texto hijo (una Image o Text)
/// con el color onSurfaceContrast correspondiente, para que ese contenido siga
/// siendo legible sin importar qué tan oscura o clara quede la superficie.
///
/// Se agrega en el MISMO GameObject que tiene el Image de la superficie
/// (el botón, el panel), igual que ThemedBackground.
/// </summary>
[RequireComponent(typeof(Image))]
public class ThemedContrastSurface : MonoBehaviour, IThemeable
{
    [Tooltip("Opcional: un ícono o texto (como Image) que vive sobre esta superficie " +
             "y debe pintarse con onSurfaceContrast (ej. las líneas del ícono " +
             "hamburguesa, o la punta de una flecha dibujada con una Image).")]
    [SerializeField] private Image iconOnSurface;

    [Tooltip("Opcional: un Text (Legacy) que vive sobre esta superficie y debe " +
             "pintarse con onSurfaceContrast (ej. el texto '<-' o '->' si está " +
             "hecho con Text en vez de con un ícono gráfico).")]
    [SerializeField] private Text textOnSurface;

    private Image surfaceImage;

    private void Awake() => surfaceImage = GetComponent<Image>();
    private void OnEnable() => ThemeManager.Instance?.Register(this);
    private void OnDisable() => ThemeManager.Instance?.Unregister(this);

    public void ApplyTheme(UIThemeSO theme)
    {
        if (theme == null) return;

        if (surfaceImage != null) surfaceImage.color = theme.surfaceContrast;
        if (iconOnSurface != null) iconOnSurface.color = theme.onSurfaceContrast;
        if (textOnSurface != null) textOnSurface.color = theme.onSurfaceContrast;
    }
}
