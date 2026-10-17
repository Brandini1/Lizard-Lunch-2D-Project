using System.Collections.Generic;
using UnityEngine;

// a small backyard made when the game starts.
public sealed class BackyardMap : MonoBehaviour
{
    private readonly List<CircleCollider2D> obstacles = new List<CircleCollider2D>();
    private readonly List<Object> artwork = new List<Object>();
    private GameObject yard;
    private Vector2 center;
    private float halfWide;
    private float halfTall;
    private bool flowerPatch;

    public bool SetArea(bool useFlowers)
    {
        if (flowerPatch == useFlowers) return false;
        flowerPatch = useFlowers;
        yard.SetActive(false);
        Destroy(yard);
        foreach (Object piece in artwork)
            if (piece != null) Destroy(piece);
        artwork.Clear();
        obstacles.Clear();
        BuildYard();
        return true;
    }

    public bool IsBlocked(Vector2 spot, float space = 0f)
    {
        if (Mathf.Abs(spot.x - center.x) > halfWide - 0.85f - space ||
            Mathf.Abs(spot.y - center.y) > halfTall - 0.85f - space) return true;

        foreach (CircleCollider2D obstacle in obstacles)
        {
            float radius = obstacle.radius * obstacle.transform.lossyScale.x + space;
            if (((Vector2)obstacle.transform.position - spot).sqrMagnitude < radius * radius)
                return true;
        }
        return false;
    }

    public float ClearTongueDistance(Vector2 start, Vector2 direction, float reach)
    {
        if (yard == null) return reach;
        float clear = reach;
        foreach (RaycastHit2D hit in Physics2D.RaycastAll(start, direction, reach))
            if (hit.collider != null && hit.collider.transform.IsChildOf(yard.transform))
                clear = Mathf.Min(clear, hit.distance);
        return Mathf.Max(0f, clear - 0.02f);
    }

    private void Awake()
    {
        Camera view = Camera.main;
        center = view != null ? (Vector2)view.transform.position : Vector2.zero;
        halfTall = view != null ? view.orthographicSize : 5f;
        halfWide = view != null ? halfTall * view.aspect : 8f;
        BuildYard();
    }

    private void BuildYard()
    {
        yard = new GameObject("Backyard");

        Sprite grass = MakeSprite(DrawGrass(flowerPatch), 32);
        GameObject ground = new GameObject("Grass");
        ground.transform.SetParent(yard.transform);
        ground.transform.position = center;
        SpriteRenderer groundPicture = ground.AddComponent<SpriteRenderer>();
        groundPicture.sprite = grass;
        groundPicture.drawMode = SpriteDrawMode.Tiled;
        groundPicture.size = new Vector2(halfWide * 2f, halfTall * 2f);
        groundPicture.sortingOrder = -10;

        Sprite fence = MakeSprite(DrawFence(), 16);
        MakeWall("Top fence", center + Vector2.up * (halfTall - 0.18f), new Vector2(halfWide * 2f, 0.36f), fence);
        MakeWall("Bottom fence", center + Vector2.down * (halfTall - 0.18f), new Vector2(halfWide * 2f, 0.36f), fence);
        MakeWall("Left fence", center + Vector2.left * (halfWide - 0.18f), new Vector2(0.36f, halfTall * 2f), fence);
        MakeWall("Right fence", center + Vector2.right * (halfWide - 0.18f), new Vector2(0.36f, halfTall * 2f), fence);

        Sprite rock = MakeSprite(DrawRock(), 24);
        Sprite bush = MakeSprite(DrawBush(), 24);
        if (flowerPatch)
        {
            MakeObstacle("Rock", center + new Vector2(-halfWide * 0.55f, halfTall * 0.30f), rock, 1.25f);
            MakeObstacle("Rock", center + new Vector2(halfWide * 0.54f, -halfTall * 0.40f), rock, 1.1f);
            MakeObstacle("Bush", center + new Vector2(-halfWide * 0.56f, -halfTall * 0.48f), bush, 1.4f);
            MakeObstacle("Bush", center + new Vector2(halfWide * 0.52f, halfTall * 0.52f), bush, 1.35f);
            MakeObstacle("Bush", center + new Vector2(0f, halfTall * 0.65f), bush, 1.1f);
            Sprite flower = MakeSprite(DrawFlower(), 24);
            MakeFlower(center + new Vector2(-halfWide * 0.27f, halfTall * 0.40f), flower);
            MakeFlower(center + new Vector2(halfWide * 0.25f, halfTall * 0.24f), flower);
            MakeFlower(center + new Vector2(-halfWide * 0.18f, -halfTall * 0.55f), flower);
            MakeFlower(center + new Vector2(halfWide * 0.42f, -halfTall * 0.62f), flower);
            return;
        }
        MakeObstacle("Rock", center + new Vector2(-halfWide * 0.37f, halfTall * 0.18f), rock, 1.45f);
        MakeObstacle("Rock", center + new Vector2(halfWide * 0.34f, -halfTall * 0.20f), rock, 1.2f);
        MakeObstacle("Rock", center + new Vector2(halfWide * 0.05f, halfTall * 0.60f), rock, 1f);
        MakeObstacle("Bush", center + new Vector2(-halfWide * 0.58f, -halfTall * 0.50f), bush, 1.35f);
        MakeObstacle("Bush", center + new Vector2(halfWide * 0.55f, halfTall * 0.42f), bush, 1.4f);
        MakeObstacle("Bush", center + new Vector2(halfWide * 0.16f, -halfTall * 0.62f), bush, 1.2f);
    }

