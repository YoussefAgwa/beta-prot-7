using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public BoardController boardManager;
    public MatchManager matchManager;
    public PersistenceManager persistenceManager;
    public AudioManager audioManager; // assign in inspector or rely on singleton

    [Header("UI Bindings (optional)")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI comboText;
    public TextMeshProUGUI matchesText;

    private void Awake()
    {
        // basic null checks
        if (boardManager == null) Debug.LogError("GameManager: BoardManager not assigned");
        if (matchManager == null) Debug.LogError("GameManager: MatchManager not assigned");
        if (persistenceManager == null) Debug.LogError("GameManager: PersistenceManager not assigned");
    }

    private void Start()
    {
        matchManager.OnScoreChanged += UpdateScoreUI;
        matchManager.OnComboChanged += UpdateComboUI;
        matchManager.OnMatchesFoundChanged += UpdateMatchesUI;

        boardManager.OnBoardGenerated += OnBoardReady;

        if (persistenceManager.HasSave())
            LoadGame();
        else
            StartNewGame(boardManager.rows, boardManager.cols);
    }

    private void OnBoardReady()
    {
        Debug.Log($"GameManager: OnBoardReady called. Hooking {boardManager.GetAllCards().Count} cards.");
        var cards = boardManager.GetAllCards();
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            c.OnFlipped += matchManager.OnCardFlipped;
            c.OnFlipped += (card) =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayFlip();
            };
            c.OnMatched += (card) =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayMatch();
            };
        }

        // unsubscribe to avoid duplicate hooking on next generation
        boardManager.OnBoardGenerated -= OnBoardReady;
    }

    public void StartNewGame(int rows, int cols)
    {
        matchManager.ResetGame();

        boardManager.GenerateBoard(rows, cols);
    }

    private IEnumerator HookCardsNextFrame()
    {
        yield return new WaitForEndOfFrame();
        // small delay to ensure board spawned
        yield return new WaitForSeconds(0.05f);

        var cards = boardManager.GetAllCards();
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            // subscribe
            c.OnFlipped += matchManager.OnCardFlipped;
            c.OnMatched += (card) =>
            {
                // Play match sound
                if (AudioManager.Instance != null) AudioManager.Instance.PlayMatch();
            };
            // assign flip sound on pointer click via Card or we can play here when flipped
            // play flip sound when OnFlipped invoked — subscribe
            c.OnFlipped += (card) =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayFlip();
            };
        }
    }

    // UI updates
    private void UpdateScoreUI(int val)
    {
        Debug.Log($"UI: UpdateScoreUI to {val}");
        if (scoreText != null) scoreText.text = $"Score: {val}";
    }

    private void UpdateComboUI(int val)
    {
        if (comboText != null) comboText.text = $"Combo: {val}";
    }
    private void UpdateMatchesUI(int val)
    {
        if (matchesText != null) matchesText.text = $"Matches: {val}";
    }

    // Save current game
    public void SaveGame()
    {
        persistenceManager.SaveGame(boardManager, matchManager);
    }

    public void LoadGame()
    {
        persistenceManager.LoadGame(boardManager, matchManager);
    }
}
