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
    }

    private readonly List<Hunter> hunters = new List<Hunter>();
    private readonly Sprite[] sprites = new Sprite[3];
    private readonly Texture2D[] pixels = new Texture2D[3];
    private BackyardMap backyard;
    private YardArea area;
    private int toSpawn;
    private float nextSpawn;
    private float nextWave;
    private bool resting;

    public int Wave { get; private set; }
    public int Remaining => toSpawn + hunters.Count;
    public int CoinReward => 3 + (int)area;
    public string EnemyName => area == YardArea.Backyard ? "Red ants"
        : area == YardArea.FlowerPatch ? "Wasps" : "Pond beetles";
    public float BreakLeft => resting ? Mathf.Max(0f, nextWave - Time.time) : 0f;

    private void Awake()
    {
        backyard = GetComponent<BackyardMap>();
        for (int i = 0; i < sprites.Length; i++)
        {
            pixels[i] = DrawHunter((YardArea)i);
            sprites[i] = Sprite.Create(pixels[i], new Rect(0, 0, 24, 24), new Vector2(0.5f, 0.5f), 32);
        }
    }

    public void Restart(YardArea nextArea)
    {
        foreach (Hunter bug in hunters)
        {
            bug.picture.gameObject.SetActive(false);
            Destroy(bug.picture.gameObject);
        }
        hunters.Clear();
        area = nextArea;
        Wave = 1;
        BeginWave();
    }

    private void BeginWave()
    {
        resting = false;
        toSpawn = 3 + (Wave - 1) * 2 + (int)area * 2;
        nextSpawn = Time.time + 1.5f;
    }

    public void Tick()
    {
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
            if (FindSpawn(out Vector2 spot))
            {
                GameObject body = new GameObject(EnemyName);
                SpriteRenderer picture = body.AddComponent<SpriteRenderer>();
                picture.sprite = sprites[(int)area];
                picture.sortingOrder = 2;
                body.transform.position = spot;
                hunters.Add(new Hunter { picture = picture, attackAt = Time.time + Random.Range(1f, 2f),
                    wobble = Random.Range(0f, 6f) });
                toSpawn--;
            }
            nextSpawn = Time.time + Mathf.Max(0.6f, 1.5f - Wave * 0.08f);
        }
        foreach (Hunter bug in hunters) MoveHunter(bug);
    }

    private bool FindSpawn(out Vector2 spot)
    {
        Camera view = Camera.main;
        Vector2 center = view != null ? (Vector2)view.transform.position : Vector2.zero;
        float tall = view != null ? view.orthographicSize - 1f : 4f;
        float wide = view != null ? view.orthographicSize * view.aspect - 1f : 6f;
        for (int attempt = 0; attempt < 60; attempt++)
        {
            spot = center + new Vector2(Random.Range(-wide, wide), Random.Range(-tall, tall));
            if ((spot - (Vector2)transform.position).sqrMagnitude < 6.25f || backyard.IsBlocked(spot, 0.35f)) continue;
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
                if (area == YardArea.FlowerPatch)
                    chase = (chase + new Vector2(-chase.y, chase.x) * Mathf.Sin(Time.time * 5f + bug.wobble) * 0.7f).normalized;
            }
            else if (Time.time < bug.attackAt + windup)
            {
                bug.charge = chase;
                bug.picture.color = new Color(1f, 0.65f, 0.65f);
                return;
            }
            else if (Time.time < bug.attackAt + windup + dash)
            {
                chase = bug.charge;
                speed *= area == YardArea.FlowerPatch ? 2.1f : 2.6f;
                charging = true;
            }
            else bug.attackAt = Time.time + (area == YardArea.FlowerPatch ? 1.8f : 2.4f);
        }
        // charges keep their direction; walking bugs can step around rocks.
        for (int turn = 0; turn < (charging ? 1 : 7); turn++)
        {
            float angle = ((turn + 1) / 2) * (turn % 2 == 0 ? -1f : 1f) * Mathf.PI * 0.25f;
            Vector2 step = new Vector2(chase.x * Mathf.Cos(angle) - chase.y * Mathf.Sin(angle),
                chase.x * Mathf.Sin(angle) + chase.y * Mathf.Cos(angle)) * (speed * Mathf.Min(Time.deltaTime, 0.05f));
            if (backyard.IsBlocked(here + step, 0.25f)) continue;
            bug.picture.transform.position = here + step;
            break;
        }
        bug.picture.flipX = chase.x < 0f;
    }

    public bool TryBite()
    {
        foreach (Hunter bug in hunters)
        {
            Vector2 away = (Vector2)bug.picture.transform.position - (Vector2)transform.position;
            if (Time.time < bug.stunnedUntil || away.sqrMagnitude > 0.49f) continue;
            Vector2 back = (Vector2)bug.picture.transform.position + (away == Vector2.zero ? Vector2.up : away.normalized) * 0.9f;
            if (!backyard.IsBlocked(back, 0.25f)) bug.picture.transform.position = back;
            bug.stunnedUntil = Time.time + 0.7f;
            bug.attackAt = Time.time + 1.5f;
            return true;
        }
        return false;
    }

    public bool TryCatch(Vector2 start, Vector2 span, out Vector2 spot)
    {
        spot = Vector2.zero;
        if (span.sqrMagnitude < 0.001f) return false;
        for (int i = 0; i < hunters.Count; i++)
        {
            Vector2 here = hunters[i].picture.transform.position;
            float along = Mathf.Clamp01(Vector2.Dot(here - start, span) / span.sqrMagnitude);
            if ((here - (start + span * along)).sqrMagnitude > 0.12f) continue;
            spot = here;
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

    private void OnDestroy()
    {
        foreach (Hunter bug in hunters)
            if (bug.picture != null) Destroy(bug.picture.gameObject);
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null) Destroy(sprites[i]);
            if (pixels[i] != null) Destroy(pixels[i]);
        }
    }
}
