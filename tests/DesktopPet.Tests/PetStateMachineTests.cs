using System.Threading;
using DesktopPet.Models;
using DesktopPet.Services;
using Xunit;

namespace DesktopPet.Tests;

public class PetStateMachineTests
{
    private void RunInSta(Action action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                ex = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (ex != null) throw ex;
    }

    [Fact]
    public void DefaultStateIsIdle()
    {
        RunInSta(() =>
        {
            var manager = SpriteSheetManager.Instance;
            manager.Load();
            var player = new SpritePlayer(manager);
            var machine = new PetStateMachine(player);

            machine.EvaluateState();

            Assert.Equal(PetPriority.Idle, machine.CurrentPriority);
            Assert.Equal(PetAnimationState.Idle, machine.CurrentVisualState);
        });
    }

    [Fact]
    public void UserTypingTriggersReviewAnimation()
    {
        RunInSta(() =>
        {
            var manager = SpriteSheetManager.Instance;
            manager.Load();
            var player = new SpritePlayer(manager);
            var machine = new PetStateMachine(player);

            machine.SetUserTyping(true);

            Assert.Equal(PetPriority.UserTyping, machine.CurrentPriority);
            Assert.Equal(PetAnimationState.Review, machine.CurrentVisualState);

            machine.SetUserTyping(false);
            Assert.Equal(PetPriority.Idle, machine.CurrentPriority);
            Assert.Equal(PetAnimationState.Idle, machine.CurrentVisualState);
        });
    }

    [Fact]
    public void ComputerWorkPreemptsUserTyping()
    {
        RunInSta(() =>
        {
            var manager = SpriteSheetManager.Instance;
            manager.Load();
            var player = new SpritePlayer(manager);
            var machine = new PetStateMachine(player);

            machine.SetUserTyping(true);
            Assert.Equal(PetPriority.UserTyping, machine.CurrentPriority);

            // Computer work has higher priority than user typing
            machine.SetComputerWork(true);
            Assert.Equal(PetPriority.ComputerWork, machine.CurrentPriority);
            Assert.Equal(PetAnimationState.Running, machine.CurrentVisualState);

            // When computer work finishes, it drops back to active typing
            machine.SetComputerWork(false);
            Assert.Equal(PetPriority.UserTyping, machine.CurrentPriority);
            Assert.Equal(PetAnimationState.Review, machine.CurrentVisualState);
        });
    }

    [Fact]
    public void NotificationPreemptsWorkAndReturnsAfterCompletion()
    {
        RunInSta(() =>
        {
            var manager = SpriteSheetManager.Instance;
            manager.Load();
            var player = new SpritePlayer(manager);
            var machine = new PetStateMachine(player);

            machine.SetComputerWork(true);
            Assert.Equal(PetPriority.ComputerWork, machine.CurrentPriority);

            // Notification arrives (one-shot waving)
            machine.TriggerNotification();
            Assert.Equal(PetPriority.Notification, machine.CurrentPriority);
            Assert.Equal(PetAnimationState.Waving, machine.CurrentVisualState);

            // Simulate end of notification
            machine.EvaluateState(); // Still waving
            // When cleared, returns to ComputerWork
            machine.ClearAllSimulations();
            Assert.Equal(PetPriority.Idle, machine.CurrentPriority);
        });
    }

    [Fact]
    public void ErrorHasHigherPriorityThanNeedsActionAndNotification()
    {
        RunInSta(() =>
        {
            var manager = SpriteSheetManager.Instance;
            manager.Load();
            var player = new SpritePlayer(manager);
            var machine = new PetStateMachine(player);

            machine.SetNeedsAction(true);
            Assert.Equal(PetPriority.NeedsAction, machine.CurrentPriority);
            Assert.Equal(PetAnimationState.Waiting, machine.CurrentVisualState);

            // Error occurs -> preempts NeedsAction
            machine.SetError(true);
            Assert.Equal(PetPriority.Error, machine.CurrentPriority);
            Assert.Equal(PetAnimationState.Failed, machine.CurrentVisualState);

            // Notification arrives -> cannot preempt Error
            machine.TriggerNotification();
            Assert.Equal(PetPriority.Error, machine.CurrentPriority);
            Assert.Equal(PetAnimationState.Failed, machine.CurrentVisualState);

            // When Error resolved, drops back to NeedsAction
            machine.SetError(false);
            Assert.Equal(PetPriority.NeedsAction, machine.CurrentPriority);
            Assert.Equal(PetAnimationState.Waiting, machine.CurrentVisualState);
        });
    }

