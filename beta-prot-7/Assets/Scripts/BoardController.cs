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
    [Tooltip("Width / Height of card (e.g. 0.75 for 3:4) - used only in KeepAspect mode")]
    public float cardAspect = 0.75f; // width / height (w/h). if 0 => ignored

    [Header("Layout Options")]
    [Tooltip("How many frames to wait/retry for layout to settle")]
    public int layoutRetries = 3;

    // internal
    private GridLayoutGroup _grid;
    private List<GameObject> _spawnedCards = new List<GameObject>();
    private Coroutine _generateRoutine = null;
    private bool isGenerating = false;

    public event Action OnBoardGenerated;

    private void Awake()
    {
        // Initialize grid if possible (will try to get from boardContainer later)
        if (boardContainer != null)
        {
            _grid = boardContainer.GetComponent<GridLayoutGroup>();
        }
    }

    private void Start()
    {
        // nothing automatic here - generation should be triggered by GameManager
    }

    /// <summary>
    /// Public entry: generate board with provided rows & cols.
    /// Safe-guards against concurrent generation and missing references.
    /// </summary>
    public void GenerateBoard(int newRows, int newCols)
    {
        // debug guards
        if (boardContainer == null)
        {
            Debug.LogError("BoardController.GenerateBoard: boardContainer is NULL! Assign Board Container in Inspector.");
            return;
        }
        if (cardPrefab == null)
        {
            Debug.LogError("BoardController.GenerateBoard: cardPrefab is NULL! Assign Card Prefab in Inspector.");
            return;
        }
        if (spritesPool == null)
        {
            Debug.LogWarning("BoardController.GenerateBoard: spritesPool is NULL. Initializing empty list.");
            spritesPool = new List<Sprite>();
        }

        // ensure _grid exists
        if (_grid == null)
        {
            _grid = boardContainer.GetComponent<GridLayoutGroup>();
            if (_grid == null)
            {
                Debug.LogError("BoardController.GenerateBoard: GridLayoutGroup missing on boardContainer. Add GridLayoutGroup to the container.");
                return;
            }
        }

        // prevent concurrent generation
        if (isGenerating)
        {
            Debug.LogWarning("BoardController: GenerateBoard called while generation is in progress. Ignoring this call.");
            return;
        }

        // sanitize inputs
        rows = Mathf.Max(1, newRows);
        cols = Mathf.Max(1, newCols);

        // stop any previous routine
        if (_generateRoutine != null)
        {
            StopCoroutine(_generateRoutine);
            _generateRoutine = null;
        }

        // clear existing board before generating
        ClearBoard();

        // configure grid
        _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = cols;
        _grid.spacing = new Vector2(spacing, spacing);

        // start generation
        _generateRoutine = StartCoroutine(ComputeCellSizeAndSpawnRoutine());
    }

    private IEnumerator ComputeCellSizeAndSpawnRoutine()
    {
        isGenerating = true;
        Debug.Log($"BoardController: Starting generation ({rows}x{cols})");

        // wait a few frames to let UI layout settle
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

        // small wait to allow cell size to apply
        yield return new WaitForEndOfFrame();

        // spawn cards
        yield return StartCoroutine(SpawnCardsAfterLayout());

        // finalize
        isGenerating = false;
        _generateRoutine = null;
        Debug.Log("BoardController: Generation finished.");
    }

    private IEnumerator SpawnCardsAfterLayout()
    {
        // final rebuild
        LayoutRebuilder.ForceRebuildLayoutImmediate(boardContainer);

        int totalCards = rows * cols;

        // enforce even number to ensure pairs (common approach). If you prefer allowing odd, remove this.
        if (totalCards % 2 != 0)
        {
            Debug.LogWarning($"BoardController: totalCards ({totalCards}) is odd. Increasing by 1 to make even.");
            totalCards += 1;
        }

        int pairsCount = totalCards / 2;
        List<int> pairIds = new List<int>();
        for (int i = 0; i < pairsCount; i++)
        {
            pairIds.Add(i);
            pairIds.Add(i);
        }

        // shuffle pairIds
        for (int i = 0; i < pairIds.Count; i++)
        {
            int j = UnityEngine.Random.Range(i, pairIds.Count);
            int tmp = pairIds[i];
            pairIds[i] = pairIds[j];
            pairIds[j] = tmp;
        }

        // extra safety: clear any leftover children that are not tracked
        for (int i = boardContainer.childCount - 1; i >= 0; i--)
        {
            var child = boardContainer.GetChild(i);
            if (!_spawnedCards.Contains(child.gameObject))
            {
                Destroy(child.gameObject);
            }
        }

        // spawn
        for (int i = 0; i < totalCards; i++)
        {
            GameObject go = Instantiate(cardPrefab, boardContainer);
            go.transform.localScale = Vector3.one;
            AdjustSpawnedRect(go);
            _spawnedCards.Add(go);

            var cardComp = go.GetComponent<Card>();
            if (cardComp == null)
            {
                Debug.LogError("BoardController: Spawned prefab does not contain Card component!");
                continue;
            }

            int pid = pairIds[i % pairIds.Count];
            Sprite front = (spritesPool != null && spritesPool.Count > 0) ? spritesPool[pid % spritesPool.Count] : null;

            // initialize card and log if missing sprite
            cardComp.Initialize(pid, front, backSprite);
        }

        // rebuild and notify
        LayoutRebuilder.ForceRebuildLayoutImmediate(boardContainer);
        Debug.Log($"BoardController: Spawned {_spawnedCards.Count} cards. CellSize = {_grid.cellSize}");
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
        // destroy tracked spawned cards
        for (int i = 0; i < _spawnedCards.Count; i++)
        {
            if (_spawnedCards[i] != null)
            {
                Destroy(_spawnedCards[i]);
            }
        }
        _spawnedCards.Clear();

        // also remove any other children in the container (safety)
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