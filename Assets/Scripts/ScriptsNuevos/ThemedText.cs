using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pinta un texto con el color del tema activo, para que cambie al alternar
/// entre modo claro y oscuro. Sirve para Text (Legacy) y para TextMeshPro, porque
/// ambos derivan de Graphic.
///
/// Se agrega en el MISMO GameObject que tiene el texto (por ejemplo el label
/// "MASTER" bajo el fader de PadsScreen, o cualquier otro texto fijo de la UI).
/// Los textos que ya cambian de color por otro medio (como los labels del Mixer)
/// no necesitan este componente.
///
/// El color sale de UIThemeSO, así que cada asset de tema debe tener un valor
/// que contraste con su fondo: texto claro en Tema_Oscuro y texto oscuro en
/// Tema_Claro.
/// </summary>
public class ThemedText : MonoBehaviour, IThemeable
{
    public enum ColorRole
    {
        TextPrimary,        // texto principal (UIThemeSO.textPrimary)
        TextSecondary,      // texto secundario/atenuado (UIThemeSO.textSecondary)
        OnSurfaceContrast   // texto sobre superficies de contraste (UIThemeSO.onSurfaceContrast)
    }

    [Tooltip("Qué color del tema usar para este texto.")]
    [SerializeField] private ColorRole role = ColorRole.TextPrimary;

    private Graphic graphic;

    private void Awake() => graphic = GetComponent<Graphic>();
    private void OnEnable() => ThemeManager.Instance?.Register(this);
    private void OnDisable() => ThemeManager.Instance?.Unregister(this);

    public void ApplyTheme(UIThemeSO theme)
    {
        if (theme == null) return;
        if (graphic == null) graphic = GetComponent<Graphic>();
        if (graphic == null) return;

        switch (role)
        {
            case ColorRole.TextSecondary:
                graphic.color = theme.textSecondary;
                break;
            case ColorRole.OnSurfaceContrast:
                graphic.color = theme.onSurfaceContrast;
                break;
            default:
                graphic.color = theme.textPrimary;
                break;
        }
    }
}
