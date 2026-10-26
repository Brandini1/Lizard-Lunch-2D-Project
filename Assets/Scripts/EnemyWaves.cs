using System.Collections.Generic;
using UnityEngine;

// each yard has its own hunters, with a breather between waves.
public sealed class EnemyWaves : MonoBehaviour
{
    private sealed class Hunter
    {
        public SpriteRenderer picture;
        public Vector2 charge;
        public float attackAt;
        public float stunnedUntil;
        public float wobble;
        public LineRenderer warning;
        public bool aimLocked;
    }

    private readonly List<Hunter> hunters = new List<Hunter>();
    private readonly Sprite[] sprites = new Sprite[3];
    private readonly Texture2D[] pixels = new Texture2D[3];
    private readonly Sprite[] bossSprites = new Sprite[3];
    private readonly Texture2D[] bossPixels = new Texture2D[3];
    private BackyardMap backyard;
    private Hunter boss;
    private YardArea area;
    private int toSpawn;
    private float nextSpawn;
    private float nextWave;
    private bool resting;
    private float nextSummon;
    private float bossFlashUntil;
    private float bossIntroUntil;
    private Material warningPaint;

    public int Wave { get; private set; }
    public int Remaining => toSpawn + hunters.Count;
    public int CoinReward => 3 + (int)area;
    public string EnemyName => area == YardArea.Backyard ? "Red ants"
        : area == YardArea.FlowerPatch ? "Wasps" : "Pond beetles";
    public float BreakLeft => resting ? Mathf.Max(0f, nextWave - Time.time) : 0f;
    public bool IsBossWave => Wave > 0 && Wave % 5 == 0;
    public bool BossAlive => boss != null;
    public float BossIntroLeft => IsBossWave && !UpgradeBreak ? Mathf.Max(0f, bossIntroUntil - Time.time) : 0f;
    public bool UpgradeBreak { get; private set; }
    public int BossHealth { get; private set; }
    public int BossMaxHealth { get; private set; }
    public string HitNotice { get; private set; }
    public string BossName => area == YardArea.Backyard ? "Queen ant"
        : area == YardArea.FlowerPatch ? "Giant hornet" : "Armored beetle";
    private bool ShellOpen => boss != null && Time.time >= boss.attackAt + 1.8f
        && Time.time < boss.attackAt + 3.4f;
    public string BossHint => area == YardArea.Backyard ? "Catch the queen. Watch for her ants!"
        : area == YardArea.FlowerPatch ? "Dodge the warning, then strike!"
        : ShellOpen ? "Shell open! Hit it now!" : "Dodge its charge to expose its shell";

    private void Awake()
    {
        backyard = GetComponent<BackyardMap>();
        warningPaint = new Material(Shader.Find("Sprites/Default"));
        for (int i = 0; i < sprites.Length; i++)
        {
            pixels[i] = DrawHunter((YardArea)i);
            sprites[i] = Sprite.Create(pixels[i], new Rect(0, 0, 24, 24), new Vector2(0.5f, 0.5f), 32);
            bossPixels[i] = DrawHunter((YardArea)i);
            // a gold crest makes the bigger bug easy to pick out.
            for (int y = 16; y <= 21; y++)
            for (int x = 16; x <= 21; x++)
                if (y <= 18 || x == 16 || x == 18 || x == 21)
                    bossPixels[i].SetPixel(x, y, new Color(1f, 0.80f, 0.15f));
            bossPixels[i].Apply();
            bossSprites[i] = Sprite.Create(bossPixels[i], new Rect(0, 0, 24, 24), new Vector2(0.5f, 0.5f), 32);
        }
    }

    public void Restart(YardArea nextArea)
    {
        ClearHunters();
        area = nextArea;
        Wave = 1;
        BeginWave();
    }

    private void ClearHunters()
    {
        foreach (Hunter bug in hunters)
        {
            bug.picture.gameObject.SetActive(false);
            Destroy(bug.picture.gameObject);
        }
        hunters.Clear();
        boss = null;
    }

    private void BeginWave()
    {
        resting = false;
        UpgradeBreak = false;
        BossHealth = BossMaxHealth = 0;
        HitNotice = "";
        toSpawn = IsBossWave ? 1 : 3 + (Wave - 1) * 2 + (int)area * 2;
        nextSpawn = Time.time + 1.5f;
        bossIntroUntil = IsBossWave ? Time.time + 3f : 0f;
    }

