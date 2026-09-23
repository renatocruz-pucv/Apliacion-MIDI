using UnityEngine;

/// <summary>
/// Datos de un único pad dentro de un banco de sonidos.
/// Es una clase serializable (no un ScriptableObject) porque vive *dentro*
/// de un SoundBankSO, como un elemento de su lista de pads.
/// </summary>
[System.Serializable]
public class PadSoundData
{
    [Tooltip("Nombre que se muestra debajo del pad en la UI.")]
    public string displayName = "Sonido";

    [Tooltip("Clip de audio que se reproduce al presionar el pad. Debe estar en formato WAV.")]
    public AudioClip clip;

    [Tooltip("Ícono opcional para el pad. Si se deja vacío se usa solo el texto.")]
    public Sprite icon;
}
