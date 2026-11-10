# MissingRed Engine — Dokumentasi Resmi (v0.1.0 First-Person Optimized)

Selamat datang di **MissingRed Engine**, game engine 3D berbasis C# (.NET 9) yang dirancang khusus untuk **Game First-Person** dengan fokus utama pada **Optimisasi Ekstrem (Zero-GC Data-Oriented ECS, Frustum/Distance Culling, Auto-LOD, dan Ukuran Disk Super Ringan)**.

---

## 1. Cara Menjalankan Engine & Visual Editor

Buka terminal di folder `w:\__Engine_MissingRed`, lalu jalankan:

```powershell
dotnet run
```

Untuk build mode **Release** (optimisasi penuh `.NET TieredPGO` & `AggressiveOptimization`):

```powershell
dotnet run -c Release
```

---

## 2. Shortcut Visual Editor & Mode First-Person

| Tombol / Aksi | Fungsi di **Mode Visual Editor** | Fungsi di **Mode PLAY First-Person** |
| :--- | :--- | :--- |
| **`F5` / `TAB`** | Masuk ke Mode **PLAY First-Person** | Kembali ke **Visual Editor** |
| **`F11`** | Toggle ukuran layar penuh Viewport | Toggle Layar Penuh First-Person |
| **`Klik Kanan (Tahan)` + `WASD/QE`** | Kamera Terbang (*Free-Fly*) di Viewport | Mengangkat & memindahkan objek di depan mata (*Telekinesis / Grab*) |
| **`Klik Kiri`** | Memilih objek 3D langsung di Viewport (*Raycast Picking*) | Menembak *Pulse Blaster* & memberi dorongan fisika ke objek |
| **`WASD`** | (Gunakan bersama Klik Kanan untuk terbang) | Jalan First-Person (*Zero Input Lag*) |
| **`Left Shift`** | Terbang lebih cepat di Editor | **Sprint** (Lari cepat + efek *Dynamic FOV*) |
| **`Space`** | — | **Jump** (Lompat) |
| **`Left Ctrl` / `C`** | — | **Crouch** (Merunduk) |
| **`Delete`** | Menghapus objek yang sedang dipilih | — |
| **`Mouse Wheel`** | Zoom kamera di Viewport / Scroll nilai di Inspector | Mengatur jarak objek yang sedang diangkat |

---

## 3. Arsitektur Optimisasi MissingRed Engine

1. **Structure-of-Arrays (SoA) Data-Oriented ECS (`ECS/WorldData.cs`):**
   * Tidak menggunakan `class GameObject` per objek yang membebani *Garbage Collector*.
   * Seluruh data `TransformComponent`, `RenderComponent`, dan `BehaviorComponent` disimpan sebagai `struct` berurutan di memori (*Contiguous Array*), memaksimalkan kecepatan **CPU L1/L2 Cache** dan menghasilkan **0 byte alokasi GC** saat game berjalan.
   * Penghapusan objek menggunakan algoritma **Swap-and-Pop $O(1)$** tanpa fragmentasi memori.

2. **6-Plane Camera Frustum Culling & Distance Culling (`Rendering/FrustumCuller.cs`):**
   * Mengekstrak 6 bidang pandang kamera menggunakan algoritma *Gribb-Hartmann*.
   * Objek di belakang kamera, di samping layar, atau di luar `EngineConfig.MaxDrawDistance` langsung dilewati dalam hitungan mikrodetik sebelum menyentuh GPU.

3. **Auto-LOD (Level of Detail) & Atmospheric Fog (`Rendering/RenderPipeline.cs`):**
   * Objek yang berada jauh dari pemain otomatis dirender dengan jumlah poligon lebih rendah (*Low-LOD*) dan menyatu mulus dengan kabut atmosferik (*Distance Fog*).

---

## 4. Custom Developer API (`MR.*`) — Cheatsheet Pembuatan Game

Kamu bisa memanggil fungsi-fungsi khusus di `MissingRed.Core.MR` dari mana saja untuk membuat mekanik game First-Person dengan cepat:

```csharp
using System.Numerics;
using MissingRed.Core;
using MissingRed.ECS;
using Raylib_cs;

// 1. Memunculkan objek 3D hanya dalam 1 baris
int boxId = MR.SpawnBox("Crate_Ammo", new Vector3(0, 1, -5), new Vector3(1, 1, 1), Color.Red);
int orbId = MR.SpawnSphere("Healing_Orb", new Vector3(3, 2, -5), diameter: 1.2f, Color.SkyBlue, EntityBehavior.HoverBob);
int pillarId = MR.SpawnPillar("Hall_Pillar", new Vector3(-4, 3, -10), new Vector3(1.5f, 6f, 1.5f), Color.Gray);

// 2. Mengecek objek apa yang sedang ditunjuk Crosshair pemain First-Person
if (MR.RaycastCrosshair(maxDistance: 25.0f, out RaycastHitResult hit))
{
    MR.Log($"Pemain melihat entitas #{hit.EntityId} pada jarak {hit.Distance:0.1f} meter!");
    MR.ApplyImpulse(hit.EntityId, new Vector3(0, 8, 0)); // Lontarkan ke atas!
}

// 3. Memicu efek guncangan kamera (Camera Shake)
MR.ShakeCamera(intensity: 0.15f, duration: 0.2f);

// 4. Mengatur kabut atmosfer & jarak render maksimum (Distance Culling)
MR.SetAtmosphere(new Color(18, 20, 28, 255), maxDrawDistance: 140.0f);

// 5. Memuat & Memunculkan Model 3D (.obj / .glb / .gltf) + Tekstur (.png / .jpg) dalam 1 baris!
int modelEntity = MR.SpawnModelFromFile(
    name: "SciFi_Artifact",
    position: new Vector3(0, 2, -8),
    scale: new Vector3(1.5f),
    modelPath: "Assets/Models/crystal_obelisk.obj",
    texturePath: "Assets/Textures/crimson_checker.png",
    tint: Color.White,
    behavior: EntityBehavior.RotateY);
```