    public void ContinueAfterBoss()
    {
        if (!UpgradeBreak) return;
        Wave++;
        BeginWave();
    }

    public void Tick()
    {
        if (UpgradeBreak) return;
        if (resting)
        {
            if (Time.time < nextWave) return;
            Wave++;
            BeginWave();
        }
        // keep the crowd manageable while the total wave keeps growing.
        int crowd = Mathf.Min(8, 2 + (Wave - 1) / 2 + (int)area);
        if (toSpawn > 0 && hunters.Count < crowd && Time.time >= nextSpawn)
        {
            if (FindSpawn(out Vector2 spot, IsBossWave ? 0.65f : 0.35f))
            {
                Hunter bug = SpawnHunter(spot, IsBossWave);
                if (IsBossWave)
                {
                    boss = bug;
                    BossHealth = BossMaxHealth = 5 + (Wave / 5 - 1) * 2 + (int)area;
                    nextSummon = Time.time + 3f;
                }
                toSpawn--;
            }
            nextSpawn = Time.time + Mathf.Max(0.6f, 1.5f - Wave * 0.08f);
        }
        if (boss != null && area == YardArea.Backyard && Time.time >= nextSummon)
        {
            SummonAnts();
            nextSummon = Time.time + 4.5f;
        }
        foreach (Hunter bug in hunters)
            if (bug == boss) MoveBoss();
            else MoveHunter(bug);
    }

    private Hunter SpawnHunter(Vector2 spot, bool big)
    {
        GameObject body = new GameObject(big ? BossName : EnemyName);
        SpriteRenderer picture = body.AddComponent<SpriteRenderer>();
        picture.sprite = big ? bossSprites[(int)area] : sprites[(int)area];
        picture.sortingOrder = big ? 3 : 2;
        body.transform.position = spot;
        body.transform.localScale = Vector3.one * (big ? 1.8f : 1f);
        Hunter bug = new Hunter { picture = picture, attackAt = Time.time + Random.Range(1f, 2f),
            wobble = Random.Range(0f, 6f) };
        hunters.Add(bug);
        return bug;
    }

    private void SummonAnts()
    {
        for (int attempt = 0, added = 0; attempt < 30 && added < 2 && hunters.Count < 5; attempt++)
        {
            Vector2 spot = (Vector2)boss.picture.transform.position + Random.insideUnitCircle.normalized * 1.4f;
            if (backyard.IsBlocked(spot, 0.35f) || Vector2.Distance(spot, transform.position) < 1.8f) continue;
            bool crowded = false;
            foreach (Hunter bug in hunters)
                if (Vector2.Distance(spot, bug.picture.transform.position) < 0.8f) crowded = true;
            if (crowded) continue;
            SpawnHunter(spot, false);
            added++;
        }
    }

    private bool FindSpawn(out Vector2 spot, float space)
    {
        Camera view = Camera.main;
        Vector2 center = view != null ? (Vector2)view.transform.position : Vector2.zero;
        float tall = view != null ? view.orthographicSize - 1f : 4f;
        float wide = view != null ? view.orthographicSize * view.aspect - 1f : 6f;
        for (int attempt = 0; attempt < 60; attempt++)
        {
            spot = center + new Vector2(Random.Range(-wide, wide), Random.Range(-tall, tall));
            float safeDistance = IsBossWave ? 9f : 6.25f;
            if ((spot - (Vector2)transform.position).sqrMagnitude < safeDistance || backyard.IsBlocked(spot, space)) continue;
            bool crowded = false;
            foreach (Hunter bug in hunters)
                if (((Vector2)bug.picture.transform.position - spot).sqrMagnitude < 0.8f) crowded = true;
            if (!crowded) return true;
        }
        spot = Vector2.zero;
        return false;
    }

