using System.Diagnostics;
using System.Numerics;
using MissingRed.Core;
using MissingRed.ECS;
using MissingRed.FirstPerson;
using MissingRed.Rendering;
using Raylib_cs;

namespace MissingRed.Editor;

/// <summary>
/// MissingRed Studio — Visual Editor 3D Terintegrasi.
/// Mencakup Top Toolbar, Scene Hierarchy, 3D Interactive Viewport, Inspector,
/// serta Optimization & Frame-Time Profiler Real-Time.
/// </summary>
public sealed class MissingRedEditor
{
    private readonly WorldData _world;
    private readonly FirstPersonController _fps;
    private readonly RenderPipeline _renderer;

    public int SelectedEntityId = 10; // Default memilih MissingRed_Artifact
    public bool IsPlayMode = false;
    public bool FullscreenPlayViewport = false;

    private int _hierarchyScroll = 0;

    // Ring buffer untuk grafik Frame-Time (ms) tanpa alokasi memori
    private readonly float[] _frameTimeHistory = new float[120];
    private int _frameHistoryIndex = 0;

    // Pengukur waktu CPU murni (mikrodetik/milidetik)
    private readonly Stopwatch _cpuTimer = new();
    public double LastCpuUpdateMs { get; private set; }
    public double LastCpuRenderMs { get; private set; }

    public MissingRedEditor(WorldData world, FirstPersonController fps, RenderPipeline renderer)
    {
        _world = world;
        _fps = fps;
        _renderer = renderer;
    }

    public void UpdateAndDraw(float dt)
    {
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        // Shortcut Keyboard Global
        if (Raylib.IsKeyPressed(KeyboardKey.F5))
        {
            TogglePlayMode();
        }
        if (IsPlayMode && Raylib.IsKeyPressed(KeyboardKey.Tab))
        {
            TogglePlayMode();
        }
        if (Raylib.IsKeyPressed(KeyboardKey.F11))
        {
            FullscreenPlayViewport = !FullscreenPlayViewport;
        }
        if (!IsPlayMode && Raylib.IsKeyPressed(KeyboardKey.Delete) && SelectedEntityId >= 0)
        {
            string removedName = _world.Names[SelectedEntityId];
            _world.RemoveEntity(SelectedEntityId);
            SelectedEntityId = Math.Min(SelectedEntityId, _world.Count - 1);
            MR.Log($"Entitas '{removedName}' dihapus.");
        }

        // Tangani Drag & Drop file 3D (.obj/.glb/.gltf) atau Tekstur (.png/.jpg) langsung dari Windows Explorer
        if (Raylib.IsFileDropped())
        {
            HandleDroppedFiles();
        }

        // Hitung tata letak (Layout) panel Visual Editor
        const int topBarH = 38;
        const int leftW = 250;
        const int rightW = 310;
        const int bottomH = 175;

        Rectangle vpRect = (IsPlayMode && FullscreenPlayViewport)
            ? new Rectangle(0, topBarH, screenW, screenH - topBarH)
            : new Rectangle(leftW, topBarH, Math.Max(320, screenW - leftW - rightW), Math.Max(200, screenH - topBarH - bottomH));

        _renderer.ResizeViewport((int)vpRect.Width, (int)vpRect.Height);

        Vector2 mousePos = Raylib.GetMousePosition();
        bool vpHovered = Raylib.CheckCollisionPointRec(mousePos, vpRect);

        // 1. Update Simulasi ECS & Kamera (Diukur dengan Stopwatch akurasi tinggi)
        _cpuTimer.Restart();
        _world.UpdateSimulation(dt);

        if (IsPlayMode)
        {
            _fps.UpdateFirstPersonPlay(_world, dt);
        }
        else
        {
            _fps.UpdateEditorFreeFly(dt, vpHovered);
            HandleEditorViewportPicking(vpRect, mousePos, vpHovered);
        }
        _cpuTimer.Stop();
        LastCpuUpdateMs = _cpuTimer.Elapsed.TotalMilliseconds;

        // 2. Render Scene 3D ke Viewport Texture
        _cpuTimer.Restart();
        _renderer.RenderSceneToViewport(
            _world,
            _fps.Camera,
            SelectedEntityId,
            IsPlayMode,
            _fps.RecoilKick,
            _fps.HeadBobTimer);
        _cpuTimer.Stop();
        LastCpuRenderMs = _cpuTimer.Elapsed.TotalMilliseconds;

        // Catat grafik Frame Time (ms)
        _frameTimeHistory[_frameHistoryIndex] = dt * 1000.0f;
        _frameHistoryIndex = (_frameHistoryIndex + 1) % _frameTimeHistory.Length;

        // 3. Gambar Seluruh UI Visual Editor
        Raylib.BeginDrawing();
        Raylib.ClearBackground(MRGui.BgDark);

        DrawTopToolbar(screenW, topBarH);
        DrawCenterViewport(vpRect);

        if (!(IsPlayMode && FullscreenPlayViewport))
        {
            DrawHierarchyPanel(new Rectangle(0, topBarH, leftW, screenH - topBarH - bottomH));
            DrawInspectorPanel(new Rectangle(screenW - rightW, topBarH, rightW, screenH - topBarH - bottomH));
            DrawBottomProfilerAndConsole(new Rectangle(0, screenH - bottomH, screenW, bottomH));
        }

        Raylib.EndDrawing();
    }

