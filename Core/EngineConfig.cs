namespace MissingRed.Core;

/// <summary>
/// Konfigurasi global untuk optimisasi dan pengaturan runtime MissingRed Engine.
/// </summary>
public static class EngineConfig
{
    public const string EngineName = "MissingRed Engine";
    public const string EngineVersion = "v0.1.0 (First-Person Optimized)";

    /// <summary>Kapasitas maksimal entitas yang dialokasikan di memori kontigu (SoA).</summary>
    public const int MaxEntities = 32768;

    /// <summary>Aktifkan Frustum Culling (mengabaikan objek di luar pandangan kamera).</summary>
    public static bool EnableFrustumCulling = true;

    /// <summary>Aktifkan Distance Culling (mengabaikan objek yang terlalu jauh).</summary>
    public static bool EnableDistanceCulling = true;

    /// <summary>Jarak maksimal render objek dalam satuan meter/unit.</summary>
    public static float MaxDrawDistance = 140.0f;

    /// <summary>Aktifkan sistem LOD (Level of Detail) otomatis berdasarkan jarak.</summary>
    public static bool EnableAutoLOD = true;

    /// <summary>Aktifkan efek kabut atmosferik sekaligus menyamarkan batas Distance Culling.</summary>
    public static bool EnableFog = true;

    /// <summary>Aktifkan mode Wireframe untuk debugging poligon.</summary>
    public static bool WireframeMode = false;

    /// <summary>Tampilkan bounding box & gizmo di Editor.</summary>
    public static bool ShowGizmos = true;

    /// <summary>Target FPS (0 = Unlimited untuk mengetes performa murni).</summary>
    public static int TargetFPS = 0;
}
