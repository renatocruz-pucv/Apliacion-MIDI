using UnityEngine;

/// <summary>
/// Configura la orientación de la app y expone si el dispositivo actual es
/// "tablet" o "teléfono", por si algún componente puntual necesita ajustarse
/// según eso (ej. tamaño de fuente). No decide qué jerarquía de UI mostrar:
/// PadsScreen y MixerScreen usan ResponsiveGridCellSizer + anclajes fraccionarios,
/// así que una sola jerarquía de cada una sirve para cualquier tamaño de pantalla.
///
/// ORIENTACIÓN: la app es solo horizontal, pero se da vuelta sola entre
/// "horizontal izquierda" y "horizontal derecha" al girar el teléfono 180°,
/// igual que cualquier otra app (respeta el bloqueo de rotación del sistema).
/// Vertical queda deshabilitado porque el diseño del ADD es horizontal.
///
/// La orientación se aplica en Awake y se reafirma en Start y al volver a
/// primer plano: así, si otro script o el sistema cambiaron la orientación
/// después, esta configuración es la que queda vigente.
///
/// IMPORTANTE: en Project Settings > Player > Android > Resolution and
/// Presentation, "Default Orientation" debe estar en "Auto Rotation" con solo
/// Landscape Left y Landscape Right marcados, para que el AndroidManifest no
/// fije la orientación antes de que este script corra.
///
/// Umbral de tablet basado en pulgadas de diagonal de pantalla, criterio
/// estándar en Android para distinguir teléfonos de tablets (~ 7 pulgadas).
/// </summary>
public class DeviceLayoutManager : MonoBehaviour
{
    [SerializeField] private float tabletDiagonalInchesThreshold = 7f;

    public bool IsTablet { get; private set; }

    private void Awake()
    {
        ApplyOrientationSettings();
        IsTablet = ComputeIsTablet();
    }

    private void Start() => ApplyOrientationSettings();

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) ApplyOrientationSettings();
    }

    /// <summary>
    /// Habilita solo las dos orientaciones horizontales y deja que el sistema
    /// elija cuál usar según cómo se sostenga el dispositivo.
    /// Los flags autorotateTo* se fijan ANTES de activar AutoRotation, para que
    /// la primera orientación elegida ya respete la restricción.
    /// </summary>
    private static void ApplyOrientationSettings()
    {
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.AutoRotation;
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
