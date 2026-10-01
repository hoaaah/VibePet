namespace DesktopPet.Models;

public enum PetAnimationState
{
    Idle = 0,          // Row 0: Frames 0-5
    RunningRight = 1,  // Row 1: Frames 0-7
    RunningLeft = 2,   // Row 2: Frames 0-7
    Waving = 3,        // Row 3: Frames 0-3
    Jumping = 4,       // Row 4: Frames 0-4
    Failed = 5,        // Row 5: Frames 0-7
    Waiting = 6,       // Row 6: Frames 0-5
    Running = 7,       // Row 7: Frames 0-5 (PC Work)
    Review = 8,        // Row 8: Frames 0-5 (Typing/Working)
    Gaze = 9           // Rows 9-10: 16 directional poses
}

/// <summary>
/// Cara pet memperlihatkan "komputer sedang bekerja".
/// </summary>
public enum WorkAnimationStyle
{
    Static = 0,   // Row 7 di tempat
    Pacing = 1,   // Berlari bolak-balik kiri/kanan (Row 1/2) di sekitar posisi pet
}

/// <summary>
/// Sumber yang membuat pet berada di state ComputerWork. Disimpan terpisah agar
/// satu sumber (misal CPU kembali normal) tidak menghapus sumber lain (misal build masih berjalan).
/// </summary>
[Flags]
public enum WorkSource
{
    None = 0,
    Simulation = 1,
    CpuLoad = 2,
    Ipc = 4,
    Process = 8,
}

public enum GazeDirection
{
    // Row 9: 0° to 157.5° (clockwise, 0° is up)
    Deg0 = 0,
    Deg22_5 = 1,
    Deg45 = 2,
    Deg67_5 = 3,
    Deg90 = 4,
    Deg112_5 = 5,
    Deg135 = 6,
    Deg157_5 = 7,

    // Row 10: 180° to 337.5°
    Deg180 = 8,
    Deg202_5 = 9,
    Deg225 = 10,
    Deg247_5 = 11,
    Deg270 = 12,
    Deg292_5 = 13,
    Deg315 = 14,
    Deg337_5 = 15
}
