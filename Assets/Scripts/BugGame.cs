using UnityEngine;
using UnityEngine.InputSystem;

// flies dart, ants patrol, and beetles scuttle away when you get close.
[RequireComponent(typeof(LizardTongue))]
public sealed class BugGame : MonoBehaviour
{
    private const int MaxLevel = 5;
    private const int FlowerPatchUnlock = 15;
    private const int PondEdgeUnlock = 35;
    private const float ComboSeconds = 4f;
    private static readonly Vector2[] FlowerSpots =
    {
        new Vector2(-0.27f, 0.40f), new Vector2(0.25f, 0.24f),
        new Vector2(-0.18f, -0.55f), new Vector2(0.42f, -0.62f)
    };
    private LizardTongue tongue;
    private LizardMovement lizard;
    private BackyardMap backyard;
    private GameObject fly;
    private Texture2D flyPixels;
    private Sprite flySprite;
    private GameObject ant;
    private SpriteRenderer antPicture;
    private Texture2D antPixels;
    private Sprite antSprite;
    private float antHeading = 1f;
    private float antRestUntil;
    private GameObject beetle;
    private Texture2D beetlePixels;
    private Sprite beetleSprite;
    private Vector2 beetleTarget;
    private float beetleRestUntil;
    private GameObject butterfly;
    private Texture2D butterflyPixels;
    private Sprite butterflySprite;
    private Vector2 butterflyTarget;
    private float butterflyRestUntil;
    private AudioSource sounds;
    private AudioClip flickSound;
    private AudioClip catchSound;
    private AudioClip upgradeSound;
    private AudioClip biteSound;
    private GUIStyle scoreStyle;
    private GUIStyle titleStyle;
    private GUIStyle playStyle;
    private GUIStyle rewardStyle;
    private int score;
    private int coins;
    private int tongueLevel = 1;
    private int speedLevel = 1;
    private int cooldownLevel = 1;
    private bool upgradesOpen;
    private bool titleOpen = true;
    private bool paused;
    private bool gameOver;
    private YardArea currentArea;
    private bool caughtThisFlick;
    private bool heardThisFlick;
    private Vector2 nextPerch;
    private float restUntil;
    private Vector2 rewardSpot;
    private int rewardAmount;
    private float rewardStarted;
    private int combo;
    private float lastCatchTime = float.NegativeInfinity;
    private float celebrationUntil;
    private string shopNotice;
    private float noticeUntil;
    private EnemyWaves waves;
    private int hearts = 3;
    private float safeUntil;
    private float hurtUntil;
    private GameObject berry;
    private Texture2D berryPixels;
    private Sprite berrySprite;
    private float nextBerryTime;

    private bool AllUpgradesMaxed => tongueLevel == MaxLevel && speedLevel == MaxLevel && cooldownLevel == MaxLevel;

    private void Start()
    {
        Time.timeScale = 0f;
        tongue = GetComponent<LizardTongue>();
        lizard = GetComponent<LizardMovement>();
        LoadProgress();
        sounds = gameObject.AddComponent<AudioSource>();
        sounds.playOnAwake = false;
        sounds.spatialBlend = 0f;
        flickSound = MakeSound("Tongue flick", 0.10f, 700f, 280f);
        catchSound = MakeSound("Bug caught", 0.18f, 420f, 820f);
        upgradeSound = MakeSound("Upgrade bought", 0.24f, 520f, 1040f);
        biteSound = MakeSound("Ant bite", 0.16f, 160f, 65f);
        backyard = GetComponent<BackyardMap>() ?? gameObject.AddComponent<BackyardMap>();
        flyPixels = DrawFly();
        flySprite = Sprite.Create(flyPixels, new Rect(0, 0, 24, 24), new Vector2(0.5f, 0.5f), 32);
        fly = new GameObject("Fly");
        SpriteRenderer picture = fly.AddComponent<SpriteRenderer>();
        picture.sprite = flySprite;
        picture.sortingOrder = 2;
        fly.transform.position = new Vector3(0f, -2.7f, 0f);
        nextPerch = NearbySpot(fly.transform.position);
        restUntil = Time.time + 0.8f;

        antPixels = DrawAnt();
        antSprite = Sprite.Create(antPixels, new Rect(0, 0, 24, 24), new Vector2(0.5f, 0.5f), 32);
        ant = new GameObject("Ant");
        antPicture = ant.AddComponent<SpriteRenderer>();
        antPicture.sprite = antSprite;
        antPicture.sortingOrder = 2;
        ant.transform.position = new Vector3(-2.8f, 1.8f, 0f);

        beetlePixels = DrawBeetle();
        beetleSprite = Sprite.Create(beetlePixels, new Rect(0, 0, 24, 24), new Vector2(0.5f, 0.5f), 32);
        beetle = new GameObject("Beetle");
        SpriteRenderer shell = beetle.AddComponent<SpriteRenderer>();
        shell.sprite = beetleSprite;
        shell.sortingOrder = 2;
        beetle.transform.position = new Vector3(2.7f, 1.5f, 0f);
        beetleTarget = NearbySpot(beetle.transform.position);
        beetleRestUntil = Time.time + 0.6f;
        waves = gameObject.AddComponent<EnemyWaves>();
        berryPixels = DrawBerry();
        berrySprite = Sprite.Create(berryPixels, new Rect(0, 0, 24, 24), new Vector2(0.5f, 0.5f), 32);
        berry = new GameObject("Healing berry");
        SpriteRenderer fruit = berry.AddComponent<SpriteRenderer>();
        fruit.sprite = berrySprite;
        fruit.sortingOrder = 2;
        berry.SetActive(false);
    }

