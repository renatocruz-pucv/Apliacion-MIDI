using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla la navegación entre bancos de sonido (las "páginas" que se cambian
/// con el swipe horizontal / flechas, indicadas por los puntitos).
///
/// OJO: esto es independiente del panel deslizable de 2 pads extra en teléfono
/// (ver PadOverflowScroller) — acá SIEMPRE se reasignan los 8 pads a un banco nuevo.
/// </summary>
public class BankManager : MonoBehaviour
{
    [SerializeField] private List<SoundBankSO> banks;
    [SerializeField] private PadButtonUI[] padSlots = new PadButtonUI[8]; // los 8 slots físicos en la escena
    [SerializeField] private PageDotsIndicator pageDots;
    [SerializeField] private SwipeNavigator swipeNavigator;

    private int currentBankIndex;

    private void Start()
    {
        if (swipeNavigator != null)
        {
            swipeNavigator.OnSwipeNext.AddListener(NextBank);
            swipeNavigator.OnSwipePrevious.AddListener(PreviousBank);
        }

        pageDots?.Build(banks.Count);
        LoadBank(0);
    }

    public void NextBank() => LoadBank((currentBankIndex + 1) % banks.Count);

    public void PreviousBank() => LoadBank((currentBankIndex - 1 + banks.Count) % banks.Count);

    public void LoadBank(int index)
    {
        if (banks == null || banks.Count == 0) return;
        currentBankIndex = index;
        SoundBankSO bank = banks[currentBankIndex];

        for (int i = 0; i < padSlots.Length; i++)
        {
            if (padSlots[i] == null) continue;
            padSlots[i].Setup(bank.GetPad(i));
        }

        pageDots?.SetActivePage(currentBankIndex);
    }
}
