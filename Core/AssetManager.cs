using System.Globalization;
using System.Numerics;
using System.Text;
using MissingRed.ECS;
using Raylib_cs;

namespace MissingRed.Core;

/// <summary>
/// Metadata untuk Model 3D yang dimuat ke VRAM.
/// </summary>
public struct LoadedModelEntry
{
    public short Id;
    public string Name;
    public string FilePath;
    public Model Model;
    public Vector3 LocalSize;
}

/// <summary>
/// Metadata untuk Tekstur 2D yang dimuat ke VRAM.
/// </summary>
public struct LoadedTextureEntry
{
    public short Id;
    public string Name;
    public string FilePath;
    public Texture2D Texture;
}

/// <summary>
/// Manajer Asset 3D (Model .obj/.glb/.gltf/.iqm & Tekstur .png/.jpg) berbasis ID (Zero-GC di ECS).
/// Entitas di ECS cukup menyimpan <c>short ModelId</c> dan <c>short TextureId</c>.
/// </summary>
public static class AssetManager
{
    public const string ModelsDirectory = "Assets/Models";
    public const string TexturesDirectory = "Assets/Textures";

    private static readonly List<LoadedModelEntry> _models = new(64);
    private static readonly List<LoadedTextureEntry> _textures = new(64);

    private static readonly Dictionary<string, short> _modelPathToId = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, short> _texturePathToId = new(StringComparer.OrdinalIgnoreCase);

    // Model primitif internal agar Cube/Sphere/Cylinder/Plane juga bisa dipasangi Texture2D
    private static Model _primCube;
    private static Model _primSphere;
    private static Model _primCylinder;
    private static Model _primPlane;
    private static Texture2D _defaultWhiteTex;
    private static bool _initialized;

    public static IReadOnlyList<LoadedModelEntry> Models => _models;
    public static IReadOnlyList<LoadedTextureEntry> Textures => _textures;

    /// <summary>
    /// Menyiapkan folder Assets, membuat sampel file 3D/Tekstur jika belum ada,
    /// dan memuat seluruh asset dari folder <c>Assets/Models</c> &amp; <c>Assets/Textures</c>.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        Directory.CreateDirectory(ModelsDirectory);
        Directory.CreateDirectory(TexturesDirectory);

        // 1. Buat tekstur putih 1x1 default & mesh primitif bertekstur
        Image whiteImg = Raylib.GenImageColor(4, 4, Color.White);
        _defaultWhiteTex = Raylib.LoadTextureFromImage(whiteImg);
        Raylib.UnloadImage(whiteImg);

        _primCube = Raylib.LoadModelFromMesh(Raylib.GenMeshCube(1f, 1f, 1f));
        _primSphere = Raylib.LoadModelFromMesh(Raylib.GenMeshSphere(0.5f, 12, 16));
        _primCylinder = Raylib.LoadModelFromMesh(Raylib.GenMeshCylinder(0.5f, 1f, 16));
        _primPlane = Raylib.LoadModelFromMesh(Raylib.GenMeshPlane(1f, 1f, 2, 2));

        // 2. Buat tekstur prosedural bawaan + simpan sampel PNG di Assets/Textures
        RegisterBuiltInProceduralTextures();

        // 3. Buat sampel model 3D (.obj) di Assets/Models jika folder masih kosong
        EnsureSample3DModelsOnDisk();