    private void HandleDroppedFiles()
    {
        string[] files = Raylib.GetDroppedFiles();
        foreach (string srcPath in files)
        {
            if (string.IsNullOrWhiteSpace(srcPath) || !File.Exists(srcPath))
                continue;

            string ext = Path.GetExtension(srcPath).ToLowerInvariant();
            string fileName = Path.GetFileName(srcPath);

            if (ext is ".obj" or ".glb" or ".gltf" or ".iqm" or ".m3d")
            {
                string destPath = Path.Combine(AssetManager.ModelsDirectory, fileName);
                if (!Path.GetFullPath(srcPath).Equals(Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(srcPath, destPath, overwrite: true);
                }

                short modelId = AssetManager.LoadModel(destPath);
                if (modelId >= 0)
                {
                    Vector3 spawnPos = _fps.Camera.Position + _fps.ForwardDirection * 5.0f;
                    SelectedEntityId = MR.SpawnModel(
                        Path.GetFileNameWithoutExtension(fileName),
                        spawnPos,
                        new Vector3(1.5f),
                        modelId,
                        Color.White);
                    MR.Log($"[Drag&Drop] Model 3D '{fileName}' di-spawn ke scene!");
                }
            }
            else if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tga")
            {
                string destPath = Path.Combine(AssetManager.TexturesDirectory, fileName);
                if (!Path.GetFullPath(srcPath).Equals(Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(srcPath, destPath, overwrite: true);
                }

                short texId = AssetManager.LoadTexture(destPath);
                if (texId >= 0 && SelectedEntityId >= 0 && SelectedEntityId < _world.Count)
                {
                    _world.Renders[SelectedEntityId].TextureId = texId;
                    MR.Log($"[Drag&Drop] Tekstur '{fileName}' dipasang ke '{_world.Names[SelectedEntityId]}'!");
                }
            }
            else
            {
                MR.Log($"[Drag&Drop] Format '{ext}' tidak didukung (gunakan .obj/.glb/.gltf/.png/.jpg).");
            }
        }
    }

    private void TogglePlayMode()
    {
        IsPlayMode = !IsPlayMode;
        if (IsPlayMode)
        {
            _fps.EnterPlayMode();
            MR.Log("Masuk ke Mode PLAY First-Person (Tekan F5 / TAB untuk kembali ke Editor).");
        }
        else
        {
            _fps.ExitPlayMode();
            MR.Log("Kembali ke Mode Visual Editor.");
        }
    }

    private void HandleEditorViewportPicking(Rectangle vpRect, Vector2 mousePos, bool vpHovered)
    {
        if (!vpHovered || !Raylib.IsMouseButtonPressed(MouseButton.Left))
            return;

        // Konversi koordinat Mouse di dalam Viewport ke Ray 3D
        float normX = (mousePos.X - vpRect.X) / vpRect.Width;
        float normY = (mousePos.Y - vpRect.Y) / vpRect.Height;

        Vector2 virtualScreenPos = new(normX * Raylib.GetScreenWidth(), normY * Raylib.GetScreenHeight());
        Ray ray = Raylib.GetMouseRay(virtualScreenPos, _fps.Camera);

        var hit = _world.Raycast(ray, 180.0f, ignorePlaneFloor: true);
        if (hit.Hit)
        {
            SelectedEntityId = hit.EntityId;
        }
    }

    private void DrawTopToolbar(int screenW, int height)
    {
        Raylib.DrawRectangle(0, 0, screenW, height, MRGui.PanelHeader);
        Raylib.DrawLine(0, height - 1, screenW, height - 1, MRGui.BorderCol);

        // Brand Logo MissingRed
        Raylib.DrawRectangle(10, 7, 10, 24, MRGui.AccentRed);
        Raylib.DrawText("MISSINGRED STUDIO", 26, 11, 16, MRGui.TextLight);

        // Tombol PLAY / STOP First-Person
        Color playBtnCol = IsPlayMode ? MRGui.AccentRed : MRGui.AccentGreen;
        string playLabel = IsPlayMode ? "STOP [F5 / TAB]" : "PLAY FIRST-PERSON [F5]";
        if (MRGui.Button(new Rectangle(215, 6, 175, 26), playLabel, playBtnCol))
        {
            TogglePlayMode();
        }

        // Toggle Optimisasi Cepat di Toolbar
        MRGui.ToggleButton(new Rectangle(402, 6, 130, 26), "Frustum Cull", ref EngineConfig.EnableFrustumCulling);
        MRGui.ToggleButton(new Rectangle(538, 6, 118, 26), "Dist Cull", ref EngineConfig.EnableDistanceCulling);
        MRGui.ToggleButton(new Rectangle(662, 6, 112, 26), "Auto-LOD", ref EngineConfig.EnableAutoLOD);
        MRGui.ToggleButton(new Rectangle(780, 6, 90, 26), "Fog", ref EngineConfig.EnableFog);
        MRGui.ToggleButton(new Rectangle(876, 6, 110, 26), "Wireframe", ref EngineConfig.WireframeMode);

        // Tombol Benchmark Stress Test (+1,000 Entitas) & Reset Demo
        if (MRGui.Button(new Rectangle(996, 6, 115, 26), "+1000 Entities", new Color(65, 50, 95, 255)))
        {
            MR.SpawnOptimizationStressTest(1000);
        }
        if (MRGui.Button(new Rectangle(1117, 6, 85, 26), "Reset Map"))
        {
            MR.BuildDemoFirstPersonArena();
            SelectedEntityId = Math.Min(10, _world.Count - 1);
        }

        // Save & Load JSON
        if (MRGui.Button(new Rectangle(1208, 6, 62, 26), "Save"))
        {
            SceneSerializer.SaveScene(_world);
        }
        if (MRGui.Button(new Rectangle(1274, 6, 62, 26), "Load"))
        {
            SceneSerializer.LoadScene(_world);
        }
    }

    private void DrawCenterViewport(Rectangle vpRect)
    {
        Texture2D tex = _renderer.ViewportTexture.Texture;
        // OpenGL RenderTexture terbalik secara vertikal, jadi tinggi source diberi tanda minus
        Rectangle src = new(0, 0, tex.Width, -tex.Height);
        Raylib.DrawTexturePro(tex, src, vpRect, Vector2.Zero, 0f, Color.White);
        Raylib.DrawRectangleLinesEx(vpRect, 1, IsPlayMode ? MRGui.AccentRed : MRGui.BorderCol);

        // Overlay Informasi di Pojok Kiri Atas Viewport
        int ox = (int)vpRect.X + 12;
        int oy = (int)vpRect.Y + 10;
        Raylib.DrawRectangle(ox - 6, oy - 4, 420, 48, new Color(12, 14, 18, 195));

        if (IsPlayMode)
        {
            Raylib.DrawText("FIRST-PERSON PLAY MODE | WASD: Jalan | Shift: Lari | Space: Lompat | Ctrl: Merunduk", ox, oy, 12, new Color(255, 110, 110, 255));
            string targetInfo = _fps.CrosshairHit.Hit && _fps.CrosshairHit.EntityId >= 0
                ? $"Target Crosshair: {_world.Names[_fps.CrosshairHit.EntityId]} ({_fps.CrosshairHit.Distance:0.1f}m) | [Klik Kiri] Tembak | [Klik Kanan] Angkat"
                : "[Klik Kiri] Tembak Pulse | [Klik Kanan] Angkat Objek | [F11] Layar Penuh | [TAB/F5] Keluar";
            Raylib.DrawText(targetInfo, ox, oy + 20, 12, MRGui.TextLight);
        }
        else
        {
            Raylib.DrawText("EDITOR VIEWPORT | [Tahan Klik Kanan + WASD/QE] Kamera Terbang | [Klik Kiri] Pilih Objek", ox, oy, 12, MRGui.TextLight);
            Raylib.DrawText($"Kamera: ({_fps.Camera.Position.X:0.1f}, {_fps.Camera.Position.Y:0.1f}, {_fps.Camera.Position.Z:0.1f}) | FOV: {_fps.Camera.FovY:0}° | Tekan [F5] untuk Masuk First-Person", ox, oy + 20, 12, MRGui.TextMuted);
        }
    }

    private void DrawHierarchyPanel(Rectangle rect)
    {
        MRGui.DrawPanel(rect, $"SCENE HIERARCHY ({_world.Count})");

        int x = (int)rect.X + 8;
        int y = (int)rect.Y + 34;
        int w = (int)rect.Width - 16;

        // Tombol Tambah Objek Baru Cepat (+Box, +Sphere, +3D Model)
        float thirdW = (w - 8) / 3.0f;
        if (MRGui.Button(new Rectangle(x, y, thirdW, 24), "+ Box", new Color(45, 85, 65, 255)))
        {
            Vector3 spawnPos = _fps.Camera.Position + _fps.ForwardDirection * 5.0f;
            SelectedEntityId = MR.SpawnBox($"Box_{_world.Count}", spawnPos, new Vector3(1.5f), new Color(225, 70, 75, 255));
        }
        if (MRGui.Button(new Rectangle(x + thirdW + 4, y, thirdW, 24), "+ Sphere", new Color(45, 70, 105, 255)))
        {
            Vector3 spawnPos = _fps.Camera.Position + _fps.ForwardDirection * 5.0f;
            SelectedEntityId = MR.SpawnSphere($"Sphere_{_world.Count}", spawnPos, 1.5f, new Color(75, 165, 245, 255));
        }
        if (MRGui.Button(new Rectangle(x + (thirdW + 4) * 2, y, thirdW, 24), "+ 3D Model", new Color(115, 50, 95, 255)))
        {
            AssetManager.ScanAssetsFolder();
            short mdlId = AssetManager.Models.Count > 0 ? (short)0 : (short)-1;
            string mdlName = AssetManager.Models.Count > 0 ? AssetManager.Models[0].Name : "Model";
            Vector3 spawnPos = _fps.Camera.Position + _fps.ForwardDirection * 5.0f;
            SelectedEntityId = MR.SpawnModel($"{mdlName}_{_world.Count}", spawnPos, new Vector3(1.5f), mdlId, Color.White, textureId: 0);
        }
        y += 30;

        // Scroll daftar entitas dengan Mouse Wheel saat kursor berada di panel Hierarchy
        if (Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), rect))
        {
            int wheel = (int)Raylib.GetMouseWheelMove();
            if (wheel != 0)
            {
                _hierarchyScroll = Math.Clamp(_hierarchyScroll - wheel * 3, 0, Math.Max(0, _world.Count - 10));
            }
        }

        int rowH = 22;
        int maxRows = ((int)rect.Height - 72) / rowH;

        for (int r = 0; r < maxRows; r++)
        {
            int id = _hierarchyScroll + r;
            if (id >= _world.Count)
                break;

            Rectangle itemRect = new(x, y + r * rowH, w, rowH - 2);
            bool selected = (id == SelectedEntityId);
            Color rowCol = selected ? new Color(155, 42, 55, 255) : new Color(32, 36, 46, 255);

            if (MRGui.Button(itemRect, $"[{id}] {_world.Names[id]}", rowCol))
            {
                SelectedEntityId = id;
            }
        }
    }

