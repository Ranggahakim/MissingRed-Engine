using System.Numerics;
using System.Runtime.CompilerServices;
using MissingRed.Core;
using Raylib_cs;

namespace MissingRed.ECS;

/// <summary>
/// Hasil deteksi Raycast 3D super cepat.
/// </summary>
public struct RaycastHitResult
{
    public bool Hit;
    public int EntityId;
    public float Distance;
    public Vector3 Point;
    public Vector3 Normal;
}

/// <summary>
/// Structure-of-Arrays (SoA) ECS World.
/// Menyimpan seluruh komponen secara berurutan di memori (Contiguous Memory)
/// sehingga Zero-GC saat runtime dan sangat ramah terhadap L1/L2 CPU Cache.
/// </summary>
public sealed class WorldData
{
    public int Count { get; private set; }

    // Array SoA (Structure of Arrays) untuk akses sekuensial berkecepatan tinggi
    public readonly EntityFlags[] Flags;
    public readonly TransformComponent[] Transforms;
    public readonly RenderComponent[] Renders;
    public readonly BehaviorComponent[] Behaviors;
    public readonly string[] Names;

    // Buffer hasil Culling per frame (Zero-Allocation)
    public readonly int[] VisibleEntityIndices;
    public int VisibleCount;
    public int CulledByFrustumCount;
    public int CulledByDistanceCount;
    public int LodLowCount;

    private float _simulationTime;

    public WorldData(int capacity = EngineConfig.MaxEntities)
    {
        Flags = new EntityFlags[capacity];
        Transforms = new TransformComponent[capacity];
        Renders = new RenderComponent[capacity];
        Behaviors = new BehaviorComponent[capacity];
        Names = new string[capacity];
        VisibleEntityIndices = new int[capacity];
    }

    /// <summary>
    /// Membuat entitas baru di dalam World.
    /// </summary>
    public int CreateEntity(
        string name,
        Vector3 position,
        Vector3 scale,
        MeshType mesh,
        Color color,
        EntityFlags flags = EntityFlags.Active | EntityFlags.SolidCollider,
        EntityBehavior behavior = EntityBehavior.None,
        float speed = 2.0f,
        float amplitude = 0.5f,
        short modelId = -1,
        short textureId = -1)
    {
        if (Count >= Flags.Length)
            return -1;

        int id = Count++;
        Flags[id] = flags;
        Names[id] = name;

        var t = new TransformComponent
        {
            Position = position,
            RotationEuler = Vector3.Zero,
            Scale = scale
        };
        t.RecalculateBounds();
        Transforms[id] = t;

        Renders[id] = new RenderComponent
        {
            Mesh = mesh,
            Roughness = 128,
            ModelId = modelId,
            TextureId = textureId,
            Tint = color
        };

        Behaviors[id] = new BehaviorComponent
        {
            Type = behavior,
            Speed = speed,
            Amplitude = amplitude,
            OriginPosition = position,
            Velocity = Vector3.Zero
        };

        return id;
    }

    /// <summary>
    /// Menghapus entitas menggunakan teknik Swap-and-Pop (O(1) tanpa fragmentasi memori).
    /// </summary>
    public void RemoveEntity(int id)
    {
        if (id < 0 || id >= Count)
            return;

        int last = Count - 1;
        if (id != last)
        {
            Flags[id] = Flags[last];
            Transforms[id] = Transforms[last];
            Renders[id] = Renders[last];
            Behaviors[id] = Behaviors[last];
            Names[id] = Names[last];
        }

        Count--;
    }

    /// <summary>
    /// Menghapus seluruh entitas di scene.
    /// </summary>
    public void Clear()
    {
        Count = 0;
        VisibleCount = 0;
        _simulationTime = 0f;
    }

