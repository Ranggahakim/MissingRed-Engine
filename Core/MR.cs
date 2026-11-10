using System.Numerics;
using MissingRed.ECS;
using MissingRed.FirstPerson;
using MissingRed.Rendering;
using Raylib_cs;

namespace MissingRed.Core;

/// <summary>
/// Custom Developer API khusus untuk MissingRed Engine (<c>MR.*</c>).
/// Dirancang agar pembuatan game First-Person dapat dilakukan hanya dalam 1 baris perintah singkat.
/// </summary>
public static class MR
{
    private static WorldData? _world;
    private static FirstPersonController? _fps;
    private static RenderPipeline? _renderer;
    private static readonly List<string> _logs = new(64);

    public static IReadOnlyList<string> Logs => _logs;

    /// <summary>Mengikat referensi inti engine ke API statis MR.</summary>
    public static void Initialize(WorldData world, FirstPersonController fps, RenderPipeline renderer)
    {
        _world = world;
        _fps = fps;
        _renderer = renderer;
        AssetManager.Initialize();
        Log("MissingRed Custom API (MR.*) & AssetManager siap digunakan.");
    }

    /// <summary>Memuat file Model 3D (.obj, .glb, .gltf, .iqm, .m3d) ke VRAM dan mengembalikan ID-nya.</summary>
    public static short LoadModel(string filePath) => AssetManager.LoadModel(filePath);

    /// <summary>Memuat file Tekstur 2D (.png, .jpg, .tga, .bmp) ke VRAM dan mengembalikan ID-nya.</summary>
    public static short LoadTexture(string filePath) => AssetManager.LoadTexture(filePath);

    /// <summary>Memunculkan entitas dengan Model 3D Custom yang sudah dimuat di AssetManager.</summary>
    public static int SpawnModel(
        string name,
        Vector3 position,
        Vector3 scale,
        short modelId,
        Color tint,
        short textureId = -1,
        EntityBehavior behavior = EntityBehavior.None)
    {
        if (_world == null) return -1;
        return _world.CreateEntity(
            name,
            position,
            scale,
            MeshType.CustomModel,
            tint,
            EntityFlags.Active | EntityFlags.SolidCollider,
            behavior,
            speed: 2.0f,
            amplitude: 0.5f,
            modelId: modelId,
            textureId: textureId);
    }

    /// <summary>Memuat file Model 3D (dan opsional file Tekstur) lalu langsung memunculkannya di dunia 3D dalam 1 baris.</summary>
    public static int SpawnModelFromFile(
        string name,
        Vector3 position,
        Vector3 scale,
        string modelPath,
        string? texturePath = null,
        Color? tint = null,
        EntityBehavior behavior = EntityBehavior.None)
    {
        short modelId = AssetManager.LoadModel(modelPath);
        short texId = string.IsNullOrWhiteSpace(texturePath) ? (short)-1 : AssetManager.LoadTexture(texturePath);
        MeshType meshType = modelId >= 0 ? MeshType.CustomModel : MeshType.Cube;
        if (_world == null) return -1;

        return _world.CreateEntity(
            name,
            position,
            scale,
            meshType,
            tint ?? Color.White,
            EntityFlags.Active | EntityFlags.SolidCollider,
            behavior,
            speed: 2.0f,
            amplitude: 0.5f,
            modelId: modelId,
            textureId: texId);
    }

    /// <summary>Memasang atau mengganti tekstur pada entitas yang sudah ada.</summary>
    public static void SetEntityTexture(int entityId, short textureId)
    {
        if (_world == null || entityId < 0 || entityId >= _world.Count) return;
        _world.Renders[entityId].TextureId = textureId;
    }

    /// <summary>Mencetak pesan ke Console panel di dalam Visual Editor.</summary>
    public static void Log(string message)
    {
        string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _logs.Add(entry);
        if (_logs.Count > 50)
            _logs.RemoveAt(0);
    }

    /// <summary>Memunculkan objek 3D Box/Cube ke dalam dunia game.</summary>
    public static int SpawnBox(string name, Vector3 position, Vector3 size, Color color, EntityBehavior behavior = EntityBehavior.None)
    {
        if (_world == null) return -1;
        return _world.CreateEntity(name, position, size, MeshType.Cube, color, EntityFlags.Active | EntityFlags.SolidCollider, behavior);
    }