    private void DrawInspectorPanel(Rectangle rect)
    {
        MRGui.DrawPanel(rect, "INSPECTOR & PROPERTIES");

        if (SelectedEntityId < 0 || SelectedEntityId >= _world.Count)
        {
            Raylib.DrawText("Pilih objek di Hierarchy atau Viewport.", (int)rect.X + 12, (int)rect.Y + 40, 13, MRGui.TextMuted);
            return;
        }

        int x = (int)rect.X + 10;
        int y = (int)rect.Y + 34;
        int w = (int)rect.Width - 20;

        ref var tr = ref _world.Transforms[SelectedEntityId];
        ref var ren = ref _world.Renders[SelectedEntityId];
        ref var beh = ref _world.Behaviors[SelectedEntityId];

        // Nama & Tombol Fokus / Hapus
        Raylib.DrawText($"Entity #{SelectedEntityId}: {_world.Names[SelectedEntityId]}", x, y, 14, MRGui.TextLight);
        y += 22;

        if (MRGui.Button(new Rectangle(x, y, (w - 6) * 0.5f, 22), "Fokus Kamera"))
        {
            _fps.Camera.Position = tr.Position - _fps.ForwardDirection * (tr.BoundingRadius * 3.0f + 2.5f);
            _fps.SyncCameraTarget();
        }
        if (MRGui.Button(new Rectangle(x + (w + 6) * 0.5f, y, (w - 6) * 0.5f, 22), "Hapus [Del]", new Color(130, 38, 45, 255)))
        {
            _world.RemoveEntity(SelectedEntityId);
            SelectedEntityId = Math.Min(SelectedEntityId, _world.Count - 1);
            return;
        }
        y += 30;

        // Transform Position
        Raylib.DrawText("TRANSFORM POSITION", x, y, 12, MRGui.TextMuted);
        y += 16;
        tr.Position.X = MRGui.SliderFloat(new Rectangle(x, y, w, 20), "Pos X", tr.Position.X, -80f, 80f, new Color(185, 50, 55, 255));
        y += 24;
        tr.Position.Y = MRGui.SliderFloat(new Rectangle(x, y, w, 20), "Pos Y", tr.Position.Y, -10f, 40f, new Color(45, 165, 80, 255));
        y += 24;
        tr.Position.Z = MRGui.SliderFloat(new Rectangle(x, y, w, 20), "Pos Z", tr.Position.Z, -80f, 80f, new Color(55, 115, 205, 255));
        beh.OriginPosition = tr.Position;
        y += 28;

        // Transform Scale & Rotation Y
        Raylib.DrawText("SCALE & ROTATION", x, y, 12, MRGui.TextMuted);
        y += 16;
        tr.Scale.X = MRGui.SliderFloat(new Rectangle(x, y, w, 20), "Scale X", tr.Scale.X, 0.2f, 40f, new Color(110, 75, 160, 255));
        y += 24;
        tr.Scale.Y = MRGui.SliderFloat(new Rectangle(x, y, w, 20), "Scale Y", tr.Scale.Y, 0.2f, 40f, new Color(110, 75, 160, 255));
        y += 24;
        tr.Scale.Z = MRGui.SliderFloat(new Rectangle(x, y, w, 20), "Scale Z", tr.Scale.Z, 0.2f, 40f, new Color(110, 75, 160, 255));
        y += 24;
        tr.RotationEuler.Y = MRGui.SliderFloat(new Rectangle(x, y, w, 20), "Yaw (°)", tr.RotationEuler.Y, 0f, 360f, new Color(80, 120, 140, 255));
        tr.RecalculateBounds();
        y += 28;

        // Mesh Type & Custom 3D Model / Texture Selector
        Raylib.DrawText($"MESH & 3D ASSETS (Models: {AssetManager.Models.Count}, Tex: {AssetManager.Textures.Count})", x, y, 12, MRGui.TextMuted);
        y += 16;
        if (MRGui.Button(new Rectangle(x, y, w, 22), $"Bentuk Mesh: {ren.Mesh}"))
        {
            ren.Mesh = (MeshType)(((int)ren.Mesh + 1) % 6);
            if (ren.Mesh == MeshType.CustomModel && ren.ModelId < 0 && AssetManager.Models.Count > 0)
            {
                ren.ModelId = 0;
            }
        }
        y += 25;

        // Pemilih File Model 3D (.obj / .glb / .gltf dari Assets/Models)
        string currentModelLabel = AssetManager.TryGetModel(ren.ModelId, out var mdlInfo)
            ? $"#{ren.ModelId} {mdlInfo.Name}"
            : "None (Primitive)";
        if (MRGui.Button(new Rectangle(x, y, w, 22), $"3D Model: {currentModelLabel}", new Color(65, 45, 85, 255)))
        {
            AssetManager.ScanAssetsFolder();
            if (AssetManager.Models.Count > 0)
            {
                ren.ModelId = (short)((ren.ModelId + 1) % AssetManager.Models.Count);
                ren.Mesh = MeshType.CustomModel;
            }
        }
        y += 25;

        // Pemilih Tekstur 2D (.png / .jpg dari Assets/Textures)
        string currentTexLabel = AssetManager.TryGetTexture(ren.TextureId, out var texInfo)
            ? $"#{ren.TextureId} {texInfo.Name}"
            : "None (Solid Color)";
        if (MRGui.Button(new Rectangle(x, y, w, 22), $"Texture: {currentTexLabel}", new Color(45, 75, 95, 255)))
        {
            AssetManager.ScanAssetsFolder();
            int nextTex = ren.TextureId + 1;
            if (nextTex >= AssetManager.Textures.Count)
                nextTex = -1; // Kembali ke None (Solid Color)
            ren.TextureId = (short)nextTex;
        }
        y += 26;

        // Material Color Tint (RGB)
        Raylib.DrawText("MATERIAL COLOR TINT (RGB)", x, y, 12, MRGui.TextMuted);
        y += 16;
        ren.Tint.R = MRGui.SliderByte(new Rectangle(x, y, w, 18), "Red", ren.Tint.R, new Color(195, 45, 55, 255));
        y += 21;
        ren.Tint.G = MRGui.SliderByte(new Rectangle(x, y, w, 18), "Green", ren.Tint.G, new Color(45, 175, 75, 255));
        y += 21;
        ren.Tint.B = MRGui.SliderByte(new Rectangle(x, y, w, 18), "Blue", ren.Tint.B, new Color(55, 115, 215, 255));
        y += 24;

        // Data-Oriented Behavior Selector
        Raylib.DrawText($"ECS BEHAVIOR: {beh.Type}", x, y, 12, MRGui.TextMuted);
        y += 16;
        if (MRGui.Button(new Rectangle(x, y, w, 22), $"Ubah Behavior ({beh.Type})"))
        {
            beh.Type = (EntityBehavior)(((int)beh.Type + 1) % 5);
        }
    }