    private void MoveHunter(Hunter bug)
    {
        if (bug.warning != null) bug.warning.enabled = false;
        if (Time.time < bug.stunnedUntil) return;
        Vector2 here = bug.picture.transform.position;
        Vector2 chase = ((Vector2)transform.position - here).normalized;
        float speed = 1.35f + (int)area * 0.25f + Mathf.Min((Wave - 1) * 0.12f, 1.4f);
        bug.picture.color = Color.white;
        bool charging = false;
        if (area != YardArea.Backyard)
        {
            float windup = area == YardArea.FlowerPatch ? 0.4f : 0.7f;
            float dash = area == YardArea.FlowerPatch ? 0.4f : 0.65f;
            if (Time.time < bug.attackAt)
            {
                bug.aimLocked = false;
                if (area == YardArea.FlowerPatch)
                    chase = (chase + new Vector2(-chase.y, chase.x) * Mathf.Sin(Time.time * 5f + bug.wobble) * 0.7f).normalized;
            }
            else if (Time.time < bug.attackAt + windup)
            {
                WarnCharge(bug, chase, speed * (area == YardArea.FlowerPatch ? 2.1f : 2.6f) * dash, 0.25f);
                bug.picture.color = new Color(1f, 0.65f, 0.65f);
                return;
            }
            else if (Time.time < bug.attackAt + windup + dash)
            {
                chase = bug.charge;
                speed *= area == YardArea.FlowerPatch ? 2.1f : 2.6f;
                charging = true;
            }
            else
            {
                bug.attackAt = Time.time + (area == YardArea.FlowerPatch ? 1.8f : 2.4f);
                bug.aimLocked = false;
            }
        }
        StepHunter(bug, chase, speed, charging, 0.25f);
    }

    private void MoveBoss()
    {
        if (boss.warning != null) boss.warning.enabled = false;
        if (Time.time < boss.attackAt) boss.aimLocked = false;
        Vector2 chase = ((Vector2)transform.position - (Vector2)boss.picture.transform.position).normalized;
        float extra = Mathf.Min((Wave / 5 - 1) * 0.1f, 0.6f);
        float speed = (area == YardArea.Backyard ? 0.8f : 1.2f) + extra;
        bool charging = false;
        boss.picture.color = Time.time < bossFlashUntil ? new Color(1f, 0.5f, 0.5f) : Color.white;
        if (area != YardArea.Backyard && Time.time >= boss.attackAt)
        {
            float windup = area == YardArea.FlowerPatch ? 0.8f : 1f;
            float dash = area == YardArea.FlowerPatch ? 0.55f : 0.8f;
            float recovery = area == YardArea.FlowerPatch ? 0.8f : 1.6f;
            if (Time.time < boss.attackAt + windup)
            {
                WarnCharge(boss, chase, (5f + extra) * dash, 0.55f);
                boss.picture.color = new Color(1f, 0.4f, 0.25f);
                return;
            }
            else if (Time.time < boss.attackAt + windup + dash)
            {
                chase = boss.charge;
                speed = 5f + extra;
                charging = true;
            }
            else if (Time.time < boss.attackAt + windup + dash + recovery)
            {
                boss.picture.color = new Color(0.65f, 1f, 0.55f);
                return;
            }
            else
            {
                boss.attackAt = Time.time + 1.6f;
                boss.aimLocked = false;
            }
        }
        StepHunter(boss, chase, speed, charging, 0.55f);
    }

    private void StepHunter(Hunter bug, Vector2 chase, float speed, bool charging, float space)
    {
        Vector2 here = bug.picture.transform.position;
        // charges keep their direction; walking bugs can step around rocks.
        for (int turn = 0; turn < (charging ? 1 : 7); turn++)
        {
            float angle = ((turn + 1) / 2) * (turn % 2 == 0 ? -1f : 1f) * Mathf.PI * 0.25f;
            Vector2 step = new Vector2(chase.x * Mathf.Cos(angle) - chase.y * Mathf.Sin(angle),
                chase.x * Mathf.Sin(angle) + chase.y * Mathf.Cos(angle)) * (speed * Mathf.Min(Time.deltaTime, 0.05f));
            if (backyard.IsBlocked(here + step, space)) continue;
            bug.picture.transform.position = here + step;
            break;
        }
        bug.picture.flipX = chase.x < 0f;
    }