    private void MakeWall(string name, Vector2 spot, Vector2 size, Sprite sprite)
    {
        GameObject wall = new GameObject(name);
        wall.transform.SetParent(yard.transform);
        wall.transform.position = spot;
        SpriteRenderer picture = wall.AddComponent<SpriteRenderer>();
        picture.sprite = sprite;
        picture.drawMode = SpriteDrawMode.Tiled;
        picture.size = size;
        picture.sortingOrder = 2;
        wall.AddComponent<BoxCollider2D>().size = size;
    }

    private void MakeObstacle(string name, Vector2 spot, Sprite sprite, float scale)
    {
        GameObject thing = new GameObject(name);
        thing.transform.SetParent(yard.transform);
        thing.transform.position = spot;
        thing.transform.localScale = Vector3.one * scale;
        SpriteRenderer picture = thing.AddComponent<SpriteRenderer>();
        picture.sprite = sprite;
        picture.sortingOrder = 1;
        CircleCollider2D hitbox = thing.AddComponent<CircleCollider2D>();
        hitbox.radius = 0.42f;
        obstacles.Add(hitbox);
    }

    private void MakeFlower(Vector2 spot, Sprite sprite)
    {
        GameObject flower = new GameObject("Flower");
        flower.transform.SetParent(yard.transform);
        flower.transform.position = spot;
        flower.AddComponent<SpriteRenderer>().sprite = sprite;
    }

    private Sprite MakeSprite(Texture2D pixels, int pixelsPerUnit)
    {
        pixels.filterMode = FilterMode.Point;
        pixels.wrapMode = TextureWrapMode.Repeat;
        Sprite sprite = Sprite.Create(pixels, new Rect(0, 0, pixels.width, pixels.height),
            new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
        artwork.Add(pixels);
        artwork.Add(sprite);
        return sprite;
    }

    private static Texture2D DrawGrass(bool flowerPatch)
    {
        Texture2D pixels = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color[] colors = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            int fleck = (x * 17 + y * 43 + x * y * 3) % 37;
            if (flowerPatch)
                colors[y * 32 + x] = fleck < 2 ? new Color(0.90f, 0.60f, 0.75f)
                    : fleck < 5 ? new Color(0.70f, 0.70f, 0.26f)
                    : new Color(0.38f, 0.58f, 0.25f);
            else
                colors[y * 32 + x] = fleck < 3 ? new Color(0.28f, 0.48f, 0.20f)
                    : fleck < 7 ? new Color(0.36f, 0.57f, 0.25f)
                    : new Color(0.32f, 0.53f, 0.23f);
        }
        pixels.SetPixels(colors);
        pixels.Apply();
        return pixels;
    }

