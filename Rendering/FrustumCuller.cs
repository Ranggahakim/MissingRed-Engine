using System.Numerics;
using System.Runtime.CompilerServices;
using MissingRed.Core;
using MissingRed.ECS;
using Raylib_cs;

namespace MissingRed.Rendering;

/// <summary>
/// Sistem Frustum & Distance Culling berkecepatan tinggi.
/// Mengekstrak 6 bidang pandang kamera (View Frustum) dan memfilter ribuan entitas
/// dalam hitungan mikrodetik di CPU sebelum dikirim ke GPU.
/// </summary>
public struct FrustumCuller
{
    private Vector4 _left, _right, _bottom, _top, _near, _far;

    /// <summary>
    /// Membangun 6 plane Frustum dari Camera3D saat ini.
    /// </summary>
    public void ExtractFromCamera(Camera3D camera, float aspect, float nearClip = 0.05f, float farClip = 200.0f)
    {
        Matrix4x4 view = Matrix4x4.CreateLookAt(camera.Position, camera.Target, camera.Up);
        float fovRad = camera.FovY * (MathF.PI / 180.0f);
        Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(fovRad, aspect, nearClip, farClip);
        Matrix4x4 vp = view * proj;

        // Gribb-Hartmann Plane Extraction
        _left = NormalizePlane(new Vector4(vp.M14 + vp.M11, vp.M24 + vp.M21, vp.M34 + vp.M31, vp.M44 + vp.M41));
        _right = NormalizePlane(new Vector4(vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31, vp.M44 - vp.M41));
        _bottom = NormalizePlane(new Vector4(vp.M14 + vp.M12, vp.M24 + vp.M22, vp.M34 + vp.M32, vp.M44 + vp.M42));
        _top = NormalizePlane(new Vector4(vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32, vp.M44 - vp.M42));
        _near = NormalizePlane(new Vector4(vp.M13, vp.M23, vp.M33, vp.M43));
        _far = NormalizePlane(new Vector4(vp.M14 - vp.M13, vp.M24 - vp.M23, vp.M34 - vp.M33, vp.M44 - vp.M43));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4 NormalizePlane(Vector4 p)
    {
        float len = MathF.Sqrt(p.X * p.X + p.Y * p.Y + p.Z * p.Z);
        return len > 1e-6f ? p / len : p;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsSphereVisible(Vector3 center, float radius)
    {
        float negR = -radius;
        if ( _left.X * center.X + _left.Y * center.Y + _left.Z * center.Z + _left.W < negR) return false;
        if (_right.X * center.X + _right.Y * center.Y + _right.Z * center.Z + _right.W < negR) return false;
        if (_bottom.X * center.X + _bottom.Y * center.Y + _bottom.Z * center.Z + _bottom.W < negR) return false;
        if (_top.X * center.X + _top.Y * center.Y + _top.Z * center.Z + _top.W < negR) return false;
        if (_near.X * center.X + _near.Y * center.Y + _near.Z * center.Z + _near.W < negR) return false;
        if (_far.X * center.X + _far.Y * center.Y + _far.Z * center.Z + _far.W < negR) return false;
        return true;
    }

    /// <summary>
    /// Memfilter seluruh entitas di WorldData menggunakan Distance Culling & Frustum Culling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void CullWorld(WorldData world, Vector3 camPos)
    {
        int visible = 0;
        int culledDist = 0;
        int culledFrustum = 0;

        bool doDist = EngineConfig.EnableDistanceCulling;
        bool doFrustum = EngineConfig.EnableFrustumCulling;
        float maxDistSq = EngineConfig.MaxDrawDistance * EngineConfig.MaxDrawDistance;

        int count = world.Count;
        var flags = world.Flags;
        var transforms = world.Transforms;
        var outIndices = world.VisibleEntityIndices;

        for (int i = 0; i < count; i++)
        {
            if ((flags[i] & EntityFlags.Active) == 0)
                continue;

            ref readonly var tr = ref transforms[i];

            // 1. Distance Culling (Abaikan objek jauh kecuali lantai/besar)
            if (doDist && tr.BoundingRadius < 15.0f)
            {
                float distSq = Vector3.DistanceSquared(camPos, tr.Position);
                if (distSq > maxDistSq)
                {
                    culledDist++;
                    continue;
                }
            }

            // 2. 6-Plane Frustum Culling
            if (doFrustum)
            {
                if (!IsSphereVisible(tr.Position, tr.BoundingRadius))
                {
                    culledFrustum++;
                    continue;
                }
            }

            outIndices[visible++] = i;
        }

        world.VisibleCount = visible;
        world.CulledByDistanceCount = culledDist;
        world.CulledByFrustumCount = culledFrustum;
    }
}
