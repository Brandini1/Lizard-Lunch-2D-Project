using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SetupLizardLunch
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Lizard Lunch/Set Up Movement Scene")]
    public static void SetUpMovementScene()
    {
        string[] directions = { "down", "up", "left", "right" };
        foreach (string direction in directions)
        {
            string texturePath = $"Assets/Resources/Lizard/walk_{direction}.png";
            TextureImporter sheetImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (sheetImporter == null)
                throw new System.IO.FileNotFoundException("Lizard sprite sheet is missing", texturePath);

            ConfigureSheet(sheetImporter);
            sheetImporter.SaveAndReimport();
        }

        Scene movementScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("Lizard") ?? new GameObject("Lizard");

        player.transform.position = Vector3.zero;
        if (player.GetComponent<SpriteRenderer>() == null)
            player.AddComponent<SpriteRenderer>();

        Rigidbody2D physicsBody = player.GetComponent<Rigidbody2D>() ?? player.AddComponent<Rigidbody2D>();
        physicsBody.gravityScale = 0f;
        physicsBody.freezeRotation = true;
        physicsBody.interpolation = RigidbodyInterpolation2D.Interpolate;

        CapsuleCollider2D hitbox = player.GetComponent<CapsuleCollider2D>() ?? player.AddComponent<CapsuleCollider2D>();
        hitbox.size = new Vector2(0.55f, 1.25f);

        if (player.GetComponent<LizardMovement>() == null)
            player.AddComponent<LizardMovement>();
        if (player.GetComponent<LizardTongue>() == null)
            player.AddComponent<LizardTongue>();
        if (player.GetComponent<BugGame>() == null)
            player.AddComponent<BugGame>();

        Camera gameCamera = Camera.main;
        if (gameCamera != null)
        {
            gameCamera.orthographic = true;
            gameCamera.orthographicSize = 5f;
            gameCamera.backgroundColor = new Color(0.18f, 0.31f, 0.20f);
        }

        EditorSceneManager.MarkSceneDirty(movementScene);
        EditorSceneManager.SaveScene(movementScene);
        Debug.Log("Lizard Lunch is ready. Move with WASD or arrow keys, and click to catch flies.");
    }

    internal static void ConfigureSheet(TextureImporter sheetImporter)
    {
        sheetImporter.textureType = TextureImporterType.Default;
        sheetImporter.maxTextureSize = 4096;
        sheetImporter.textureCompression = TextureImporterCompression.Uncompressed;
        sheetImporter.mipmapEnabled = false;
        sheetImporter.alphaIsTransparency = true;
        sheetImporter.npotScale = TextureImporterNPOTScale.None;
        sheetImporter.filterMode = FilterMode.Point;
    }
}

// keep each 2172x724 strip at its original size; unity's default 2048 limit
// would shrink it and prevent the three 724x724 frames from being sliced.
public sealed class LizardTextureImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Lizard/"))
            return;

        SetupLizardLunch.ConfigureSheet((TextureImporter)assetImporter);
    }
}