    [Fact]
    public void CpuReturningToNormal_DoesNotEndWorkFromOtherSources()
    {
        RunInSta(() =>
        {
            var manager = SpriteSheetManager.Instance;
            manager.Load();
            var machine = new PetStateMachine(new SpritePlayer(manager));

            machine.SetWorkSource(WorkSource.Process, true);
            machine.SetWorkSource(WorkSource.CpuLoad, true);
            Assert.Equal(PetPriority.ComputerWork, machine.CurrentPriority);

            // CPU load drops, but the watched build is still busy (AGENTS.md rule 5)
            machine.SetWorkSource(WorkSource.CpuLoad, false);
            Assert.Equal(PetPriority.ComputerWork, machine.CurrentPriority);
            Assert.Equal(WorkSource.Process, machine.ActiveWorkSources);

            machine.SetWorkSource(WorkSource.Process, false);
            Assert.False(machine.HasComputerWork);
            Assert.Equal(PetPriority.Idle, machine.CurrentPriority);
        });
    }

    [Fact]
    public void ClearAllSimulations_KeepsMeasuredWorkSources()
    {
        RunInSta(() =>
        {
            var manager = SpriteSheetManager.Instance;
            manager.Load();
            var machine = new PetStateMachine(new SpritePlayer(manager));

            machine.SetComputerWork(true);
            machine.SetWorkSource(WorkSource.Ipc, true);
            machine.SetWorkSource(WorkSource.Process, true);

            machine.ClearAllSimulations();

            Assert.Equal(WorkSource.Process, machine.ActiveWorkSources);
            Assert.Equal(PetPriority.ComputerWork, machine.CurrentPriority);
        });
    }

    [Fact]
    public void WorkPacingDirection_SelectsRunningRowsDuringComputerWork()
    {
        RunInSta(() =>
        {
            var manager = SpriteSheetManager.Instance;
            manager.Load();
            var machine = new PetStateMachine(new SpritePlayer(manager));

            machine.SetComputerWork(true);
            Assert.Equal(PetAnimationState.Running, machine.CurrentVisualState);

            machine.SetWorkPacingDirection(PetAnimationState.RunningLeft);
            Assert.Equal(PetPriority.ComputerWork, machine.CurrentPriority);
            Assert.Equal(PetAnimationState.RunningLeft, machine.CurrentVisualState);

            machine.SetWorkPacingDirection(PetAnimationState.RunningRight);
            Assert.Equal(PetAnimationState.RunningRight, machine.CurrentVisualState);

            // Higher priority still wins over pacing
            machine.SetError(true);
            Assert.Equal(PetAnimationState.Failed, machine.CurrentVisualState);
            machine.SetError(false);
            Assert.Equal(PetAnimationState.RunningRight, machine.CurrentVisualState);

            machine.SetWorkPacingDirection(null);
            Assert.Equal(PetAnimationState.Running, machine.CurrentVisualState);

            Assert.Throws<ArgumentOutOfRangeException>(() => machine.SetWorkPacingDirection(PetAnimationState.Idle));
        });
    }

    [Fact]
    public void DirectInteractionHasHighestPriority()
    {
        RunInSta(() =>
        {
            var manager = SpriteSheetManager.Instance;
            manager.Load();
            var player = new SpritePlayer(manager);
            var machine = new PetStateMachine(player);

            machine.SetError(true);
            Assert.Equal(PetPriority.Error, machine.CurrentPriority);

            // User drags pet
            machine.SetDirectInteraction(true);
            Assert.True(machine.HasDirectInteraction);
        });
    }
}
