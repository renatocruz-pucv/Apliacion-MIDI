using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pantalla de edición de bancos personalizados. Permite, sobre un banco creado
/// por la persona: crearlo, renombrarlo y eliminarlo; y sobre cada uno de sus 8 pads:
/// añadir un audio del dispositivo, cambiarlo, quitarlo, probarlo y ponerle nombre.
/// Los bancos de fábrica no aparecen aquí: son de solo lectura.
///
/// La pantalla se dibuja ENCIMA de las demás (debe ser el ÚLTIMO hijo del Canvas y
/// tener un fondo opaco que cubra todo) y arranca desactivada. Se abre llamando a
/// Open() desde el botón de acceso en PadsScreen, y se cierra con Close().
///
/// Los cambios se guardan solos: cada acción pasa por CustomBankLibrary, que
/// escribe el JSON al instante. Los nombres se confirman al terminar de escribir
/// (cerrar el teclado), al cambiar de pad/banco y al cerrar la pantalla.
/// </summary>
public class BankEditorScreen : MonoBehaviour
{
    [Header("Datos")]
    [SerializeField] private CustomBankLibrary library;

    [Tooltip("Para mostrar en el carrusel principal el banco editado al cerrar la pantalla.")]
    [SerializeField] private BankManager bankManager;

    [Header("Barra superior")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button prevBankButton;
    [SerializeField] private Button nextBankButton;
    [SerializeField] private Button newBankButton;
    [SerializeField] private Button deleteBankButton;
    [SerializeField] private Text deleteButtonLabel;
    [SerializeField] private InputField bankNameInput;
    [SerializeField] private Text bankCounterLabel;

    [Header("Contenido")]
    [Tooltip("Mensaje que se muestra cuando todavía no hay ningún banco creado (ej. 'Crea tu primer banco').")]
    [SerializeField] private GameObject emptyStatePanel;

    [Tooltip("Agrupa la grilla de pads y el panel del pad; se oculta cuando no hay bancos.")]
    [SerializeField] private GameObject editingContent;

    [SerializeField] private EditorPadSlot[] padSlots = new EditorPadSlot[8];

    [Header("Panel del pad seleccionado")]
    [SerializeField] private Text selectedPadTitle;
    [SerializeField] private InputField padNameInput;
    [SerializeField] private Button pickAudioButton;
    [SerializeField] private Text pickAudioButtonLabel;
    [SerializeField] private Button removeAudioButton;
    [SerializeField] private Button previewButton;
    [SerializeField] private Text statusLabel;

    [Header("Textos")]
    [SerializeField] private float statusDuration = 3.5f;
    [SerializeField] private float deleteConfirmSeconds = 3f;

    private int currentBank;
    private int selectedPad;
    private bool changed;          // hubo ediciones: al cerrar se muestra ese banco en el carrusel
    private bool deleteArmed;
    private string defaultDeleteLabel = "Eliminar";
    private Coroutine deleteDisarmRoutine;
    private Coroutine statusRoutine;
    private bool wired;

    // ------------------------------------------------------------------
    // Ciclo de vida
    // ------------------------------------------------------------------

    private void Awake() => WireOnce();

    private void OnEnable()
    {
        if (library != null) library.OnLibraryChanged += HandleLibraryChanged;
    }

    private void OnDisable()
    {
        if (library != null) library.OnLibraryChanged -= HandleLibraryChanged;
        deleteArmed = false;
        ResetDeleteLabel();
    }

    private void WireOnce()
    {
        if (wired) return;
        wired = true;

        if (library != null)
        {
            if (bankNameInput != null) bankNameInput.characterLimit = library.MaxBankNameLength;
            if (padNameInput != null) padNameInput.characterLimit = library.MaxPadNameLength;
        }
        if (deleteButtonLabel != null) defaultDeleteLabel = deleteButtonLabel.text;

        closeButton?.onClick.AddListener(Close);
        prevBankButton?.onClick.AddListener(() => ChangeBank(-1));
        nextBankButton?.onClick.AddListener(() => ChangeBank(+1));
        newBankButton?.onClick.AddListener(OnNewBankClicked);
        deleteBankButton?.onClick.AddListener(OnDeleteBankClicked);
        pickAudioButton?.onClick.AddListener(OnPickAudioClicked);
        removeAudioButton?.onClick.AddListener(OnRemoveAudioClicked);
        previewButton?.onClick.AddListener(OnPreviewClicked);

        bankNameInput?.onEndEdit.AddListener(_ => CommitBankName());
        padNameInput?.onEndEdit.AddListener(_ => CommitPadName());
    }

    // ------------------------------------------------------------------
    // Abrir / cerrar
    // ------------------------------------------------------------------

    /// <summary>Conectar al botón de acceso en PadsScreen.</summary>
    public void Open()
    {
        gameObject.SetActive(true); // ejecuta Awake/OnEnable si era la primera vez
        changed = false;

        if (library.BankCount > 0) currentBank = Mathf.Clamp(currentBank, 0, library.BankCount - 1);
        selectedPad = Mathf.Clamp(selectedPad, 0, CustomBankLibrary.PadsPerBank - 1);
        Refresh();
    }

    public void Close()
    {
        CommitNames();

        SoundBankSO edited = library.GetBank(currentBank);
        gameObject.SetActive(false);

        if (changed && edited != null && bankManager != null) bankManager.ShowBank(edited);
    }

    // ------------------------------------------------------------------
    // Acciones de banco
    // ------------------------------------------------------------------

    private void ChangeBank(int direction)
    {
        int count = library.BankCount;
        if (count < 2) return;

        CommitNames();
        currentBank = (currentBank + direction + count) % count;
        selectedPad = 0;
        Refresh();
    }

    private void OnNewBankClicked()
    {
        CommitNames();

        int index = library.CreateBank();
        if (index < 0)
        {
            ShowStatus($"Ya llegaste al máximo de {library.MaxBanks} bancos.");
            return;
        }

        currentBank = index;
        selectedPad = 0;
        changed = true;
        Refresh();
        ShowStatus("Banco creado.");
    }

    /// <summary>Eliminar pide confirmación: el primer toque arma el botón ("¿Seguro?") y el segundo elimina.</summary>
    private void OnDeleteBankClicked()
    {
        if (library.BankCount == 0) return;

        if (!deleteArmed)
        {
            deleteArmed = true;
            if (deleteButtonLabel != null) deleteButtonLabel.text = "¿Seguro?";
            if (deleteDisarmRoutine != null) StopCoroutine(deleteDisarmRoutine);
            deleteDisarmRoutine = StartCoroutine(DisarmDeleteAfterDelay());
            return;
        }

        DisarmDelete();
        library.DeleteBank(currentBank);
        changed = true;

        currentBank = Mathf.Clamp(currentBank, 0, Mathf.Max(0, library.BankCount - 1));
        selectedPad = 0;
        Refresh();
        ShowStatus("Banco eliminado.");
    }

    private IEnumerator DisarmDeleteAfterDelay()
    {
        yield return new WaitForSeconds(deleteConfirmSeconds);
        DisarmDelete();
    }

    private void DisarmDelete()
    {
        deleteArmed = false;
        if (deleteDisarmRoutine != null)
        {
            StopCoroutine(deleteDisarmRoutine);
            deleteDisarmRoutine = null;
        }
        ResetDeleteLabel();
    }

    private void ResetDeleteLabel()
    {
        if (deleteButtonLabel != null) deleteButtonLabel.text = defaultDeleteLabel;
    }

    // ------------------------------------------------------------------
    // Acciones de pad
    // ------------------------------------------------------------------

    private void SelectPad(int index)
    {
        if (index == selectedPad) return;

        CommitPadName();
        selectedPad = index;
        Refresh();
    }

    private void OnPickAudioClicked()
    {
        if (library.BankCount == 0 || AudioFilePicker.IsBusy) return;

        CommitNames();

        // Se capturan ahora: la persona podría cambiar de pad mientras elige el archivo.
        int bankIndex = currentBank;
        int padIndex = selectedPad;

        AudioFilePicker.PickAudio(
            path =>
            {
                if (string.IsNullOrEmpty(path)) return; // canceló

                ShowStatus("Cargando audio...");
                library.SetPadAudio(bankIndex, padIndex, path, (ok, message) =>
                {
                    if (ok) changed = true;
                    if (!isActiveAndEnabled) return;
                    ShowStatus(message);
                    Refresh();
                });
            },
            error => ShowStatus(error));
    }

    private void OnRemoveAudioClicked()
    {
        CustomPadData pad = library.GetPadData(currentBank, selectedPad);
        if (pad == null || !pad.HasAudio) return;

        library.ClearPadAudio(currentBank, selectedPad);
        changed = true;
        Refresh();
        ShowStatus("Audio quitado.");
    }

    private void OnPreviewClicked()
    {
        PadSoundData pad = library.GetBank(currentBank)?.GetPad(selectedPad);
        if (pad == null || pad.clip == null)
        {
            ShowStatus("Este pad no tiene audio.");
            return;
        }

        // Se rutea por el canal del pad, igual que al tocarlo en la pantalla principal.
        AudioManager.Instance?.PlaySound(pad.clip, selectedPad);
    }

    // ------------------------------------------------------------------
    // Nombres
    // ------------------------------------------------------------------

    private void CommitNames()
    {
        CommitBankName();
        CommitPadName();
    }

    private void CommitBankName()
    {
        if (library.BankCount == 0 || bankNameInput == null) return;
        if (library.RenameBank(currentBank, bankNameInput.text)) changed = true;
    }

    private void CommitPadName()
    {
        if (library.BankCount == 0 || padNameInput == null) return;

        CustomPadData pad = library.GetPadData(currentBank, selectedPad);
        if (pad == null || !pad.HasAudio) return; // un pad vacío no tiene nombre que guardar

        if (library.RenamePad(currentBank, selectedPad, padNameInput.text)) changed = true;
    }

    // ------------------------------------------------------------------
    // Pintado
    // ------------------------------------------------------------------

    /// <summary>La biblioteca avisa: terminó de cargar audios, o algo cambió. Se repinta si la pantalla está abierta.</summary>
    private void HandleLibraryChanged()
    {
        if (isActiveAndEnabled) Refresh();
    }

    private void Refresh()
    {
        int bankCount = library.BankCount;
        bool hasBanks = bankCount > 0;

        // La biblioteca puede avisar de un cambio (p. ej. al borrar el último banco)
        // antes de que currentBank se haya corregido: se ajusta aquí para no salirse de rango.
        currentBank = Mathf.Clamp(currentBank, 0, Mathf.Max(0, bankCount - 1));
        selectedPad = Mathf.Clamp(selectedPad, 0, CustomBankLibrary.PadsPerBank - 1);

        if (emptyStatePanel != null) emptyStatePanel.SetActive(!hasBanks);
        if (editingContent != null) editingContent.SetActive(hasBanks);

        if (newBankButton != null) newBankButton.interactable = bankCount < library.MaxBanks;
        if (deleteBankButton != null) deleteBankButton.interactable = hasBanks;
        if (prevBankButton != null) prevBankButton.interactable = bankCount > 1;
        if (nextBankButton != null) nextBankButton.interactable = bankCount > 1;

        if (bankNameInput != null)
        {
            bankNameInput.interactable = hasBanks;
            bankNameInput.SetTextWithoutNotify(hasBanks ? (library.GetBankData(currentBank)?.name ?? "") : "");
        }
        if (bankCounterLabel != null) bankCounterLabel.text = hasBanks ? $"{currentBank + 1}/{bankCount}" : "";

        if (!hasBanks) return;

        RefreshSlots();
        RefreshPadPanel();
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < padSlots.Length; i++)
        {
            if (padSlots[i] == null) continue;

            CustomPadData pad = library.GetPadData(currentBank, i);
            bool hasAudio = pad != null && pad.HasAudio;

            string text = "Vacío";
            if (hasAudio) text = string.IsNullOrWhiteSpace(pad.displayName) ? $"Pad {i + 1}" : pad.displayName;

            padSlots[i].Bind(i, text, hasAudio, i == selectedPad,
                             library.PadIconOff, library.PadIconOn, SelectPad);
        }
    }

