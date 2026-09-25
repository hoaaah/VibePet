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
