using System.Numerics;
using MissingRed.Core;
using MissingRed.ECS;
using Raylib_cs;

namespace MissingRed.FirstPerson;

/// <summary>
/// First-Person Character Controller & Editor Free-Fly Camera.
/// Dirancang untuk Zero Input Lag, lengkap dengan Walk, Sprint, Jump, Crouch,
/// Head-Bobbing, Dynamic FOV, Camera Shake, Crosshair Raycast, dan Object Grabbing.
/// </summary>
public sealed class FirstPersonController
{
    public Camera3D Camera;

    // Orientasi Kamera (Radian)
    public float Yaw = -MathF.PI * 0.75f;
    public float Pitch = -0.15f;

    // Parameter Pergerakan First-Person
    public float WalkSpeed = 7.5f;
    public float SprintSpeed = 13.0f;
    public float JumpForce = 7.2f;
    public float Gravity = 19.5f;
    public float MouseSensitivity = 0.0024f;
    public float BaseFov = 75.0f;

    // Status Fisika Pemain
    public Vector3 PlayerPosition = new(0f, 1.75f, 10f);
    public Vector3 Velocity = Vector3.Zero;
    public bool IsGrounded = true;
    public bool IsCrouching = false;

    // Efek Kamera First-Person
    public float HeadBobTimer { get; private set; }
    public float RecoilKick { get; private set; }
    private float _shakeTimer;
    private float _shakeIntensity;

    // Hasil Crosshair Raycast Real-time
    public RaycastHitResult CrosshairHit;
    public int GrabbedEntityId = -1;
    public float GrabDistance = 4.0f;

    public FirstPersonController()
    {
        Camera = new Camera3D
        {
            Position = new Vector3(12f, 9f, 14f),
            Target = Vector3.Zero,
            Up = Vector3.UnitY,
            FovY = BaseFov,
            Projection = CameraProjection.Perspective
        };
        SyncCameraTarget();
    }

    public Vector3 ForwardDirection => Vector3.Normalize(new Vector3(
        MathF.Cos(Pitch) * MathF.Cos(Yaw),
        MathF.Sin(Pitch),
        MathF.Cos(Pitch) * MathF.Sin(Yaw)));

    public Vector3 FlatForward => Vector3.Normalize(new Vector3(MathF.Cos(Yaw), 0f, MathF.Sin(Yaw)));
    public Vector3 RightDirection => Vector3.Normalize(Vector3.Cross(FlatForward, Vector3.UnitY));

    /// <summary>
    /// Memicu guncangan kamera (misal saat menembak, ledakan, atau mendarat).
    /// </summary>
    public void TriggerShake(float intensity, float duration)
    {
        _shakeIntensity = MathF.Max(_shakeIntensity, intensity);
        _shakeTimer = MathF.Max(_shakeTimer, duration);
    }

    /// <summary>
    /// Masuk ke mode First-Person Play dari posisi kamera saat ini.
    /// </summary>
    public void EnterPlayMode()
    {
        PlayerPosition = new Vector3(Camera.Position.X, MathF.Max(1.75f, Camera.Position.Y), Camera.Position.Z);
        Velocity = Vector3.Zero;
        GrabbedEntityId = -1;
        Raylib.DisableCursor();
    }

    /// <summary>
    /// Keluar dari mode First-Person Play kembali ke Visual Editor.
    /// </summary>
    public void ExitPlayMode()
    {
        GrabbedEntityId = -1;
        Camera.FovY = BaseFov;
        Raylib.EnableCursor();
    }

    /// <summary>
    /// Update kamera saat berada di mode Editor (tahan Klik Kanan di Viewport untuk Free-Fly).
    /// </summary>
    public void UpdateEditorFreeFly(float dt, bool viewportHovered)
    {
        if (viewportHovered && Raylib.IsMouseButtonDown(MouseButton.Right))
        {
            Vector2 delta = Raylib.GetMouseDelta();
            Yaw += delta.X * MouseSensitivity;
            Pitch = Math.Clamp(Pitch - delta.Y * MouseSensitivity, -1.52f, 1.52f);

            float speed = Raylib.IsKeyDown(KeyboardKey.LeftShift) ? 22.0f : 10.0f;
            Vector3 move = Vector3.Zero;

            if (Raylib.IsKeyDown(KeyboardKey.W)) move += ForwardDirection;
            if (Raylib.IsKeyDown(KeyboardKey.S)) move -= ForwardDirection;
            if (Raylib.IsKeyDown(KeyboardKey.D)) move += RightDirection;
            if (Raylib.IsKeyDown(KeyboardKey.A)) move -= RightDirection;
            if (Raylib.IsKeyDown(KeyboardKey.E)) move += Vector3.UnitY;
            if (Raylib.IsKeyDown(KeyboardKey.Q)) move -= Vector3.UnitY;

            if (move.LengthSquared() > 0.001f)
                Camera.Position += Vector3.Normalize(move) * (speed * dt);
        }

        // Scroll wheel zoom di dalam viewport
        if (viewportHovered)
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (MathF.Abs(wheel) > 0.01f)
                Camera.Position += ForwardDirection * (wheel * 2.2f);
        }

