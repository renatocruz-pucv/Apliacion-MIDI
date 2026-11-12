using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla la navegación entre bancos de sonido (las "páginas" que se cambian
/// con el swipe horizontal / flechas, indicadas por los puntitos).
///
/// El carrusel junta dos grupos, en este orden: primero los bancos de fábrica
/// (la lista "banks", assets del proyecto) y después los bancos creados por la
/// persona (CustomBankLibrary, opcional). Para el resto de la app son todos
/// SoundBankSO iguales.
///
/// OJO: esto es independiente del panel deslizable de 2 pads extra en teléfono
/// (ver PadOverflowScroller) — acá SIEMPRE se reasignan los 8 pads a un banco nuevo.
/// </summary>
public class BankManager : MonoBehaviour
{
    [Tooltip("Bancos de fábrica (assets del proyecto).")]
    [SerializeField] private List<SoundBankSO> banks;

    [Tooltip("Opcional: biblioteca de bancos creados por la persona. Si se deja vacío, " +
             "la app funciona solo con los bancos de fábrica.")]
    [SerializeField] private CustomBankLibrary customLibrary;

    [SerializeField] private PadButtonUI[] padSlots = new PadButtonUI[8]; // los 8 slots físicos en la escena
    [SerializeField] private PageDotsIndicator pageDots;
    [SerializeField] private SwipeNavigator swipeNavigator;

    [Tooltip("Opcional: texto donde se muestra el nombre del banco actual.")]
    [SerializeField] private Text bankNameLabel;

    // Lista combinada (fábrica + personalizados) sobre la que navega todo lo demás.
    private readonly List<SoundBankSO> activeBanks = new List<SoundBankSO>();
    private int currentBankIndex;

    /// <summary>
    /// Se dispara cada vez que se carga un banco (al iniciar, al cambiar de página y
    /// cuando cambia el contenido de un banco personalizado).
    /// MixerChannelController se suscribe a esto para actualizar el NOMBRE que
    /// aparece bajo cada uno de los 8 faders de canal, ya que cada fader controla
    /// "lo que sea que esté en ese slot ahora mismo", no un sonido fijo.
    ///
    /// OJO: si algo se suscribe a este evento DESPUÉS de que ya se disparó (por
    /// ejemplo un panel de Mezcla que arranca con SetActive(false) y recién se
    /// activa más tarde), se pierde ese primer aviso — por eso también existe
    /// CurrentBank, para que un suscriptor tardío pueda PREGUNTAR el estado
    /// actual en vez de depender de haber escuchado el evento a tiempo.
    /// </summary>
    public event System.Action<SoundBankSO> OnBankLoaded;

    /// <summary>El banco cargado ahora mismo — consultable en cualquier momento,
    /// a diferencia del evento OnBankLoaded que solo se recibe si ya estabas
    /// suscrito cuando se disparó.</summary>
    public SoundBankSO CurrentBank
        => (currentBankIndex >= 0 && currentBankIndex < activeBanks.Count) ? activeBanks[currentBankIndex] : null;

    private void Awake()
    {
        // La lista se arma en Awake (no en Start) para que CurrentBank ya responda
        // aunque alguien lo consulte antes de que corra Start (p. ej. el panel de Mezcla).
        RebuildBankList();
    }

    private void Start()
    {
        if (swipeNavigator != null)
        {
            swipeNavigator.OnSwipeNext.AddListener(NextBank);
            swipeNavigator.OnSwipePrevious.AddListener(PreviousBank);
        }

        if (customLibrary != null) customLibrary.OnLibraryChanged += HandleLibraryChanged;

        pageDots?.Build(activeBanks.Count);
        LoadBank(0);
    }

    private void OnDestroy()
    {
        if (customLibrary != null) customLibrary.OnLibraryChanged -= HandleLibraryChanged;
    }

    public void NextBank()
    {
        if (activeBanks.Count == 0) return;
        LoadBank((currentBankIndex + 1) % activeBanks.Count);
    }

    public void PreviousBank()
    {
        if (activeBanks.Count == 0) return;
        LoadBank((currentBankIndex - 1 + activeBanks.Count) % activeBanks.Count);
    }

    /// <summary>Salta directo a un banco concreto (por ejemplo al cerrar el editor, al banco que se editó).</summary>
    public void ShowBank(SoundBankSO bank)
    {
        int index = activeBanks.IndexOf(bank);
        if (index >= 0) LoadBank(index);
    }

    public void LoadBank(int index)
    {
        if (activeBanks.Count == 0) return;
        currentBankIndex = Mathf.Clamp(index, 0, activeBanks.Count - 1);
        SoundBankSO bank = activeBanks[currentBankIndex];

        for (int i = 0; i < padSlots.Length; i++)
        {
            if (padSlots[i] == null) continue;
            // El índice i (0-7) es el "slot" físico del pad — es lo que usa
            // AudioManager para rutear al canal Channel(i+1) del Mixer, sin
            // importar qué sonido específico haya en este banco.
            padSlots[i].Setup(bank.GetPad(i), i, bank);
        }

        if (bankNameLabel != null) bankNameLabel.text = bank.bankName;

        pageDots?.SetActivePage(currentBankIndex);
        OnBankLoaded?.Invoke(bank);
    }

    private void RebuildBankList()
    {
        activeBanks.Clear();
        if (banks != null) activeBanks.AddRange(banks);
        if (customLibrary != null) activeBanks.AddRange(customLibrary.Banks);
    }

    /// <summary>La biblioteca cambió (banco creado, borrado, renombrado o con audios nuevos).</summary>
    private void HandleLibraryChanged()
    {
        // Si el banco que se estaba mostrando sigue existiendo, se queda ahí aunque
        // su posición haya cambiado; si lo borraron, se cae al más cercano.
        SoundBankSO before = CurrentBank;

        RebuildBankList();

        int index = (before != null) ? activeBanks.IndexOf(before) : -1;
        if (index < 0) index = Mathf.Clamp(currentBankIndex, 0, Mathf.Max(0, activeBanks.Count - 1));

        pageDots?.Build(activeBanks.Count);
        LoadBank(index);
    }
}