    private void Update()
    {
        if (gameOver) return;
        Keyboard keys = Keyboard.current;
        if (titleOpen)
        {
            if (keys != null && (keys.enterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame))
                StartGame(YardArea.Backyard);
            return;
        }
        if (keys != null && keys.escapeKey.wasPressedThisFrame)
        {
            paused = !paused;
            Time.timeScale = paused ? 0f : 1f;
        }
        if (paused) return;

        if (keys != null)
        {
            if (keys.spaceKey.wasPressedThisFrame) lizard.TryDash();
            if (keys.eKey.wasPressedThisFrame) upgradesOpen = !upgradesOpen;
            if (upgradesOpen)
            {
                if (keys.digit1Key.wasPressedThisFrame) BuyTongueLength();
                if (keys.digit2Key.wasPressedThisFrame) BuyMovementSpeed();
                if (keys.digit3Key.wasPressedThisFrame) BuyTongueCooldown();
            }
        }

        MoveAnt();
        MoveBeetle();
        MoveButterfly();
        waves.Tick();
        UpdateBerry();
        if (fly == null || Time.time < restUntil) return;

        Vector2 here = fly.transform.position;
        if ((nextPerch - here).sqrMagnitude < 0.01f)
        {
            nextPerch = NearbySpot(here);
            restUntil = Time.time + Random.Range(0.2f, 0.6f);
            return;
        }

        fly.transform.position = Vector2.MoveTowards(here, nextPerch, 1.5f * Time.deltaTime);
    }

    private void MoveAnt()
    {
        if (ant == null || Time.time < antRestUntil) return;

        Camera view = Camera.main;
        float centerX = view != null ? view.transform.position.x : 0f;
        float edge = view != null ? view.orthographicSize * view.aspect - 0.7f : 6f;
        Vector2 here = ant.transform.position;
        float nextX = here.x + antHeading * 0.85f * Time.deltaTime;
        if (nextX < centerX - edge || nextX > centerX + edge ||
            backyard.IsBlocked(new Vector2(nextX, here.y), 0.25f))
        {
            antHeading *= -1f;
            antPicture.flipX = antHeading < 0f;
            antRestUntil = Time.time + 0.35f;
            return;
        }
        ant.transform.position = new Vector3(nextX, here.y, 0f);
    }

    private void MoveBeetle()
    {
        if (beetle == null || Time.time < beetleRestUntil) return;

        Vector2 here = beetle.transform.position;
        Vector2 away = here - (Vector2)transform.position;
        if (away.sqrMagnitude < 4f && away.sqrMagnitude > 0.01f)
        {
            Camera view = Camera.main;
            Vector2 center = view != null ? (Vector2)view.transform.position : Vector2.zero;
            float tall = view != null ? view.orthographicSize - 0.7f : 4f;
            float wide = view != null ? view.orthographicSize * view.aspect - 0.7f : 6f;
            Vector2 next = here + away.normalized * (1.6f * Time.deltaTime);
            next.x = Mathf.Clamp(next.x, center.x - wide, center.x + wide);
            next.y = Mathf.Clamp(next.y, center.y - tall, center.y + tall);
            if (backyard.IsBlocked(next, 0.25f))
            {
                Vector2 side = new Vector2(-away.y, away.x).normalized;
                next = here + side * (1.6f * Time.deltaTime);
                if (backyard.IsBlocked(next, 0.25f)) return;
            }
            beetle.transform.position = new Vector3(next.x, next.y, 0f);
            return;
        }

        if ((beetleTarget - here).sqrMagnitude < 0.01f)
        {
            beetleTarget = NearbySpot(here);
            beetleRestUntil = Time.time + Random.Range(0.5f, 1f);
            return;
        }
        Vector2 step = Vector2.MoveTowards(here, beetleTarget, 0.65f * Time.deltaTime);
        if (backyard.IsBlocked(step, 0.25f))
        {
            beetleTarget = NearbySpot(here);
            beetleRestUntil = Time.time + 0.3f;
            return;
        }
        beetle.transform.position = step;
    }

    private void MoveButterfly()
    {
        if (butterfly == null || !butterfly.activeSelf) return;
        Vector2 here = butterfly.transform.position;
        float flap = Mathf.Abs(Mathf.Sin(Time.time * 12f));
        butterfly.transform.localScale = new Vector3(0.65f + flap * 0.35f, 1f, 1f);
        if (Time.time < butterflyRestUntil) return;
        if ((butterflyTarget - here).sqrMagnitude < 0.05f)
        {
            butterflyTarget = FlowerSpot(here);
            butterflyRestUntil = Time.time + Random.Range(0.35f, 0.8f);
            return;
        }
        Vector2 next = Vector2.MoveTowards(here, butterflyTarget, 1.1f * Time.deltaTime);
        if (backyard.IsBlocked(next, 0.22f)) butterflyTarget = FlowerSpot(here);
        else butterfly.transform.position = next;
    }

