using UnityEngine;

/// <summary>
/// Representa un "banco" de sonidos: el grupo de 8 pads que se muestra en la
/// grilla principal.
///
/// Hay dos orígenes:
///  - Bancos de fábrica: assets creados desde el menú Assets > Create >
///    MIDI Soundboard > Sound Bank, de solo lectura para la persona.
///  - Bancos personalizados: instancias creadas EN EJECUCIÓN por CustomBankLibrary
///    a partir de los audios que la persona eligió en su dispositivo (ver SetPad).
/// </summary>
[CreateAssetMenu(fileName = "NuevoBanco", menuName = "MIDI Soundboard/Sound Bank")]
public class SoundBankSO : ScriptableObject
{
    [Tooltip("Nombre del banco (se puede mostrar en el título de la pestaña).")]
    public string bankName = "Banco 1";

    [Tooltip("Siempre 8 elementos: en tablet se ven los 8, en teléfono se ven 6 " +
             "y los 2 últimos quedan en el panel deslizable.")]
    [SerializeField]
    private PadSoundData[] pads = new PadSoundData[8];

    public PadSoundData GetPad(int index)
    {
        if (index < 0 || index >= pads.Length)
        {
            Debug.LogWarning($"[SoundBankSO] Índice de pad fuera de rango: {index} en banco '{bankName}'.");
            return null;
        }
        return pads[index];
    }

    /// <summary>
    /// Reemplaza el pad de una posición. Lo usa CustomBankLibrary para armar bancos
    /// en ejecución; los bancos de fábrica se llenan desde el Inspector y no lo necesitan.
    /// </summary>
    public void SetPad(int index, PadSoundData data)
    {
        if (pads == null || pads.Length != 8) pads = new PadSoundData[8];
        if (index < 0 || index >= pads.Length)
        {
            Debug.LogWarning($"[SoundBankSO] Índice de pad fuera de rango: {index} en banco '{bankName}'.");
            return;
        }
        pads[index] = data;
    }

    public int PadCount => pads.Length;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Nos aseguramos de que el banco siempre tenga exactamente 8 pads,
        // según lo definido en el GDD (8 pads en tablet / 6+2 en teléfono).
        if (pads == null || pads.Length != 8)
        {
            System.Array.Resize(ref pads, 8);
        }
    }
#endif
}
