using UnityEngine;

/// <summary>
/// Fuerza orientación horizontal y expone si el dispositivo actual es "tablet"
/// o "teléfono" (por si algún componente necesita un ajuste puntual, ej. tamaño
/// de fuente). YA NO decide qué GameObject de Pads mostrar: al ser
/// ResponsiveGridCellSizer + anclajes fraccionarios (ver PadsScreen), una sola
/// jerarquía de pads se adapta sola a cualquier tamaño de pantalla, así que
/// tablet y teléfono comparten la misma PadsScreen.
///
/// La pestaña de Mezcla todavía puede diferenciarse por dispositivo si hace
/// falta (quedan sus campos), pero revisen si con el tiempo también conviene
/// unificarla del mismo modo.
///
/// Umbral basado en pulgadas de diagonal de pantalla, criterio estándar en
/// Android para distinguir teléfonos de tablets (~ 7 pulgadas).
/// </summary>
public class DeviceLayoutManager : MonoBehaviour
{
    [SerializeField] private float tabletDiagonalInchesThreshold = 7f;

    [Header("Layouts - Pestaña de Mezcla (si aún se diferencian por dispositivo)")]
    [SerializeField] private GameObject tabletMixerLayout;
    [SerializeField] private GameObject phoneMixerLayout;

    public bool IsTablet { get; private set; }

    private void Awake()
    {
        Screen.orientation = ScreenOrientation.AutoRotation;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;

        IsTablet = ComputeIsTablet();
        ApplyMixerLayout();
    }

    private bool ComputeIsTablet()
    {
        if (Screen.dpi <= 0f)
        {
            // Algunos dispositivos/editores no reportan DPI; asumimos tablet
            // si la resolución es muy alta, como red de seguridad.
            return Mathf.Max(Screen.width, Screen.height) > 2000;
        }

        float widthInches = Screen.width / Screen.dpi;
        float heightInches = Screen.height / Screen.dpi;
        float diagonal = Mathf.Sqrt(widthInches * widthInches + heightInches * heightInches);

        return diagonal >= tabletDiagonalInchesThreshold;
    }

    private void ApplyMixerLayout()
    {
        if (tabletMixerLayout != null) tabletMixerLayout.SetActive(IsTablet);
        if (phoneMixerLayout != null) phoneMixerLayout.SetActive(!IsTablet);
    }
}
