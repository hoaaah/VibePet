namespace DesktopPet.Models;

public record AnimationDefinition(
    PetAnimationState State,
    string DisplayName,
    int RowIndex,
    int[] FrameIndices,
    int[] FrameDurationsMs,
    bool IsLooping
)
{
    public int FrameCount => FrameIndices.Length;

    public int GetDuration(int frameIndex)
    {
        if (frameIndex >= 0 && frameIndex < FrameDurationsMs.Length)
        {
            return FrameDurationsMs[frameIndex];
        }
        return 150;
    }
}

public static class AnimationCatalog
{
    public const int CellWidth = 192;
    public const int CellHeight = 208;
    public const int SheetColumns = 8;
    public const int SheetRows = 11;

    public static readonly IReadOnlyDictionary<PetAnimationState, AnimationDefinition> Animations =
        new Dictionary<PetAnimationState, AnimationDefinition>
        {
            [PetAnimationState.Idle] = new(
                PetAnimationState.Idle,
                "Idle",
                RowIndex: 0,
                FrameIndices: [0, 1, 2, 3, 4, 5],
                FrameDurationsMs: [280, 110, 110, 140, 140, 320],
                IsLooping: true
            ),
            [PetAnimationState.RunningRight] = new(
                PetAnimationState.RunningRight,
                "Running Right",
                RowIndex: 1,
                FrameIndices: [0, 1, 2, 3, 4, 5, 6, 7],
                FrameDurationsMs: [120, 120, 120, 120, 120, 120, 120, 220],
                IsLooping: true
            ),
            [PetAnimationState.RunningLeft] = new(
                PetAnimationState.RunningLeft,
                "Running Left",
                RowIndex: 2,
                FrameIndices: [0, 1, 2, 3, 4, 5, 6, 7],
                FrameDurationsMs: [120, 120, 120, 120, 120, 120, 120, 220],
                IsLooping: true
            ),
            [PetAnimationState.Waving] = new(
                PetAnimationState.Waving,
                "Waving (Notification)",
                RowIndex: 3,
                FrameIndices: [0, 1, 2, 3],
                FrameDurationsMs: [140, 140, 140, 280],
                IsLooping: false
            ),
            [PetAnimationState.Jumping] = new(
                PetAnimationState.Jumping,
                "Jumping (Success/Click)",
                RowIndex: 4,
                FrameIndices: [0, 1, 2, 3, 4],
                FrameDurationsMs: [140, 140, 140, 140, 280],
                IsLooping: false
            ),
            [PetAnimationState.Failed] = new(
                PetAnimationState.Failed,
                "Failed (Error)",
                RowIndex: 5,
                FrameIndices: [0, 1, 2, 3, 4, 5, 6, 7],
                FrameDurationsMs: [140, 140, 140, 140, 140, 140, 140, 240],
                IsLooping: true
            ),
            [PetAnimationState.Waiting] = new(
                PetAnimationState.Waiting,
                "Waiting (Needs Action)",
                RowIndex: 6,
                FrameIndices: [0, 1, 2, 3, 4, 5],
                FrameDurationsMs: [150, 150, 150, 150, 150, 260],
                IsLooping: true
            ),
            [PetAnimationState.Running] = new(
                PetAnimationState.Running,
                "Running (PC Work)",
                RowIndex: 7,
                FrameIndices: [0, 1, 2, 3, 4, 5],
                FrameDurationsMs: [120, 120, 120, 120, 120, 220],
                IsLooping: true
            ),
            [PetAnimationState.Review] = new(
                PetAnimationState.Review,
                "Review (User Typing)",
                RowIndex: 8,
                FrameIndices: [0, 1, 2, 3, 4, 5],
                FrameDurationsMs: [150, 150, 150, 150, 150, 280],
                IsLooping: true
            )
        };

    public static (int row, int col) GetGazeCell(GazeDirection direction)
    {
        int index = (int)direction;
        if (index < 8)
        {
            // Row 9: 0° to 157.5°
            return (9, index);
        }
        else
        {
            // Row 10: 180° to 337.5°
            return (10, index - 8);
        }
    }

    public static string GetGazeDisplayName(GazeDirection direction)
    {
        return direction switch
        {
            GazeDirection.Deg0 => "0° (Up)",
            GazeDirection.Deg22_5 => "22.5°",
            GazeDirection.Deg45 => "45° (Up-Right)",
            GazeDirection.Deg67_5 => "67.5°",
            GazeDirection.Deg90 => "90° (Right)",
            GazeDirection.Deg112_5 => "112.5°",
            GazeDirection.Deg135 => "135° (Down-Right)",
            GazeDirection.Deg157_5 => "157.5°",
            GazeDirection.Deg180 => "180° (Down)",
            GazeDirection.Deg202_5 => "202.5°",
            GazeDirection.Deg225 => "225° (Down-Left)",
            GazeDirection.Deg247_5 => "247.5°",
            GazeDirection.Deg270 => "270° (Left)",
            GazeDirection.Deg292_5 => "292.5°",
            GazeDirection.Deg315 => "315° (Up-Left)",
            GazeDirection.Deg337_5 => "337.5°",
            _ => direction.ToString()
        };
    }
}
