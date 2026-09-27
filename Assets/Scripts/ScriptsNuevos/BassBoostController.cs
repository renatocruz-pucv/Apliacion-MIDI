using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Conecta el Slider de "Bass" de la pestaña de Mezcla al potenciador de graves
/// continuo del AudioManager (0 = sin potenciar, 1 = máximo). A diferencia del
/// volumen general, no hay una segunda copia de este control en otra pantalla,
/// así que no necesita sincronizarse con nada más — solo empuja el valor.
/// </summary>
[RequireComponent(typeof(Slider))]
public class BassBoostController : MonoBehaviour
{
    private Slider slider;

    private void Awake()
    {
        slider = GetComponent<Slider>();
    }

    private void OnEnable()
    {
        slider.onValueChanged.AddListener(HandleChanged);

        if (AudioManager.Instance != null)
        {
            slider.SetValueWithoutNotify(AudioManager.Instance.CurrentBassAmount01);
        }
    }

    private void OnDisable()
    {
        slider.onValueChanged.RemoveListener(HandleChanged);
    }

    private void HandleChanged(float value01)
    {
        AudioManager.Instance?.SetBassBoostAmount(value01);
    }
}
