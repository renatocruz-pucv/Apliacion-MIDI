using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Indicador de pulso: una fila de puntitos (uno por tiempo del compás, normalmente 4) en la que se
/// enciende el tiempo actual. El primer punto (el "1") tiene su propio color para distinguirlo.
/// El "1" es el momento en que sonó el primer loop (el que define el reloj); desde ahí se cuenta
/// 1-2-3-4, 1-2-3-4... Con eso se ve en qué tiempo conviene activar cada pad.
///
/// Se muestra encendido solo mientras hay un reloj (modo SYNC con loops sonando); si no, los puntitos
/// quedan apagados. Poner Raycast Target desactivado en las Image.
/// </summary>
public class LoopBeatIndicator : MonoBehaviour
{
    [Tooltip("Los puntitos en orden: el primero es el tiempo 1. Se usan hasta LoopManager.BeatsPerBar (4).")]
    [SerializeField] private Image[] dots;

    [Tooltip("Opcional: se oculta cuando el modo SYNC está apagado. No debe ser el mismo GameObject que lleva este script.")]
    [SerializeField] private GameObject root;

    [SerializeField] private Color idleColor = new Color(0.55f, 0.55f, 0.55f, 0.5f);
    [SerializeField] private Color beatColor = new Color(0.1f, 0.8f, 0.7f, 1f);
    [SerializeField] private Color downbeatColor = new Color(1f, 0.55f, 0.1f, 1f);

    [Tooltip("Cuánto se agranda el punto del tiempo actual.")]
    [SerializeField] private float activeScale = 1.35f;

    private void OnEnable()
    {
        LoopManager.OnBeat += HandleBeat;
        LoopManager.OnSyncChanged += HandleSyncChanged;
        ShowBeat(-1);
        RefreshRoot();
    }

    private void OnDisable()
    {
        LoopManager.OnBeat -= HandleBeat;
        LoopManager.OnSyncChanged -= HandleSyncChanged;
    }

    private void HandleBeat(int beatInBar) => ShowBeat(beatInBar);
    private void HandleSyncChanged(bool synced) => RefreshRoot();

    private void RefreshRoot()
    {
        if (root != null) root.SetActive(LoopManager.Instance != null && LoopManager.Instance.SyncEnabled);
    }

    private void ShowBeat(int beatInBar)
    {
        if (dots == null) return;

        for (int i = 0; i < dots.Length; i++)
        {
            if (dots[i] == null) continue;

            bool active = i == beatInBar;
            dots[i].color = active ? (i == 0 ? downbeatColor : beatColor) : idleColor;
            dots[i].rectTransform.localScale = active ? Vector3.one * activeScale : Vector3.one;
        }
    }
}
