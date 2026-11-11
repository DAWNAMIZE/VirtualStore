using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Procedurally builds the Virtual Store scene: checkered floor, stocked
/// shelves, posters, hanging signs, lighting, post processing and the
/// first-person player. Everything uses a small set of shared materials so
/// the WebGL build stays light.
///
/// Menu: Tools > Virtual Store > Build Store Scene
/// </summary>
public static class VirtualStoreBuilder
{
    const string ScenePath = "Assets/Scenes/StoreAisle.unity";
    const string MaterialPath = "Assets/Materials";
    const string TexturePath = "Assets/Textures";
    const string VolumeProfilePath = "Assets/Settings/StoreVolumeProfile.asset";

    const float FloorWidth = 5.6f;
    const float FloorLength = 28f;
    const float CeilingHeight = 3.25f;
    const float ShelfFrontX = 1.62f;
    const float ShelfDepth = 0.42f;
    const float ShelfCenterX = ShelfFrontX + ShelfDepth * 0.5f;
    const float ShelfBackX = ShelfFrontX + ShelfDepth;
    const float AisleLength = 24f;
    const float CaseHeight = 0.29f;

    static readonly string[] PosterNames =
    {
        "NIGHTHAWKS", "LONG SHOT", "NO HOPE", "CAUTION", "MIDNIGHT REWIND"
    };

    static readonly string[] Genres = { "HORROR", "ACTION", "COMEDY", "SCI-FI", "DRAMA" };

    static readonly string[] Titles =
    {
        "THE LAST REWIND", "MIDNIGHT STATIC", "NEON HIGHWAY", "VHS NIGHTMARE", "PALACE OF STATIC",
        "REEL FEAR", "TAPE 13", "THE QUIET SIGNAL", "ROADSIDE ATTRACTION", "GHOST IN THE AISLE",
        "NIGHT SHIFT", "DEAD AIR", "THE LONG TAKE", "FRIDAY THE 14TH STREET", "CINEMA OF SHADOWS",
        "FAST FORWARD", "REWIND TO HELL", "THE FINAL CUT", "SOFT FOCUS", "BETA MAX",
        "TRACKING ERROR", "SNOW STATIC", "THE LAST MATINEE", "PURPLE SUNSET", "MOTEL ZERO",
        "DRIVE-IN MASSACRE", "THE PROJECTIONIST", "SIGNAL LOST", "ANALOG DREAMS", "THE HORROR SECTION",
        "SUMMER OF STATIC", "CASSETTE 88", "NIGHT VISIONS", "THE VIDEO WITCH", "DOUBLE FEATURE",
        "LATE FEE", "BE KIND REWIND", "THE GRAIN", "HALF-LIGHT", "CHANNEL ZERO"
    };

    class StoreMaterials
    {
        public Material floor;
        public Material ceiling;
        public Material wall;
        public Material sideWall;
        public Material trim;
        public Material shelfMetal;
        public Material shelfBoard;
        public Material shelfBack;
        public Material labelStrip;
        public Material frame;
        public Material signBoard;
        public Material lightPanel;
        public Material neon;
        public Material[] cases;
        public Material[] posters;
    }

    // ------------------------------------------------------------------ menu

