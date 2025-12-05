using UnityEngine;
using System.Collections.Generic;

    public class PersistenceManager : MonoBehaviour
    {
        private const string HAS_SAVE = "HAS_SAVE";
        private const string ROWS = "ROWS";
        private const string COLS = "COLS";
        private const string SCORE = "SCORE";
        private const string COMBO = "COMBO";
        private const string MATCHES = "MATCHES";
        private const string CARD_COUNT = "CARD_COUNT";

        // -------- SAVE --------
        public void SaveGame(BoardController board, MatchManager match)
        {
            PlayerPrefs.SetInt(HAS_SAVE, 1);

            PlayerPrefs.SetInt(ROWS, board.rows);
            PlayerPrefs.SetInt(COLS, board.cols);
            PlayerPrefs.SetInt(SCORE, match.GetScore());
            PlayerPrefs.SetInt(COMBO, match.GetCombo());
            PlayerPrefs.SetInt(MATCHES, match.GetMatchesFound());

            var cards = board.GetAllCards();
            PlayerPrefs.SetInt(CARD_COUNT, cards.Count);

            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                PlayerPrefs.SetInt($"Card_{i}_ID", c.id);
                PlayerPrefs.SetInt($"Card_{i}_Matched", c.IsMatched ? 1 : 0);

                int spriteIndex = board.spritesPool.IndexOf(c.frontImage.sprite);
                if (spriteIndex < 0) spriteIndex = 0;
                PlayerPrefs.SetInt($"Card_{i}_Sprite", spriteIndex);
            }

            PlayerPrefs.Save();
            Debug.Log("✅ Game Saved With PlayerPrefs");
        }

        // -------- LOAD --------
        public void LoadGame(BoardController board, MatchManager match)
        {
            if (!HasSave())
            {
                Debug.LogWarning("⚠ No Save Found");
                return;
            }

            int rows = PlayerPrefs.GetInt(ROWS);
            int cols = PlayerPrefs.GetInt(COLS);

            board.GenerateBoard(rows, cols);

            int score = PlayerPrefs.GetInt(SCORE);
            int combo = PlayerPrefs.GetInt(COMBO);
            int matches = PlayerPrefs.GetInt(MATCHES);

            match.SetScore(score);
            match.SetCombo(combo);
            match.SetMatchesFound(matches);

            int cardCount = PlayerPrefs.GetInt(CARD_COUNT);

            var cards = board.GetAllCards();

            for (int i = 0; i < cards.Count && i < cardCount; i++)
            {
                int id = PlayerPrefs.GetInt($"Card_{i}_ID");
                int matched = PlayerPrefs.GetInt($"Card_{i}_Matched");
                int spriteIndex = PlayerPrefs.GetInt($"Card_{i}_Sprite");

                spriteIndex = Mathf.Clamp(spriteIndex, 0, board.spritesPool.Count - 1);

                cards[i].Initialize(id, board.spritesPool[spriteIndex], board.backSprite);

                if (matched == 1)
                {
                    cards[i].ForceRevealInstant();
                    cards[i].SetMatched();
                }
                else
                {
                    cards[i].ForceHideInstant();
                }
            }

            Debug.Log("✅ Game Loaded With PlayerPrefs");
        }

        public bool HasSave()
        {
            return PlayerPrefs.GetInt(HAS_SAVE, 0) == 1;
        }

        public void ClearSave()
        {
            PlayerPrefs.DeleteAll();
            Debug.Log("🗑 Save Cleared");
        }
    }
