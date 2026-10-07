using UnityEngine;

/// <summary>
/// Paleta de colores de un tema visual (claro u oscuro), tomada del Art Design
/// Document: fondo gris oscuro/claro, pads gris medio, pad activo turquesa,
/// fader turquesa, texto según contraste.
///
/// Se crean 2 instancias: "Tema_Oscuro" y "Tema_Claro" (Assets > Create >
/// MIDI Soundboard > UI Theme), con los valores tomados directo del ADD.
/// </summary>
[CreateAssetMenu(fileName = "NuevoTema", menuName = "MIDI Soundboard/UI Theme")]
public class UIThemeSO : ScriptableObject
{
    [Header("Fondo")]
    public Color background = new Color32(0x2B, 0x25, 0x24, 0xFF); // gris oscuro del ADD

    [Header("Pads")]
    public Color padIdle = new Color32(0x6E, 0x6E, 0x6E, 0xFF);    // gris medio
    public Color padActive = new Color32(0x1B, 0xBF, 0xAE, 0xFF);  // turquesa (pad "activado")

    [Header("Faders / controles")]
    [Tooltip("Color del track (fondo) del fader. Según el ADD, el track NO es " +
             "fijo entre temas: en 'Tema_Oscuro' debe quedar gris oscuro (p.ej. " +
             "0x4A4A4A) y en 'Tema_Claro' gris claro (p.ej. 0xC9C9C9) — ajustar " +
             "este valor en cada asset de tema, no dejar el mismo gris en ambos.")]
    public Color faderTrack = new Color32(0x4A, 0x4A, 0x4A, 0xFF);
    public Color faderHandle = new Color32(0xC9, 0xC9, 0xC9, 0xFF);
    public Color faderFill = new Color32(0x1B, 0xBF, 0xAE, 0xFF);  // mismo turquesa, marca el nivel actual

    [Header("Texto e íconos")]
    public Color textPrimary = Color.white;
    public Color textSecondary = new Color(1f, 1f, 1f, 0.6f);

    [Header("Superficies de contraste (botones, cuadro tras el fader)")]
    [Tooltip("Color de elementos que deben leerse como figura sobre el fondo del " +
             "tema actual: los botones de navegación (flechas, hamburguesa) y el " +
             "panel/cuadro detrás del fader maestro. Es la INVERSA del fondo: " +
             "oscuro cuando el tema es claro, claro cuando el tema es oscuro — " +
             "así siempre contrasta, sea cual sea el tema activo.")]
    public Color surfaceContrast = new Color32(0x1A, 0x1A, 0x1A, 0xFF);

    [Tooltip("Color del ÍCONO/TEXTO que va ENCIMA de surfaceContrast (ej. las " +
             "flechas, las líneas del ícono hamburguesa). Debe leerse sobre " +
             "surfaceContrast, no sobre el fondo general.")]
    public Color onSurfaceContrast = Color.white;
}
