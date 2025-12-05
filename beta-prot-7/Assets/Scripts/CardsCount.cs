using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardsCount : MonoBehaviour
{
    [Header("References")]
    public BoardController boardManager;
    public GameManager gameManager;

    // ========== BUTTON EVENTS ==========

    public void SetBoard_2x2()
    {
        StartNewBoard(2, 2);
    }

    public void SetBoard_2x3()
    {
        StartNewBoard(2, 3);
    }

    public void SetBoard_5x6()
    {
        StartNewBoard(5, 6);
    }

    // ========== CORE ==========
    private void StartNewBoard(int rows, int cols)
    {
        Debug.Log($"BoardSizeSelector: Start {rows}x{cols}");

        // مهم جدًا: نمسح أي Save قديم علشان ما يحصلش تضارب
        if (gameManager.persistenceManager != null)
            gameManager.persistenceManager.ClearSave();

        gameManager.StartNewGame(rows, cols);
    }
}