    private static Texture2D DrawFence()
    {
        Texture2D pixels = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        Color[] colors = new Color[16 * 16];
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
            colors[y * 16 + x] = x == 0 || x == 8 ? new Color(0.27f, 0.14f, 0.07f)
                : y == 4 || y == 11 ? new Color(0.66f, 0.40f, 0.19f)
                : new Color(0.49f, 0.27f, 0.12f);
        pixels.SetPixels(colors);
        pixels.Apply();
        return pixels;
    }

    private static Texture2D DrawRock()
    {
        Texture2D pixels = new Texture2D(24, 24, TextureFormat.RGBA32, false);
        Color[] colors = new Color[24 * 24];
        for (int y = 0; y < 24; y++)
        for (int x = 0; x < 24; x++)
        {
            float shape = Mathf.Pow((x - 11.5f) / 10f, 2) + Mathf.Pow((y - 10f) / 7f, 2);
            if (shape > 1f) continue;
            colors[y * 24 + x] = shape > 0.82f ? new Color(0.22f, 0.27f, 0.24f)
                : y > 11 && x < 13 ? new Color(0.66f, 0.72f, 0.65f)
                : new Color(0.42f, 0.49f, 0.45f);
        }
        pixels.SetPixels(colors);
        pixels.Apply();
        return pixels;
    }

    private static Texture2D DrawBush()
    {
        Texture2D pixels = new Texture2D(24, 24, TextureFormat.RGBA32, false);
        Color[] colors = new Color[24 * 24];
        for (int y = 0; y < 24; y++)
        for (int x = 0; x < 24; x++)
        {
            float left = (new Vector2(x - 7, y - 11)).sqrMagnitude;
            float right = (new Vector2(x - 17, y - 11)).sqrMagnitude;
            float top = (new Vector2(x - 12, y - 16)).sqrMagnitude;
            bool leaf = left < 36 || right < 36 || top < 40;
            colors[y * 24 + x] = leaf ? (x + y) % 7 < 2
                ? new Color(0.34f, 0.62f, 0.20f) : new Color(0.13f, 0.38f, 0.15f)
                : x >= 10 && x <= 13 && y >= 3 && y <= 9
                ? new Color(0.34f, 0.19f, 0.09f) : Color.clear;
        }
        pixels.SetPixels(colors);
        pixels.Apply();
        return pixels;
    }

    private static Texture2D DrawFlower()
    {
        Texture2D pixels = new Texture2D(24, 24, TextureFormat.RGBA32, false);
        Color[] colors = new Color[24 * 24];
        for (int y = 0; y < 24; y++)
        for (int x = 0; x < 24; x++)
        {
            float petal = (new Vector2(x - 12, y - 18)).sqrMagnitude;
            bool leaves = (new Vector2(x - 8, y - 8)).sqrMagnitude < 16 ||
                          (new Vector2(x - 16, y - 8)).sqrMagnitude < 16;
            bool stem = x >= 11 && x <= 13 && y >= 3 && y <= 17;
            colors[y * 24 + x] = petal < 30 ? new Color(0.93f, 0.42f, 0.66f)
                : stem || leaves ? new Color(0.15f, 0.43f, 0.18f) : Color.clear;
            if (petal < 7) colors[y * 24 + x] = new Color(1f, 0.82f, 0.22f);
        }
        pixels.SetPixels(colors);
        pixels.Apply();
        return pixels;
    }

    private void OnDestroy()
    {
        if (yard != null) Destroy(yard);
        foreach (Object piece in artwork)
            if (piece != null) Destroy(piece);
    }
}