---

## 5. Cara Menambahkan Assets 3D (Model & Tekstur)

Ada **3 cara mudah** untuk memasukkan model 3D (`.obj`, `.glb`, `.gltf`, `.iqm`, `.m3d`) dan tekstur (`.png`, `.jpg`, `.bmp`, `.tga`):

1. **Drag & Drop Langsung dari Windows Explorer ke Jendela Editor:**
   * Tarik (*drag*) file `.obj` / `.glb` / `.gltf` ke jendela **MissingRed Studio** → file otomatis disalin ke `Assets/Models/` dan langsung di-*spawn* di depan kamera.
   * Tarik (*drag*) file `.png` / `.jpg` ke jendela **MissingRed Studio** → file otomatis disalin ke `Assets/Textures/` dan langsung dipasang ke objek yang sedang dipilih.
2. **Lewat Folder `Assets/` & Panel Inspector:**
   * Taruh file model 3D di `Assets/Models/` dan gambar tekstur di `Assets/Textures/`.
   * Pilih objek di **Scene Hierarchy** (atau klik tombol **`+ 3D Model`**), lalu klik tombol **`3D Model: ...`** dan **`Texture: ...`** di panel **Inspector** sebelah kanan untuk mengganti model atau teksturnya secara *real-time*.
3. **Lewat Kode C# (`MR.*` API):**
   * Gunakan `MR.SpawnModelFromFile(...)`, `MR.LoadModel(...)`, `MR.LoadTexture(...)`, atau `MR.SetEntityTexture(entityId, texId)`.

---

## 6. Cara Save & Load Level (`scene_main.json`)

1. **Lewat Visual Editor (Top Toolbar):**
   * Klik tombol **`Save`** di kanan atas toolbar untuk menyimpan seluruh isi level (termasuk posisi, skala, rotasi, warna, behavior, serta path file model 3D & tekstur) ke **`scene_main.json`**.
   * Klik tombol **`Load`** untuk memuat kembali file **`scene_main.json`**.
   * Klik tombol **`Reset Map`** untuk mengembalikan level ke arena demo bawaan.
2. **Lewat Kode C# (`SceneSerializer`):**
   ```csharp
   // Simpan ke file JSON kustom
   SceneSerializer.SaveScene(world, "Assets/Levels/level_01.json");

   // Muat dari file JSON kustom
   SceneSerializer.LoadScene(world, "Assets/Levels/level_01.json");
   ```

---

## 7. Struktur Folder Project

* `Assets/Models/` & `Assets/Textures/` — Folder penyimpanan model 3D eksternal & tekstur gambar.
* `Core/AssetManager.cs` — Manajer Asset 3D & Tekstur berbasis ID (`short`) yang menjaga ECS tetap *Zero-GC*.
* `Core/EngineConfig.cs` — Pengaturan global optimisasi (Frustum Culling, Distance Culling, Auto-LOD, Fog, Target FPS).
* `Core/MR.cs` — Custom High-Level API (`MR.*`) untuk mempermudah pembuatan game First-Person.
* `Core/SceneSerializer.cs` — Sistem Save & Load level ke file JSON (`scene_main.json`).
* `ECS/Components.cs` — Definisi `struct` komponen memori kontigu.
* `ECS/WorldData.cs` — Inti ECS SoA, simulasi fisika/perilaku Zero-GC, dan *Fast Spatial Raycast*.
* `Rendering/FrustumCuller.cs` — Algoritma *6-Plane Frustum Culling* & *Distance Culling*.
* `Rendering/RenderPipeline.cs` — Pipeline render 3D, Auto-LOD, Gizmo 3D, dan *First-Person Viewmodel*.
* `FirstPerson/FirstPersonController.cs` — Kontroler First-Person responsif (*Walk, Sprint, Jump, Crouch, Headbob, Dynamic FOV, Crosshair Raycast, Telekinesis Grab*).
* `Editor/MRGui.cs` & `Editor/MissingRedEditor.cs` — Visual Editor **MissingRed Studio** lengkap dengan *Real-Time Profiler*.