    private void RefreshPadPanel()
    {
        CustomPadData pad = library.GetPadData(currentBank, selectedPad);
        bool hasAudio = pad != null && pad.HasAudio;

        if (selectedPadTitle != null) selectedPadTitle.text = $"Pad {selectedPad + 1}";

        if (padNameInput != null)
        {
            padNameInput.interactable = hasAudio;
            padNameInput.SetTextWithoutNotify(hasAudio ? pad.displayName : "");
        }

        if (pickAudioButtonLabel != null) pickAudioButtonLabel.text = hasAudio ? "Cambiar audio" : "Añadir audio";
        if (removeAudioButton != null) removeAudioButton.interactable = hasAudio;

        if (previewButton != null)
        {
            PadSoundData runtimePad = library.GetBank(currentBank)?.GetPad(selectedPad);
            previewButton.interactable = hasAudio && runtimePad != null && runtimePad.clip != null;
        }
    }

    private void ShowStatus(string message)
    {
        if (statusLabel == null || !isActiveAndEnabled) return;

        statusLabel.text = message;
        if (statusRoutine != null) StopCoroutine(statusRoutine);
        statusRoutine = StartCoroutine(ClearStatusAfterDelay());
    }

    private IEnumerator ClearStatusAfterDelay()
    {
        yield return new WaitForSeconds(statusDuration);
        statusLabel.text = "";
        statusRoutine = null;
    }
}
