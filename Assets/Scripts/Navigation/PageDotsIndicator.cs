using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Genera y actualiza la fila de puntitos que indica en qué página/banco
/// se encuentra el usuario (visible tanto en la pestaña de Pads como en la de Mezcla).
/// </summary>
public class PageDotsIndicator : MonoBehaviour
{
    [SerializeField] private GameObject dotPrefab; // Image simple, circular
    [SerializeField] private Transform container;
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color inactiveColor = new Color(1, 1, 1, 0.35f);

    private readonly List<Image> dots = new List<Image>();

    public void Build(int pageCount)
    {
        foreach (Transform child in container) Destroy(child.gameObject);
        dots.Clear();

        for (int i = 0; i < pageCount; i++)
        {
            var dotObj = Instantiate(dotPrefab, container);
            dots.Add(dotObj.GetComponent<Image>());
        }
    }

    public void SetActivePage(int index)
    {
        for (int i = 0; i < dots.Count; i++)
        {
            dots[i].color = (i == index) ? activeColor : inactiveColor;
        }
    }
}
