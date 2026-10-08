using UnityEngine;

/// <summary>
/// Datos de un sonido asignable a un pad: el clip a reproducir, su nombre para
/// mostrar en labels (pad y mixer), y los DOS sprites de arte que entregó el
/// compañero de diseño para ese sonido — uno para el estado "apagado" (idle,
/// sin sonar) y otro para "encendido" (activo/disponible). Cada sprite ya trae
/// su propio color de fondo + dibujo resuelto por el diseñador: el pad NO debe
/// teñirlos con el color del tema (ThemeManager), solo elegir cuál mostrar.
/// </summary>
[System.Serializable]
public class PadSoundData
{
    public string displayName = "Sonido";
    public AudioClip clip;

    [Tooltip("Sprite completo (fondo + ícono) para cuando el pad está vacío o " +
             "en reposo, tal como lo entregó el diseñador.")]
    public Sprite iconOff;

    [Tooltip("Sprite completo (fondo + ícono) para cuando el pad tiene un " +
             "sonido asignado / está activo, tal como lo entregó el diseñador.")]
    public Sprite iconOn;
}