    private void DrawBottomProfilerAndConsole(Rectangle rect)
    {
        float halfW = rect.Width * 0.52f;
        Rectangle profRect = new(rect.X, rect.Y, halfW, rect.Height);
        Rectangle logRect = new(rect.X + halfW, rect.Y, rect.Width - halfW, rect.Height);

        MRGui.DrawPanel(profRect, "OPTIMIZATION & PERFORMANCE PROFILER (ZERO-GC ECS)");
        MRGui.DrawPanel(logRect, "ENGINE CONSOLE & CUSTOM API (MR.*)");

        // 1. Isi Panel Profiler
        int px = (int)profRect.X + 12;
        int py = (int)profRect.Y + 34;

        int fps = Raylib.GetFPS();
        float frameMs = Raylib.GetFrameTime() * 1000.0f;
        long managedRamMb = GC.GetTotalMemory(false) / (1024 * 1024);

        Color fpsCol = fps >= 120 ? MRGui.AccentGreen : (fps >= 60 ? Color.Gold : MRGui.AccentRed);
        Raylib.DrawText($"FPS: {fps} ({frameMs:0.2f} ms/frame)", px, py, 16, fpsCol);
        Raylib.DrawText($"CPU ECS Update: {LastCpuUpdateMs:0.03f} ms | CPU Render/Cull: {LastCpuRenderMs:0.03f} ms", px, py + 22, 13, MRGui.TextLight);
        Raylib.DrawText(
            $"Entities Total: {_world.Count} | Visible: {_world.VisibleCount} | Culled (Frustum: {_world.CulledByFrustumCount}, Dist: {_world.CulledByDistanceCount})",
            px, py + 42, 13, MRGui.TextLight);
        Raylib.DrawText(
            $"Auto-LOD Low-Poly: {_world.LodLowCount} | Draw Calls: {_renderer.DrawCallsThisFrame} | Managed RAM: {managedRamMb} MB (GC Gen0: {GC.CollectionCount(0)})",
            px, py + 62, 13, MRGui.TextMuted);

        // Slider Jarak Culling Langsung di Profiler
        EngineConfig.MaxDrawDistance = MRGui.SliderFloat(
            new Rectangle(px, py + 86, 280, 20),
            "Max Draw Distance (m)",
            EngineConfig.MaxDrawDistance,
            25.0f,
            220.0f,
            new Color(55, 125, 195, 255));

        // Grafik Real-Time Frame Time (ms) di sebelah kanan Profiler
        Rectangle graphRect = new(profRect.X + profRect.Width - 255, profRect.Y + 34, 240, profRect.Height - 44);
        Raylib.DrawRectangleRec(graphRect, new Color(14, 16, 22, 255));
        Raylib.DrawRectangleLinesEx(graphRect, 1, MRGui.BorderCol);
        Raylib.DrawText("Frame Time Graph (ms)", (int)graphRect.X + 8, (int)graphRect.Y + 6, 12, MRGui.TextMuted);

        int barCount = _frameTimeHistory.Length;
        float barW = (graphRect.Width - 8) / barCount;
        for (int i = 0; i < barCount; i++)
        {
            int idx = (_frameHistoryIndex + i) % barCount;
            float ms = _frameTimeHistory[idx];
            float normH = Math.Clamp(ms / 33.3f, 0.04f, 1.0f) * (graphRect.Height - 26);
            int bx = (int)(graphRect.X + 4 + i * barW);
            int by = (int)(graphRect.Y + graphRect.Height - 4 - normH);
            Color bCol = ms <= 8.5f ? MRGui.AccentGreen : (ms <= 16.7f ? Color.Gold : MRGui.AccentRed);
            Raylib.DrawRectangle(bx, by, Math.Max(1, (int)barW), (int)normH, bCol);
        }

        // 2. Isi Panel Console Log
        int lx = (int)logRect.X + 12;
        int ly = (int)logRect.Y + 34;
        var logs = MR.Logs;
        int startIdx = Math.Max(0, logs.Count - 6);
        for (int i = startIdx; i < logs.Count; i++)
        {
            Color c = (i == logs.Count - 1) ? MRGui.TextLight : MRGui.TextMuted;
            Raylib.DrawText(logs[i], lx, ly + (i - startIdx) * 20, 13, c);
        }
    }
}
