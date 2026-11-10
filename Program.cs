using MissingRed.Core;
using MissingRed.ECS;
using MissingRed.Editor;
using MissingRed.FirstPerson;
using MissingRed.Rendering;
using Raylib_cs;

namespace MissingRed;

internal static class Program
{
    private static void Main()
    {
        // Konfigurasi Window & OpenGL MSAA 4x untuk tepi 3D yang tajam namun ringan
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint);
        Raylib.InitWindow(1440, 840, $"{EngineConfig.EngineName} — {EngineConfig.EngineVersion}");
        Raylib.SetWindowMinSize(1024, 640);
        Raylib.SetExitKey(KeyboardKey.Null); // Agar tombol ESC tidak langsung menutup Editor
        Raylib.SetTargetFPS(EngineConfig.TargetFPS);

        // Inisialisasi Core Systems (Zero-GC SoA World, First-Person Controller, Optimized Renderer)
        var world = new WorldData(EngineConfig.MaxEntities);
        var fpsController = new FirstPersonController();
        using var renderPipeline = new RenderPipeline(1280, 720);

        // Hubungkan Custom Developer API (MR.*) dan muat Demo Level First-Person
        MR.Initialize(world, fpsController, renderPipeline);
        MR.BuildDemoFirstPersonArena();

        var editor = new MissingRedEditor(world, fpsController, renderPipeline);

        // Main Engine Loop
        while (!Raylib.WindowShouldClose())
        {
            float dt = Math.Min(Raylib.GetFrameTime(), 0.1f);
            editor.UpdateAndDraw(dt);
        }

        AssetManager.UnloadAll();
        Raylib.CloseWindow();
    }
}
