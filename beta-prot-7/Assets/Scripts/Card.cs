// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.UI;

// public class Card : MonoBehaviour
// {

//     [Header("Card Info")]
//     public int cardId;
//     public Image frontImg;
//     public Image backImg;

//     public Button button;

//     void Update()
//     {

//     }
//     public void Initialize(int pairId, Sprite frontImage, Sprite backImage)
//     {
//         cardId = pairId;
//         frontImg.sprite = frontImage;
//         backImg.sprite = backImage;
//         button.interactable = true;
//     }

//     // void HandleMatching()
//     // {
//     //     this.button.onClick.AddListener(() =>
//     //     {
//     //         int firstCardId = button.GetComponentInParent<Card>().cardId;
//     //         button.interactable = false;
//     //         Debug.Log("firstCardId" + firstCardId);
//     //     });


//     // }

// }


using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Button))]
public class Card : MonoBehaviour, IPointerClickHandler
{
    public int id { get; private set; }
    public bool IsFaceUp { get; private set; }
    public bool IsMatched { get; private set; }

    [Header("Visuals")]
    public Image frontImage;     // set in prefab
    public Image backImage;      // set in prefab

    [Header("Flip Settings")]
    public float flipDuration = 0.25f; // seconds
    public bool useScaleFlip = true;   // flip by scaleX

    // Events
    public event Action<Card> OnFlipped;
    public event Action<Card> OnMatched;

    // internal
    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        // optionally ensure images are assigned
    }

    public void Initialize(int pairId, Sprite frontSprite, Sprite backSprite)
    {
        id = pairId;
        frontImage.sprite = frontSprite;
        backImage.sprite = backSprite;
        IsFaceUp = false;
        IsMatched = false;
        frontImage.gameObject.SetActive(false);
        backImage.gameObject.SetActive(true);
        _button.interactable = true;

        Debug.Log($"Card.Initialize: id={id}, front={(frontSprite != null ? frontSprite.name : "NULL")}, back={(backSprite != null ? backSprite.name : "NULL")}");
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        // prevent flipping if already matched
        if (IsMatched || IsFaceUp) return;
        // call Flip
        Flip();
    }

    public void Flip()
    {
        if (IsMatched) return;
        // disable button briefly to avoid double-click during animation
        _button.interactable = false;
        StartCoroutine(FlipCoroutine());
    }

    private IEnumerator FlipCoroutine()
    {
        if (useScaleFlip)
        {
            // scaleX 1 -> 0
            float t = 0f;
            Vector3 start = transform.localScale;
            while (t < flipDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / flipDuration);
                float sx = Mathf.Lerp(1f, 0f, p);
                transform.localScale = new Vector3(sx, start.y, start.z);
                yield return null;
            }

            // swap visuals (midpoint)
            IsFaceUp = !IsFaceUp;
            frontImage.gameObject.SetActive(IsFaceUp);
            backImage.gameObject.SetActive(!IsFaceUp);

            // scaleX 0 -> 1
            t = 0f;
            while (t < flipDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / flipDuration);
                float sx = Mathf.Lerp(0f, 1f, p);
                transform.localScale = new Vector3(sx, start.y, start.z);
                yield return null;
            }

            transform.localScale = start;
        }
        else
        {
            // instant flip fallback
            IsFaceUp = !IsFaceUp;
            frontImage.gameObject.SetActive(IsFaceUp);
            backImage.gameObject.SetActive(!IsFaceUp);
        }

        // fire OnFlipped event
        OnFlipped?.Invoke(this);
        Debug.Log($"Card: Flipped id={id} name={gameObject.name} IsFaceUp={IsFaceUp}");

        // re-enable button only if not matched and face down (otherwise MatchManager will disable permanently)
        _button.interactable = !IsMatched && !IsFaceUp;
    }

    public void ForceRevealInstant()
    {
        StopAllCoroutines();
        IsFaceUp = true;
        frontImage.gameObject.SetActive(true);
        backImage.gameObject.SetActive(false);
        _button.interactable = false;
        OnFlipped?.Invoke(this);
    }

    public void ForceHideInstant()
    {
        StopAllCoroutines();
        IsFaceUp = false;
        frontImage.gameObject.SetActive(false);
        backImage.gameObject.SetActive(true);
        _button.interactable = true;
    }

    public void SetMatched()
    {
        IsMatched = true;
        _button.interactable = false;
        // optional: play matched animation or effect
        OnMatched?.Invoke(this);
    }
}
