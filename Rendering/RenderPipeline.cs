using System.Numerics;
using System.Runtime.CompilerServices;
using MissingRed.Core;
using MissingRed.ECS;
using Raylib_cs;

namespace MissingRed.Rendering;

/// <summary>
/// Pipeline Rendering 3D yang dioptimisasi dengan Frustum Culling, Distance Fog,
/// Auto-LOD (Level of Detail), Directional Shading, 3D Editor Gizmo, dan First-Person Viewmodel.
/// </summary>
public sealed class RenderPipeline : IDisposable
{
    private FrustumCuller _culler;
    private RenderTexture2D _viewportTarget;
    private int _vpWidth;
    private int _vpHeight;

    // Warna langit / kabut atmosferik khas MissingRed
    public Color FogColor = new(18, 20, 28, 255);
    public Vector3 SunDirection = Vector3.Normalize(new Vector3(-0.45f, -0.8f, -0.35f));

    // Statistik performa per-frame
    public int DrawCallsThisFrame { get; private set; }

    public RenderTexture2D ViewportTexture => _viewportTarget;

    public RenderPipeline(int initialWidth = 1280, int initialHeight = 720)
    {
        ResizeViewport(initialWidth, initialHeight);
    }

    public void ResizeViewport(int width, int height)
    {
        width = Math.Max(320, width);
        height = Math.Max(200, height);
        if (width == _vpWidth && height == _vpHeight)
            return;

        if (_viewportTarget.Id != 0)
            Raylib.UnloadRenderTexture(_viewportTarget);

        _viewportTarget = Raylib.LoadRenderTexture(width, height);
        _vpWidth = width;
        _vpHeight = height;
    }

