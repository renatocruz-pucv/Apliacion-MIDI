using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Conecta un Slider al volumen general (AudioManager), sin importar en qué
/// pantalla esté. Se usa en DOS lugares: el MasterFaderPanel de la pestaña de
/// Pads, y el fader "0" (maestro) de la pestaña de Mezcla — ambos controlan
/// el mismo volumen real, así que si arrastras uno, el otro se mueve solo
/// para reflejar el mismo valor (por el evento AudioManager.OnMasterVolumeChanged).
///
/// Antes había dos caminos separados: este control (nuevo) pasa siempre por
/// AudioManager.SetMasterVolume, así el mute explícito y el bass boost quedan
/// consistentes sin importar desde qué pantalla se toque el volumen.
/// </summary>
[RequireComponent(typeof(Slider))]
public class MasterVolumeController : MonoBehaviour
{
    private Slider slider;

    private void Awake()
    {
        slider = GetComponent<Slider>();
    }

    private void OnEnable()
    {
        slider.onValueChanged.AddListener(HandleSliderChanged);

        if (AudioManager.Instance != null)
        {
            // Sincroniza la perilla con el valor actual sin disparar otro SetMasterVolume.
            slider.SetValueWithoutNotify(AudioManager.Instance.CurrentVolume01);
            AudioManager.Instance.OnMasterVolumeChanged += HandleExternalChange;
        }
    }

    private void OnDisable()
    {
        slider.onValueChanged.RemoveListener(HandleSliderChanged);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.OnMasterVolumeChanged -= HandleExternalChange;
        }
    }

    private void HandleSliderChanged(float value01)
    {
        AudioManager.Instance?.SetMasterVolume(value01);
    }

    /// <summary>Llamado cuando el OTRO fader (en la otra pantalla) cambió el volumen.</summary>
    private void HandleExternalChange(float value01)
    {
        // SetValueWithoutNotify evita un loop infinito (este cambio no debe
        // volver a llamar a SetMasterVolume).
        slider.SetValueWithoutNotify(value01);
    }
}
