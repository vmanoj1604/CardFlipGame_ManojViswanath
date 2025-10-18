using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(GridLayoutGroup))]
public class GridAutoFitter : UIBehaviour
{
    [Header("Target")]
    [SerializeField] private GridLayoutGroup grid;

    [Header("Behavior")]
    [SerializeField] private bool squareCells = true;

    [Header("Auto Fit Controls")]
    [Range(0f, 0.2f)] public float paddingPercent = 0.04f;
    public float minSpacing = 4f;
    public float maxSpacing = 40f;

    private RectTransform rt;
    private RectOffset padCache;
    private int columns = 4;
    private int rows = 4;

    protected override void Awake()
    {
        base.Awake();
        rt = GetComponent<RectTransform>();
        if (!grid) grid = GetComponent<GridLayoutGroup>();
        padCache = grid.padding ?? new RectOffset();
        grid.padding = padCache;
    }

    public void SetGrid(int cols, int rows)
    {
        columns = Mathf.Max(1, cols);
        this.rows = Mathf.Max(1, rows);
        if (grid)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
        }
        RecalculateNow();
    }

    public void SetSquareCells(bool square) => squareCells = square;

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        RecalculateNow();
    }

    public void RecalculateNow()
    {
        if (!grid || !rt || columns <= 0 || rows <= 0) return;

        var rect = rt.rect;
        float w = Mathf.Max(0f, rect.width);
        float h = Mathf.Max(0f, rect.height);
        if (w <= 0f || h <= 0f) return;

        float minD = Mathf.Min(w, h);
        int padPx = Mathf.RoundToInt(minD * Mathf.Clamp01(paddingPercent));
        padCache.left = padPx; padCache.right = padPx; padCache.top = padPx; padCache.bottom = padPx;

        float innerW = Mathf.Max(0f, w - (padCache.left + padCache.right));
        float innerH = Mathf.Max(0f, h - (padCache.top + padCache.bottom));

        float baseSpacingX = Mathf.Max(minSpacing, grid.spacing.x);
        float baseSpacingY = Mathf.Max(minSpacing, grid.spacing.y);

        float candCellW = (innerW - baseSpacingX * Mathf.Max(0, columns - 1)) / columns;
        float candCellH = (innerH - baseSpacingY * Mathf.Max(0, rows - 1)) / rows;

        float cellW, cellH;
        if (squareCells)
        {
            float size = Mathf.Floor(Mathf.Max(0f, Mathf.Min(candCellW, candCellH)));
            size = Mathf.Max(1f, size);
            cellW = size; cellH = size;
        }
        else
        {
            cellW = Mathf.Max(1f, Mathf.Floor(Mathf.Max(0f, candCellW)));
            cellH = Mathf.Max(1f, Mathf.Floor(Mathf.Max(0f, candCellH)));
        }

        float spacingX = columns > 1 ? (innerW - cellW * columns) / (columns - 1) : 0f;
        float spacingY = rows > 1 ? (innerH - cellH * rows) / (rows - 1) : 0f;

        spacingX = Mathf.Clamp(spacingX, columns > 1 ? minSpacing : 0f, columns > 1 ? maxSpacing : 0f);
        spacingY = Mathf.Clamp(spacingY, rows > 1 ? minSpacing : 0f, rows > 1 ? maxSpacing : 0f);

        if (columns > 0)
        {
            float usedW = cellW * columns + spacingX * Mathf.Max(0, columns - 1);
            if (usedW > innerW)
            {
                cellW = Mathf.Floor((innerW - spacingX * Mathf.Max(0, columns - 1)) / columns);
                if (squareCells) cellH = cellW;
            }
        }

        if (rows > 0)
        {
            float usedH = cellH * rows + spacingY * Mathf.Max(0, rows - 1);
            if (usedH > innerH)
            {
                cellH = Mathf.Floor((innerH - spacingY * Mathf.Max(0, rows - 1)) / rows);
                if (squareCells) cellW = Mathf.Min(cellW, cellH);
            }
        }

        grid.cellSize = new Vector2(cellW, cellH);
        grid.spacing = new Vector2(spacingX, spacingY);

        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }
}