    /// <summary>
    /// Merender seluruh dunia 3D ke dalam RenderTexture Viewport (untuk Editor maupun Play Mode).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void RenderSceneToViewport(
        WorldData world,
        Camera3D camera,
        int selectedEntityId,
        bool isPlayMode,
        float recoilKick,
        float headBobOffset)
    {
        float aspect = (float)_vpWidth / _vpHeight;
        _culler.ExtractFromCamera(camera, aspect, 0.05f, EngineConfig.MaxDrawDistance + 25.0f);
        _culler.CullWorld(world, camera.Position);

        DrawCallsThisFrame = 0;
        world.LodLowCount = 0;

        Raylib.BeginTextureMode(_viewportTarget);
        Raylib.ClearBackground(FogColor);

        Raylib.BeginMode3D(camera);

        // Gambar Grid referensi saat di mode Editor
        if (!isPlayMode && EngineConfig.ShowGizmos)
        {
            DrawOptimizedGrid(36, 2.0f);
            DrawCallsThisFrame++;
        }

        int visibleCount = world.VisibleCount;
        var visibleIndices = world.VisibleEntityIndices;
        var transforms = world.Transforms;
        var renders = world.Renders;

        Vector3 camPos = camera.Position;
        float maxDist = EngineConfig.MaxDrawDistance;
        float fogStart = maxDist * 0.45f;
        bool useFog = EngineConfig.EnableFog;
        bool useLod = EngineConfig.EnableAutoLOD;
        bool wireframe = EngineConfig.WireframeMode;

        for (int v = 0; v < visibleCount; v++)
        {
            int id = visibleIndices[v];
            ref readonly var tr = ref transforms[id];
            ref readonly var ren = ref renders[id];

            float dist = Vector3.Distance(camPos, tr.Position);

            // Hitung warna dengan Directional Lighting + Atmospheric Fog
            Color finalColor = ComputeShadedColor(ren.Tint, dist, fogStart, maxDist, useFog);

            // Tentukan Level of Detail (LOD) berdasarkan jarak ke kamera
            bool isLowLod = useLod && dist > 32.0f;
            if (isLowLod)
                world.LodLowCount++;

            if (ren.Mesh == MeshType.CustomModel || ren.TextureId >= 0)
            {
                AssetManager.DrawTexturedOrCustomEntity(in tr, in ren, finalColor, wireframe);
            }
            else if (wireframe)
            {
                DrawEntityWireframe(in tr, ren.Mesh, finalColor);
            }
            else
            {
                DrawEntitySolid(in tr, ren.Mesh, finalColor, isLowLod);
            }

            DrawCallsThisFrame++;
        }

        // Gambar Selection Highlight & 3D Transform Gizmo di Editor
        if (!isPlayMode && selectedEntityId >= 0 && selectedEntityId < world.Count)
        {
            DrawEditorSelectionGizmo(world, selectedEntityId);
            DrawCallsThisFrame += 2;
        }

        Raylib.EndMode3D();

        // Gambar Viewmodel Tangan/Senjata First-Person & HUD Crosshair saat Play Mode
        if (isPlayMode)
        {
            DrawFirstPersonViewmodel(_vpWidth, _vpHeight, recoilKick, headBobOffset);
        }

        Raylib.EndTextureMode();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Color ComputeShadedColor(Color baseColor, float dist, float fogStart, float maxDist, bool useFog)
    {
        if (!useFog || dist <= fogStart)
            return baseColor;

        float fogFactor = Math.Clamp((dist - fogStart) / MathF.Max(1.0f, maxDist - fogStart), 0f, 1f);
        byte r = (byte)(baseColor.R + (FogColor.R - baseColor.R) * fogFactor);
        byte g = (byte)(baseColor.G + (FogColor.G - baseColor.G) * fogFactor);
        byte b = (byte)(baseColor.B + (FogColor.B - baseColor.B) * fogFactor);
        return new Color(r, g, b, baseColor.A);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DrawEntitySolid(in TransformComponent tr, MeshType mesh, Color col, bool lowLod)
    {
        Vector3 pos = tr.Position;
        Vector3 scale = tr.Scale;

        // Buat warna sisi gelap sedikit untuk efek 3D tegas tanpa overhead shader berat
        Color edgeCol = new(
            (byte)(col.R * 0.65f),
            (byte)(col.G * 0.65f),
            (byte)(col.B * 0.65f),
            (byte)255);

        switch (mesh)
        {
            case MeshType.Cube:
                if (MathF.Abs(tr.RotationEuler.Y) > 0.1f)
                {
                    Rlgl.PushMatrix();
                    Rlgl.Translatef(pos.X, pos.Y, pos.Z);
                    Rlgl.Rotatef(tr.RotationEuler.Y, 0, 1, 0);
                    Raylib.DrawCubeV(Vector3.Zero, scale, col);
                    if (!lowLod)
                        Raylib.DrawCubeWiresV(Vector3.Zero, scale, edgeCol);
                    Rlgl.PopMatrix();
                }
                else
                {
                    Raylib.DrawCubeV(pos, scale, col);
                    if (!lowLod)
                        Raylib.DrawCubeWiresV(pos, scale, edgeCol);
                }
                break;

            case MeshType.Sphere:
                int rings = lowLod ? 5 : 10;
                int slices = lowLod ? 6 : 12;
                Raylib.DrawSphereEx(pos, scale.X * 0.5f, rings, slices, col);
                break;

            case MeshType.Cylinder:
                int cylSlices = lowLod ? 6 : 12;
                Vector3 bottom = new(pos.X, pos.Y - scale.Y * 0.5f, pos.Z);
                Raylib.DrawCylinder(bottom, scale.X * 0.5f, scale.X * 0.5f, scale.Y, cylSlices, col);
                if (!lowLod)
                    Raylib.DrawCylinderWires(bottom, scale.X * 0.5f, scale.X * 0.5f, scale.Y, cylSlices, edgeCol);
                break;

            case MeshType.Plane:
                Raylib.DrawPlane(pos, new Vector2(scale.X, scale.Z), col);
                break;

            case MeshType.Pillar:
                Raylib.DrawCubeV(pos, scale, col);
                if (!lowLod)
                {
                    Raylib.DrawCubeWiresV(pos, scale, edgeCol);
                    Vector3 capSize = new(scale.X * 1.25f, 0.25f, scale.Z * 1.25f);
                    Raylib.DrawCubeV(pos + new Vector3(0, scale.Y * 0.5f, 0), capSize, edgeCol);
                    Raylib.DrawCubeV(pos - new Vector3(0, scale.Y * 0.5f, 0), capSize, edgeCol);
                }
                break;
        }
    }

    private static void DrawEntityWireframe(in TransformComponent tr, MeshType mesh, Color col)
    {
        if (mesh == MeshType.Sphere)
            Raylib.DrawSphereWires(tr.Position, tr.Scale.X * 0.5f, 6, 8, col);
        else
            Raylib.DrawCubeWiresV(tr.Position, tr.Scale, col);
    }

    private static void DrawEditorSelectionGizmo(WorldData world, int id)
    {
        BoundingBox box = world.GetEntityBoundingBox(id);
        Raylib.DrawBoundingBox(box, new Color(255, 210, 60, 255));

        Vector3 p = world.Transforms[id].Position;
        float axisLen = MathF.Max(1.6f, world.Transforms[id].BoundingRadius * 1.4f);

        // Sumbu X (Merah), Y (Hijau), Z (Biru)
        Raylib.DrawLine3D(p, p + new Vector3(axisLen, 0, 0), new Color(255, 65, 65, 255));
        Raylib.DrawCube(p + new Vector3(axisLen, 0, 0), 0.16f, 0.16f, 0.16f, new Color(255, 65, 65, 255));

        Raylib.DrawLine3D(p, p + new Vector3(0, axisLen, 0), new Color(65, 255, 95, 255));
        Raylib.DrawCube(p + new Vector3(0, axisLen, 0), 0.16f, 0.16f, 0.16f, new Color(65, 255, 95, 255));

        Raylib.DrawLine3D(p, p + new Vector3(0, 0, axisLen), new Color(75, 150, 255, 255));
        Raylib.DrawCube(p + new Vector3(0, 0, axisLen), 0.16f, 0.16f, 0.16f, new Color(75, 150, 255, 255));
    }

    private static void DrawOptimizedGrid(int slices, float spacing)
    {
        int half = slices / 2;
        float extent = half * spacing;
        Color gridCol = new(48, 52, 66, 255);
        Color axisCol = new(90, 45, 55, 255);

        for (int i = -half; i <= half; i++)
        {
            float p = i * spacing;
            Color c = (i == 0) ? axisCol : gridCol;
            Raylib.DrawLine3D(new Vector3(p, 0.01f, -extent), new Vector3(p, 0.01f, extent), c);
            Raylib.DrawLine3D(new Vector3(-extent, 0.01f, p), new Vector3(extent, 0.01f, p), c);
        }
    }

    /// <summary>
    /// Menggambar Viewmodel Senjata/Alat First-Person & Crosshair Dinamis di ruang layar.
    /// </summary>
    private static void DrawFirstPersonViewmodel(int w, int h, float recoilKick, float headBob)
    {
        int cx = w / 2;
        int cy = h / 2;

        // Crosshair presisi First-Person
        int gap = 5 + (int)(recoilKick * 14.0f);
        int len = 7;
        Color chCol = new(255, 75, 75, 230);
        Raylib.DrawLine(cx - gap - len, cy, cx - gap, cy, chCol);
        Raylib.DrawLine(cx + gap, cy, cx + gap + len, cy, chCol);
        Raylib.DrawLine(cx, cy - gap - len, cx, cy - gap, chCol);
        Raylib.DrawLine(cx, cy + gap, cx, cy + gap + len, chCol);
        Raylib.DrawPixel(cx, cy, Color.White);

        // Viewmodel (Pulse Tool / Blaster di kanan bawah)
        int bobX = (int)(MathF.Cos(headBob * 0.5f) * 8.0f);
        int bobY = (int)(MathF.Abs(MathF.Sin(headBob)) * 9.0f + recoilKick * 28.0f);

        int bx = w - 250 + bobX;
        int by = h - 135 + bobY;

        // Muzzle flash saat menembak
        if (recoilKick > 0.55f)
        {
            Raylib.DrawCircleGradient(bx + 12, by - 14, 38f, new Color(255, 190, 80, 210), new Color(255, 50, 30, 0));
        }

        // Bodi senjata / perangkat First-Person
        Raylib.DrawRectangle(bx, by, 58, 160, new Color(36, 39, 48, 255));
        Raylib.DrawRectangle(bx + 8, by - 18, 42, 32, new Color(55, 60, 74, 255));
        Raylib.DrawRectangle(bx + 18, by - 10, 22, 95, new Color(225, 55, 65, 255)); // Core energi merah
        Raylib.DrawRectangleLines(bx, by, 58, 160, new Color(90, 96, 115, 255));
    }

    public void Dispose()
    {
        if (_viewportTarget.Id != 0)
        {
            Raylib.UnloadRenderTexture(_viewportTarget);
            _viewportTarget.Id = 0;
        }
    }
}
