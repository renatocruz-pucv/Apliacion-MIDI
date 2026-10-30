using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pinta el Handle de un Slider según el tema activo (UIThemeSO.faderHandle),
/// tal como funcionaba originalmente — el handle no cambia.
///
/// El Fill (Deslizador) y el Background (rail) NO se tiñen desde acá: el
/// diseñador entregó sprites propios por tema para esas dos piezas, y teñirlas
/// con un color encima las oscurece (Unity multiplica color * sprite). Ese cambio
/// de SPRITE lo resuelve ThemedFaderSpriteSwap, un componente aparte en el mismo
/// GameObject — ambos conviven en cada fader, cada uno a cargo de lo suyo.
///
/// Se agrega en el MISMO GameObject que tiene el componente Slider — UNO por
/// cada fader (8 canales + Master + Bass = 10). El autocompletado de referencias
/// corre en Reset() y en Awake(), así que no hace falta arrastrar el campo a mano
/// si la jerarquía sigue el estándar de Unity (Handle Slide Area/Handle).
/// </summary>
[RequireComponent(typeof(Slider))]
public class ThemedFader : MonoBehaviour, IThemeable
{
    [Tooltip("La Image del 'Handle' (dentro de Handle Slide Area), la perilla que se arrastra.")]
    [SerializeField] private Image handle;

    private void Reset() => AutoAssignReferences();
    private void Awake() => AutoAssignReferences();

    private void AutoAssignReferences()
    {
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
        if (handle != null) handle.color = theme.faderHandle;
    }
}