    /// <summary>Memunculkan objek 3D Sphere (Bola) dengan dukungan Auto-LOD.</summary>
    public static int SpawnSphere(string name, Vector3 position, float diameter, Color color, EntityBehavior behavior = EntityBehavior.None)
    {
        if (_world == null) return -1;
        return _world.CreateEntity(name, position, new Vector3(diameter), MeshType.Sphere, color, EntityFlags.Active | EntityFlags.SolidCollider, behavior);
    }

    /// <summary>Memunculkan Pilar Arsitektur 3D untuk level First-Person.</summary>
    public static int SpawnPillar(string name, Vector3 position, Vector3 size, Color color)
    {
        if (_world == null) return -1;
        return _world.CreateEntity(name, position, size, MeshType.Pillar, color, EntityFlags.Active | EntityFlags.Static | EntityFlags.SolidCollider);
    }

    /// <summary>Memunculkan lantai datar (Plane) berukuran tertentu.</summary>
    public static int SpawnFloor(string name, Vector3 position, float width, float length, Color color)
    {
        if (_world == null) return -1;
        return _world.CreateEntity(name, position, new Vector3(width, 0.1f, length), MeshType.Plane, color, EntityFlags.Active | EntityFlags.Static | EntityFlags.SolidCollider);
    }

    /// <summary>Melakukan Raycast langsung dari tengah layar (Crosshair pemain First-Person).</summary>
    public static bool RaycastCrosshair(float maxDistance, out RaycastHitResult hit)
    {
        if (_world == null || _fps == null)
        {
            hit = default;
            return false;
        }
        Ray ray = new(_fps.Camera.Position, _fps.ForwardDirection);
        hit = _world.Raycast(ray, maxDistance, ignorePlaneFloor: true);
        return hit.Hit;
    }

    /// <summary>Memberikan efek guncangan kamera First-Person (Camera Shake).</summary>
    public static void ShakeCamera(float intensity = 0.12f, float duration = 0.15f)
    {
        _fps?.TriggerShake(intensity, duration);
    }

    /// <summary>Mengatur warna kabut atmosferik dan jarak pandang maksimum (Distance Culling).</summary>
    public static void SetAtmosphere(Color fogColor, float maxDrawDistance)
    {
        if (_renderer != null)
            _renderer.FogColor = fogColor;
        EngineConfig.MaxDrawDistance = maxDrawDistance;
    }

    /// <summary>Memberikan dorongan/impuls kecepatan ke entitas tertentu.</summary>
    public static void ApplyImpulse(int entityId, Vector3 impulse)
    {
        if (_world == null || entityId < 0 || entityId >= _world.Count) return;
        _world.Behaviors[entityId].Velocity += impulse;
    }

