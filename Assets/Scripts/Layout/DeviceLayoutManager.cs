using UnityEngine;

/// <summary>
/// Decide si el dispositivo actual se comporta como "tablet" (8 pads visibles,
/// más separación entre faders) o "teléfono" (6 pads + panel deslizable, 10
/// faders más compactos), y fuerza orientación horizontal.
///
/// Umbral basado en pulgadas de diagonal de pantalla, criterio estándar en
/// Android para distinguir teléfonos de tablets (~ 7 pulgadas).
/// </summary>
public class DeviceLayoutManager : MonoBehaviour
{
    [SerializeField] private float tabletDiagonalInchesThreshold = 7f;

    [Header("Layouts - Pestaña de Pads")]
    [SerializeField] private GameObject tabletPadLayout;   // 8 pads fijos, sin drawer
    [SerializeField] private GameObject phonePadLayout;    // 6 pads + PadOverflowScroller

    [Header("Layouts - Pestaña de Mezcla")]
    [SerializeField] private GameObject tabletMixerLayout; // 10 faders más separados
    [SerializeField] private GameObject phoneMixerLayout;  // 10 faders más compactos

    public bool IsTablet { get; private set; }

    private void Awake()
    {
        Screen.orientation = ScreenOrientation.AutoRotation;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;

        IsTablet = ComputeIsTablet();
        ApplyLayout();
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

    private void ApplyLayout()
    {
        if (tabletPadLayout != null) tabletPadLayout.SetActive(IsTablet);
        if (phonePadLayout != null) phonePadLayout.SetActive(!IsTablet);
        if (tabletMixerLayout != null) tabletMixerLayout.SetActive(IsTablet);
        if (phoneMixerLayout != null) phoneMixerLayout.SetActive(!IsTablet);
    }
}
