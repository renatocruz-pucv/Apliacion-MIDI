using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cambia el SPRITE completo de una Image según el tema activo (oscuro/claro),
/// en vez de teñir el sprite con un color (eso era ThemedContrastSurface, y
/// oscurecía el arte porque Unity multiplica color * sprite).
///
/// Usar esto para cualquier elemento donde el diseñador entregó DOS versiones
/// ya resueltas del mismo gráfico — una para tema oscuro y otra para tema claro
/// (ej. "Boton de cambio" / flechas, "Boton superior deslizante" / hamburguesa).
/// El componente solo elige cuál mostrar; no aplica ningún tinte.
///
/// Se agrega en el MISMO GameObject que tiene la Image a cambiar.
/// </summary>
[RequireComponent(typeof(Image))]
public class ThemedSpriteSwap : MonoBehaviour, IThemeable
{
    [Tooltip("Sprite a mostrar cuando el tema activo es el oscuro (Tema_Oscuro).")]
    [SerializeField] private Sprite darkModeSprite;

    [Tooltip("Sprite a mostrar cuando el tema activo es el claro (Tema_Claro).")]
    [SerializeField] private Sprite lightModeSprite;

    private Image image;

    private void Awake() => image = GetComponent<Image>();
    private void OnEnable() => ThemeManager.Instance?.Register(this);
    private void OnDisable() => ThemeManager.Instance?.Unregister(this);

    public void ApplyTheme(UIThemeSO theme)
    {
        if (image == null || ThemeManager.Instance == null) return;

        bool isDark = ThemeManager.Instance.IsDarkMode;
        Sprite target = isDark ? darkModeSprite : lightModeSprite;

        if (target != null)
        {
            image.sprite = target;
        }
    }
}
