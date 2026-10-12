using UnityEngine;
using UnityEngine.InputSystem;

// one fly, a few coins, and some little upgrades to work toward.
[RequireComponent(typeof(LizardTongue))]
public sealed class FlyGame : MonoBehaviour
{
    private const int MaxLevel = 5;
    private LizardTongue tongue;
    private LizardMovement lizard;
    private GameObject fly;
    private Texture2D flyPixels;
    private Sprite flySprite;
    private GUIStyle scoreStyle;
    private int score;
    private int coins;
    private int tongueLevel = 1;
    private int speedLevel = 1;
    private int cooldownLevel = 1;
    private bool upgradesOpen;
    private bool caughtThisFlick;
    private Vector2 nextPerch;
    private float restUntil;

    private void Start()
    {
        tongue = GetComponent<LizardTongue>();
        lizard = GetComponent<LizardMovement>();
        flyPixels = DrawFly();
        flySprite = Sprite.Create(flyPixels, new Rect(0, 0, 24, 24), new Vector2(0.5f, 0.5f), 32);
        fly = new GameObject("Fly");
        SpriteRenderer picture = fly.AddComponent<SpriteRenderer>();
        picture.sprite = flySprite;
        picture.sortingOrder = 2;
        fly.transform.position = new Vector3(0f, -2.7f, 0f);
        nextPerch = NearbySpot(fly.transform.position);
        restUntil = Time.time + 0.8f;
    }

    private void Update()
    {
        Keyboard keys = Keyboard.current;
        if (keys != null)
        {
            if (keys.eKey.wasPressedThisFrame) upgradesOpen = !upgradesOpen;
            if (upgradesOpen)
            {
                if (keys.digit1Key.wasPressedThisFrame) BuyTongueLength();
                if (keys.digit2Key.wasPressedThisFrame) BuyMovementSpeed();
                if (keys.digit3Key.wasPressedThisFrame) BuyTongueCooldown();
            }
        }

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

    private void LateUpdate()
    {
        if (!tongue.IsOut) { caughtThisFlick = false; return; }
        if (caughtThisFlick || fly == null) return;

        Vector2 start = tongue.StartPoint;
        Vector2 span = tongue.TipPoint - start;
        if (span.sqrMagnitude < 0.001f) return;

        Vector2 bugSpot = fly.transform.position;
        float along = Mathf.Clamp01(Vector2.Dot(bugSpot - start, span) / span.sqrMagnitude);
        Vector2 closest = start + span * along;
        if ((bugSpot - closest).sqrMagnitude > 0.12f) return;

        score++;
        coins++;
        caughtThisFlick = true;
        MoveFly();
    }

    private void BuyTongueLength()
    {
        if (!SpendOn(tongueLevel)) return;
        tongueLevel++;
        tongue.Reach += 0.5f;
    }

    private void BuyMovementSpeed()
    {
        if (!SpendOn(speedLevel)) return;
        speedLevel++;
        lizard.MoveSpeed += 0.5f;
    }

    private void BuyTongueCooldown()
    {
        if (!SpendOn(cooldownLevel)) return;
        cooldownLevel++;
        tongue.CooldownSeconds -= 0.04f;
    }

    private bool SpendOn(int level)
    {
        if (level >= MaxLevel || coins < level + 2) return false;
        coins -= level + 2;
        return true;
    }

    private void MoveFly()
    {
        Camera view = Camera.main;
        float tall = view != null ? view.orthographicSize - 0.7f : 4f;
        float wide = view != null ? view.orthographicSize * view.aspect - 0.7f : 6f;
        Vector2 center = view != null ? (Vector2)view.transform.position : Vector2.zero;
        Vector2 next = center;

        for (int tryNumber = 0; tryNumber < 20; tryNumber++)
        {
            next = center + new Vector2(Random.Range(-wide, wide), Random.Range(-tall, tall));
            if (Vector2.Distance(next, transform.position) > 1.5f) break;
        }

        fly.transform.position = new Vector3(next.x, next.y, 0f);
        nextPerch = NearbySpot(next);
        restUntil = Time.time + 0.5f;
    }

    private static Vector2 NearbySpot(Vector2 here)
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
            if ((spot - here).sqrMagnitude > 0.36f) return spot;
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
        }
        GUI.Label(new Rect(20, 16, 400, 40), $"Flies: {score}    Coins: {coins}", scoreStyle);
        GUI.Label(new Rect(20, 52, 400, 40), upgradesOpen ? "E: close upgrades" : "E: upgrades", scoreStyle);
        if (!upgradesOpen) return;

        GUI.Label(new Rect(20, 88, 720, 40), UpgradeLine(1, "Longer tongue", tongueLevel), scoreStyle);
        GUI.Label(new Rect(20, 124, 720, 40), UpgradeLine(2, "Faster feet", speedLevel), scoreStyle);
        GUI.Label(new Rect(20, 160, 720, 40), UpgradeLine(3, "Shorter cooldown", cooldownLevel), scoreStyle);
    }

    private static string UpgradeLine(int key, string name, int level) => level >= MaxLevel
        ? $"{key}: {name} (max)" : $"{key}: {name} - {level + 2} coins (Lv {level})";

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

    private void OnDestroy()
    {
        if (fly != null) Destroy(fly);
        if (flySprite != null) Destroy(flySprite);
        if (flyPixels != null) Destroy(flyPixels);
    }
}