    /// <summary>
    /// Memproses seluruh Behavior dan kecepatan fisika dalam satu loop kontigu (Data-Oriented).
    /// Tidak ada alokasi memori (0 bytes GC).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void UpdateSimulation(float dt)
    {
        _simulationTime += dt;
        float t = _simulationTime;

        int count = Count;
        for (int i = 0; i < count; i++)
        {
            if ((Flags[i] & EntityFlags.Active) == 0)
                continue;

            ref var b = ref Behaviors[i];
            ref var tr = ref Transforms[i];

            // Terapkan impuls kecepatan jika ada (misal saat ditembak/didorong di mode First-Person)
            if (b.Velocity.LengthSquared() > 0.001f)
            {
                tr.Position += b.Velocity * dt;
                b.OriginPosition += b.Velocity * dt;
                b.Velocity = Vector3.Lerp(b.Velocity, Vector3.Zero, dt * 6.0f);

                // Batas lantai sederhana
                float minHeight = tr.Scale.Y * 0.5f;
                if (tr.Position.Y < minHeight)
                {
                    tr.Position.Y = minHeight;
                    b.OriginPosition.Y = minHeight;
                    b.Velocity.Y = 0f;
                }
            }

            switch (b.Type)
            {
                case EntityBehavior.RotateY:
                    tr.RotationEuler.Y = (tr.RotationEuler.Y + b.Speed * 45.0f * dt) % 360.0f;
                    break;

                case EntityBehavior.HoverBob:
                    tr.Position.Y = b.OriginPosition.Y + MathF.Sin(t * b.Speed + i * 0.3f) * b.Amplitude;
                    break;

                case EntityBehavior.PatrolX:
                    tr.Position.X = b.OriginPosition.X + MathF.Sin(t * b.Speed + i * 0.5f) * b.Amplitude;
                    break;

                case EntityBehavior.PulseScale:
                    float s = 1.0f + MathF.Sin(t * b.Speed + i) * (b.Amplitude * 0.25f);
                    tr.Scale = new Vector3(s, s, s);
                    tr.RecalculateBounds();
                    break;
            }
        }
    }

    /// <summary>
    /// Melakukan Raycast 3D berkecepatan tinggi menggunakan Bounding Sphere pre-filter + AABB tepat.
    /// Digunakan oleh Crosshair First-Person maupun klik Mouse di Editor.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public RaycastHitResult Raycast(Ray ray, float maxDistance = 100f, bool ignorePlaneFloor = false)
    {
        RaycastHitResult best = new() { Hit = false, EntityId = -1, Distance = maxDistance };
        float maxDistSq = maxDistance * maxDistance;

        int count = Count;
        for (int i = 0; i < count; i++)
        {
            if ((Flags[i] & EntityFlags.Active) == 0)
                continue;

            if (ignorePlaneFloor && Renders[i].Mesh == MeshType.Plane)
                continue;

            ref readonly var tr = ref Transforms[i];

            // Fast distance pre-check
            Vector3 toCenter = tr.Position - ray.Position;
            float distSq = toCenter.LengthSquared();
            float rad = tr.BoundingRadius + maxDistance;
            if (distSq > rad * rad && distSq > maxDistSq)
                continue;

            // Fast Ray-Sphere pre-check
            float proj = Vector3.Dot(toCenter, ray.Direction);
            if (proj < -tr.BoundingRadius)
                continue;

            float perpSq = distSq - proj * proj;
            float boundSq = tr.BoundingRadius * tr.BoundingRadius;
            if (perpSq > boundSq)
                continue;

            // Exact AABB check
            Vector3 half = tr.Scale * 0.5f;
            if (Renders[i].Mesh == MeshType.Plane)
                half.Y = 0.05f;

            BoundingBox box = new(tr.Position - half, tr.Position + half);
            RayCollision col = Raylib.GetRayCollisionBox(ray, box);

            if (col.Hit && col.Distance >= 0f && col.Distance < best.Distance)
            {
                best.Hit = true;
                best.EntityId = i;
                best.Distance = col.Distance;
                best.Point = col.Point;
                best.Normal = col.Normal;
            }
        }

        return best;
    }

    /// <summary>
    /// Mendapatkan AABB (Axis-Aligned Bounding Box) dari entitas tertentu.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BoundingBox GetEntityBoundingBox(int id)
    {
        ref readonly var tr = ref Transforms[id];
        Vector3 half = tr.Scale * 0.5f;
        if (Renders[id].Mesh == MeshType.Plane)
            half.Y = 0.05f;
        return new BoundingBox(tr.Position - half, tr.Position + half);
    }
}
