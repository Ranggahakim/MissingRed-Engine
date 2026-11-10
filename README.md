# MissingRed Engine 🔴

[![.NET Version](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-blue.svg)](https://github.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Zero-GC ECS](https://img.shields.io/badge/ECS-Zero--GC%20Data--Oriented-red.svg)](ECS/WorldData.cs)

**MissingRed Engine** adalah game engine 3D berbasis C# (.NET 9) dan [Raylib-cs](https://github.com/ChrisD/Raylib-cs) yang dirancang khusus untuk **Game First-Person** dengan performa tinggi. Engine ini menggabungkan arsitektur **Zero-GC Data-Oriented ECS (Structure-of-Arrays)**, **Frustum & Distance Culling**, **Auto-LOD**, serta **Visual In-Game Editor** terintegrasi.

---

## ✨ Fitur Utama

- ⚡ **Zero-GC Data-Oriented ECS (Structure-of-Arrays):**
  - Penyimpanan komponen secara kontigu di memori (`TransformComponent`, `RenderComponent`, `BehaviorComponent`).
  - Cache-friendly (memaksimalkan CPU L1/L2 cache).
  - Alokasi memori GC 0-byte selama gameplay loop berjalan.
  - Penghapusan entitas dengan algoritma **Swap-and-Pop $O(1)$** tanpa fragmentasi memori.

- 👁️ **6-Plane Camera Frustum & Distance Culling:**
  - Ekstraksi 6 bidang frustum kamera (*Gribb-Hartmann*).
  - Objek di luar jarak pandang atau di belakang kamera otomatis diabaikan (*culled*) sebelum perintah gambar dikirim ke GPU.

- 🎮 **Dedicated First-Person Controller:**
  - Gerakan mulus dengan sistem ground detection, jumping, crouch, dan sprint.
  - Dynamic FOV transitions saat sprint.
  - Interaksi *Physics Grab / Telekinesis* dan *Pulse Blaster* langsung terpasang.

- 🛠️ **In-Game Visual Editor:**
  - Tekan `F5` / `TAB` untuk beralih secara instan antara mode **Editor** dan mode **Play First-Person**.
  - Raycast 3D Object Picking untuk memilih dan memodifikasi objek secara visual.
  - Hierarchy & Inspector panel untuk manipulasi transform, warna, dan behavior.
  - Scene Save & Load berbasis JSON.

- 📦 **3D Asset Pipeline:**
  - Mendukung model 3D (`.obj`, `.glb`, `.gltf`, `.iqm`) dan tekstur (`.png`, `.jpg`).
  - Caching aset otomatis untuk menghindari duplikasi memori.

---

## 🕹️ Kontrol & Navigasi

| Tombol / Aksi | Mode Visual Editor | Mode PLAY First-Person |
| :--- | :--- | :--- |
| **`F5` / `TAB`** | Masuk ke mode **PLAY First-Person** | Kembali ke **Visual Editor** |
| **`F11`** | Toggle Fullscreen | Toggle Fullscreen |
| **`Klik Kanan (Tahan)` + `WASD/QE`** | Free-Fly Camera Viewport | Angkat & bawa objek (*Telekinesis / Grab*) |
| **`Klik Kiri`** | Pilih objek 3D (*Raycast Picking*) | Tembak *Pulse Blaster* (dorong objek fisik) |
| **`WASD`** | Gerak kamera saat Klik Kanan | Gerak pemain First-Person |
| **`Left Shift`** | Gerak kamera lebih cepat | **Sprint** (Lari cepat + Dynamic FOV) |
| **`Space`** | — | **Jump** (Lompat) |
| **`Left Ctrl` / `C`** | — | **Crouch** (Merunduk) |
| **`Delete`** | Hapus objek terpilih | — |
| **`Mouse Wheel`** | Zoom kamera / scroll nilai | Atur jarak objek yang diangkat |

---

## 🚀 Memulai (Quickstart)

### Prasyarat
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Menjalankan Engine

1. Clone repository ini:
   ```bash
   git clone https://github.com/ranggahakim05/MissingRed-Engine.git
   cd MissingRed-Engine
   ```

2. Jalankan dalam mode development:
   ```bash
   dotnet run
   ```

3. Atau jalankan mode **Release** untuk performa maksimal (.NET TieredPGO):
   ```bash
   dotnet run -c Release
   ```

---

## 💻 Developer API (`MR.*`)

Anda dapat memanipulasi dunia game dengan API statis yang ringkas:

```csharp
using System.Numerics;
using MissingRed.Core;
using MissingRed.ECS;
using Raylib_cs;

// Spawn objek primitif
int boxId = MR.SpawnBox("Crate_Ammo", new Vector3(0, 1, -5), new Vector3(1, 1, 1), Color.Red);
int orbId = MR.SpawnSphere("Healing_Orb", new Vector3(3, 2, -5), diameter: 1.2f, Color.SkyBlue, EntityBehavior.HoverBob);

// Spawn model 3D eksternal
int modelId = MR.SpawnModelFromFile(
    name: "Crystal",
    position: new Vector3(0, 2, -8),
    scale: new Vector3(1.5f),
    modelPath: "Assets/Models/crystal_obelisk.obj",
    texturePath: "Assets/Textures/crimson_checker.png",
    tint: Color.White,
    behavior: EntityBehavior.RotateY
);

// Raycast crosshair pemain
if (MR.RaycastCrosshair(maxDistance: 25.0f, out RaycastHitResult hit))
{
    MR.ApplyImpulse(hit.EntityId, new Vector3(0, 8, 0));
}

// Efek kamera & atmosfer
MR.ShakeCamera(intensity: 0.15f, duration: 0.2f);
MR.SetAtmosphere(new Color(18, 20, 28, 255), maxDrawDistance: 140.0f);
```

Untuk panduan dan arsitektur lengkap, silakan baca [DOCS.md](DOCS.md).

---

## 📁 Struktur Direktori

```text
├── Assets/
│   ├── Models/            # Model 3D (.obj, .glb, dll.)
│   └── Textures/          # Tekstur & gambar (.png, .jpg)
├── Core/
│   ├── AssetManager.cs    # Asset caching & loader
│   ├── EngineConfig.cs    # Konfigurasi resolusi, render, gameplay
│   ├── MR.cs              # High-level Developer API
│   └── SceneSerializer.cs # Save / Load sistem scene JSON
├── ECS/
│   ├── Components.cs      # Struct komponen SoA
│   └── WorldData.cs       # Contiguous memory array storage
├── Editor/
│   ├── MissingRedEditor.cs# Editor controller & UI
│   └── MRGui.cs           # Immediate-mode GUI renderer
├── FirstPerson/
│   └── FirstPersonController.cs # Movement, physics grab, shooting
├── Rendering/
│   ├── FrustumCuller.cs   # 6-plane frustum & distance culling
│   └── RenderPipeline.cs  # Draw calls & lighting/fog
├── Program.cs             # Entry point engine
├── MissingRed.Engine.csproj
└── DOCS.md                # Dokumentasi arsitektur lengkap
```

---

## 📄 Lisensi

Proyek ini dilisensikan di bawah lisensi [MIT](LICENSE).