    private void WarnCharge(Hunter bug, Vector2 chase, float distance, float space)
    {
        // commit to the shown direction so a well-timed dodge works.
        if (!bug.aimLocked)
        {
            bug.charge = chase == Vector2.zero ? Vector2.right : chase;
            bug.aimLocked = true;
        }
        if (bug.warning == null)
        {
            GameObject lane = new GameObject("Charge warning");
            lane.transform.SetParent(bug.picture.transform, false);
            bug.warning = lane.AddComponent<LineRenderer>();
            bug.warning.sharedMaterial = warningPaint;
            bug.warning.useWorldSpace = true;
            bug.warning.positionCount = 2;
            bug.warning.sortingOrder = 0;
            bug.warning.startWidth = space * 2f;
            bug.warning.endWidth = 0.08f;
        }
        Vector2 here = bug.picture.transform.position;
        float clear = distance;
        for (float step = 0.1f; step <= distance; step += 0.1f)
            if (backyard.IsBlocked(here + bug.charge * step, space))
            {
                clear = Mathf.Max(0f, step - 0.1f);
                break;
            }
        Color glow = new Color(1f, 0.25f, 0.1f, 0.4f + Mathf.Sin(Time.time * 18f) * 0.15f);
        bug.warning.startColor = bug.warning.endColor = glow;
        bug.warning.SetPosition(0, here);
        bug.warning.SetPosition(1, here + bug.charge * clear);
        bug.picture.flipX = bug.charge.x < 0f;
        bug.warning.enabled = true;
    }

    public bool TryBite()
    {
        foreach (Hunter bug in hunters)
        {
            Vector2 away = (Vector2)bug.picture.transform.position - (Vector2)transform.position;
            if (Time.time < bug.stunnedUntil || away.sqrMagnitude > (bug == boss ? 0.81f : 0.49f)) continue;
            // a bite shouldn't cancel a boss's warning or open-shell window.
            if (bug == boss) return true;
            Vector2 back = (Vector2)bug.picture.transform.position + (away == Vector2.zero ? Vector2.up : away.normalized) * 0.9f;
            if (!backyard.IsBlocked(back, 0.25f)) bug.picture.transform.position = back;
            bug.stunnedUntil = Time.time + 0.7f;
            bug.attackAt = Time.time + 1.5f;
            bug.aimLocked = false;
            if (bug.warning != null) bug.warning.enabled = false;
            return true;
        }
        return false;
    }

    public bool TryCatch(Vector2 start, Vector2 span, out Vector2 spot, out int reward)
    {
        spot = Vector2.zero;
        reward = 0;
        if (span.sqrMagnitude < 0.001f) return false;
        for (int i = 0; i < hunters.Count; i++)
        {
            Vector2 here = hunters[i].picture.transform.position;
            float along = Mathf.Clamp01(Vector2.Dot(here - start, span) / span.sqrMagnitude);
            Hunter bug = hunters[i];
            if ((here - (start + span * along)).sqrMagnitude > (bug == boss ? 0.36f : 0.12f)) continue;
            spot = here;
            if (bug == boss)
            {
                if (area == YardArea.PondEdge && !ShellOpen)
                {
                    HitNotice = "Armor blocked it! Wait for its charge.";
                    return true;
                }
                BossHealth--;
                bossFlashUntil = Time.time + 0.2f;
                HitNotice = $"{BossName}: {BossHealth}/{BossMaxHealth} health";
                if (BossHealth > 0) return true;
                reward = 15 + Wave * 2 + (int)area * 5;
                HitNotice = $"{BossName} defeated! +{reward} coins";
                ClearHunters();
                toSpawn = 0;
                UpgradeBreak = true;
                return true;
            }
            reward = CoinReward;
            hunters[i].picture.gameObject.SetActive(false);
            Destroy(hunters[i].picture.gameObject);
            hunters.RemoveAt(i);
            if (Remaining == 0)
            {
                resting = true;
                nextWave = Time.time + 5f;
            }
            return true;
        }
        return false;
    }

