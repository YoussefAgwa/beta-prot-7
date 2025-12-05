using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class BoardController : MonoBehaviour
{
    public enum FitMode { FillCell, KeepAspect }

    [Header("Board Settings")]
    public RectTransform boardContainer;  // UI Panel with GridLayoutGroup
    public GameObject cardPrefab;         // prefab created above
    public int rows = 4;
    public int cols = 4;
    public float spacing = 8f;
    public Sprite backSprite;             // common back sprite

    [Header("Sprites Pool (pairs)")]
    public List<Sprite> spritesPool = new List<Sprite>();

    [Header("Cell Fit Settings")]
    public FitMode fitMode = FitMode.FillCell;
    public float cardAspect = 0.75f;

    [Header("Layout Options")]
    public int layoutRetries = 3;

    private GridLayoutGroup _grid;
    private List<GameObject> _spawnedCards = new List<GameObject>();
    private Coroutine _generateRoutine = null;
    private bool isGenerating = false;

    public event Action OnBoardGenerated;

    private void Awake()
    {
        if (boardContainer == null)
        {
            Debug.LogError("BoardManager: boardContainer not assigned!");
        }

        if (cardPrefab == null)
        {
            Debug.LogError("BoardManager: cardPrefab not assigned!");
        }

        _grid = boardContainer.GetComponent<GridLayoutGroup>();
        if (_grid == null)
        {
            _grid = boardContainer.gameObject.AddComponent<GridLayoutGroup>();
        }
    }

    public void GenerateBoard(int newRows, int newCols)
    {
        // guard to avoid concurrent generation
        if (isGenerating)
        {
            Debug.LogWarning("BoardManager: GenerateBoard called while generation is in progress. Ignoring this call.");
            return;
        }

        rows = Mathf.Max(1, newRows);
        cols = Mathf.Max(1, newCols);

        // stop any previous routine and clear existing board
        if (_generateRoutine != null)
        {
            StopCoroutine(_generateRoutine);
            _generateRoutine = null;
        }

        ClearBoard(); // ensure clean state before generating

        _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = cols;
        _grid.spacing = new Vector2(spacing, spacing);

        _generateRoutine = StartCoroutine(ComputeCellSizeAndSpawnRoutine());

        if (newRows * newCols % 2 != 0)
        {
            Debug.LogError("Board size must be EVEN for matching game!");
            return;
        }

    }

    private IEnumerator ComputeCellSizeAndSpawnRoutine()
    {
        isGenerating = true;
        Debug.Log($"BoardManager: Starting generation ({rows}x{cols}).");

        int tries = Mathf.Max(1, layoutRetries);
        for (int i = 0; i < tries; i++)
        {
            yield return new WaitForEndOfFrame();
            LayoutRebuilder.ForceRebuildLayoutImmediate(boardContainer);
        }

        float totalWidth = boardContainer.rect.width - (_grid.padding.left + _grid.padding.right);
        float totalHeight = boardContainer.rect.height - (_grid.padding.top + _grid.padding.bottom);

        int visibleCols = Mathf.Max(1, cols);
        int visibleRows = Mathf.Max(1, rows);

        float cellWidth = (totalWidth - (_grid.spacing.x * (visibleCols - 1))) / visibleCols;
        float cellHeight = (totalHeight - (_grid.spacing.y * (visibleRows - 1))) / visibleRows;

        float finalWidth = Mathf.Floor(Mathf.Max(1f, cellWidth));
        float finalHeight = Mathf.Floor(Mathf.Max(1f, cellHeight));

        if (fitMode == FitMode.KeepAspect && cardAspect > 0f)
        {
            float byHeightWidth = Mathf.Floor(cellHeight * cardAspect);
            float byWidthHeight = Mathf.Floor(cellWidth / cardAspect);

            if (byHeightWidth <= cellWidth)
            {
                finalWidth = byHeightWidth;
                finalHeight = Mathf.Floor(finalWidth / cardAspect);
            }
            else
            {
                finalWidth = Mathf.Floor(cellWidth);
                finalHeight = Mathf.Floor(byWidthHeight);
            }
        }
        else
        {
            float minSize = Mathf.Floor(Mathf.Min(cellWidth, cellHeight));
            finalWidth = finalHeight = Mathf.Max(1f, minSize);
        }

        _grid.cellSize = new Vector2(finalWidth, finalHeight);

        // spawn after layout settled
        yield return new WaitForEndOfFrame();
        yield return StartCoroutine(SpawnCardsAfterLayout());

        isGenerating = false;
        _generateRoutine = null;
    }

    private IEnumerator SpawnCardsAfterLayout()
    {
        // ensure the cell size applied
        LayoutRebuilder.ForceRebuildLayoutImmediate(boardContainer);

        int totalCards = rows * cols;

        // ensure even
        if (totalCards % 2 != 0)
        {
            Debug.LogWarning($"BoardManager: totalCards ({totalCards}) is odd. Increasing by 1 to make even.");
            totalCards += 1;
        }

        int pairsCount = totalCards / 2;
        List<int> pairIds = new List<int>();
        for (int i = 0; i < pairsCount; i++)
        {
            pairIds.Add(i);
            pairIds.Add(i);
        }

        // shuffle
        for (int i = 0; i < pairIds.Count; i++)
        {
            int j = UnityEngine.Random.Range(i, pairIds.Count);
            int tmp = pairIds[i];
            pairIds[i] = pairIds[j];
            pairIds[j] = tmp;
        }

        // spawn exactly totalCards (but first ensure container is clean)
        // Extra safety: remove any stray children just in case
        for (int i = boardContainer.childCount - 1; i >= 0; i--)
        {
            var child = boardContainer.GetChild(i);
            if (!_spawnedCards.Contains(child.gameObject))
            {
                Destroy(child.gameObject);
            }
        }

        for (int i = 0; i < totalCards; i++)
        {
            GameObject go = Instantiate(cardPrefab, boardContainer);
            go.transform.localScale = Vector3.one;
            AdjustSpawnedRect(go);

            _spawnedCards.Add(go);

            var cardComp = go.GetComponent<Card>();
            if (cardComp == null)
            {
                Debug.LogError("Spawned prefab does not contain Card component!");
                continue;
            }

            int pid = pairIds[i % pairIds.Count];
            Sprite front = spritesPool.Count > 0 ? spritesPool[pid % spritesPool.Count] : null;
            cardComp.Initialize(pid, front, backSprite);
        }

        // final rebuild and notify
        LayoutRebuilder.ForceRebuildLayoutImmediate(boardContainer);
        Debug.Log($"BoardManager: Spawned {_spawnedCards.Count} cards. CellSize = {_grid.cellSize}");
        OnBoardGenerated?.Invoke();

        yield return null;
    }

    private void AdjustSpawnedRect(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = _grid.cellSize;
        }

        var img = go.GetComponentInChildren<Image>();
        if (img != null)
        {
            img.preserveAspect = false;
        }
    }

    public void ClearBoard()
    {
        // destroy known spawned
        foreach (var go in _spawnedCards)
        {
            if (go != null) Destroy(go);
        }
        _spawnedCards.Clear();

        // also destroy any extra children just in case
        for (int i = boardContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(boardContainer.GetChild(i).gameObject);
        }
    }

    public List<Card> GetAllCards()
    {
        List<Card> list = new List<Card>();
        foreach (var go in _spawnedCards)
        {
            if (go == null) continue;
            var c = go.GetComponent<Card>();
            if (c != null) list.Add(c);
        }
        return list;
    }
}
