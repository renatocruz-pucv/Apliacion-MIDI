using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cambia los SPRITES del Deslizador (Fill) y del rail (Background/track) de un
/// fader según el tema activo — el equivalente de ThemedSpriteSwap pero para
/// VARIOS sub-elementos de un mismo Slider a la vez, en vez de uno solo.
///
/// Mapeo de los assets del diseñador a las piezas del Slider de Unity:
///   - "Deslizador" -> Fill (Fill Area/Fill), la pieza que marca el nivel.
///   - "rail"       -> Background, el riel/línea de fondo del fader.
///   - El Handle NO se toca desde acá: lo sigue resolviendo ThemedFader.
///
/// Los sprites ya traen su color final por tema, así que NO deben teñirse
/// (Unity multiplica color * sprite y los oscurece, igual que pasó con los pads).
/// Por eso ThemedFader ya no aplica color al Fill ni al Background.
///
/// Se agrega en el MISMO GameObject que tiene el componente Slider, junto a
/// ThemedFader — uno por cada uno de los 10 faders (8 canales + Master + Bass).
/// </summary>
[RequireComponent(typeof(Slider))]
public class ThemedFaderSpriteSwap : MonoBehaviour, IThemeable
{
    [Header("Deslizador (Fill)")]
    [Tooltip("La Image del 'Fill' (Fill Area/Fill del Slider). Aquí va el asset 'Deslizador'.")]
    [SerializeField] private Image fillImage;
    [SerializeField] private Sprite deslizadorDarkSprite;
    [SerializeField] private Sprite deslizadorLightSprite;

    [Header("Rail (Background)")]
    [Tooltip("La Image del 'Background' del Slider. Aquí va el asset 'rail'.")]
    [SerializeField] private Image railImage;
    [SerializeField] private Sprite railDarkSprite;
    [SerializeField] private Sprite railLightSprite;

    private void Reset() => AutoAssignReferences();
    private void Awake() => AutoAssignReferences();

    /// <summary>Busca los hijos estándar del Slider de Unity, igual que ThemedFader,
    /// para no depender de arrastrar las referencias a mano en los 10 faders.</summary>
    private void AutoAssignReferences()
    {
        if (fillImage == null)
        {
            Transform fillArea = transform.Find("Fill Area");
            if (fillArea != null)
            {
                Transform fillT = fillArea.Find("Fill");
                if (fillT != null) fillImage = fillT.GetComponent<Image>();
            }
        }
        if (railImage == null)
        {
            Transform bg = transform.Find("Background");
            if (bg != null) railImage = bg.GetComponent<Image>();
        }
    }

    private void OnEnable() => ThemeManager.Instance?.Register(this);
    private void OnDisable() => ThemeManager.Instance?.Unregister(this);

    public void ApplyTheme(UIThemeSO theme)
    {
        if (ThemeManager.Instance == null) return;
        bool isDark = ThemeManager.Instance.IsDarkMode;

        if (fillImage != null)
        {
            Sprite target = isDark ? deslizadorDarkSprite : deslizadorLightSprite;
            if (target != null) fillImage.sprite = target;
        }

        if (railImage != null)
        {
            Sprite target = isDark ? railDarkSprite : railLightSprite;
            if (target != null) railImage.sprite = target;
        }
    }
}