    private void UpdateBerry()
    {
        if (hearts >= 3)
        {
            berry.SetActive(false);
            return;
        }
        if (!berry.activeSelf && Time.time >= nextBerryTime)
        {
            Vector2 spot = NewBugSpot();
            // try again shortly if the yard didn't have a clear spot.
            if (backyard.IsBlocked(spot, 0.35f) || Vector2.Distance(spot, transform.position) < 1.5f)
            {
                nextBerryTime = Time.time + 1f;
                return;
            }
            berry.transform.position = spot;
            berry.SetActive(true);
        }
        if (!berry.activeSelf) return;
        berry.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 5f) * 0.08f);
        if (Vector2.Distance(berry.transform.position, transform.position) > 0.55f) return;
        hearts++;
        berry.SetActive(false);
        nextBerryTime = Time.time + 12f;
        sounds.PlayOneShot(upgradeSound);
        shopNotice = "Berry snack! +1 heart";
        noticeUntil = Time.time + 2f;
    }

    private void CheckBite()
    {
        if (lizard.IsDashing || Time.time < safeUntil || !waves.TryBite()) return;
        hearts--;
        sounds.PlayOneShot(biteSound);
        hurtUntil = Time.time + 0.2f;
        safeUntil = Time.time + 1.5f;
        combo = 0;
        lastCatchTime = float.NegativeInfinity;
        shopNotice = "Ouch! Catch the enemy bugs!";
        noticeUntil = Time.time + 1.5f;
        if (hearts > 0) return;
        // the third bite ends the run until the player retries.
        gameOver = true;
        upgradesOpen = false;
        tongue.enabled = false;
        lizard.ResetDash();
        GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        Time.timeScale = 0f;
    }

    private void LateUpdate()
    {
        if (paused || titleOpen || gameOver) return;
        if (!tongue.IsOut)
        {
            caughtThisFlick = false;
            heardThisFlick = false;
            CheckBite();
            return;
        }
        if (!heardThisFlick)
        {
            sounds.PlayOneShot(flickSound);
            heardThisFlick = true;
        }
        if (caughtThisFlick) { CheckBite(); return; }

        Vector2 start = tongue.StartPoint;
        Vector2 span = tongue.TipPoint - start;
        if (span.sqrMagnitude < 0.001f) { CheckBite(); return; }

        bool caughtFly = TongueTouches(fly, start, span);
        bool caughtAnt = !caughtFly && TongueTouches(ant, start, span);
        bool caughtBeetle = !caughtFly && !caughtAnt && TongueTouches(beetle, start, span);
        bool caughtButterfly = !caughtFly && !caughtAnt && !caughtBeetle && TongueTouches(butterfly, start, span);
        Vector2 enemySpot = Vector2.zero;
        int enemyCoins = 0;
        bool caughtEnemy = !caughtFly && !caughtAnt && !caughtBeetle && !caughtButterfly
            && waves.TryCatch(start, span, out enemySpot, out enemyCoins);
        if (!caughtFly && !caughtAnt && !caughtBeetle && !caughtButterfly && !caughtEnemy) { CheckBite(); return; }
        if (caughtEnemy && enemyCoins == 0)
        {
            // one tongue flick gets one boss hit, even when it stays extended.
            caughtThisFlick = true;
            sounds.PlayOneShot(catchSound, 0.5f);
            shopNotice = waves.HitNotice;
            noticeUntil = Time.time + 1.5f;
            CheckBite();
            return;
        }

        GameObject caught = caughtFly ? fly : caughtAnt ? ant : caughtBeetle ? beetle : butterfly;
        rewardSpot = caughtEnemy ? enemySpot : (Vector2)caught.transform.position;
        combo = Time.time - lastCatchTime <= ComboSeconds ? combo + 1 : 1;
        lastCatchTime = Time.time;
        rewardAmount = (caughtEnemy ? enemyCoins : caughtButterfly ? 4 : caughtBeetle ? 3 : caughtAnt ? 2 : 1)
            + (combo >= 3 ? 1 : 0);
        rewardStarted = Time.time;
        score++;
        coins += rewardAmount;
        SaveProgress();
        sounds.PlayOneShot(catchSound);
        caughtThisFlick = true;
        if (waves.UpgradeBreak)
        {
            upgradesOpen = true;
            sounds.PlayOneShot(upgradeSound);
        }
        if (caughtFly) MoveFly();
        else if (caughtAnt) RespawnAnt();
        else if (caughtBeetle) RespawnBeetle();
        else if (caughtButterfly) RespawnButterfly();
        CheckBite();
    }

    private static bool TongueTouches(GameObject bug, Vector2 start, Vector2 span)
    {
        if (bug == null || !bug.activeInHierarchy) return false;
        Vector2 bugSpot = bug.transform.position;
        float along = Mathf.Clamp01(Vector2.Dot(bugSpot - start, span) / span.sqrMagnitude);
        Vector2 closest = start + span * along;
        return (bugSpot - closest).sqrMagnitude <= 0.12f;
    }

    private void BuyTongueLength()
    {
        if (!SpendOn(tongueLevel)) return;
        tongueLevel++;
        tongue.Reach += 0.5f;
        SaveProgress();
        CelebrateMaxed();
    }

    private void BuyMovementSpeed()
    {
        if (!SpendOn(speedLevel)) return;
        speedLevel++;
        lizard.MoveSpeed += 0.5f;
        SaveProgress();
        CelebrateMaxed();
    }

    private void BuyTongueCooldown()
    {
        if (!SpendOn(cooldownLevel)) return;
        cooldownLevel++;
        tongue.CooldownSeconds -= 0.04f;
        SaveProgress();
        CelebrateMaxed();
    }

    private void CelebrateMaxed()
    {
        if (AllUpgradesMaxed) celebrationUntil = Time.time + 4f;
    }

    private bool SpendOn(int level)
    {
        noticeUntil = Time.time + 2f;
        if (level >= MaxLevel)
        {
            shopNotice = "Already maxed out!";
            return false;
        }
        if (coins < level + 2)
        {
            shopNotice = $"Need {level + 2 - coins} more coins";
            return false;
        }
        coins -= level + 2;
        shopNotice = "Upgrade bought!";
        sounds.PlayOneShot(upgradeSound);
        return true;
    }

    private void LoadProgress()
    {
        score = Mathf.Max(0, PlayerPrefs.GetInt("LizardLunch.BugsCaught"));
        coins = Mathf.Max(0, PlayerPrefs.GetInt("LizardLunch.Coins"));
        tongueLevel = Mathf.Clamp(PlayerPrefs.GetInt("LizardLunch.TongueLevel", 1), 1, MaxLevel);
        speedLevel = Mathf.Clamp(PlayerPrefs.GetInt("LizardLunch.SpeedLevel", 1), 1, MaxLevel);
        cooldownLevel = Mathf.Clamp(PlayerPrefs.GetInt("LizardLunch.CooldownLevel", 1), 1, MaxLevel);

        tongue.Reach += (tongueLevel - 1) * 0.5f;
        lizard.MoveSpeed += (speedLevel - 1) * 0.5f;
        tongue.CooldownSeconds -= (cooldownLevel - 1) * 0.04f;
    }

    private void SaveProgress()
    {
        PlayerPrefs.SetInt("LizardLunch.BugsCaught", score);
        PlayerPrefs.SetInt("LizardLunch.Coins", coins);
        PlayerPrefs.SetInt("LizardLunch.TongueLevel", tongueLevel);
        PlayerPrefs.SetInt("LizardLunch.SpeedLevel", speedLevel);
        PlayerPrefs.SetInt("LizardLunch.CooldownLevel", cooldownLevel);
        PlayerPrefs.Save();
    }

    private void MoveFly()
    {
        Vector2 next = NewBugSpot();
        fly.transform.position = new Vector3(next.x, next.y, 0f);
        nextPerch = NearbySpot(next);
        restUntil = Time.time + 0.5f;
    }

    private void RespawnAnt()
    {
        Vector2 next = NewBugSpot();
        ant.transform.position = new Vector3(next.x, next.y, 0f);
        antHeading = Random.value < 0.5f ? -1f : 1f;
        antPicture.flipX = antHeading < 0f;
        antRestUntil = Time.time + 0.5f;
    }

    private void RespawnBeetle()
    {
        Vector2 next = NewBugSpot();
        beetle.transform.position = new Vector3(next.x, next.y, 0f);
        beetleTarget = NearbySpot(next);
        beetleRestUntil = Time.time + 0.7f;
    }

    private void RespawnButterfly()
    {
        Vector2 next = FlowerSpot(transform.position);
        butterfly.transform.position = next;
        butterflyTarget = FlowerSpot(next);
        butterflyRestUntil = Time.time + 0.4f;
    }

    private Vector2 FlowerSpot(Vector2 awayFrom)
    {
        Camera view = Camera.main;
        Vector2 center = view != null ? (Vector2)view.transform.position : Vector2.zero;
        float tall = view != null ? view.orthographicSize : 5f;
        float wide = view != null ? tall * view.aspect : 8f;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 flower = FlowerSpots[Random.Range(0, FlowerSpots.Length)];
            Vector2 spot = center + new Vector2(flower.x * wide, flower.y * tall);
            if ((spot - awayFrom).sqrMagnitude > 1f && !backyard.IsBlocked(spot, 0.25f))
                return spot;
        }
        return NewBugSpot();
    }

    private Vector2 NewBugSpot()
    {
        Camera view = Camera.main;
        float tall = view != null ? view.orthographicSize - 0.7f : 4f;
        float wide = view != null ? view.orthographicSize * view.aspect - 0.7f : 6f;
        Vector2 center = view != null ? (Vector2)view.transform.position : Vector2.zero;
        Vector2 next = center;

        for (int tryNumber = 0; tryNumber < 20; tryNumber++)
        {
            next = center + new Vector2(Random.Range(-wide, wide), Random.Range(-tall, tall));
            if (Vector2.Distance(next, transform.position) > 1.5f &&
                !backyard.IsBlocked(next, 0.35f)) break;
        }

        return next;
    }

    private Vector2 NearbySpot(Vector2 here)
    {
        Camera view = Camera.main;
        Vector2 center = view != null ? (Vector2)view.transform.position : Vector2.zero;
        float tall = view != null ? view.orthographicSize - 0.7f : 4f;
        float wide = view != null ? view.orthographicSize * view.aspect - 0.7f : 6f;

        for (int tryNumber = 0; tryNumber < 12; tryNumber++)
        {
            Vector2 wander = Random.insideUnitCircle.normalized * Random.Range(0.9f, 2.1f);
            Vector2 spot = here + wander;
            spot.x = Mathf.Clamp(spot.x, center.x - wide, center.x + wide);
            spot.y = Mathf.Clamp(spot.y, center.y - tall, center.y + tall);
            if ((spot - here).sqrMagnitude > 0.36f && !backyard.IsBlocked(spot, 0.35f))
                return spot;
        }
        return here;
    }

    private void OnGUI()
    {
        if (scoreStyle == null)
        {
            scoreStyle = new GUIStyle(GUI.skin.label);
            scoreStyle.fontSize = 28;
            scoreStyle.fontStyle = FontStyle.Bold;
            scoreStyle.normal.textColor = Color.white;
            titleStyle = new GUIStyle(scoreStyle) { fontSize = 38, alignment = TextAnchor.MiddleCenter };
            playStyle = new GUIStyle(GUI.skin.button) { fontSize = 22 };
            rewardStyle = new GUIStyle(scoreStyle) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            rewardStyle.normal.textColor = new Color(1f, 0.88f, 0.25f);
        }
        if (gameOver)
        {
            float x = Screen.width * 0.5f - 210f;
            float y = Screen.height * 0.5f - 130f;
            GUI.Box(new Rect(x, y, 420, 260), "");
            GUI.Label(new Rect(x + 20, y + 20, 380, 55), "Game over", titleStyle);
            GUI.Label(new Rect(x + 20, y + 85, 380, 40), "Hearts: 0/3", rewardStyle);
            GUI.Label(new Rect(x + 20, y + 125, 380, 35), $"You reached wave {waves.Wave}.", rewardStyle);
            if (GUI.Button(new Rect(x + 70, y + 185, 280, 48), "Try again", playStyle))
                StartGame(currentArea);
            return;
        }
        if (titleOpen)
        {
            float x = Screen.width * 0.5f - 210f;
            float y = Screen.height * 0.5f - 200f;
            GUI.Box(new Rect(x, y, 420, 400), "");
            GUI.Label(new Rect(x + 20, y + 18, 380, 55), "Lizard Lunch", titleStyle);
            GUI.Label(new Rect(x + 82, y + 76, 300, 25), $"Bugs caught: {score}    Coins: {coins}");
            GUI.Label(new Rect(x + 82, y + 105, 300, 25), "Move: WASD / arrows    Tongue: click");
            GUI.Label(new Rect(x + 62, y + 134, 340, 25), "Space: dash    E: upgrades    Esc: pause");
            if (GUI.Button(new Rect(x + 50, y + 172, 320, 44), "Play Backyard", playStyle)) StartGame(YardArea.Backyard);
            GUI.enabled = score >= FlowerPatchUnlock;
            if (GUI.Button(new Rect(x + 50, y + 224, 320, 44), "Play Flower Patch", playStyle)) StartGame(YardArea.FlowerPatch);
            GUI.enabled = score >= PondEdgeUnlock;
            if (GUI.Button(new Rect(x + 50, y + 276, 320, 44), "Play Pond Edge", playStyle)) StartGame(YardArea.PondEdge);
            GUI.enabled = true;
            string areaHint = score < FlowerPatchUnlock
                ? $"Flower patch: {score}/{FlowerPatchUnlock} bugs"
                : score < PondEdgeUnlock ? $"Pond edge: {score}/{PondEdgeUnlock} bugs"
                : "All areas unlocked!";
            if (AllUpgradesMaxed) areaHint = "All upgrades maxed!";
            GUI.Label(new Rect(x + 80, y + 342, 300, 32), areaHint,
                AllUpgradesMaxed ? rewardStyle : GUI.skin.label);
            return;
        }
        if (paused)
        {
            float middleX = Screen.width * 0.5f;
            float middleY = Screen.height * 0.5f;
            GUI.Box(new Rect(middleX - 180, middleY - 105, 360, 210), "");
            GUI.Label(new Rect(middleX - 80, middleY - 85, 200, 45), "Paused", scoreStyle);
            GUI.Label(new Rect(middleX - 110, middleY - 30, 250, 45), "Esc: resume", scoreStyle);
            if (GUI.Button(new Rect(middleX - 120, middleY + 32, 240, 48), "Main menu", playStyle))
            {
                paused = false;
                titleOpen = true;
                upgradesOpen = false;
            }
            return;
        }
        if (Time.time < hurtUntil)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 0.12f, 0.08f, (hurtUntil - Time.time) * 0.8f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }
        GUI.Label(new Rect(20, 16, 400, 40), $"Bugs: {score}    Coins: {coins}", scoreStyle);
        GUI.Label(new Rect(Screen.width - 190, 16, 180, 40), $"Hearts: {hearts}/3", rewardStyle);
        if (Time.time < safeUntil)
            GUI.Label(new Rect(Screen.width - 190, 52, 180, 30), "Recovering...", rewardStyle);
        else if (hearts < 3)
            GUI.Label(new Rect(Screen.width - 240, 52, 230, 30), "Berries heal you", rewardStyle);
        GUI.Label(new Rect(20, 52, 400, 40), upgradesOpen ? "E: close upgrades" : "E: upgrades", scoreStyle);
        string waveLine = waves.UpgradeBreak ? "Boss beaten! Spend your bonus coins."
            : waves.BossAlive ? $"Wave {waves.Wave} • {waves.BossName}: {waves.BossHealth}/{waves.BossMaxHealth}"
            : waves.BreakLeft > 0f
            ? $"Wave {waves.Wave} cleared! Next in {Mathf.CeilToInt(waves.BreakLeft)}s"
            : waves.IsBossWave ? $"Wave {waves.Wave} • {waves.BossName} incoming!"
            : $"Wave {waves.Wave} • {waves.EnemyName}: {waves.Remaining} left";
        GUI.Label(new Rect(Screen.width * 0.5f - 240, Screen.height - 155, 480, 35), waveLine, rewardStyle);
        if (waves.BossAlive)
        {
            GUI.Label(new Rect(Screen.width * 0.5f - 280, Screen.height - 220, 560, 35), waves.BossHint, rewardStyle);
            DrawMeter(new Rect(Screen.width * 0.5f - 160, Screen.height - 175, 320, 10),
                waves.BossHealth / (float)waves.BossMaxHealth, new Color(0.95f, 0.23f, 0.12f));
        }
        if (waves.UpgradeBreak && GUI.Button(new Rect(Screen.width * 0.5f - 110, Screen.height - 108, 220, 44),
            "Next wave", playStyle))
        {
            waves.ContinueAfterBoss();
            upgradesOpen = false;
        }
        float ready = tongue.Readiness;
        GUI.Label(new Rect(20, Screen.height - 68, 300, 30),
            ready >= 1f ? "Tongue ready • click" : tongue.IsOut ? "Catch it!" : "Getting ready...", rewardStyle);
        DrawMeter(new Rect(24, Screen.height - 30, 280, 10), ready, new Color(1f, 0.49f, 0.65f));
        float dashReady = lizard.DashReadiness;
        GUI.Label(new Rect(Screen.width - 300, Screen.height - 68, 280, 30),
            lizard.IsDashing ? "Dodging!" : dashReady >= 1f ? "Dash ready • Space" : "Dash recharging...", rewardStyle);
        DrawMeter(new Rect(Screen.width - 304, Screen.height - 30, 280, 10), dashReady,
            new Color(0.4f, 0.95f, 1f));
        if (Time.time < noticeUntil && !waves.UpgradeBreak)
            GUI.Label(new Rect(Screen.width * 0.5f - 180, Screen.height - 108, 360, 35), shopNotice, rewardStyle);
        float comboLeft = ComboSeconds - (Time.time - lastCatchTime);
        if (combo >= 2 && comboLeft > 0f)
        {
            string bonus = combo >= 3 ? "  +1 coin per catch" : "";
            GUI.Label(new Rect(20, upgradesOpen ? 196 : 88, 540, 40),
                $"Combo x{combo}  {comboLeft:0.0}s{bonus}", scoreStyle);
            DrawMeter(new Rect(24, upgradesOpen ? 236 : 128, 240, 6), comboLeft / ComboSeconds,
                new Color(1f, 0.88f, 0.25f));
        }
        if (rewardAmount > 0 && Time.time - rewardStarted < 0.9f)
        {
            Camera view = Camera.main;
            if (view != null)
            {
                Vector3 screen = view.WorldToScreenPoint(rewardSpot);
                float fade = 1f - (Time.time - rewardStarted) / 0.9f;
                Color oldColor = GUI.color;
                // a little burst stays at the catch spot after the bug respawns.
                for (int spark = 0; spark < 10; spark++)
                {
                    float angle = spark * Mathf.PI * 0.2f;
                    float spread = (1f - fade) * (spark % 2 == 0 ? 70f : 45f);
                    float size = 3f + fade * 4f;
                    GUI.color = spark % 2 == 0 ? new Color(1f, 0.85f, 0.2f, fade)
                        : new Color(0.7f, 1f, 0.45f, fade);
                    GUI.DrawTexture(new Rect(screen.x + Mathf.Cos(angle) * spread - size * 0.5f,
                        Screen.height - screen.y + Mathf.Sin(angle) * spread - size * 0.5f, size, size),
                        Texture2D.whiteTexture);
                }
                GUI.color = new Color(1f, 1f, 1f, fade);
                GUI.Label(new Rect(screen.x - 65f, Screen.height - screen.y - 40f * (1f - fade), 130f, 35f),
                    $"+{rewardAmount} coins", rewardStyle);
                GUI.color = oldColor;
            }
        }
        if (Time.time < celebrationUntil)
        {
            float x = Screen.width * 0.5f - 220f;
            float y = Screen.height - 270f;
            GUI.Box(new Rect(x, y, 440, 110), "");
            GUI.Label(new Rect(x + 10, y + 12, 420, 45), "Master Lizard!", titleStyle);
            GUI.Label(new Rect(x + 10, y + 58, 420, 35), "All upgrades maxed. Keep catching bugs!", rewardStyle);
        }
        if (!upgradesOpen) return;

        GUI.Label(new Rect(20, 88, 720, 40), UpgradeLine(1, "Longer tongue", tongueLevel), scoreStyle);
        GUI.Label(new Rect(20, 124, 720, 40), UpgradeLine(2, "Faster feet", speedLevel), scoreStyle);
        GUI.Label(new Rect(20, 160, 720, 40), UpgradeLine(3, "Shorter cooldown", cooldownLevel), scoreStyle);
    }

    private static string UpgradeLine(int key, string name, int level) => level >= MaxLevel
        ? $"{key}: {name} (max)" : $"{key}: {name} - {level + 2} coins (Lv {level})";

    private static void DrawMeter(Rect box, float fill, Color color)
    {
        Color oldColor = GUI.color;
        GUI.color = new Color(0.08f, 0.12f, 0.09f, 0.85f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        box.width *= Mathf.Clamp01(fill);
        GUI.color = color;
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private void StartGame(YardArea area)
    {
        noticeUntil = 0f;
        rewardAmount = 0;
        combo = 0;
        lastCatchTime = float.NegativeInfinity;
        bool changedArea = backyard.SetArea(area);
        bool newRun = changedArea || gameOver || waves.Wave == 0;
        if (newRun)
        {
            lizard.ResetDash();
            berry.SetActive(false);
            Camera view = Camera.main;
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            body.position = view != null ? (Vector2)view.transform.position : Vector2.zero;
            body.linearVelocity = Vector2.zero;
            MoveFly();
            RespawnAnt();
            RespawnBeetle();
            waves.Restart(area);
            hearts = 3;
            nextBerryTime = 0f;
        }
        if (gameOver)
        {
            hurtUntil = 0f;
            caughtThisFlick = false;
            heardThisFlick = false;
            tongue.enabled = true;
            gameOver = false;
        }
        currentArea = area;
        safeUntil = Time.time + 1.5f;
        bool useFlowers = area == YardArea.FlowerPatch;
        if (useFlowers && butterfly == null)
        {
            butterflyPixels = DrawButterfly();
            butterflySprite = Sprite.Create(butterflyPixels, new Rect(0, 0, 24, 24), new Vector2(0.5f, 0.5f), 32);
            butterfly = new GameObject("Butterfly");
            SpriteRenderer wings = butterfly.AddComponent<SpriteRenderer>();
            wings.sprite = butterflySprite;
            wings.sortingOrder = 2;
        }
        if (butterfly != null)
        {
            butterfly.SetActive(useFlowers);
            if (useFlowers) RespawnButterfly();
        }
        titleOpen = false;
        Time.timeScale = 1f;
    }

    private static AudioClip MakeSound(string name, float seconds, float firstNote, float lastNote)
    {
        const int sampleRate = 22050;
        int count = Mathf.RoundToInt(seconds * sampleRate);
        float[] notes = new float[count];
        float wave = 0f;
        for (int i = 0; i < count; i++)
        {
            float part = i / (float)count;
            wave += 2f * Mathf.PI * Mathf.Lerp(firstNote, lastNote, part) / sampleRate;
            float fade = Mathf.Min(1f, part * 20f) * (1f - part);
            notes[i] = Mathf.Sin(wave) * fade * 0.25f;
        }
        AudioClip clip = AudioClip.Create(name, count, 1, sampleRate, false);
        clip.SetData(notes, 0);
        return clip;
    }

    private static Texture2D DrawFly()
    {
        Texture2D dots = new Texture2D(24, 24, TextureFormat.RGBA32, false);
        dots.filterMode = FilterMode.Point;
        Color[] colors = new Color[24 * 24];
        for (int y = 0; y < 24; y++)
        for (int x = 0; x < 24; x++)
        {
            float wingLeft = Mathf.Pow((x - 7f) / 5f, 2) + Mathf.Pow((y - 16f) / 5f, 2);
            float wingRight = Mathf.Pow((x - 16f) / 5f, 2) + Mathf.Pow((y - 16f) / 5f, 2);
            float body = Mathf.Pow((x - 11.5f) / 3.5f, 2) + Mathf.Pow((y - 10f) / 6f, 2);
            colors[y * 24 + x] = body < 1f ? new Color(0.13f, 0.10f, 0.18f)
                : wingLeft < 1f || wingRight < 1f ? new Color(0.72f, 0.91f, 0.89f, 0.9f)
                : Color.clear;
            if ((x == 10 || x == 13) && y == 14) colors[y * 24 + x] = new Color(1f, 0.78f, 0.20f);
        }
        dots.SetPixels(colors);
        dots.Apply();
        return dots;
    }

    private static Texture2D DrawBerry()
    {
        Texture2D dots = new Texture2D(24, 24, TextureFormat.RGBA32, false);
        dots.filterMode = FilterMode.Point;
        Color[] colors = new Color[24 * 24];
        for (int y = 0; y < 24; y++)
        for (int x = 0; x < 24; x++)
        {
            float round = Mathf.Pow((x - 11.5f) / 7f, 2) + Mathf.Pow((y - 10f) / 7f, 2);
            bool leaf = y >= 17 && y <= 20 && x >= 12 && x <= 20 - (y - 17);
            colors[y * 24 + x] = leaf ? new Color(0.45f, 0.85f, 0.22f)
                : round < 0.7f ? new Color(0.80f, 0.25f, 0.66f)
                : round < 1f ? new Color(0.35f, 0.12f, 0.38f) : Color.clear;
            if (x >= 8 && x <= 10 && y >= 12 && y <= 14)
                colors[y * 24 + x] = new Color(1f, 0.75f, 0.90f);
        }
        dots.SetPixels(colors);
        dots.Apply();
        return dots;
    }

    private static Texture2D DrawAnt()
    {
        Texture2D dots = new Texture2D(24, 24, TextureFormat.RGBA32, false);
        dots.filterMode = FilterMode.Point;
        Color[] colors = new Color[24 * 24];
        for (int y = 0; y < 24; y++)
        for (int x = 0; x < 24; x++)
        {
            bool legs = NearLine(x, y, 8, 10, 4, 4) || NearLine(x, y, 12, 10, 12, 3)
                || NearLine(x, y, 15, 10, 19, 4) || NearLine(x, y, 8, 14, 4, 20)
                || NearLine(x, y, 12, 14, 12, 21) || NearLine(x, y, 15, 14, 19, 20);
            bool feelers = NearLine(x, y, 19, 13, 23, 17) || NearLine(x, y, 19, 11, 23, 7);
            float back = Mathf.Pow((x - 6.5f) / 4f, 2) + Mathf.Pow((y - 12f) / 3.5f, 2);
            float middle = Mathf.Pow((x - 12f) / 2.7f, 2) + Mathf.Pow((y - 12f) / 2.7f, 2);
            float head = Mathf.Pow((x - 17.5f) / 3.5f, 2) + Mathf.Pow((y - 12f) / 3.5f, 2);
            colors[y * 24 + x] = head < 1f ? new Color(0.43f, 0.19f, 0.10f)
                : middle < 1f ? new Color(0.34f, 0.14f, 0.09f)
                : back < 1f ? new Color(0.53f, 0.25f, 0.11f)
                : legs || feelers ? new Color(0.25f, 0.11f, 0.08f) : Color.clear;
            if (x == 19 && y == 14) colors[y * 24 + x] = new Color(1f, 0.85f, 0.55f);
        }
        dots.SetPixels(colors);
        dots.Apply();
        return dots;
    }

    private static Texture2D DrawBeetle()
    {
        Texture2D dots = new Texture2D(24, 24, TextureFormat.RGBA32, false);
        dots.filterMode = FilterMode.Point;
        Color[] colors = new Color[24 * 24];
        for (int y = 0; y < 24; y++)
        for (int x = 0; x < 24; x++)
        {
            bool legs = NearLine(x, y, 8, 8, 3, 4) || NearLine(x, y, 7, 12, 2, 12)
                || NearLine(x, y, 8, 16, 3, 20) || NearLine(x, y, 16, 8, 21, 4)
                || NearLine(x, y, 17, 12, 22, 12) || NearLine(x, y, 16, 16, 21, 20);
            float shell = Mathf.Pow((x - 12f) / 6f, 2) + Mathf.Pow((y - 10.5f) / 8f, 2);
            float head = Mathf.Pow((x - 12f) / 3.5f, 2) + Mathf.Pow((y - 18f) / 3f, 2);
            colors[y * 24 + x] = head < 1f ? new Color(0.10f, 0.20f, 0.23f)
                : shell < 1f ? new Color(0.10f, 0.50f, 0.53f)
                : legs ? new Color(0.05f, 0.22f, 0.25f) : Color.clear;
            if (shell < 1f && x == 12 && y < 17) colors[y * 24 + x] = new Color(0.04f, 0.24f, 0.28f);
            if ((x == 9 || x == 15) && (y == 8 || y == 12))
                colors[y * 24 + x] = new Color(0.95f, 0.70f, 0.21f);
            if ((x == 10 || x == 14) && y == 19)
                colors[y * 24 + x] = new Color(1f, 0.89f, 0.59f);
        }
        dots.SetPixels(colors);
        dots.Apply();
        return dots;
    }

    private static Texture2D DrawButterfly()
    {
        Texture2D dots = new Texture2D(24, 24, TextureFormat.RGBA32, false);
        dots.filterMode = FilterMode.Point;
        Color[] colors = new Color[24 * 24];
        for (int y = 0; y < 24; y++)
        for (int x = 0; x < 24; x++)
        {
            float wingX = Mathf.Abs(x - 11.5f);
            float topWing = Mathf.Pow((wingX - 5f) / 5f, 2) + Mathf.Pow((y - 15f) / 5f, 2);
            float lowWing = Mathf.Pow((wingX - 5f) / 4f, 2) + Mathf.Pow((y - 8f) / 4f, 2);
            bool body = x >= 11 && x <= 12 && y >= 5 && y <= 19;
            colors[y * 24 + x] = body ? new Color(0.25f, 0.13f, 0.24f)
                : topWing < 1f || lowWing < 1f ? new Color(0.85f, 0.42f, 0.78f)
                : Color.clear;
            if ((topWing < 0.12f || lowWing < 0.15f) && !body)
                colors[y * 24 + x] = new Color(1f, 0.86f, 0.35f);
        }
        dots.SetPixels(colors);
        dots.Apply();
        return dots;
    }

    private static bool NearLine(float x, float y, float fromX, float fromY, float toX, float toY)
    {
        Vector2 point = new Vector2(x - fromX, y - fromY);
        Vector2 line = new Vector2(toX - fromX, toY - fromY);
        float along = Mathf.Clamp01(Vector2.Dot(point, line) / line.sqrMagnitude);
        return (point - line * along).sqrMagnitude < 0.9f;
    }

    private void OnDestroy()
    {
        if (berry != null) Destroy(berry);
        if (berrySprite != null) Destroy(berrySprite);
        if (berryPixels != null) Destroy(berryPixels);
        if (waves != null) Destroy(waves);
        if (fly != null) Destroy(fly);
        if (flySprite != null) Destroy(flySprite);
        if (flyPixels != null) Destroy(flyPixels);
        if (ant != null) Destroy(ant);
        if (antSprite != null) Destroy(antSprite);
        if (antPixels != null) Destroy(antPixels);
        if (beetle != null) Destroy(beetle);
        if (beetleSprite != null) Destroy(beetleSprite);
        if (beetlePixels != null) Destroy(beetlePixels);
        if (butterfly != null) Destroy(butterfly);
        if (butterflySprite != null) Destroy(butterflySprite);
        if (butterflyPixels != null) Destroy(butterflyPixels);
        if (flickSound != null) Destroy(flickSound);
        if (catchSound != null) Destroy(catchSound);
        if (upgradeSound != null) Destroy(upgradeSound);
        if (biteSound != null) Destroy(biteSound);
    }

    private void OnDisable()
    {
        if (paused || titleOpen || gameOver) Time.timeScale = 1f;
        paused = false;
    }
}
