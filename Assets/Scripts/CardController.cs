using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class CardController : MonoBehaviour
{
    [Header("Prefabs & Layout")]
    [SerializeField] private Card cardPrefab;
    [SerializeField] private Transform gridTransform;
    [SerializeField] private GridLayoutGroup grid;
    [SerializeField] private GridAutoFitter autoFitter;

    [Header("Sprites (Fronts)")]
    [SerializeField] private Sprite[] sprites;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI matchesText;
    [SerializeField] private TextMeshProUGUI turnsText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private float winShowDelay = 0.25f;

    [Header("Win Banner UI")]
    [SerializeField] private GameObject winBanner;
    [SerializeField] private CanvasGroup winBannerGroup;
    [SerializeField] private TextMeshProUGUI winBannerText;
    [SerializeField] private float winBannerFadeIn = 0.25f;
    [SerializeField] private float winBannerScalePop = 0.1f;
    [SerializeField] private TextMeshProUGUI winText;

    [Header("Win VFX")]
    [SerializeField] private ParticleSystem victoryVfxPrefab;
    [SerializeField] private Transform victoryVfxAnchor;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip flipSfx;
    [SerializeField] private AudioClip matchSfx;
    [SerializeField] private AudioClip wrongSfx;
    [SerializeField] private AudioClip victorySfx;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public Vector2 flipPitchRange = new Vector2(0.97f, 1.03f);

    [System.Serializable]
    public struct LevelDef { public int rows; public int cols; }

    [Header("Levels (order = progression)")]
    [SerializeField]
    private LevelDef[] levels = new LevelDef[]
    {
        new LevelDef{ rows = 2, cols = 2 },
        new LevelDef{ rows = 2, cols = 3 },
        new LevelDef{ rows = 4, cols = 4 },
        new LevelDef{ rows = 5, cols = 6 },
    };

    [Header("Progression")]
    [SerializeField] private bool autoAdvanceOnWin = true;
    [SerializeField] private float nextLevelDelay = 1f;

    [Header("Initial Preview")]
    [SerializeField] private bool enableInitialPreview = true;
    [SerializeField] private float previewDuration = 2.0f;
    [SerializeField] private float previewStagger = 0f;

    private readonly List<Card> pending = new List<Card>();
    private bool processingPairs;
    private int matchesCount;
    private int turnsCount;
    private int totalPairsNeeded;
    private bool gameOver;
    private int lastRows, lastCols;
    private int currentLevelIndex = -1;
    private int score;
    private int boardVersion = 0;
    private Coroutine previewRoutine = null;
    private bool inputLocked = false;
    private Dictionary<Sprite, int> spriteIndexMap;

    [System.Serializable]
    public class SaveData
    {
        public int version = 1;
        public int currentLevelIndex;
        public int rows, cols;
        public int matchesCount, turnsCount, score;
        public int[] deckSpriteIndices;
        public bool[] matchedFlags;
    }

    private const string SAVE_KEY = "CARD_MATCH_SAVE_V1";
    private static readonly List<Card> _tempCards = new List<Card>(64);

    void Awake()
    {
        if (!grid && gridTransform) grid = gridTransform.GetComponent<GridLayoutGroup>();
        if (!autoFitter && gridTransform) autoFitter = gridTransform.GetComponent<GridAutoFitter>();
        if (!sfxSource)
        {
            sfxSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
        }
        BuildSpriteIndexMap();
        if (!autoFitter) Debug.LogWarning("CardController: GridAutoFitter is missing on the Grid object.");
    }

    public bool HasSave() => PlayerPrefs.HasKey(SAVE_KEY);

    public void StartNewGame()
    {
        ClearSave();
        score = 0;
        currentLevelIndex = 0;
        BuildCurrentLevel();
        SaveGame();
    }

    public void ResumeGame()
    {
        if (!LoadGame())
        {
            StartCampaign();
            SaveGame();
        }
    }

    public void StartCampaign()
    {
        if (levels == null || levels.Length == 0)
        {
            Debug.LogWarning("CardController: No levels configured.");
            return;
        }
        currentLevelIndex = 0;
        BuildCurrentLevel();
    }

    public bool TryLoadOrStartNew()
    {
        if (LoadGame()) return true;
        StartCampaign();
        return false;
    }

    void BuildCurrentLevel()
    {
        var lv = levels[Mathf.Clamp(currentLevelIndex, 0, levels.Length - 1)];
        BuildBoard(lv.rows, lv.cols, true);
    }

    void GoToNextLevel()
    {
        currentLevelIndex++;
        if (currentLevelIndex < levels.Length)
        {
            if (winText) winText.gameObject.SetActive(false);
            BuildCurrentLevel();
            SaveGame();
        }
        else
        {
            gameOver = true;
            if (winText)
            {
                winText.text = "All Levels Completed";
                winText.gameObject.SetActive(true);
            }
            SaveGame();
        }
    }

    private void BuildBoard(int rows, int cols, bool shuffleNewDeck)
    {
        if (!grid || !gridTransform || !cardPrefab || sprites == null) return;

        if (previewRoutine != null) { StopCoroutine(previewRoutine); previewRoutine = null; }
        boardVersion++;
        ClearBoard();

        rows = Mathf.Max(1, rows);
        cols = Mathf.Max(1, cols);
        if (((rows * cols) & 1) == 1) { Debug.LogError("CardController: rows*cols must be even."); return; }

        lastRows = rows; lastCols = cols;

        int totalCards = rows * cols;
        totalPairsNeeded = totalCards / 2;

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;

        if (autoFitter) autoFitter.SetGrid(cols, rows);

        if (shuffleNewDeck)
        {
            var chosenFronts = new List<Sprite>(totalPairsNeeded);
            for (int i = 0; i < totalPairsNeeded; i++)
                chosenFronts.Add(sprites[i < sprites.Length ? i : i % sprites.Length]);

            var spritePairs = new List<Sprite>(totalCards);
            for (int i = 0; i < chosenFronts.Count; i++) { var sp = chosenFronts[i]; spritePairs.Add(sp); spritePairs.Add(sp); }
            Shuffle(spritePairs);

            for (int i = 0; i < spritePairs.Count; i++)
            {
                var card = Instantiate(cardPrefab, gridTransform);
                card.SetIconSprite(spritePairs[i]);
                card.controller = this;
            }

            matchesCount = 0;
            turnsCount = 0;
        }

        gameOver = false;
        inputLocked = false;
        if (winText) winText.gameObject.SetActive(false);
        UpdateUI();

        if (enableInitialPreview && shuffleNewDeck)
            previewRoutine = StartCoroutine(InitialPreviewRoutine(boardVersion));
    }

    public void ClearBoard()
    {
        if (previewRoutine != null)
        {
            StopCoroutine(previewRoutine);
            previewRoutine = null;
        }

        StopAllCoroutines();

        if (gridTransform)
        {
            for (int i = gridTransform.childCount - 1; i >= 0; i--)
            {
                var t = gridTransform.GetChild(i);
                var c = t.GetComponent<Card>();
                if (c) c.StopAllCoroutines();
                Destroy(t.gameObject);
            }
        }

        pending.Clear();
        processingPairs = false;
        gameOver = false;
        inputLocked = false;

        if (winText) winText.gameObject.SetActive(false);
        UpdateUI();
    }

    IEnumerator InitialPreviewRoutine(int myVersion)
    {
        inputLocked = true;

        _tempCards.Clear();
        int n = gridTransform.childCount;
        for (int i = 0; i < n; i++)
        {
            var c = gridTransform.GetChild(i).GetComponent<Card>();
            if (c) _tempCards.Add(c);
        }

        if (previewStagger <= 0f)
        {
            for (int i = 0; i < _tempCards.Count; i++)
            {
                if (myVersion != boardVersion) yield break;
                var c = _tempCards[i];
                if (c) StartCoroutine(c.FlipToReveal());
            }
        }
        else
        {
            for (int i = 0; i < _tempCards.Count; i++)
            {
                if (myVersion != boardVersion) yield break;
                var c = _tempCards[i];
                if (c) StartCoroutine(c.FlipToReveal());
                yield return new WaitForSeconds(previewStagger);
            }
        }

        float elapsed = 0f;
        while (elapsed < previewDuration)
        {
            if (myVersion != boardVersion) yield break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < _tempCards.Count; i++)
        {
            if (myVersion != boardVersion) yield break;
            var c = _tempCards[i];
            if (c) StartCoroutine(c.FlipToHide());
        }

        float settle = 0.35f;
        elapsed = 0f;
        while (elapsed < settle)
        {
            if (myVersion != boardVersion) yield break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (myVersion == boardVersion) inputLocked = false;
    }

    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int r = Random.Range(0, i + 1);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }

    public void SetSelected(Card card)
    {
        if (gameOver || inputLocked) return;
        if (card.isSelected || card.IsAnimating || card.isMatched) return;
        StartCoroutine(RevealAndEnqueue(card));
    }

    IEnumerator RevealAndEnqueue(Card card)
    {
        yield return card.FlipToReveal();
        pending.Add(card);
        if (!processingPairs && pending.Count >= 2)
            StartCoroutine(ProcessPairs());
    }

    IEnumerator ProcessPairs()
    {
        processingPairs = true;
        while (pending.Count >= 2)
        {
            var a = pending[0];
            var b = pending[1];

            yield return new WaitForSeconds(0.15f);
            turnsCount++;

            if (a.iconSprite == b.iconSprite)
            {
                a.isMatched = true;
                b.isMatched = true;
                PlayMatchSfx();

                matchesCount++;
                score += 2;

                StartCoroutine(Pop(a));
                StartCoroutine(Pop(b));

                if (matchesCount >= totalPairsNeeded)
                {
                    gameOver = true;
                    StartCoroutine(ShowWin());
                }
            }
            else
            {
                PlayWrongSfx();
                yield return a.FlipToHide();
                yield return b.FlipToHide();
            }

            UpdateUI();
            SaveGame();

            pending.RemoveAt(0);
            pending.RemoveAt(0);
        }
        processingPairs = false;
    }

    IEnumerator Pop(Card c)
    {
        var rt = c ? c.GetComponent<RectTransform>() : null;
        if (!rt) yield break;

        Vector3 a = Vector3.one, b = a * 1.08f;
        float d = 0.12f;

        for (float t = 0f; t < d; t += Time.deltaTime)
        {
            rt.localScale = Vector3.Lerp(a, b, Mathf.SmoothStep(0, 1, t / d));
            yield return null;
        }
        for (float t = 0f; t < d; t += Time.deltaTime)
        {
            rt.localScale = Vector3.Lerp(b, a, Mathf.SmoothStep(0, 1, t / d));
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    IEnumerator ShowWin()
    {
        yield return new WaitForSeconds(winShowDelay);
        PlayVictorySfx();
        yield return StartCoroutine(ShowWinBannerRoutine());
        if (autoAdvanceOnWin)
        {
            yield return new WaitForSeconds(nextLevelDelay);
            HideWinBannerInstant();
            GoToNextLevel();
        }
    }

    IEnumerator ShowWinBannerRoutine()
    {
        if (!winBanner || !winBannerGroup || !winBannerText) yield break;

        string label = (currentLevelIndex >= 0 && currentLevelIndex < levels.Length)
            ? $"Level {currentLevelIndex + 1} Completed"
            : "Level Completed";
        winBannerText.text = label;

        winBanner.SetActive(true);
        winBannerGroup.alpha = 0f;

        var rt = winBanner.transform as RectTransform;
        Vector3 start = Vector3.one * Mathf.Clamp01(1f - winBannerScalePop);
        Vector3 end = Vector3.one;
        if (rt) rt.localScale = start;

        float t = 0f;
        while (t < winBannerFadeIn)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / winBannerFadeIn);
            winBannerGroup.alpha = k;
            if (rt) rt.localScale = Vector3.Lerp(start, end, Mathf.SmoothStep(0, 1, k));
            yield return null;
        }
        winBannerGroup.alpha = 1f;
        if (rt) rt.localScale = end;

        PlayVictoryVfx();
    }

    void HideWinBannerInstant()
    {
        if (!winBanner || !winBannerGroup) return;
        winBannerGroup.alpha = 0f;
        winBanner.SetActive(false);
    }

    void UpdateUI()
    {
        if (matchesText) matchesText.text = matchesCount.ToString();
        if (turnsText) turnsText.text = turnsCount.ToString();
        if (scoreText) scoreText.text = score.ToString();
    }

    public void PlayFlipSfx()
    {
        if (!sfxSource || !flipSfx) return;
        sfxSource.pitch = Random.Range(flipPitchRange.x, flipPitchRange.y);
        sfxSource.PlayOneShot(flipSfx, sfxVolume);
        sfxSource.pitch = 1f;
    }

    public void PlayMatchSfx()
    {
        if (!sfxSource || !matchSfx) return;
        sfxSource.pitch = 1f;
        sfxSource.PlayOneShot(matchSfx, sfxVolume);
    }

    public void PlayWrongSfx()
    {
        if (!sfxSource || !wrongSfx) return;
        sfxSource.pitch = 1f;
        sfxSource.PlayOneShot(wrongSfx, sfxVolume);
    }

    public void PlayVictorySfx()
    {
        if (sfxSource && victorySfx)
        {
            sfxSource.pitch = 1f;
            sfxSource.PlayOneShot(victorySfx, sfxVolume);
        }
        PlayVictoryVfx();
    }

    private void PlayVictoryVfx()
    {
        if (!victoryVfxPrefab) return;

        Transform parent = victoryVfxAnchor ? victoryVfxAnchor : (winBanner ? winBanner.transform : transform);
        ParticleSystem ps = Instantiate(victoryVfxPrefab, parent);

        var psRt = ps.GetComponent<RectTransform>();
        if (psRt != null)
        {
            psRt.anchoredPosition = Vector2.zero;
            psRt.localScale = Vector3.one;
            psRt.localRotation = Quaternion.identity;
        }
        else
        {
            ps.transform.position = parent.position;
            ps.transform.localRotation = Quaternion.identity;
            ps.transform.localScale = Vector3.one;
        }

        ps.Play();

        var main = ps.main;
        if (main.stopAction != ParticleSystemStopAction.Destroy)
        {
            float life = main.duration;
            if (main.startLifetime.mode == ParticleSystemCurveMode.Constant)
                life += main.startLifetime.constant;
            else if (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants)
                life += main.startLifetime.constantMax;

            Destroy(ps.gameObject, life + 0.25f);
        }
    }

    void OnApplicationPause(bool paused) { if (paused) SaveGame(); }
    void OnApplicationQuit() { SaveGame(); }

    private void BuildSpriteIndexMap()
    {
        if (sprites == null) { spriteIndexMap = new Dictionary<Sprite, int>(0); return; }
        spriteIndexMap = new Dictionary<Sprite, int>(sprites.Length);
        for (int i = 0; i < sprites.Length; i++)
        {
            var s = sprites[i];
            if (s) spriteIndexMap[s] = i;
        }
    }

    private void SaveGame()
    {
        if (!gridTransform) return;
        int n = gridTransform.childCount;
        if (n == 0) return;

        var deck = new int[n];
        var matched = new bool[n];

        for (int i = 0; i < n; i++)
        {
            var c = gridTransform.GetChild(i).GetComponent<Card>();
            int idx = 0;
            if (c && c.iconSprite && spriteIndexMap != null && spriteIndexMap.TryGetValue(c.iconSprite, out var si))
                idx = si;
            deck[i] = idx;
            matched[i] = (c && c.isMatched);
        }

        var data = new SaveData
        {
            version = 1,
            currentLevelIndex = Mathf.Clamp(currentLevelIndex, 0, Mathf.Max(0, levels.Length - 1)),
            rows = lastRows,
            cols = lastCols,
            matchesCount = matchesCount,
            turnsCount = turnsCount,
            score = score,
            deckSpriteIndices = deck,
            matchedFlags = matched
        };

        var json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    private bool LoadGame()
    {
        if (!PlayerPrefs.HasKey(SAVE_KEY)) return false;

        var json = PlayerPrefs.GetString(SAVE_KEY);
        if (string.IsNullOrEmpty(json)) return false;

        var data = JsonUtility.FromJson<SaveData>(json);
        if (data == null) return false;

        currentLevelIndex = Mathf.Clamp(data.currentLevelIndex, 0, Mathf.Max(0, levels.Length - 1));
        lastRows = data.rows; lastCols = data.cols;
        matchesCount = data.matchesCount;
        turnsCount = data.turnsCount;
        score = data.score;

        BuildBoard(lastRows, lastCols, false);

        int need = lastRows * lastCols;
        if (data.deckSpriteIndices == null || data.deckSpriteIndices.Length != need)
        {
            Debug.LogWarning("Save data deck size mismatch. Starting new level.");
            BuildCurrentLevel();
            return true;
        }

        for (int i = gridTransform.childCount - 1; i >= 0; i--)
        {
            var t = gridTransform.GetChild(i);
            var c = t.GetComponent<Card>(); if (c) c.StopAllCoroutines();
            Destroy(t.gameObject);
        }

        for (int i = 0; i < data.deckSpriteIndices.Length; i++)
        {
            int spriteIdx = Mathf.Clamp(data.deckSpriteIndices[i], 0, Mathf.Max(0, sprites.Length - 1));
            var card = Instantiate(cardPrefab, gridTransform);
            card.SetIconSprite(sprites[spriteIdx]);
            card.controller = this;
        }

        if (data.matchedFlags != null)
        {
            int n = Mathf.Min(data.matchedFlags.Length, gridTransform.childCount);
            for (int i = 0; i < n; i++)
            {
                if (!data.matchedFlags[i]) continue;
                var c = gridTransform.GetChild(i).GetComponent<Card>();
                if (!c) continue;
                c.isMatched = true;
                StartCoroutine(c.FlipToReveal());
            }
        }

        inputLocked = false;
        gameOver = (matchesCount >= totalPairsNeeded);
        UpdateUI();
        if (winText) winText.gameObject.SetActive(false);
        return true;
    }

    public void ClearSave()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY);
        PlayerPrefs.Save();
    }
}
