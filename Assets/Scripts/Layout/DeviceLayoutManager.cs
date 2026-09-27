using UnityEngine;

/// <summary>
/// Fuerza orientación horizontal y expone si el dispositivo actual es "tablet"
/// o "teléfono", por si algún componente puntual necesita ajustarse según eso
/// (ej. tamaño de fuente). Ya NO decide qué jerarquía de UI mostrar: tanto
/// PadsScreen como MixerScreen usan ResponsiveGridCellSizer + anclajes
/// fraccionarios, así que una sola jerarquía de cada una sirve para cualquier
/// tamaño de pantalla — tablet y teléfono comparten ambas pantallas completas.
///
/// Umbral basado en pulgadas de diagonal de pantalla, criterio estándar en
/// Android para distinguir teléfonos de tablets (~ 7 pulgadas).
/// </summary>
public class DeviceLayoutManager : MonoBehaviour
{
    [SerializeField] private float tabletDiagonalInchesThreshold = 7f;

    public bool IsTablet { get; private set; }

    private void Awake()
    {
        Screen.orientation = ScreenOrientation.AutoRotation;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;

        IsTablet = ComputeIsTablet();
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
}
