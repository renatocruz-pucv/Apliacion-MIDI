using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aplica un UIThemeSO a todos los elementos registrados, y permite alternar
/// entre claro/oscuro (toggle visible en el ADD, junto al resto de controles).
///
/// Cualquier componente que necesite repintarse al cambiar de tema (fondo, pad,
/// fader) implementa IThemeable y se registra solo en su OnEnable/OnDisable —
/// así no hay que mantener una lista a mano en el Inspector.
/// </summary>
[DefaultExecutionOrder(-200)] // debe existir antes que cualquier pad/fader se registre en su OnEnable
public class ThemeManager : MonoBehaviour
{
    public static ThemeManager Instance { get; private set; }

    [SerializeField] private UIThemeSO darkTheme;
    [SerializeField] private UIThemeSO lightTheme;
    [SerializeField] private bool startInDarkMode = true;

    public UIThemeSO CurrentTheme { get; private set; }
    public bool IsDarkMode { get; private set; }

    private readonly List<IThemeable> registered = new List<IThemeable>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        IsDarkMode = startInDarkMode;
        CurrentTheme = IsDarkMode ? darkTheme : lightTheme;
    }

    public void Register(IThemeable element)
    {
        registered.Add(element);
        element.ApplyTheme(CurrentTheme); // pinta apenas se registra, sin esperar el próximo toggle
    }

    public void Unregister(IThemeable element) => registered.Remove(element);

    public void ToggleTheme() => SetDarkMode(!IsDarkMode);

    public void SetDarkMode(bool dark)
    {
        IsDarkMode = dark;
        CurrentTheme = dark ? darkTheme : lightTheme;

        foreach (var element in registered)
        {
            element.ApplyTheme(CurrentTheme);
        }
    }
}

/// <summary>Implementado por cualquier componente visual que deba repintarse al cambiar de tema.</summary>
public interface IThemeable
{
    void ApplyTheme(UIThemeSO theme);
}
