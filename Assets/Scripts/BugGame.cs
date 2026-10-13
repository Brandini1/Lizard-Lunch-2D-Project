using UnityEngine;
using UnityEngine.InputSystem;

// flies dart around, ants patrol, and both pay for upgrades.
[RequireComponent(typeof(LizardTongue))]
public sealed class BugGame : MonoBehaviour
{
    private const int MaxLevel = 5;
    private LizardTongue tongue;
    private LizardMovement lizard;
    private GameObject fly;
    private Texture2D flyPixels;
    private Sprite flySprite;
    private GameObject ant;
    private SpriteRenderer antPicture;
    private Texture2D antPixels;
    private Sprite antSprite;
    private float antHeading = 1f;
    private float antRestUntil;
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

        antPixels = DrawAnt();
        antSprite = Sprite.Create(antPixels, new Rect(0, 0, 24, 24), new Vector2(0.5f, 0.5f), 32);
        ant = new GameObject("Ant");
        antPicture = ant.AddComponent<SpriteRenderer>();
        antPicture.sprite = antSprite;
        antPicture.sortingOrder = 2;
        ant.transform.position = new Vector3(-2.8f, 1.8f, 0f);
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

        MoveAnt();
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
        if (nextX < centerX - edge || nextX > centerX + edge)
        {
            antHeading *= -1f;
            antPicture.flipX = antHeading < 0f;
            antRestUntil = Time.time + 0.35f;
            return;
        }
        ant.transform.position = new Vector3(nextX, here.y, 0f);
    }

    private void LateUpdate()
    {
        if (!tongue.IsOut) { caughtThisFlick = false; return; }
        if (caughtThisFlick) return;

        Vector2 start = tongue.StartPoint;
        Vector2 span = tongue.TipPoint - start;
        if (span.sqrMagnitude < 0.001f) return;

        bool caughtFly = TongueTouches(fly, start, span);
        bool caughtAnt = !caughtFly && TongueTouches(ant, start, span);
        if (!caughtFly && !caughtAnt) return;

        score++;
        coins += caughtAnt ? 2 : 1;
        caughtThisFlick = true;
        if (caughtFly) MoveFly();
        else RespawnAnt();
    }

    private static bool TongueTouches(GameObject bug, Vector2 start, Vector2 span)
    {
        if (bug == null) return false;
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
            if (Vector2.Distance(next, transform.position) > 1.5f) break;
        }

        return next;
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
        GUI.Label(new Rect(20, 16, 400, 40), $"Bugs: {score}    Coins: {coins}", scoreStyle);
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

    private static bool NearLine(float x, float y, float fromX, float fromY, float toX, float toY)
    {
        Vector2 point = new Vector2(x - fromX, y - fromY);
        Vector2 line = new Vector2(toX - fromX, toY - fromY);
        float along = Mathf.Clamp01(Vector2.Dot(point, line) / line.sqrMagnitude);
        return (point - line * along).sqrMagnitude < 0.9f;
    }

    private void OnDestroy()
    {
        if (fly != null) Destroy(fly);
        if (flySprite != null) Destroy(flySprite);
        if (flyPixels != null) Destroy(flyPixels);
        if (ant != null) Destroy(ant);
        if (antSprite != null) Destroy(antSprite);
        if (antPixels != null) Destroy(antPixels);
    }
}