    [MenuItem("Tools/Virtual Store/Build Store Scene", priority = 0)]
    public static void BuildStoreScene()
    {
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets", "Textures");
        AssetDatabase.Refresh();

        StoreMaterials materials = BuildMaterials();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var root = new GameObject("Virtual Store").transform;

        BuildRoom(root, materials);
        BuildShelvesAndCases(root, materials);
        BuildWallDressing(root, materials);
        BuildSigns(root, materials);
        BuildLighting(root, materials);
        BuildPlayer(root);
        BuildPostProcessing(root);
        ConfigureLighting();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Virtual Store] Scene built and saved to " + ScenePath);
    }

    [MenuItem("Tools/Virtual Store/Open Store Scene", priority = 1)]
    public static void OpenStoreScene()
    {
        if (!File.Exists(ScenePath))
        {
            BuildStoreScene();
            return;
        }

        EditorSceneManager.OpenScene(ScenePath);
    }

    [MenuItem("Tools/Virtual Store/Build WebGL", priority = 20)]
    public static void BuildWebGL()
    {
        if (!File.Exists(ScenePath))
            BuildStoreScene();

        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.runInBackground = true;

        // Brotli + decompression fallback means the build works on any plain
        // static file server (no custom Content-Encoding headers required).
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.memorySize = 256;

        Directory.CreateDirectory("Builds/WebGL");

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            Debug.Log($"[Virtual Store] WebGL build succeeded: {report.summary.outputPath} ({report.summary.totalSize / 1048576f:F1} MB)");
        else
            Debug.LogError("[Virtual Store] WebGL build failed: " + report.summary.result);
    }

    // -------------------------------------------------------------- materials

    static StoreMaterials BuildMaterials()
    {
        BuildTextures();

        var materials = new StoreMaterials();

        materials.floor = MakeLit(MaterialPath + "/Floor.mat", Color.white, 0.62f, 0.05f,
            LoadTexture("FloorChecker"), new Vector2(4.67f, 23.33f));
        materials.ceiling = MakeLit(MaterialPath + "/Ceiling.mat", new Color(0.20f, 0.20f, 0.22f), 0.12f, 0f);
        materials.wall = MakeLit(MaterialPath + "/Wall.mat", new Color(0.24f, 0.15f, 0.17f), 0.15f, 0f);
        materials.sideWall = MakeLit(MaterialPath + "/SideWall.mat", new Color(0.19f, 0.17f, 0.18f), 0.12f, 0f);
        materials.trim = MakeLit(MaterialPath + "/Trim.mat", new Color(0.42f, 0.33f, 0.22f), 0.3f, 0.1f);
        materials.shelfMetal = MakeLit(MaterialPath + "/ShelfMetal.mat", new Color(0.13f, 0.13f, 0.15f), 0.55f, 0.7f);
        materials.shelfBoard = MakeLit(MaterialPath + "/ShelfBoard.mat", new Color(0.30f, 0.19f, 0.14f), 0.35f, 0f);
        materials.shelfBack = MakeLit(MaterialPath + "/ShelfBack.mat", new Color(0.17f, 0.12f, 0.10f), 0.2f, 0f);
        materials.labelStrip = MakeLit(MaterialPath + "/LabelStrip.mat", new Color(0.82f, 0.79f, 0.70f), 0.3f, 0f);
        materials.frame = MakeLit(MaterialPath + "/Frame.mat", new Color(0.05f, 0.05f, 0.06f), 0.35f, 0.2f);
        materials.signBoard = MakeLit(MaterialPath + "/SignBoard.mat", new Color(0.88f, 0.84f, 0.74f), 0.22f, 0f);
        materials.lightPanel = MakeEmissive(MaterialPath + "/LightPanel.mat", new Color(1f, 0.93f, 0.82f), 3.6f);
        materials.neon = MakeEmissive(MaterialPath + "/Neon.mat", new Color(1f, 0.10f, 0.18f), 5f);

        var caseColors = new[]
        {
            new Color(0.62f, 0.09f, 0.10f),
            new Color(0.08f, 0.16f, 0.35f),
            new Color(0.05f, 0.30f, 0.32f),
            new Color(0.72f, 0.55f, 0.12f),
            new Color(0.28f, 0.12f, 0.40f),
            new Color(0.78f, 0.74f, 0.65f),
            new Color(0.10f, 0.10f, 0.11f),
            new Color(0.60f, 0.28f, 0.08f)
        };

        Texture2D caseTexture = LoadTexture("CaseSpine");
        materials.cases = new Material[caseColors.Length];

        for (int i = 0; i < caseColors.Length; i++)
        {
            // Lift the spine colours slightly so the aisle reads clearly under
            // the store lighting without washing out the genre palette.
            Color spine = Color.Lerp(caseColors[i], Color.white, 0.22f);
            Material material = MakeLit($"{MaterialPath}/Case_{i:00}.mat", spine, 0.42f, 0.02f, caseTexture);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(material);
            materials.cases[i] = material;
        }

        materials.posters = new Material[PosterNames.Length];
        for (int i = 0; i < PosterNames.Length; i++)
        {
            materials.posters[i] = MakeLit($"{MaterialPath}/Poster_{i + 1:00}.mat", Color.white, 0.28f, 0f,
                LoadTexture("Poster_" + (i + 1)));
        }

        AssetDatabase.SaveAssets();
        return materials;
    }

    static void BuildTextures()
    {
        SaveTexture(CreateCheckerTexture(), TexturePath + "/FloorChecker.png",
            FilterMode.Point, TextureWrapMode.Repeat, 512);

        SaveTexture(CreateCaseTexture(), TexturePath + "/CaseSpine.png",
            FilterMode.Bilinear, TextureWrapMode.Repeat, 256);

        for (int i = 0; i < PosterNames.Length; i++)
        {
            SaveTexture(CreatePoster(i), TexturePath + "/Poster_" + (i + 1) + ".png",
                FilterMode.Bilinear, TextureWrapMode.Clamp, 1024);
        }
    }

    static Material MakeLit(string path, Color color, float smoothness, float metallic,
        Texture2D texture = null, Vector2 tiling = default)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            AssetDatabase.DeleteAsset(path);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[Virtual Store] Could not find the URP Lit shader.");
            return null;
        }

        var material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);

        if (texture != null)
        {
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", tiling == Vector2.zero ? Vector2.one : tiling);
        }

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static Material MakeEmissive(string path, Color color, float intensity)
    {
        Material material = MakeLit(path, color, 0f, 0f);
        if (material == null) return null;

        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * intensity);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(material);
        return material;
    }

    static Texture2D LoadTexture(string name)
    {
        string path = TexturePath + "/" + name + ".png";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
            Debug.LogError("[Virtual Store] Missing texture: " + path);
        return texture;
    }

    static void SaveTexture(Texture2D texture, string path, FilterMode filter, TextureWrapMode wrap, int maxSize)
    {
        File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("[Virtual Store] Could not import texture: " + path);
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = wrap;
        importer.filterMode = filter;
        importer.anisoLevel = 4;
        importer.maxTextureSize = maxSize;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    // --------------------------------------------------------------- geometry

    static void BuildRoom(Transform root, StoreMaterials materials)
    {
        var room = new GameObject("Room").transform;
        room.SetParent(root, false);

        Box(room, "Floor", new Vector3(0f, -0.05f, 0f),
            new Vector3(FloorWidth, 0.1f, FloorLength), materials.floor, true, false);

        Box(room, "Ceiling", new Vector3(0f, CeilingHeight + 0.05f, 0f),
            new Vector3(FloorWidth, 0.1f, FloorLength), materials.ceiling, true, false);

        Box(room, "Wall Left", new Vector3(-2.8f, CeilingHeight * 0.5f, 0f),
            new Vector3(0.1f, CeilingHeight, FloorLength), materials.sideWall);
        Box(room, "Wall Right", new Vector3(2.8f, CeilingHeight * 0.5f, 0f),
            new Vector3(0.1f, CeilingHeight, FloorLength), materials.sideWall);

        Box(room, "Wall Back", new Vector3(0f, CeilingHeight * 0.5f, 14f),
            new Vector3(FloorWidth, CeilingHeight, 0.1f), materials.wall);
        Box(room, "Wall Front", new Vector3(0f, CeilingHeight * 0.5f, -14f),
            new Vector3(FloorWidth, CeilingHeight, 0.1f), materials.wall);

        Box(room, "Baseboard Back", new Vector3(0f, 0.08f, 13.93f),
            new Vector3(FloorWidth, 0.16f, 0.03f), materials.trim, false);
        Box(room, "Baseboard Front", new Vector3(0f, 0.08f, -13.93f),
            new Vector3(FloorWidth, 0.16f, 0.03f), materials.trim, false);
        Box(room, "Baseboard Left", new Vector3(-2.735f, 0.08f, 0f),
            new Vector3(0.03f, 0.16f, FloorLength), materials.trim, false);
        Box(room, "Baseboard Right", new Vector3(2.735f, 0.08f, 0f),
            new Vector3(0.03f, 0.16f, FloorLength), materials.trim, false);
    }

    static void BuildShelvesAndCases(Transform root, StoreMaterials materials)
    {
        var shelfRoot = new GameObject("Shelves").transform;
        shelfRoot.SetParent(root, false);

        var random = new System.Random(20241111);
        float[] levels = { 0.42f, 0.88f, 1.34f, 1.80f };
        int caseCount = 0;

        for (int sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            float side = sideIndex == 0 ? -1f : 1f;

            Box(shelfRoot, "Shelf Back", new Vector3(side * ShelfBackX, 1.075f, 0f),
                new Vector3(0.04f, 2.15f, AisleLength), materials.shelfBack);
            Box(shelfRoot, "Shelf Base", new Vector3(side * ShelfCenterX, 0.10f, 0f),
                new Vector3(0.44f, 0.20f, AisleLength), materials.shelfBoard);
            Box(shelfRoot, "Shelf Top", new Vector3(side * ShelfCenterX, 2.13f, 0f),
                new Vector3(0.46f, 0.06f, AisleLength), materials.shelfBoard);

            for (int i = 0; i <= 12; i++)
            {
                float z = -12f + i * 2f;
                Box(shelfRoot, "Shelf Upright", new Vector3(side * (ShelfBackX - 0.035f), 1.06f, z),
                    new Vector3(0.06f, 2.12f, 0.06f), materials.shelfMetal);
            }

            Box(shelfRoot, "Shelf End", new Vector3(side * ShelfCenterX, 1.06f, -12.02f),
                new Vector3(0.46f, 2.12f, 0.05f), materials.shelfBoard);
            Box(shelfRoot, "Shelf End", new Vector3(side * ShelfCenterX, 1.06f, 12.02f),
                new Vector3(0.46f, 2.12f, 0.05f), materials.shelfBoard);

            foreach (float level in levels)
            {
                Box(shelfRoot, "Shelf Board", new Vector3(side * ShelfCenterX, level, 0f),
                    new Vector3(ShelfDepth, 0.035f, AisleLength), materials.shelfBoard);

                Box(shelfRoot, "Price Strip", new Vector3(side * (ShelfFrontX + 0.006f), level + 0.012f, 0f),
                    new Vector3(0.012f, 0.055f, AisleLength), materials.labelStrip, false);

                float z = -11.92f;

                while (z < 11.92f)
                {
                    // Occasional empty gap, like a store that has been picked over.
                    if (random.NextDouble() < 0.10)
                    {
                        z += 0.3f + (float)random.NextDouble() * 0.55f;
                        continue;
                    }

                    int run = 6 + random.Next(11);

                    for (int i = 0; i < run && z < 11.92f; i++)
                    {
                        bool faceOut = random.NextDouble() < 0.022;

                        float thickness = 0.062f + (float)random.NextDouble() * 0.03f;
                        Vector3 size = faceOut
                            ? new Vector3(0.032f, CaseHeight, 0.19f)
                            : new Vector3(0.15f, CaseHeight, thickness);

                        float x = side * (ShelfFrontX + 0.012f + size.x * 0.5f);
                        float y = level + 0.0175f + CaseHeight * 0.5f;

                        GameObject caseObject = Box(shelfRoot, "Movie Case",
                            new Vector3(x, y, z + size.z * 0.5f), size,
                            materials.cases[random.Next(materials.cases.Length)]);

                        var movie = caseObject.AddComponent<MovieCase>();
                        movie.title = Titles[random.Next(Titles.Length)];
                        movie.year = 1978 + random.Next(22);
                        movie.genre = Genres[Mathf.Clamp((int)((z + 12f) / 4.8f), 0, Genres.Length - 1)];
                        movie.baseMaterial = caseObject.GetComponent<MeshRenderer>().sharedMaterial;

                        Color baseColor = movie.baseMaterial.GetColor("_BaseColor");
                        movie.accent = Color.Lerp(baseColor, Color.white, 0.6f);
                        movie.accent.a = 1f;

                        z += size.z + 0.012f;
                        caseCount++;
                    }

                    z += 0.35f + (float)random.NextDouble() * 0.5f;
                }
            }
        }

        Debug.Log($"[Virtual Store] Placed {caseCount} interactive movie cases.");
    }

    static void BuildWallDressing(Transform root, StoreMaterials materials)
    {
        var posters = new GameObject("Posters").transform;
        posters.SetParent(root, false);

        float[] xs = { -2.0f, -1.0f, 0f, 1.0f, 2.0f };

        for (int i = 0; i < PosterNames.Length; i++)
        {
            Box(posters, "Poster Frame", new Vector3(xs[i], 1.76f, 13.94f),
                new Vector3(1.0f, 1.43f, 0.025f), materials.frame, false);
            Box(posters, "Poster", new Vector3(xs[i], 1.76f, 13.915f),
                new Vector3(0.92f, 1.35f, 0.02f), materials.posters[i], false);

            CreateWorldText(posters, "Poster Title", PosterNames[i], 0.105f, Color.white,
                new Vector3(xs[i], 1.14f, 13.895f), Quaternion.identity);
        }

        CreateWorldText(posters, "Store Neon", "NIGHTHAWKS VIDEO", 0.30f, new Color(1f, 0.15f, 0.22f),
            new Vector3(0f, 2.72f, 13.88f), Quaternion.identity);

        Box(posters, "Neon Tube", new Vector3(0f, 2.50f, 13.89f),
            new Vector3(2.9f, 0.025f, 0.02f), materials.neon, false, false);

        // Front wall dressing.
        Box(posters, "Front Door", new Vector3(1.35f, 1.08f, -13.93f),
            new Vector3(1.05f, 2.16f, 0.04f), materials.frame, false);
        Box(posters, "Door Panel", new Vector3(1.35f, 1.35f, -13.90f),
            new Vector3(0.85f, 1.05f, 0.015f), materials.shelfMetal, false);

        CreateWorldText(posters, "Exit Sign", "EXIT", 0.14f, new Color(0.55f, 1f, 0.7f),
            new Vector3(1.35f, 2.36f, -13.89f), Quaternion.Euler(0f, 180f, 0f));
        CreateWorldText(posters, "Rewind Sign", "BE KIND  \u00B7  REWIND", 0.16f, new Color(0.85f, 0.78f, 0.6f),
            new Vector3(-0.9f, 2.30f, -13.89f), Quaternion.Euler(0f, 180f, 0f));
    }

    static void BuildSigns(Transform root, StoreMaterials materials)
    {
        var signs = new GameObject("Signage").transform;
        signs.SetParent(root, false);

        string[] hanging = { "CAUTION", "NO HOPE", "NIGHTHAWKS", "LONG SHOT" };
        float[] zPositions = { -8.5f, -3f, 2.5f, 8f };

        for (int i = 0; i < hanging.Length; i++)
        {
            float x = (i % 2 == 0) ? -0.62f : 0.62f;
            float z = zPositions[i];

            Box(signs, "Hanging Sign Frame", new Vector3(x, 2.56f, z),
                new Vector3(1.21f, 0.61f, 0.04f), materials.frame, false);
            Box(signs, "Hanging Sign Board", new Vector3(x, 2.56f, z),
                new Vector3(1.15f, 0.55f, 0.055f), materials.signBoard, false);
            Box(signs, "Sign Stripe", new Vector3(x, 2.805f, z - 0.03f),
                new Vector3(1.15f, 0.045f, 0.015f), materials.cases[i % materials.cases.Length], false);

            Box(signs, "Sign Rod", new Vector3(x - 0.42f, 3.02f, z),
                new Vector3(0.018f, 0.46f, 0.018f), materials.shelfMetal, false, false);
            Box(signs, "Sign Rod", new Vector3(x + 0.42f, 3.02f, z),
                new Vector3(0.018f, 0.46f, 0.018f), materials.shelfMetal, false, false);

            CreateWorldText(signs, "Sign Text Front", hanging[i], 0.17f, new Color(0.06f, 0.05f, 0.05f),
                new Vector3(x, 2.56f, z - 0.042f), Quaternion.identity);
            CreateWorldText(signs, "Sign Text Back", hanging[i], 0.17f, new Color(0.06f, 0.05f, 0.05f),
                new Vector3(x, 2.56f, z + 0.042f), Quaternion.Euler(0f, 180f, 0f));
        }

        string[] sections = { "HORROR", "ACTION", "COMEDY", "SCI-FI", "DRAMA" };
        float[] sectionZ = { -9.5f, -4.75f, 0f, 4.75f, 9.5f };

        for (int sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            float side = sideIndex == 0 ? -1f : 1f;
            Quaternion rotation = side < 0f ? Quaternion.Euler(0f, -90f, 0f) : Quaternion.Euler(0f, 90f, 0f);

            for (int i = 0; i < sections.Length; i++)
            {
                float z = sectionZ[i];

                Box(signs, "Section Header Frame", new Vector3(side * 1.60f, 2.44f, z),
                    new Vector3(0.06f, 0.36f, 1.56f), materials.frame, false);
                Box(signs, "Section Header", new Vector3(side * 1.585f, 2.44f, z),
                    new Vector3(0.04f, 0.32f, 1.5f), materials.signBoard, false);

                CreateWorldText(signs, "Section Text", sections[i], 0.15f, new Color(0.06f, 0.05f, 0.05f),
                    new Vector3(side * 1.55f, 2.44f, z), rotation);
            }
        }
    }

    static void BuildLighting(Transform root, StoreMaterials materials)
    {
        var lighting = new GameObject("Lighting").transform;
        lighting.SetParent(root, false);

        var keyObject = new GameObject("Key Light");
        keyObject.transform.SetParent(lighting, false);
        keyObject.transform.rotation = Quaternion.Euler(52f, -28f, 0f);

        var key = keyObject.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1f, 0.94f, 0.85f);
        key.intensity = 1.1f;
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.6f;

        float[] lightZ = { -10f, -5f, 0f, 5f, 10f };

        foreach (float z in lightZ)
        {
            Box(lighting, "Ceiling Fixture", new Vector3(0f, CeilingHeight - 0.06f, z),
                new Vector3(0.34f, 0.08f, 1.9f), materials.shelfMetal, false, false);
            Box(lighting, "Ceiling Panel", new Vector3(0f, CeilingHeight - 0.105f, z),
                new Vector3(0.26f, 0.02f, 1.78f), materials.lightPanel, false, false);

            var lightObject = new GameObject("Ceiling Light");
            lightObject.transform.SetParent(lighting, false);
            lightObject.transform.localPosition = new Vector3(0f, CeilingHeight - 0.35f, z);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 13f;
            light.intensity = 14f;
            light.color = new Color(1f, 0.92f, 0.80f);
            light.shadows = LightShadows.None;
        }

        var washObject = new GameObject("Poster Wash");
        washObject.transform.SetParent(lighting, false);
        washObject.transform.localPosition = new Vector3(0f, 2.35f, 12.55f);

        var wash = washObject.AddComponent<Light>();
        wash.type = LightType.Point;
        wash.range = 7f;
        wash.intensity = 9f;
        wash.color = new Color(1f, 0.88f, 0.75f);
        wash.shadows = LightShadows.None;

        var neonObject = new GameObject("Neon Glow");
        neonObject.transform.SetParent(lighting, false);
        neonObject.transform.localPosition = new Vector3(0f, 2.65f, 12.8f);

        var neonLight = neonObject.AddComponent<Light>();
        neonLight.type = LightType.Point;
        neonLight.range = 4.5f;
        neonLight.intensity = 5f;
        neonLight.color = new Color(1f, 0.16f, 0.24f);
        neonLight.shadows = LightShadows.None;
    }

    static void BuildPlayer(Transform root)
    {
        var player = new GameObject("Player");
        player.transform.SetParent(root, false);
        player.transform.localPosition = new Vector3(0f, 0.05f, -12.6f);

        var character = player.AddComponent<CharacterController>();
        character.height = 1.8f;
        character.radius = 0.3f;
        character.center = new Vector3(0f, 0.9f, 0f);
        character.skinWidth = 0.03f;
        character.stepOffset = 0.25f;
        character.slopeLimit = 45f;

        var cameraObject = new GameObject("Player Camera");
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.62f, 0f);

        var camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.fieldOfView = 68f;
        camera.nearClipPlane = 0.03f;
        camera.farClipPlane = 60f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.015f, 0.015f, 0.02f);
        camera.allowHDR = true;
        camera.allowMSAA = true;

        cameraObject.AddComponent<AudioListener>();

        var cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        cameraData.antialiasingQuality = AntialiasingQuality.Medium;
        cameraData.renderShadows = true;

        var hud = player.AddComponent<VirtualStoreHUD>();

        var controller = player.AddComponent<FirstPersonController>();
        controller.cameraTransform = cameraObject.transform;
        controller.hud = hud;

        var interaction = player.AddComponent<StoreInteraction>();
        interaction.viewCamera = camera;
        interaction.hud = hud;

        player.AddComponent<StoreAmbience>();
    }

    static void BuildPostProcessing(Transform root)
    {
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath) != null)
            AssetDatabase.DeleteAsset(VolumeProfilePath);

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, VolumeProfilePath);

        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.45f);
        bloom.threshold.Override(1.05f);
        bloom.scatter.Override(0.6f);
        bloom.tint.Override(new Color(1f, 0.9f, 0.85f));
        AssetDatabase.AddObjectToAsset(bloom, profile);

        var tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.ACES);
        AssetDatabase.AddObjectToAsset(tonemapping, profile);

        var colorAdjustments = profile.Add<ColorAdjustments>(true);
        colorAdjustments.postExposure.Override(0.40f);
        colorAdjustments.contrast.Override(8f);
        colorAdjustments.saturation.Override(8f);
        AssetDatabase.AddObjectToAsset(colorAdjustments, profile);

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.18f);
        vignette.smoothness.Override(0.45f);
        AssetDatabase.AddObjectToAsset(vignette, profile);

        AssetDatabase.SaveAssets();

        var volumeObject = new GameObject("Post Processing");
        volumeObject.transform.SetParent(root, false);

        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = profile;
    }

    static void ConfigureLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.46f, 0.47f, 0.53f);
        RenderSettings.ambientEquatorColor = new Color(0.32f, 0.33f, 0.38f);
        RenderSettings.ambientGroundColor = new Color(0.16f, 0.16f, 0.19f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.075f, 0.07f, 0.085f);
        RenderSettings.fogStartDistance = 20f;
        RenderSettings.fogEndDistance = 60f;

        RenderSettings.skybox = null;
        RenderSettings.reflectionIntensity = 0.35f;
    }

    // ---------------------------------------------------------------- helpers

    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size,
        Material material, bool addCollider = true, bool castShadows = true)
    {
        var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = position;
        gameObject.transform.localScale = size;

        var renderer = gameObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

        if (!addCollider)
            Object.DestroyImmediate(gameObject.GetComponent<Collider>());

        gameObject.isStatic = true;
        return gameObject;
    }

    static TextMesh CreateWorldText(Transform parent, string name, string content, float height,
        Color color, Vector3 localPosition, Quaternion localRotation)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = localPosition;
        gameObject.transform.localRotation = localRotation;

        var text = gameObject.AddComponent<TextMesh>();
        text.text = content;
        text.font = EditorFont;
        text.fontSize = 96;
        text.characterSize = 0.1f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = color;
        text.richText = false;
        text.fontStyle = FontStyle.Normal;

        float lineHeight = text.fontSize * text.characterSize * 0.1f;
        float scale = height / Mathf.Max(0.001f, lineHeight);
        gameObject.transform.localScale = new Vector3(scale, scale, scale);

        var renderer = gameObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (text.font != null)
                renderer.sharedMaterial = text.font.material;
        }

        return text;
    }

    static Font EditorFont
    {
        get
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }
    }

    static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child))
            AssetDatabase.CreateFolder(parent, child);
    }

    // ------------------------------------------------------------- textures

    static Texture2D CreateCheckerTexture()
    {
        const int size = 512;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];

        var light = new Color(0.90f, 0.90f, 0.88f);
        var dark = new Color(0.055f, 0.055f, 0.065f);
        var random = new System.Random(7);
        int half = size / 2;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool white = (x < half) ^ (y < half);
                Color color = white ? light : dark;

                float noise = ((float)random.NextDouble() - 0.5f) * 0.035f;
                color.r += noise;
                color.g += noise;
                color.b += noise;

                if (x % half < 2 || y % half < 2)
                    color *= 0.82f;

                pixels[y * size + x] = color;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    static Texture2D CreateCaseTexture()
    {
        const int width = 64;
        const int height = 256;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float value = 0.82f;

                // Paper label band.
                if (y > 138 && y < 214) value = 1f;
                if (y > 138 && y < 142) value = 0.45f;
                if (y > 210 && y < 214) value = 0.45f;

                // Barcode block near the bottom.
                if (y > 22 && y < 70)
                    value = (x / 3) % 2 == 0 ? 0.30f : 0.95f;

                // Top and bottom shading.
                if (y < 8) value *= 0.7f;
                if (y > height - 8) value *= 0.6f;

                pixels[y * width + x] = new Color(value, value, value, 1f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    static Texture2D CreatePoster(int index)
    {
        const int width = 512;
        const int height = 768;
        var pixels = new Color[width * height];

        switch (index)
        {
            case 0: DrawNighthawks(pixels, width, height); break;
            case 1: DrawLongShot(pixels, width, height); break;
            case 2: DrawNoHope(pixels, width, height); break;
            case 3: DrawCaution(pixels, width, height); break;
            default: DrawMidnightRewind(pixels, width, height); break;
        }

        FinishPoster(pixels, width, height, index + 11);

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    static void DrawNighthawks(Color[] pixels, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            float v = y / (height - 1f);
            Color color = v < 0.34f
                ? Color.Lerp(new Color(0.02f, 0.01f, 0.06f), new Color(0.10f, 0.02f, 0.15f), v / 0.34f)
                : Color.Lerp(new Color(0.22f, 0.04f, 0.24f), new Color(0.03f, 0.01f, 0.07f), (v - 0.34f) / 0.66f);

            float glow = Mathf.Exp(-Mathf.Pow((v - 0.34f) / 0.06f, 2f));
            color += new Color(0.35f, 0.05f, 0.16f) * glow;

            for (int x = 0; x < width; x++)
                pixels[y * width + x] = color;
        }

        int horizon = (int)(0.34f * height);

        for (int i = 0; i < 7; i++)
        {
            float t = Mathf.Pow((i + 1) / 7f, 1.7f);
            int y = horizon + (int)(t * (height - horizon));
            DrawLine(pixels, width, height, 0, y, width, y, new Color(0.75f, 0.12f, 0.5f, 0.85f), 1.2f);
        }

        for (int k = -5; k <= 5; k++)
        {
            float bx = width * 0.5f + k * (width / 5f);
            DrawLine(pixels, width, height, width * 0.5f, horizon, bx, 0, new Color(0.75f, 0.12f, 0.5f, 0.85f), 1.2f);
        }

        float cx = width * 0.5f;
        float cy = height * 0.47f;
        float radius = width * 0.21f;

        for (int y = (int)(cy - radius); y <= (int)(cy + radius); y++)
        {
            for (int x = (int)(cx - radius); x <= (int)(cx + radius); x++)
            {
                if (x < 0 || y < 0 || x >= width || y >= height) continue;

                float dx = x - cx;
                float dy = y - cy;
                if (dx * dx + dy * dy > radius * radius) continue;
                if (y < cy && (y / 11) % 2 == 0) continue;

                float t = Mathf.Clamp01((y - (cy - radius)) / (2f * radius));
                pixels[y * width + x] = Color.Lerp(new Color(1f, 0.28f, 0.55f), new Color(1f, 0.85f, 0.35f), t);
            }
        }

        var random = new System.Random(42);
        for (int i = 0; i < 160; i++)
        {
            int x = random.Next(width);
            int y = random.Next((int)(height * 0.55f), height);
            float brightness = 0.4f + (float)random.NextDouble() * 0.6f;
            pixels[y * width + x] = new Color(brightness, brightness, brightness * 1.1f);
        }
    }

    static void DrawLongShot(Color[] pixels, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            float v = y / (height - 1f);
            Color color = Color.Lerp(new Color(0.28f, 0.05f, 0.03f), new Color(0.95f, 0.55f, 0.12f), v);

            for (int x = 0; x < width; x++)
            {
                float diagonal = Mathf.Abs(y - (0.10f * height + x * 0.55f));
                if (diagonal < 16f)
                    color = Color.Lerp(color, new Color(0.07f, 0.02f, 0.02f), 0.9f);

                pixels[y * width + x] = color;
            }
        }

        DrawCircle(pixels, width, height, width * 0.60f, height * 0.58f, width * 0.20f,
            new Color(1f, 0.93f, 0.72f, 1f));

        for (int y = 0; y < height; y += 18)
            DrawLine(pixels, width, height, 0, y, width, y, new Color(0f, 0f, 0f, 0.10f), 0.8f);
    }

    static void DrawNoHope(Color[] pixels, int width, int height)
    {
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(0.015f, 0.015f, 0.02f);

        for (int x = 0; x < width; x += 32)
            DrawLine(pixels, width, height, x, 0, x, height, new Color(0.45f, 0.03f, 0.05f, 0.55f), 1f);
        for (int y = 0; y < height; y += 32)
            DrawLine(pixels, width, height, 0, y, width, y, new Color(0.45f, 0.03f, 0.05f, 0.55f), 1f);

        for (int x = 0; x < width; x += 128)
            DrawLine(pixels, width, height, x, 0, x, height, new Color(0.75f, 0.04f, 0.06f, 0.85f), 2f);

        float cx = width * 0.5f;
        float cy = height * 0.55f;
        float radius = width * 0.22f;

        for (int y = (int)(cy - radius - 4); y <= (int)(cy + radius + 4); y++)
        {
            for (int x = (int)(cx - radius - 4); x <= (int)(cx + radius + 4); x++)
            {
                if (x < 0 || y < 0 || x >= width || y >= height) continue;

                float dx = x - cx;
                float dy = y - cy;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                if (Mathf.Abs(distance - radius) < 3.5f)
                    pixels[y * width + x] = new Color(0.9f, 0.05f, 0.08f);
            }
        }

        DrawLine(pixels, width, height, width * 0.22f, height * 0.24f, width * 0.78f, height * 0.84f,
            new Color(0.95f, 0.06f, 0.08f, 0.95f), 12f);
    }

    static void DrawCaution(Color[] pixels, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            float v = y / (height - 1f);
            Color color = Color.Lerp(new Color(0.10f, 0.08f, 0.02f), new Color(0.55f, 0.42f, 0.06f), v);

            for (int x = 0; x < width; x++)
            {
                if (y < height * 0.40f)
                {
                    bool yellow = ((int)((x + y) / 26f)) % 2 == 0;
                    color = yellow ? new Color(0.92f, 0.72f, 0.08f) : new Color(0.05f, 0.05f, 0.05f);
                }

                pixels[y * width + x] = color;
            }
        }

        float topX = width * 0.5f;
        float topY = height * 0.73f;
        float leftX = width * 0.31f;
        float leftY = height * 0.46f;
        float rightX = width * 0.69f;
        float rightY = height * 0.46f;

        var warning = new Color(0.95f, 0.87f, 0.35f);
        DrawLine(pixels, width, height, topX, topY, leftX, leftY, warning, 7f);
        DrawLine(pixels, width, height, leftX, leftY, rightX, rightY, warning, 7f);
        DrawLine(pixels, width, height, rightX, rightY, topX, topY, warning, 7f);

        DrawLine(pixels, width, height, width * 0.5f, height * 0.52f, width * 0.5f, height * 0.63f, warning, 8f);
        DrawCircle(pixels, width, height, width * 0.5f, height * 0.49f, 4.5f, warning);
    }

    static void DrawMidnightRewind(Color[] pixels, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            float v = y / (height - 1f);
            Color color = Color.Lerp(new Color(0.02f, 0.10f, 0.11f), new Color(0.08f, 0.35f, 0.34f), v);

            for (int x = 0; x < width; x++)
                pixels[y * width + x] = color;
        }

        for (int y = 0; y < height; y += 4)
            DrawLine(pixels, width, height, 0, y, width, y, new Color(0f, 0f, 0f, 0.07f), 1f);

        float left = width * 0.18f;
        float right = width * 0.82f;
        float bottom = height * 0.40f;
        float top = height * 0.70f;

        FillRect(pixels, width, height, left, bottom, right, top, new Color(0.03f, 0.03f, 0.04f));
        FillRect(pixels, width, height, left + 10f, bottom + 10f, right - 10f, top - 10f, new Color(0.05f, 0.05f, 0.06f));

        FillRect(pixels, width, height, left + 26f, bottom + 22f, right - 26f, bottom + 52f,
            new Color(0.85f, 0.80f, 0.64f));

        DrawCircle(pixels, width, height, width * 0.38f, height * 0.56f, width * 0.055f, new Color(0.92f, 0.92f, 0.88f));
        DrawCircle(pixels, width, height, width * 0.62f, height * 0.56f, width * 0.055f, new Color(0.92f, 0.92f, 0.88f));
        DrawCircle(pixels, width, height, width * 0.38f, height * 0.56f, width * 0.022f, new Color(0.06f, 0.06f, 0.07f));
        DrawCircle(pixels, width, height, width * 0.62f, height * 0.56f, width * 0.022f, new Color(0.06f, 0.06f, 0.07f));
    }

    static void FinishPoster(Color[] pixels, int width, int height, int seed)
    {
        var random = new System.Random(seed);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                Color color = pixels[index];

                float nx = (x - width * 0.5f) / (width * 0.5f);
                float ny = (y - height * 0.5f) / (height * 0.5f);
                float distance = Mathf.Sqrt(nx * nx * 0.75f + ny * ny);
                float vignette = Mathf.Clamp01(1f - 0.5f * Mathf.Pow(Mathf.Clamp01((distance - 0.35f) / 0.9f), 1.6f));
                color *= vignette;

                float band = Mathf.Clamp01((0.15f * height - y) / (0.15f * height));
                color = Color.Lerp(color, new Color(0.02f, 0.02f, 0.03f), band * 0.92f);

                float grain = ((float)random.NextDouble() - 0.5f) * 0.03f;
                color.r += grain;
                color.g += grain;
                color.b += grain;
                color.a = 1f;

                pixels[index] = color;
            }
        }
    }

    static void FillRect(Color[] pixels, int width, int height,
        float x0, float y0, float x1, float y1, Color color)
    {
        int minX = Mathf.Max(0, (int)x0);
        int maxX = Mathf.Min(width - 1, (int)x1);
        int minY = Mathf.Max(0, (int)y0);
        int maxY = Mathf.Min(height - 1, (int)y1);

        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                BlendPixel(pixels, width, height, x, y, color);
    }

    static void DrawCircle(Color[] pixels, int width, int height, float cx, float cy, float radius, Color color)
    {
        int minX = Mathf.Max(0, (int)(cx - radius - 1f));
        int maxX = Mathf.Min(width - 1, (int)(cx + radius + 1f));
        int minY = Mathf.Max(0, (int)(cy - radius - 1f));
        int maxY = Mathf.Min(height - 1, (int)(cy + radius + 1f));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                if (dx * dx + dy * dy <= radius * radius)
                    BlendPixel(pixels, width, height, x, y, color);
            }
        }
    }

    static void DrawLine(Color[] pixels, int width, int height,
        float x0, float y0, float x1, float y1, Color color, float thickness)
    {
        float dx = x1 - x0;
        float dy = y1 - y0;
        float length = Mathf.Sqrt(dx * dx + dy * dy);
        int steps = Mathf.Max(1, (int)(length * 2f));
        float radius = thickness * 0.5f;

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            float x = Mathf.Lerp(x0, x1, t);
            float y = Mathf.Lerp(y0, y1, t);

            if (radius <= 0.6f)
            {
                BlendPixel(pixels, width, height, Mathf.RoundToInt(x), Mathf.RoundToInt(y), color);
                continue;
            }

            int minX = Mathf.Max(0, (int)(x - radius));
            int maxX = Mathf.Min(width - 1, (int)(x + radius));
            int minY = Mathf.Max(0, (int)(y - radius));
            int maxY = Mathf.Min(height - 1, (int)(y + radius));

            for (int py = minY; py <= maxY; py++)
            {
                for (int px = minX; px <= maxX; px++)
                {
                    float ddx = px - x;
                    float ddy = py - y;
                    if (ddx * ddx + ddy * ddy <= radius * radius)
                        BlendPixel(pixels, width, height, px, py, color);
                }
            }
        }
    }

    static void BlendPixel(Color[] pixels, int width, int height, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return;

        int index = y * width + x;
        float alpha = Mathf.Clamp01(color.a);
        pixels[index] = Color.Lerp(pixels[index], new Color(color.r, color.g, color.b, 1f), alpha);
    }
}