        SyncCameraTarget();
    }

    /// <summary>
    /// Update penuh karakter First-Person saat mode Play aktif.
    /// </summary>
    public void UpdateFirstPersonPlay(WorldData world, float dt)
    {
        // 1. Raw Mouse Look
        Vector2 mouseDelta = Raylib.GetMouseDelta();
        Yaw += mouseDelta.X * MouseSensitivity;
        Pitch = Math.Clamp(Pitch - mouseDelta.Y * MouseSensitivity, -1.52f, 1.52f);

        // 2. Input Pergerakan (WASD + Sprint + Crouch + Jump)
        bool sprinting = Raylib.IsKeyDown(KeyboardKey.LeftShift);
        IsCrouching = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.C);

        float targetSpeed = IsCrouching ? WalkSpeed * 0.55f : (sprinting ? SprintSpeed : WalkSpeed);
        float eyeHeight = IsCrouching ? 1.05f : 1.75f;

        Vector3 wishDir = Vector3.Zero;
        if (Raylib.IsKeyDown(KeyboardKey.W)) wishDir += FlatForward;
        if (Raylib.IsKeyDown(KeyboardKey.S)) wishDir -= FlatForward;
        if (Raylib.IsKeyDown(KeyboardKey.D)) wishDir += RightDirection;
        if (Raylib.IsKeyDown(KeyboardKey.A)) wishDir -= RightDirection;

        if (wishDir.LengthSquared() > 0.001f)
        {
            wishDir = Vector3.Normalize(wishDir);
            HeadBobTimer += dt * (sprinting ? 14.0f : 9.5f);
        }
        else
        {
            HeadBobTimer = 0f;
        }

        // Kecepatan horizontal responsif
        Velocity.X = wishDir.X * targetSpeed;
        Velocity.Z = wishDir.Z * targetSpeed;

        // Lompat & Gravitasi
        if (IsGrounded && Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            Velocity.Y = JumpForce;
            IsGrounded = false;
        }

        Velocity.Y -= Gravity * dt;
        PlayerPosition += Velocity * dt;

        // Deteksi lantai dasar
        if (PlayerPosition.Y <= eyeHeight)
        {
            PlayerPosition.Y = eyeHeight;
            Velocity.Y = 0f;
            IsGrounded = true;
        }

        // Dynamic FOV saat Sprint
        float targetFov = sprinting && wishDir.LengthSquared() > 0.1f ? BaseFov + 10.0f : BaseFov;
        Camera.FovY = Single.Lerp(Camera.FovY, targetFov, dt * 10.0f);

        // Redam Recoil & Shake
        RecoilKick = MathF.Max(0f, RecoilKick - dt * 4.5f);
        Vector3 shakeOffset = Vector3.Zero;
        if (_shakeTimer > 0f)
        {
            _shakeTimer -= dt;
            shakeOffset = new Vector3(
                (Random.Shared.NextSingle() - 0.5f) * _shakeIntensity,
                (Random.Shared.NextSingle() - 0.5f) * _shakeIntensity,
                0f);
        }

        float bobY = IsGrounded ? MathF.Sin(HeadBobTimer) * 0.055f : 0f;
        Camera.Position = PlayerPosition + new Vector3(0f, bobY, 0f) + shakeOffset;
        SyncCameraTarget();

        // 3. Crosshair Raycast & Interaksi Senjata / Grabbing First-Person
        Ray centerRay = new(Camera.Position, ForwardDirection);
        CrosshairHit = world.Raycast(centerRay, 65.0f, ignorePlaneFloor: true);

        // Klik Kiri: Tembak / Beri Impuls Fisika ke Objek
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            RecoilKick = 1.0f;
            TriggerShake(0.08f, 0.09f);

            if (CrosshairHit.Hit && CrosshairHit.EntityId >= 0)
            {
                int id = CrosshairHit.EntityId;
                world.Behaviors[id].Velocity += ForwardDirection * 18.0f + new Vector3(0, 4.5f, 0);
            }
        }

        // Klik Kanan (Tahan): Telekinesis / Grab & Pindahkan Objek di Depan Kamera
        if (Raylib.IsMouseButtonPressed(MouseButton.Right) && CrosshairHit.Hit)
        {
            GrabbedEntityId = CrosshairHit.EntityId;
            GrabDistance = Math.Clamp(CrosshairHit.Distance, 2.5f, 15.0f);
        }
        else if (Raylib.IsMouseButtonReleased(MouseButton.Right))
        {
            GrabbedEntityId = -1;
        }

        if (GrabbedEntityId >= 0 && GrabbedEntityId < world.Count)
        {
            GrabDistance = Math.Clamp(GrabDistance + Raylib.GetMouseWheelMove() * 0.8f, 2.0f, 20.0f);
            Vector3 targetHoldPos = Camera.Position + ForwardDirection * GrabDistance;
            world.Transforms[GrabbedEntityId].Position = Vector3.Lerp(
                world.Transforms[GrabbedEntityId].Position,
                targetHoldPos,
                dt * 15.0f);
            world.Behaviors[GrabbedEntityId].OriginPosition = world.Transforms[GrabbedEntityId].Position;
        }
    }

    public void SyncCameraTarget()
    {
        Camera.Target = Camera.Position + ForwardDirection;
    }
}
