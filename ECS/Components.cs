using System.Numerics;
using System.Runtime.InteropServices;
using Raylib_cs;

namespace MissingRed.ECS;

/// <summary>
/// Jenis bentuk mesh primitif bawaan engine (mendukung auto-LOD).
/// </summary>
public enum MeshType : byte
{
    Cube = 0,
    Sphere = 1,
    Cylinder = 2,
    Plane = 3,
    Pillar = 4,
    CustomModel = 5
}

/// <summary>
/// Perilaku (Behavior) bawaan yang diproses secara Data-Oriented tanpa alokasi GC.
/// </summary>
public enum EntityBehavior : byte
{
    None = 0,
    RotateY = 1,
    HoverBob = 2,
    PatrolX = 3,
    PulseScale = 4
}

/// <summary>
/// Flag sifat entitas (bisa dikombinasikan dengan bitwise).
/// </summary>
[Flags]
public enum EntityFlags : byte
{
    None = 0,
    Active = 1 << 0,
    Static = 1 << 1,
    SolidCollider = 1 << 2,
    Interactable = 1 << 3,
    CastShadow = 1 << 4
}

/// <summary>
/// Data Transform 3D murni berbasis struct untuk optimalisasi CPU Cache Line.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct TransformComponent
{
    public Vector3 Position;
    public Vector3 RotationEuler; // Dalam derajat (Pitch, Yaw, Roll)
    public Vector3 Scale;
    public float BoundingRadius;

    public static TransformComponent Default => new()
    {
        Position = Vector3.Zero,
        RotationEuler = Vector3.Zero,
        Scale = Vector3.One,
        BoundingRadius = 0.87f // sqrt(0.5^2 * 3)
    };

    public void RecalculateBounds()
    {
        float maxAxis = MathF.Max(Scale.X, MathF.Max(Scale.Y, Scale.Z));
        BoundingRadius = maxAxis * 0.87f;
    }
}

/// <summary>
/// Data visual/rendering entitas.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct RenderComponent
{
    public MeshType Mesh;
    public byte Roughness;
    public short ModelId;   // -1 jika menggunakan mesh primitif bawaan
    public short TextureId; // -1 jika tanpa tekstur (warna solid murni)
    public Color Tint;

    public static RenderComponent Default => new()
    {
        Mesh = MeshType.Cube,
        Roughness = 128,
        ModelId = -1,
        TextureId = -1,
        Tint = new Color(220, 60, 60, 255)
    };
}

/// <summary>
/// Data perilaku dinamis & fisika ringan (Zero-GC Behavior System).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct BehaviorComponent
{
    public EntityBehavior Type;
    public float Speed;
    public float Amplitude;
    public Vector3 OriginPosition;
    public Vector3 Velocity;
}