    /// <summary>
    /// Menyusun level demo First-Person ("Crimson Courtyard") lengkap dengan bangunan, pilar, dan objek interaktif.
    /// </summary>
    public static void BuildDemoFirstPersonArena()
    {
        if (_world == null) return;
        _world.Clear();

        // Lantai utama
        SpawnFloor("Floor_MainArena", Vector3.Zero, 160f, 160f, new Color(34, 37, 46, 255));

        // Dinding pembatas koridor & arena
        SpawnBox("Wall_North", new Vector3(0, 3f, -35f), new Vector3(70f, 6f, 1.5f), new Color(52, 56, 68, 255));
        SpawnBox("Wall_South", new Vector3(0, 3f, 35f), new Vector3(70f, 6f, 1.5f), new Color(52, 56, 68, 255));
        SpawnBox("Wall_East", new Vector3(35f, 3f, 0), new Vector3(1.5f, 6f, 70f), new Color(52, 56, 68, 255));
        SpawnBox("Wall_West", new Vector3(-35f, 3f, 0), new Vector3(1.5f, 6f, 70f), new Color(52, 56, 68, 255));

        // Barisan Pilar Koridor First-Person
        for (int i = -24; i <= 24; i += 8)
        {
            if (i == 0) continue;
            SpawnPillar($"Pillar_L_{i}", new Vector3(-10f, 3.0f, i), new Vector3(1.4f, 6.0f, 1.4f), new Color(85, 90, 108, 255));
            SpawnPillar($"Pillar_R_{i}", new Vector3(10f, 3.0f, i), new Vector3(1.4f, 6.0f, 1.4f), new Color(85, 90, 108, 255));
        }

        // Monumen Tengah ("MissingRed Core") menggunakan Custom 3D Model (.obj) & Tekstur
        SpawnBox("Core_Pedestal", new Vector3(0, 0.75f, 0), new Vector3(3.5f, 1.5f, 3.5f), new Color(65, 70, 82, 255));
        SpawnModelFromFile(
            "MissingRed_Artifact",
            new Vector3(0, 3.4f, 0),
            new Vector3(1.5f, 1.5f, 1.5f),
            "Assets/Models/crystal_obelisk.obj",
            "Assets/Textures/crimson_checker.png",
            new Color(235, 50, 60, 255),
            EntityBehavior.RotateY);
        SpawnSphere("Energy_Orb_North", new Vector3(0, 2.5f, -6f), 1.3f, new Color(255, 140, 50, 255), EntityBehavior.HoverBob);
        SpawnSphere("Energy_Orb_South", new Vector3(0, 2.5f, 6f), 1.3f, new Color(70, 180, 255, 255), EntityBehavior.HoverBob);

        // Target Crate / Pod Interaktif (menggunakan model 3D octa_pod.obj & tekstur)
        for (int x = -6; x <= 6; x += 3)
        {
            if (x == 0)
            {
                SpawnModelFromFile(
                    $"TargetPod_{x}",
                    new Vector3(x, 1.1f, -16f),
                    new Vector3(1.4f, 1.4f, 1.4f),
                    "Assets/Models/octa_pod.obj",
                    "Assets/Textures/crimson_checker.png",
                    new Color(240, 90, 75, 255));
            }
            else
            {
                int boxId = SpawnBox($"TargetCrate_{x}", new Vector3(x, 1.0f, -16f), new Vector3(1.4f, 1.4f, 1.4f), new Color(220, 75, 65, 255));
                SetEntityTexture(boxId, 0); // Pasang tekstur checker default
            }
        }

        // Drone Patroli
        SpawnSphere("Sentinel_Drone_1", new Vector3(0, 4.2f, -22f), 1.5f, new Color(255, 70, 90, 255), EntityBehavior.PatrolX);
        _world.Behaviors[_world.Count - 1].Amplitude = 12.0f;

        Log($"Demo Level 'Crimson Courtyard' dimuat ({_world.Count} entitas).");
    }

    /// <summary>
    /// Menambahkan ribuan objek secara instan untuk menguji kekuatan Frustum Culling, Auto-LOD, dan Zero-GC ECS.
    /// </summary>
    public static void SpawnOptimizationStressTest(int count = 1000)
    {
        if (_world == null) return;

        int spawned = 0;
        Random rng = Random.Shared;
        MeshType[] meshes = [MeshType.Cube, MeshType.Sphere, MeshType.Cylinder, MeshType.Pillar];
        EntityBehavior[] behaviors = [EntityBehavior.None, EntityBehavior.RotateY, EntityBehavior.HoverBob];

        for (int i = 0; i < count; i++)
        {
            float angle = rng.NextSingle() * MathF.PI * 2.0f;
            float radius = 16.0f + rng.NextSingle() * 115.0f;
            Vector3 pos = new(
                MathF.Cos(angle) * radius,
                1.0f + rng.NextSingle() * 10.0f,
                MathF.Sin(angle) * radius);

            float s = 0.8f + rng.NextSingle() * 1.4f;
            Color col = new(
                (byte)rng.Next(60, 245),
                (byte)rng.Next(50, 220),
                (byte)rng.Next(70, 250),
                (byte)255);

            MeshType m = meshes[rng.Next(meshes.Length)];
            EntityBehavior beh = behaviors[rng.Next(behaviors.Length)];

            int id = _world.CreateEntity($"StressObj_{_world.Count}", pos, new Vector3(s), m, col, EntityFlags.Active, beh);
            if (id >= 0) spawned++;
        }

        Log($"Stress Test: +{spawned} entitas ditambahkan (Total: {_world.Count}).");
    }
}
