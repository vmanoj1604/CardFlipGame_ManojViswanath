using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Card : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private float flipDuration = 0.25f;

    private RectTransform rt;

    [SerializeField] public Sprite hiddenIconSprite;
    public Sprite iconSprite;

    public bool isSelected { get; private set; }
    public bool isMatched;
    public bool IsAnimating { get; private set; }

    public CardController controller;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        if (iconImage != null) iconImage.sprite = hiddenIconSprite;
        if (rt != null) rt.localScale = Vector3.one;
        isSelected = false;
        isMatched = false;
    }

    public void OnCardClick()
    {
        if (IsAnimating || isMatched || isSelected) return;
        controller?.SetSelected(this);
    }

    public void SetIconSprite(Sprite sp) => iconSprite = sp;

    public IEnumerator FlipToReveal()
    {
        if (this == null || iconImage == null) yield break;
        if (isSelected || IsAnimating) yield break;
        controller?.PlayFlipSfx();
        yield return FlipRoutine(true);
        if (this != null) isSelected = true;
    }

    public IEnumerator FlipToHide()
    {
        if (this == null || iconImage == null) yield break;
        if (!isSelected || IsAnimating) yield break;
        controller?.PlayFlipSfx();
        yield return FlipRoutine(false);
        if (this != null) isSelected = false;
    }

    private IEnumerator FlipRoutine(bool showFront)
    {
        if (this == null || rt == null || iconImage == null) yield break;

        IsAnimating = true;
        float half = flipDuration * 0.5f;

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            if (this == null || rt == null) { IsAnimating = false; yield break; }
            float s = Mathf.Lerp(1f, 0f, t / half);
            rt.localScale = new Vector3(s, 1f, 1f);
            yield return null;
        }
        if (this == null || rt == null) { IsAnimating = false; yield break; }

        rt.localScale = new Vector3(0f, 1f, 1f);
        iconImage.sprite = showFront ? iconSprite : hiddenIconSprite;

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            if (this == null || rt == null) { IsAnimating = false; yield break; }
            float s = Mathf.Lerp(0f, 1f, t / half);
            rt.localScale = new Vector3(s, 1f, 1f);
            yield return null;
        }

        if (this != null && rt != null) rt.localScale = Vector3.one;
        IsAnimating = false;
    }
}
