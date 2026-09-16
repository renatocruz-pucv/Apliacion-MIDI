using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// Conecta los 10 Sliders de la pestaña de Mezcla (9 canales + 1 general, ver
/// prototipo con faders numerados 1-9 y 0) a los parámetros expuestos del AudioMixer.
///
/// IMPORTANTE — decisión de diseño a validar con el equipo/cliente:
/// El GDD pide 9 faders de canal + 1 maestro, pero la grilla de pads tiene 8 pads
/// por banco. No son 1 a 1. Este script asume que los 9 canales representan
/// CATEGORÍAS de sonido fijas (por ejemplo: Efectos, Ambiente, Voces, etc.), y que
/// cada pad de cada banco está pre-asignado a una de esas 9 categorías/grupos del
/// AudioMixer (eso se hace en el Editor, ruteando el AudioMixerGroup de cada pad).
/// Si el cliente en realidad quiere que los 9 faders correspondan a "canal según
/// posición del pad", hay que ajustar channelParamNames a 8 en vez de 9, o revisar
/// el alcance con el cliente (ver Requerimientos.md, discrepancia comentada en el README).
/// </summary>
public class MixerChannelController : MonoBehaviour
{
    [SerializeField] private AudioMixer audioMixer;

    [Header("Canales 1-9")]
    [SerializeField] private Slider[] channelSliders = new Slider[9];
    [SerializeField] private string[] channelParamNames = new string[9]
    {
        "Channel1Vol", "Channel2Vol", "Channel3Vol", "Channel4Vol", "Channel5Vol",
        "Channel6Vol", "Channel7Vol", "Channel8Vol", "Channel9Vol"
    };

    [Header("Canal maestro (\"0\" en el prototipo)")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private string masterParamName = "MasterVolume";

    private void Start()
    {
        for (int i = 0; i < channelSliders.Length; i++)
        {
            int index = i; // captura para el closure
            if (channelSliders[i] == null) continue;
            channelSliders[i].onValueChanged.AddListener(value => SetChannelVolume(index, value));
        }

        if (masterSlider != null)
        {
            masterSlider.onValueChanged.AddListener(SetMasterVolume);
        }
    }

    private void SetChannelVolume(int channelIndex, float linear01)
    {
        float db = LinearToDecibel(linear01);
        audioMixer.SetFloat(channelParamNames[channelIndex], db);
    }

    private void SetMasterVolume(float linear01)
    {
        float db = LinearToDecibel(linear01);
        audioMixer.SetFloat(masterParamName, db);
    }

    private static float LinearToDecibel(float linear01)
    {
        float clamped = Mathf.Clamp(linear01, 0.0001f, 1f);
        return Mathf.Log10(clamped) * 20f;
    }
}
