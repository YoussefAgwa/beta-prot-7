using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
public class MatchManager : MonoBehaviour
{
    [Header("Scoring")]
    public int pointsPerMatch = 10;
    public int mismatchPenalty = 2;
    public float mismatchRevealTime = 0.7f; // how long mismatched cards stay face up
    public float minimumTimeBetweenMatches = 0.05f; // slight buffer

    // state
    private List<Card> openCards = new List<Card>(); // ordered by flip time
    private HashSet<Card> processing = new HashSet<Card>(); // cards currently processed (matched/mismatched)
    private int score = 0;
    private int comboCount = 0;
    private int matchesFound = 0;

    // events for UI
    public event Action<int> OnScoreChanged;
    public event Action<int> OnComboChanged;
    public event Action<int> OnMatchesFoundChanged;

    private void Start()
    {
        // optionally initialize UI via events
        OnScoreChanged?.Invoke(score);
        OnComboChanged?.Invoke(comboCount);
        OnMatchesFoundChanged?.Invoke(matchesFound);
    }

    public void OnCardFlipped(Card card)
    {
        if (card == null) return;
        if (card.IsMatched)
        {
            Debug.Log($"MatchManager: Ignoring flipped matched card id={card.id}");
            return;
        }

        Debug.Log($"MatchManager: OnCardFlipped called for id={card.id} name={card.gameObject.name}");

        // Add if not already present
        if (!openCards.Contains(card))
            openCards.Add(card);

        // Always try compare when there are at least 2 face-up, non-matched cards
        TryCompareLatestSimple();
    }

    private void TryCompareLatestSimple()
    {
        // find last two face-up, non-matched cards in openCards (by order)
        Card last = null;
        Card secondLast = null;
        for (int i = openCards.Count - 1; i >= 0; i--)
        {
            var c = openCards[i];
            if (c == null) continue;
            if (c.IsMatched) continue;
            if (!c.IsFaceUp) continue;

            if (last == null) last = c;
            else
            {
                secondLast = c;
                break;
            }
        }

        if (last == null || secondLast == null)
        {
            Debug.Log("MatchManager: Not enough face-up cards to compare.");
            return;
        }

        Debug.Log($"MatchManager: Comparing cards idA={secondLast.id} nameA={secondLast.gameObject.name} | idB={last.id} nameB={last.gameObject.name}");

        // if equal => match
        if (secondLast.id == last.id)
        {
            // mark matched
            secondLast.SetMatched();
            last.SetMatched();

            matchesFound++;
            comboCount++;
            int gained = pointsPerMatch * comboCount;
            score += gained;

            Debug.Log($"MatchManager: MATCH! id={last.id} gained={gained} totalScore={score} combo={comboCount} matchesFound={matchesFound}");

            OnScoreChanged?.Invoke(score);
            OnComboChanged?.Invoke(comboCount);
            OnMatchesFoundChanged?.Invoke(matchesFound);

            // remove matched cards from openCards
            openCards.Remove(secondLast);
            openCards.Remove(last);
        }
        else
        {
            // mismatch: keep them face up for a bit, then hide
            Debug.Log($"MatchManager: MISMATCH idA={secondLast.id} idB={last.id}. Will hide after {mismatchRevealTime}s");
            StartCoroutine(HandleMismatchCoroutine(secondLast, last));
        }
    }

    private void HandleMatch(Card a, Card b)
    {
        // set matched state
        a.SetMatched();
        b.SetMatched();

        matchesFound++;
        comboCount++;
        int gained = pointsPerMatch * comboCount;
        score += gained;

        // fire events
        OnScoreChanged?.Invoke(score);
        OnComboChanged?.Invoke(comboCount);
        OnMatchesFoundChanged?.Invoke(matchesFound);

        // clear processed from openCards (keep them but they'll be ignored because IsMatched true)
        // remove them from openCards to keep list small
        openCards.RemoveAll(c => c == a || c == b);

        // remove from processing after a tiny delay to allow any animation
        StartCoroutine(RemoveProcessingDelayed(a, b, 0.05f));
    }

    private IEnumerator HandleMismatchCoroutine(Card a, Card b)
    {
        // play mismatch sound if available
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMismatch();

        // reset combo and penalty
        comboCount = 0;
        score = Mathf.Max(0, score - mismatchPenalty);
        OnComboChanged?.Invoke(comboCount);
        OnScoreChanged?.Invoke(score);
        Debug.Log($"MatchManager: Mismatch applied. score={score} combo={comboCount}");

        // wait then hide
        yield return new WaitForSeconds(mismatchRevealTime);

        a.ForceHideInstant();
        b.ForceHideInstant();

        // remove them from openCards if present
        openCards.Remove(a);
        openCards.Remove(b);

        // small delay to allow other comparisons
        yield return new WaitForSeconds(minimumTimeBetweenMatches);
        TryCompareLatestSimple();
    }

    private IEnumerator RemoveProcessingDelayed(Card a, Card b, float delay)
    {
        yield return new WaitForSeconds(delay);
        processing.Remove(a);
        processing.Remove(b);
        // try next comparisons if player flipped more cards
        TryCompareLatestSimple();
    }

    // Expose getters for persistence and UI
    public int GetScore() => score;
    public int GetCombo() => comboCount;
    public int GetMatchesFound() => matchesFound;

    public void SetScore(int s)
    {
        score = s;
        OnScoreChanged?.Invoke(score);
    }
    public void SetCombo(int c)
    {
        comboCount = c;
        OnComboChanged?.Invoke(comboCount);
    }
    public void SetMatchesFound(int m)
    {
        matchesFound = m;
        OnMatchesFoundChanged?.Invoke(matchesFound);
    }

    // Reset manager state (new board)
    public void ResetState()
    {
        openCards.Clear();
        processing.Clear();
        score = 0;
        comboCount = 0;
        matchesFound = 0;
        OnScoreChanged?.Invoke(score);
        OnComboChanged?.Invoke(comboCount);
        OnMatchesFoundChanged?.Invoke(matchesFound);
    }
    public void ResetGame()
    {
        score = 0;
        comboCount = 0;
        matchesFound = 0;
        openCards.Clear();

        OnScoreChanged?.Invoke(score);
        OnComboChanged?.Invoke(comboCount);
        OnMatchesFoundChanged?.Invoke(matchesFound);
    }
}