    private static Texture2D DrawHunter(YardArea yard)
    {
        Texture2D dots = new Texture2D(24, 24, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        Color[] colors = new Color[24 * 24];
        for (int y = 0; y < 24; y++)
        for (int x = 0; x < 24; x++)
        {
            if (yard == YardArea.Backyard)
            {
                colors[y * 24 + x] = AntPixel(x, y);
                continue;
            }
            float belly = Mathf.Pow((x - 9f) / (yard == YardArea.PondEdge ? 8f : 6f), 2)
                + Mathf.Pow((y - 12f) / (yard == YardArea.PondEdge ? 6f : 4f), 2);
            bool head = new Vector2(x - 18, y - 12).sqrMagnitude < 13f;
            bool legs = (y == 5 || y == 19) && x >= 5 && x <= 16
                || (x == 6 || x == 11 || x == 16) && y >= 6 && y <= 18;
            bool wing = yard == YardArea.FlowerPatch && (new Vector2(x - 9, y - 18).sqrMagnitude < 22f
                || new Vector2(x - 9, y - 6).sqrMagnitude < 22f);
            Color shell = yard == YardArea.Backyard ? new Color(0.85f, 0.16f, 0.10f)
                : yard == YardArea.FlowerPatch ? (x % 6 < 3 ? new Color(1f, 0.78f, 0.12f) : new Color(0.18f, 0.13f, 0.12f))
                : new Color(0.28f, 0.29f, 0.70f);
            colors[y * 24 + x] = head ? new Color(0.28f, 0.12f, 0.16f)
                : belly < 1f ? shell : wing ? new Color(0.8f, 0.95f, 1f, 0.85f)
                : legs ? new Color(0.22f, 0.12f, 0.18f) : Color.clear;
            if (head && x == 19 && (y == 10 || y == 14)) colors[y * 24 + x] = Color.white;
            if (yard == YardArea.PondEdge && belly < 1f && y == 12) colors[y * 24 + x] = new Color(0.12f, 0.14f, 0.33f);
        }
        dots.SetPixels(colors);
        dots.Apply();
        return dots;
    }

    private static Color AntPixel(int x, int y)
    {
        // separate the abdomen, waist, and head instead of drawing one shell.
        float back = Mathf.Pow((x - 5f) / 4f, 2) + Mathf.Pow((y - 12f) / 3f, 2);
        float middle = Mathf.Pow((x - 12f) / 2.2f, 2) + Mathf.Pow((y - 12f) / 2f, 2);
        float head = Mathf.Pow((x - 19f) / 3f, 2) + Mathf.Pow((y - 12f) / 3f, 2);
        int legY = y < 12 ? y : 24 - y;
        bool legs = AntLine(x, legY, 10, 11, 8, 8) || AntLine(x, legY, 8, 8, 4, 6)
            || AntLine(x, legY, 12, 11, 12, 7) || AntLine(x, legY, 12, 7, 10, 3)
            || AntLine(x, legY, 14, 11, 17, 8) || AntLine(x, legY, 17, 8, 20, 5);
        bool feelers = AntLine(x, legY, 21, 10, 22, 7) || AntLine(x, legY, 22, 7, 23, 6);
        float body = Mathf.Min(back, Mathf.Min(middle, head));
        if (head < 1f && x == 20 && (y == 11 || y == 13)) return new Color(1f, 0.88f, 0.65f);
        if (body < 1f) return body > 0.6f ? new Color(0.45f, 0.08f, 0.05f)
            : new Color(0.95f, 0.23f, 0.12f);
        bool waist = y == 12 && x >= 8 && x <= 16;
        return legs || feelers || waist ? new Color(0.48f, 0.12f, 0.07f) : Color.clear;
    }

    private static bool AntLine(int x, int y, float ax, float ay, float bx, float by)
    {
        Vector2 offset = new Vector2(x - ax, y - ay);
        Vector2 line = new Vector2(bx - ax, by - ay);
        float along = Mathf.Clamp01(Vector2.Dot(offset, line) / line.sqrMagnitude);
        return (offset - line * along).sqrMagnitude < 0.45f;
    }

    private void OnDestroy()
    {
        if (warningPaint != null) Destroy(warningPaint);
        foreach (Hunter bug in hunters)
            if (bug.picture != null) Destroy(bug.picture.gameObject);
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null) Destroy(sprites[i]);
            if (pixels[i] != null) Destroy(pixels[i]);
            if (bossSprites[i] != null) Destroy(bossSprites[i]);
            if (bossPixels[i] != null) Destroy(bossPixels[i]);
        }
    }
}
