using DesktopPet.Models;
using DesktopPet.Services;
using Xunit;

namespace DesktopPet.Tests;

public class AnimationCatalogTests
{
    [Fact]
    public void AllNineMainAnimationsAreDefined()
    {
        Assert.Equal(9, AnimationCatalog.Animations.Count);

        Assert.True(AnimationCatalog.Animations.ContainsKey(PetAnimationState.Idle));
        Assert.True(AnimationCatalog.Animations.ContainsKey(PetAnimationState.RunningRight));
        Assert.True(AnimationCatalog.Animations.ContainsKey(PetAnimationState.RunningLeft));
        Assert.True(AnimationCatalog.Animations.ContainsKey(PetAnimationState.Waving));
        Assert.True(AnimationCatalog.Animations.ContainsKey(PetAnimationState.Jumping));
        Assert.True(AnimationCatalog.Animations.ContainsKey(PetAnimationState.Failed));
        Assert.True(AnimationCatalog.Animations.ContainsKey(PetAnimationState.Waiting));
        Assert.True(AnimationCatalog.Animations.ContainsKey(PetAnimationState.Running));
        Assert.True(AnimationCatalog.Animations.ContainsKey(PetAnimationState.Review));
    }

    [Theory]
    [InlineData(PetAnimationState.Idle, 0, 6, new[] { 280, 110, 110, 140, 140, 320 })]
    [InlineData(PetAnimationState.RunningRight, 1, 8, new[] { 120, 120, 120, 120, 120, 120, 120, 220 })]
    [InlineData(PetAnimationState.RunningLeft, 2, 8, new[] { 120, 120, 120, 120, 120, 120, 120, 220 })]
    [InlineData(PetAnimationState.Waving, 3, 4, new[] { 140, 140, 140, 280 })]
    [InlineData(PetAnimationState.Jumping, 4, 5, new[] { 140, 140, 140, 140, 280 })]
    [InlineData(PetAnimationState.Failed, 5, 8, new[] { 140, 140, 140, 140, 140, 140, 140, 240 })]
    [InlineData(PetAnimationState.Waiting, 6, 6, new[] { 150, 150, 150, 150, 150, 260 })]
    [InlineData(PetAnimationState.Running, 7, 6, new[] { 120, 120, 120, 120, 120, 220 })]
    [InlineData(PetAnimationState.Review, 8, 6, new[] { 150, 150, 150, 150, 150, 280 })]
    public void AnimationMetadataMatchesAgentsMdSpec(
        PetAnimationState state,
        int expectedRow,
        int expectedFrameCount,
        int[] expectedDurations)
    {
        var def = AnimationCatalog.Animations[state];

        Assert.Equal(expectedRow, def.RowIndex);
        Assert.Equal(expectedFrameCount, def.FrameCount);
        Assert.Equal(expectedDurations, def.FrameDurationsMs);

        for (int i = 0; i < expectedFrameCount; i++)
        {
            Assert.Equal(expectedDurations[i], def.GetDuration(i));
        }
    }

    [Fact]
    public void SixteenGazePosesMatchRowNineAndTen()
    {
        var gazes = Enum.GetValues<GazeDirection>();
        Assert.Equal(16, gazes.Length);

        // First 8 poses (0° to 157.5°) are row 9, cols 0-7
        for (int i = 0; i < 8; i++)
        {
            var (row, col) = AnimationCatalog.GetGazeCell(gazes[i]);
            Assert.Equal(9, row);
            Assert.Equal(i, col);
        }

        // Next 8 poses (180° to 337.5°) are row 10, cols 0-7
        for (int i = 8; i < 16; i++)
        {
            var (row, col) = AnimationCatalog.GetGazeCell(gazes[i]);
            Assert.Equal(10, row);
            Assert.Equal(i - 8, col);
        }
    }

    [Fact]
    public void SpriteDimensionsAreCorrect()
    {
        Assert.Equal(192, AnimationCatalog.CellWidth);
        Assert.Equal(208, AnimationCatalog.CellHeight);
        Assert.Equal(8, AnimationCatalog.SheetColumns);
        Assert.Equal(11, AnimationCatalog.SheetRows);

        // Total sheet size: 8 * 192 = 1536, 11 * 208 = 2288
        Assert.Equal(1536, AnimationCatalog.CellWidth * AnimationCatalog.SheetColumns);
        Assert.Equal(2288, AnimationCatalog.CellHeight * AnimationCatalog.SheetRows);
    }
}