        // 4. Scan otomatis semua file di Assets/Textures dan Assets/Models
        ScanAssetsFolder();
    }

    private static void RegisterBuiltInProceduralTextures()
    {
        // [0] Checker Grid
        Image imgChecker = Raylib.GenImageChecked(128, 128, 16, 16, new Color(235, 235, 240, 255), new Color(65, 70, 85, 255));
        string sampleTexPath = Path.Combine(TexturesDirectory, "crimson_checker.png").Replace('\\', '/');
        if (!File.Exists(sampleTexPath))
        {
            Raylib.ExportImage(imgChecker, sampleTexPath);
        }
        RegisterTextureFromImage("Grid_Checker", sampleTexPath, imgChecker);
        Raylib.UnloadImage(imgChecker);

        // [1] Sci-Fi Tech Panel (Cellular / Cyber)
        Image imgCell = Raylib.GenImageCellular(128, 128, 24);
        RegisterTextureFromImage("SciFi_Cellular", "builtin://scifi_cellular", imgCell);
        Raylib.UnloadImage(imgCell);

        // [2] Industrial Concrete Noise
        Image imgNoise = Raylib.GenImageWhiteNoise(128, 128, 0.25f);
        RegisterTextureFromImage("Noise_Concrete", "builtin://noise_concrete", imgNoise);
        Raylib.UnloadImage(imgNoise);
    }

    private static short RegisterTextureFromImage(string name, string virtualPath, Image img)
    {
        Texture2D tex = Raylib.LoadTextureFromImage(img);
        Raylib.GenTextureMipmaps(ref tex);
        Raylib.SetTextureFilter(tex, TextureFilter.Trilinear);

        short id = (short)_textures.Count;
        _textures.Add(new LoadedTextureEntry
        {
            Id = id,
            Name = name,
            FilePath = virtualPath,
            Texture = tex
        });
        _texturePathToId[virtualPath] = id;
        return id;
    }

    /// <summary>
    /// Membuat 2 file model 3D (.obj) contoh di folder <c>Assets/Models/</c> agar pengguna
    /// langsung memiliki contoh nyata asset 3D eksternal yang siap diedit/diganti.
    /// </summary>
    private static void EnsureSample3DModelsOnDisk()
    {
        string obeliskPath = Path.Combine(ModelsDirectory, "crystal_obelisk.obj");
        if (!File.Exists(obeliskPath))
        {
            File.WriteAllText(obeliskPath, GenerateCrystalObeliskObj());
        }

        string cratePath = Path.Combine(ModelsDirectory, "octa_pod.obj");
        if (!File.Exists(cratePath))
        {
            File.WriteAllText(cratePath, GenerateOctaPodObj());
        }
    }

    /// <summary>
    /// Memindai ulang folder <c>Assets/Models</c> dan <c>Assets/Textures</c> untuk memuat file baru.
    /// </summary>
    public static void ScanAssetsFolder()
    {
        if (Directory.Exists(TexturesDirectory))
        {
            string[] texFiles = Directory.GetFiles(TexturesDirectory);
            foreach (string f in texFiles)
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tga")
                {
                    LoadTexture(f);
                }
            }
        }

        if (Directory.Exists(ModelsDirectory))
        {
            string[] modelFiles = Directory.GetFiles(ModelsDirectory);
            foreach (string f in modelFiles)
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext is ".obj" or ".glb" or ".gltf" or ".iqm" or ".m3d")
                {
                    LoadModel(f);
                }
            }
        }
    }

    /// <summary>
    /// Memuat file Tekstur 2D (.png, .jpg, .tga, .bmp) ke VRAM dan mengembalikan ID-nya.
    /// Jika sudah pernah dimuat, langsung mengembalikan ID dari cache ($O(1)$).
    /// </summary>
    public static short LoadTexture(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return -1;

        string normPath = filePath.Replace('\\', '/');
        if (_texturePathToId.TryGetValue(normPath, out short existingId))
            return existingId;

        if (!File.Exists(filePath))
        {
            MR.Log($"[Asset] File tekstur tidak ditemukan: {filePath}");
            return -1;
        }

        Texture2D tex = Raylib.LoadTexture(filePath);
        if (tex.Id == 0)
        {
            MR.Log($"[Asset] Gagal memuat tekstur: {filePath}");
            return -1;
        }

        Raylib.GenTextureMipmaps(ref tex);
        Raylib.SetTextureFilter(tex, TextureFilter.Trilinear);

        short id = (short)_textures.Count;
        string name = Path.GetFileNameWithoutExtension(filePath);

        _textures.Add(new LoadedTextureEntry
        {
            Id = id,
            Name = name,
            FilePath = normPath,
            Texture = tex
        });
        _texturePathToId[normPath] = id;

        MR.Log($"[Asset] Tekstur #{id} '{name}' dimuat ({tex.Width}x{tex.Height}).");
        return id;
    }

    /// <summary>
    /// Memuat file Model 3D (.obj, .glb, .gltf, .iqm, .m3d) ke VRAM dan mengembalikan ID-nya.
    /// Jika sudah pernah dimuat, langsung mengembalikan ID dari cache ($O(1)$).
    /// </summary>
    public static short LoadModel(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return -1;

        string normPath = filePath.Replace('\\', '/');
        if (_modelPathToId.TryGetValue(normPath, out short existingId))
            return existingId;

        if (!File.Exists(filePath))
        {
            MR.Log($"[Asset] File model 3D tidak ditemukan: {filePath}");
            return -1;
        }

        Model model = Raylib.LoadModel(filePath);
        if (model.MeshCount == 0)
        {
            MR.Log($"[Asset] Gagal memuat model 3D: {filePath}");
            return -1;
        }

        BoundingBox bounds = Raylib.GetModelBoundingBox(model);
        Vector3 size = bounds.Max - bounds.Min;
        if (size.LengthSquared() < 0.001f)
            size = Vector3.One;

        short id = (short)_models.Count;
        string name = Path.GetFileNameWithoutExtension(filePath);

        _models.Add(new LoadedModelEntry
        {
            Id = id,
            Name = name,
            FilePath = normPath,
            Model = model,
            LocalSize = size
        });
        _modelPathToId[normPath] = id;

        MR.Log($"[Asset] Model 3D #{id} '{name}' dimuat ({model.MeshCount} mesh).");
        return id;
    }

    public static bool TryGetModel(short modelId, out LoadedModelEntry entry)
    {
        if (modelId >= 0 && modelId < _models.Count)
        {
            entry = _models[modelId];
            return true;
        }
        entry = default;
        return false;
    }

    public static bool TryGetTexture(short textureId, out LoadedTextureEntry entry)
    {
        if (textureId >= 0 && textureId < _textures.Count)
        {
            entry = _textures[textureId];
            return true;
        }
        entry = default;
        return false;
    }

    /// <summary>
    /// Menggambar mesh primitif yang dipasangi Texture2D atau Model 3D Custom.
    /// </summary>
    public static unsafe void DrawTexturedOrCustomEntity(
        in TransformComponent tr,
        in RenderComponent ren,
        Color shadedColor,
        bool wireframe)
    {
        Texture2D tex = _defaultWhiteTex;
        if (ren.TextureId >= 0 && ren.TextureId < _textures.Count)
        {
            tex = _textures[ren.TextureId].Texture;
        }

        // 1. Jika menggunakan Custom 3D Model (.obj / .glb / .gltf)
        if (ren.Mesh == MeshType.CustomModel && ren.ModelId >= 0 && ren.ModelId < _models.Count)
        {
            Model model = _models[ren.ModelId].Model;
            if (ren.TextureId >= 0 && model.MaterialCount > 0)
            {
                Raylib.SetMaterialTexture(ref model.Materials[0], MaterialMapIndex.Albedo, tex);
            }

            if (wireframe)
            {
                Raylib.DrawModelWiresEx(model, tr.Position, Vector3.UnitY, tr.RotationEuler.Y, tr.Scale, shadedColor);
            }
            else
            {
                Raylib.DrawModelEx(model, tr.Position, Vector3.UnitY, tr.RotationEuler.Y, tr.Scale, shadedColor);
                Color edgeCol = new(
                    (byte)(shadedColor.R * 0.58f),
                    (byte)(shadedColor.G * 0.58f),
                    (byte)(shadedColor.B * 0.58f),
                    (byte)255);
                Raylib.DrawModelWiresEx(model, tr.Position, Vector3.UnitY, tr.RotationEuler.Y, tr.Scale, edgeCol);
            }
            return;
        }

        // 2. Jika menggunakan Mesh Primitif tetapi dipasangi Tekstur (TextureId >= 0)
        Model primModel = ren.Mesh switch
        {
            MeshType.Sphere => _primSphere,
            MeshType.Cylinder => _primCylinder,
            MeshType.Plane => _primPlane,
            _ => _primCube
        };

        Raylib.SetMaterialTexture(ref primModel.Materials[0], MaterialMapIndex.Albedo, tex);

        Vector3 drawPos = tr.Position;
        if (ren.Mesh == MeshType.Cylinder)
        {
            drawPos.Y -= tr.Scale.Y * 0.5f;
        }

        if (wireframe)
        {
            Raylib.DrawModelWiresEx(primModel, drawPos, Vector3.UnitY, tr.RotationEuler.Y, tr.Scale, shadedColor);
        }
        else
        {
            Raylib.DrawModelEx(primModel, drawPos, Vector3.UnitY, tr.RotationEuler.Y, tr.Scale, shadedColor);
        }
    }

    public static void UnloadAll()
    {
        for (int i = 0; i < _models.Count; i++)
        {
            Raylib.UnloadModel(_models[i].Model);
        }
        _models.Clear();
        _modelPathToId.Clear();

        for (int i = 0; i < _textures.Count; i++)
        {
            Raylib.UnloadTexture(_textures[i].Texture);
        }
        _textures.Clear();
        _texturePathToId.Clear();

        if (_initialized)
        {
            Raylib.UnloadModel(_primCube);
            Raylib.UnloadModel(_primSphere);
            Raylib.UnloadModel(_primCylinder);
            Raylib.UnloadModel(_primPlane);
            Raylib.UnloadTexture(_defaultWhiteTex);
            _initialized = false;
        }
    }

    private static string GenerateCrystalObeliskObj()
    {
        // Model 3D Kristal / Obelisk meruncing dengan koordinat UV
        return """
        # MissingRed Sample 3D Asset: Crystal Obelisk
        o CrystalObelisk
        v -0.45 -1.00 -0.45
        v  0.45 -1.00 -0.45
        v  0.45 -1.00  0.45
        v -0.45 -1.00  0.45
        v -0.60  0.35 -0.60
        v  0.60  0.35 -0.60
        v  0.60  0.35  0.60
        v -0.60  0.35  0.60
        v  0.00  1.25  0.00
        v  0.00 -1.25  0.00
        vt 0.0 0.0
        vt 1.0 0.0
        vt 1.0 1.0
        vt 0.0 1.0
        vt 0.5 1.0
        f 1/1 2/2 6/3 5/4
        f 2/1 3/2 7/3 6/4
        f 3/1 4/2 8/3 7/4
        f 4/1 1/2 5/3 8/4
        f 5/1 6/2 9/5
        f 6/1 7/2 9/5
        f 7/1 8/2 9/5
        f 8/1 5/2 9/5
        f 2/1 1/2 10/5
        f 3/1 2/2 10/5
        f 4/1 3/2 10/5
        f 1/1 4/2 10/5
        """;
    }

    private static string GenerateOctaPodObj()
    {
        // Model 3D Octagonal Sci-Fi Pod / Crate dengan 8 sisi
        var sb = new StringBuilder();
        sb.AppendLine("# MissingRed Sample 3D Asset: Octagonal Sci-Fi Pod");
        sb.AppendLine("o OctaPod");

        int sides = 8;
        for (int ring = 0; ring < 4; ring++)
        {
            float y = ring switch { 0 => -0.7f, 1 => -0.45f, 2 => 0.45f, _ => 0.7f };
            float r = (ring == 0 || ring == 3) ? 0.52f : 0.72f;
            for (int s = 0; s < sides; s++)
            {
                float a = s * MathF.PI * 2f / sides;
                float x = MathF.Cos(a) * r;
                float z = MathF.Sin(a) * r;
                sb.AppendLine( string.Format(CultureInfo.InvariantCulture, "v {0:0.000} {1:0.000} {2:0.000}", x, y, z));
            }
        }
        sb.AppendLine("v 0.000 -0.700 0.000"); // 33 bottom center
        sb.AppendLine("v 0.000  0.700 0.000"); // 34 top center
        sb.AppendLine("vt 0.0 0.0\nvt 1.0 0.0\nvt 1.0 1.0\nvt 0.0 1.0\nvt 0.5 0.5");

        for (int ring = 0; ring < 3; ring++)
        {
            int baseIdx = ring * sides + 1;
            int nextIdx = (ring + 1) * sides + 1;
            for (int s = 0; s < sides; s++)
            {
                int sNext = (s + 1) % sides;
                int v1 = baseIdx + s;
                int v2 = baseIdx + sNext;
                int v3 = nextIdx + sNext;
                int v4 = nextIdx + s;
                sb.AppendLine($"f {v1}/1 {v2}/2 {v3}/3 {v4}/4");
            }
        }

        // Top & Bottom caps
        int topRing = 3 * sides + 1;
        for (int s = 0; s < sides; s++)
        {
            int sNext = (s + 1) % sides;
            sb.AppendLine($"f {topRing + s}/1 {topRing + sNext}/2 34/5");
            sb.AppendLine($"f {1 + sNext}/1 {1 + s}/2 33/5");
        }

        return sb.ToString();
    }
}
