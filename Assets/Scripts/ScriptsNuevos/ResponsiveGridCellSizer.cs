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
///
/// [ExecuteAlways]: sin esto, el script SOLO recalcula durante Play. Fuera de
/// Play, Grid Layout Group sigue usando el último Cell Size que haya quedado
/// guardado (de una sesión de Play anterior, o el valor por defecto), sin
/// importar qué tan grande o chico sea el contenedor en ese momento en el
/// Editor — por eso el layout podía verse mal en la Scene/Simulator sin haber
/// apretado Play, o al cambiar de dispositivo sin volver a correr la escena.
/// Con ExecuteAlways, se recalcula en vivo apenas cambias los Anchors, el
/// tamaño del Canvas, o el dispositivo simulado, sin necesidad de Play.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(GridLayoutGroup))]
[RequireComponent(typeof(RectTransform))]
public class ResponsiveGridCellSizer : MonoBehaviour
{
    [SerializeField] private int columns = 4;
    [SerializeField] private int rows = 2;

    [Tooltip("Si está activo, las celdas mantienen exactamente Cell Aspect Ratio aunque sobre " +
             "espacio (uso: pads, que deben quedar cuadrados como en el ADD). Si está desactivado, " +
             "las celdas llenan el contenedor completo en ambos ejes sin guardar proporción fija " +
             "(uso: faders, que solo necesitan ocupar todo el ancho/alto disponible).")]
    [SerializeField] private bool lockAspectRatio = true;

    [Tooltip("Relación ancho/alto deseada por celda (1 = cuadrada, como los pads del ADD). " +
             "Se ignora si Lock Aspect Ratio está desactivado.")]
    [SerializeField] private float cellAspectRatio = 1f;

    [Tooltip("Umbral en píxeles para avisar en la Console si el CONTENEDOR (no la celda) parece " +
             "demasiado chico — síntoma de un RectTransform mal configurado, no del script. " +
             "Se compara contra el ancho y el alto reales del contenedor, no contra columnas/filas, " +
             "para que no dé falsos positivos con grillas angostas como la de 10 faders.")]
    [SerializeField] private float minExpectedContainerSize = 150f;

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
        // Fuerza que cualquier layout pendiente del Canvas se resuelva ANTES de
        // medir. Sin esto, si este script corre muy temprano (Awake/OnEnable
        // en el primer frame), rectTransform.rect puede devolver un tamaño
        // provisorio antes de que el Canvas termine su primer cálculo real,
        // disparando un aviso falso de "contenedor sospechosamente chico".
        Canvas.ForceUpdateCanvases();

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

        float cellWidth;
        float cellHeight;

        if (lockAspectRatio)
        {
            // Respeta el aspecto deseado tomando el lado más chico como límite,
            // así los pads nunca se salen del contenedor en ningún eje (puede
            // dejar espacio libre sobrante si el contenedor no tiene ese mismo
            // aspecto — es el comportamiento correcto para pads cuadrados).
            cellWidth = Mathf.Min(cellWidthFromWidth, cellHeightFromHeight * cellAspectRatio);
            cellHeight = cellWidth / cellAspectRatio;
        }
        else
        {
            // Llena el contenedor completo en ambos ejes de forma independiente,
            // sin guardar una proporción fija (faders: siempre altos y angostos,
            // pero no necesitan un ratio exacto, solo ocupar todo el espacio).
            cellWidth = cellWidthFromWidth;
            cellHeight = cellHeightFromHeight;
        }

        grid.cellSize = new Vector2(cellWidth, cellHeight);

        // Aviso temprano: si el CONTENEDOR real quedó mucho más chico de lo
        // esperado (por ejemplo por un Left/Top/Right/Bottom residual de haber
        // arrastrado el panel a mano en el Editor), esto no lo corrige el
        // script — solo avisa para no tener que descubrirlo a ojo comparando
        // el Inspector con el Game View. Se compara el contenedor, no la celda,
        // para no dar falsos positivos en grillas de muchas columnas (ej. 10
        // faders angostos son normales; un contenedor de 100px de ancho no).
        if (rect.width < minExpectedContainerSize || rect.height < minExpectedContainerSize)
        {
            Debug.LogWarning(
                $"[ResponsiveGridCellSizer] '{name}': el contenedor mide {rect.width:F0}x{rect.height:F0}px, " +
                "sospechosamente chico. Revisa si su RectTransform tiene Left/Top/Right/Bottom (u offsets) " +
                "distintos de 0 encima de sus Anchors — eso achica el rect real sin que se note en los Anchors.",
                this);
        }
    }
}
