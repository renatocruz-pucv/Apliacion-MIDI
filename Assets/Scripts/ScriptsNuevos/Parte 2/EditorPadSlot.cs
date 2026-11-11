using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Un pad de la grilla del editor de bancos. Es solo la parte visual y el toque:
/// muestra el nombre (o "Vacío"), usa el sprite de pad apagado/encendido según
/// tenga audio, y marca con un marco cuál es el pad seleccionado.
/// BankEditorScreen es quien decide qué mostrar y qué hacer al tocarlo.
///
/// Se agrega en cada uno de los 8 pads del editor (un GameObject con Image + Button).
/// </summary>
[RequireComponent(typeof(Button))]
public class EditorPadSlot : MonoBehaviour
{
    [Tooltip("La Image del pad. Si se deja vacío usa la del mismo GameObject.")]
    [SerializeField] private Image background;

    [SerializeField] private Text label;

    [Tooltip("Imagen de marco/resaltado que se enciende cuando este pad está seleccionado.")]
    [SerializeField] private Image selectedFrame;

    private Button button;
    private int index;
    private Action<int> onClicked;
    private bool initialized;

    private void Awake() => EnsureInitialized();

    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        button = GetComponent<Button>();
        if (background == null) background = GetComponent<Image>();
        button.onClick.AddListener(() => onClicked?.Invoke(index));
    }

    public void Bind(int padIndex, string text, bool hasAudio, bool selected,
                     Sprite spriteOff, Sprite spriteOn, Action<int> clickCallback)
    {
        EnsureInitialized();

        index = padIndex;
        onClicked = clickCallback;

        if (label != null) label.text = text;

        Sprite sprite = hasAudio ? spriteOn : spriteOff;
        if (background != null && sprite != null) background.sprite = sprite;

        if (selectedFrame != null) selectedFrame.enabled = selected;
    }
}
