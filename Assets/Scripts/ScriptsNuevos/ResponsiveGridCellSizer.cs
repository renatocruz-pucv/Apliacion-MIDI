using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ajusta el Cell Size de un Grid Layout Group para que las celdas siempre
/// llenen exactamente su contenedor (PadGrid), sin importar el aspecto de
/// pantalla del dispositivo. Sin esto, el Cell Size queda fijo en píxeles y
/// solo se ve bien en la proporción de pantalla para la que se calibró a mano
/// (por eso tablet y celular no pueden compartir la misma jerarquía tal cual).
///
/// Se agrega en el MISMO GameObject que tiene el Grid Layout Group.
/// </summary>
[RequireComponent(typeof(GridLayoutGroup))]
[RequireComponent(typeof(RectTransform))]
public class ResponsiveGridCellSizer : MonoBehaviour
{
    [SerializeField] private int columns = 4;
    [SerializeField] private int rows = 2;

    [Tooltip("Relación ancho/alto deseada por celda (1 = cuadrada, como los pads del ADD).")]
    [SerializeField] private float cellAspectRatio = 1f;

    [Tooltip("Umbral en píxeles para avisar en la Console si el Cell Size calculado parece " +
             "demasiado chico (síntoma de un RectTransform mal configurado, no del script).")]
    [SerializeField] private float minExpectedCellSize = 60f;

    private GridLayoutGroup grid;
    private RectTransform rectTransform;
    private Vector2 lastSize;

    private void Awake()
    {
        grid = GetComponent<GridLayoutGroup>();
        rectTransform = GetComponent<RectTransform>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
    }

    private void OnEnable()
    {
        Recalculate();
    }

    private void Update()
    {
        // Solo recalcula si el tamaño del contenedor cambió (rotación de pantalla,
        // resize de ventana en el editor, o el switch tablet/teléfono de
        // DeviceLayoutManager activando este panel con otro tamaño).
        if (rectTransform.rect.size != lastSize)
        {
            Recalculate();
        }
    }

    private void Recalculate()
    {
        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f) return;

        lastSize = rect.size;

        float spacingX = grid.spacing.x;
        float spacingY = grid.spacing.y;
        float paddingX = grid.padding.left + grid.padding.right;
        float paddingY = grid.padding.top + grid.padding.bottom;

        float availableWidth = rect.width - paddingX - spacingX * (columns - 1);
        float availableHeight = rect.height - paddingY - spacingY * (rows - 1);

        float cellWidthFromWidth = availableWidth / columns;
        float cellHeightFromHeight = availableHeight / rows;

        // Respeta el aspecto deseado tomando el lado más chico como límite,
        // así los pads nunca se salen del contenedor en ningún eje.
        float cellWidth = Mathf.Min(cellWidthFromWidth, cellHeightFromHeight * cellAspectRatio);
        float cellHeight = cellWidth / cellAspectRatio;

        grid.cellSize = new Vector2(cellWidth, cellHeight);

        // Aviso temprano: si el contenedor real quedó mucho más chico de lo
        // esperado (por ejemplo por un Left/Top/Right/Bottom residual de haber
        // arrastrado el panel a mano en el Editor), el Cell Size resultante
        // queda diminuto y los pads se ven "amontonados". Esto no lo corrige
        // el script — solo avisa para no tener que descubrirlo a ojo comparando
        // el Inspector con el Game View.
        if (cellWidth < minExpectedCellSize)
        {
            Debug.LogWarning(
                $"[ResponsiveGridCellSizer] '{name}': Cell Size calculado ({cellWidth:F1}px) es " +
                $"sospechosamente chico. El contenedor mide {rect.width:F0}x{rect.height:F0}px real. " +
                "Revisa si su RectTransform tiene Left/Top/Right/Bottom (u offsets) distintos de 0 " +
                "encima de sus Anchors — eso achica el rect real sin que se note en los Anchors.",
                this);
        }
    }
}
