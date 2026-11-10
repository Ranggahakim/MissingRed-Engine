using System.Numerics;
using System.Text.Json;
using MissingRed.ECS;
using Raylib_cs;

namespace MissingRed.Core;

/// <summary>
/// Sistem penyimpanan dan pemuatan Scene berbasis JSON ringan.
/// </summary>
public static class SceneSerializer
{
    public sealed class SerializedEntity
    {
        public string Name { get; set; } = "Entity";
        public float[] Pos { get; set; } = [0, 0, 0];
        public float[] Rot { get; set; } = [0, 0, 0];
        public float[] Scale { get; set; } = [1, 1, 1];
        public byte Mesh { get; set; }
        public string? ModelPath { get; set; }
        public string? TexturePath { get; set; }
        public byte[] Color { get; set; } = [255, 255, 255, 255];
        public byte Flags { get; set; }
        public byte Behavior { get; set; }
        public float Speed { get; set; } = 2.0f;
        public float Amplitude { get; set; } = 0.5f;
    }

    public static void SaveScene(WorldData world, string filePath = "scene_main.json")
    {
        var list = new List<SerializedEntity>(world.Count);
        for (int i = 0; i < world.Count; i++)
        {
            ref readonly var tr = ref world.Transforms[i];
            ref readonly var ren = ref world.Renders[i];
            ref readonly var beh = ref world.Behaviors[i];

            string? modelPath = AssetManager.TryGetModel(ren.ModelId, out var mEntry) ? mEntry.FilePath : null;
            string? texPath = AssetManager.TryGetTexture(ren.TextureId, out var tEntry) ? tEntry.FilePath : null;

            list.Add(new SerializedEntity
            {
                Name = world.Names[i],
                Pos = [tr.Position.X, tr.Position.Y, tr.Position.Z],
                Rot = [tr.RotationEuler.X, tr.RotationEuler.Y, tr.RotationEuler.Z],
                Scale = [tr.Scale.X, tr.Scale.Y, tr.Scale.Z],
                Mesh = (byte)ren.Mesh,
                ModelPath = modelPath,
                TexturePath = texPath,
                Color = [ren.Tint.R, ren.Tint.G, ren.Tint.B, ren.Tint.A],
                Flags = (byte)world.Flags[i],
                Behavior = (byte)beh.Type,
                Speed = beh.Speed,
                Amplitude = beh.Amplitude
            });
        }

        string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
        MR.Log($"Scene berhasil disimpan ke '{filePath}' ({list.Count} entitas).");
    }

    public static void LoadScene(WorldData world, string filePath = "scene_main.json")
    {
        if (!File.Exists(filePath))
        {
            MR.Log($"File '{filePath}' belum ditemukan.");
            return;
        }

        string json = File.ReadAllText(filePath);
        var list = JsonSerializer.Deserialize<List<SerializedEntity>>(json);
        if (list == null) return;

        world.Clear();
        foreach (var item in list)
        {
            short modelId = !string.IsNullOrWhiteSpace(item.ModelPath) ? AssetManager.LoadModel(item.ModelPath) : (short)-1;
            short texId = !string.IsNullOrWhiteSpace(item.TexturePath) ? AssetManager.LoadTexture(item.TexturePath) : (short)-1;

            int id = world.CreateEntity(
                item.Name,
                new Vector3(item.Pos[0], item.Pos[1], item.Pos[2]),
                new Vector3(item.Scale[0], item.Scale[1], item.Scale[2]),
                (MeshType)item.Mesh,
                new Color(item.Color[0], item.Color[1], item.Color[2], item.Color[3]),
                (EntityFlags)item.Flags,
                (EntityBehavior)item.Behavior,
                item.Speed,
                item.Amplitude,
                modelId,
                texId);

            if (id >= 0)
            {
                world.Transforms[id].RotationEuler = new Vector3(item.Rot[0], item.Rot[1], item.Rot[2]);
            }
        }

        MR.Log($"Scene berhasil dimuat dari '{filePath}' ({world.Count} entitas).");
    }
}
