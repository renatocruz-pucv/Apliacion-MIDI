using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Conecta el Slider de Bass de MixerScreen con AudioManager.SetBassBoostAmount.
///
/// El slider NO decide su propio valor inicial: lo toma de AudioManager
/// (initialBassAmount01), que es quien lo aplica al AudioMixer al iniciar la app.
/// Así el slider y el audio parten siempre del mismo valor, en vez de que el
/// slider muestre 0 mientras el mixer conserva otro valor guardado y el audio
/// "salte" recién cuando el usuario toca el fader.
///
/// Se agrega en el MISMO GameObject que el Slider de Bass (el de MixerScreen).
/// Funciona aunque MixerScreen arranque desactivado: OnEnable se ejecuta la
/// primera vez que el panel se activa y ahí se sincroniza el slider.
/// </summary>
[RequireComponent(typeof(Slider))]
public class BassBoostController : MonoBehaviour
{
    private Slider slider;
    private AudioManager subscribedTo;

    private void Awake() => slider = GetComponent<Slider>();

    private void OnEnable()
    {
        slider.onValueChanged.AddListener(HandleSliderChanged);
        TrySyncAndSubscribe();
    }

    // Respaldo: si AudioManager todavía no existía durante OnEnable (orden de
    // ejecución), se reintenta acá, cuando ya todos los Awake corrieron.
    private void Start() => TrySyncAndSubscribe();

    private void OnDisable()
    {
        slider.onValueChanged.RemoveListener(HandleSliderChanged);

        if (subscribedTo != null)
        {
            subscribedTo.OnBassAmountChanged -= HandleBassChanged;
            subscribedTo = null;
        }
    }

    private void TrySyncAndSubscribe()
    {
        AudioManager manager = AudioManager.Instance;
        if (manager == null) return;

        // SetValueWithoutNotify: posiciona el slider sin disparar onValueChanged,
        // para no reaplicar al mixer un valor que ya tiene.
        slider.SetValueWithoutNotify(manager.CurrentBassAmount01);

        if (subscribedTo != manager)
        {
            manager.OnBassAmountChanged += HandleBassChanged;
            subscribedTo = manager;
        }
    }

    private void HandleSliderChanged(float value01)
    {
        AudioManager.Instance?.SetBassBoostAmount(value01);
    }

    private void HandleBassChanged(float value01)
    {
        slider.SetValueWithoutNotify(value01);
    }
}
