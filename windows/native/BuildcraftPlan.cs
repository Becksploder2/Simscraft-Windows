using System;
using System.Collections.Generic;
using System.Linq;

namespace Simcraft.Buildcraft;

/// <summary>
/// Safe, deterministic planning layer. This does NOT claim that Sims 4 executed commands.
/// Execution must be performed by the calibrated in-game controller, one verified step at a time.
/// </summary>
public enum BuildAction { Wall, Floor, Door, Window, Roof, Furniture }
public enum BuildStepStatus { Pending, AwaitingVerification, Verified, Failed, Skipped }
public record BuildCommand(BuildAction Action, int X1, int Y1, int X2, int Y2, string Asset = "", int Story = 1);
public sealed class BuildPlan {
    public List<BuildCommand> Commands { get; } = new();
    public IReadOnlyList<BuildCommand> ForStory(int story) =>
        Commands.Where(x => x.Story == story).ToList();
    public IReadOnlyList<BuildCommand> ForStage(BuildAction action) =>
        Commands.Where(x => x.Action == action).ToList();
    public static BuildPlan Rectangle(int width, int depth, int story = 1) {
        if (story < 1 || story > 4)
            throw new ArgumentOutOfRangeException(nameof(story), "Stories must be between 1 and 4.");
        if (width < 2 || depth < 2 || width > 100 || depth > 100)
            throw new ArgumentOutOfRangeException(nameof(width), "Room dimensions must be 2–100 tiles.");
        var plan = new BuildPlan();
        plan.Commands.AddRange(new[] {
            new BuildCommand(BuildAction.Wall, 0, 0, width, 0, Story: story),
            new BuildCommand(BuildAction.Wall, width, 0, width, depth, Story: story),
            new BuildCommand(BuildAction.Wall, width, depth, 0, depth, Story: story),
            new BuildCommand(BuildAction.Wall, 0, depth, 0, 0, Story: story),
            new BuildCommand(BuildAction.Floor, 0, 0, width, depth, Story: story)
        });
        return plan;
    }
}
public static class MultiStoryBuildPlans {
    // Planning only: changing Sims 4 floors, stairs, heights and foundations
    // requires a separate verified in-game controller.
    public static BuildPlan RectangularStories(int width, int depth, int stories) {
        if (stories < 1 || stories > 4)
            throw new ArgumentOutOfRangeException(nameof(stories), "Supported preview range: 1–4 stories.");
        var result = new BuildPlan();
        for (int story = 1; story <= stories; story++)
            result.Commands.AddRange(BuildPlan.Rectangle(width, depth, story).Commands);
        return result;
    }
}
public sealed class BuildExecutionQueue {
    private readonly IReadOnlyList<BuildCommand> commands;
    public int Index { get; private set; }
    public BuildStepStatus Status { get; private set; } = BuildStepStatus.Pending;
    public bool Canceled { get; private set; }
    public bool Finished => Index >= commands.Count;
    public BuildCommand? Current => Finished || Canceled ? null : commands[Index];
    public BuildExecutionQueue(BuildPlan plan) {
        commands = plan.Commands.ToList();
    }
    public BuildCommand? BeginNext() {
        if (Canceled || Finished || Status == BuildStepStatus.AwaitingVerification || Status == BuildStepStatus.Failed)
            return null;
        Status = BuildStepStatus.AwaitingVerification;
        return Current;
    }
    public void Verify(bool matched) {
        if (Status != BuildStepStatus.AwaitingVerification)
            throw new InvalidOperationException("No construction step is awaiting verification.");
        if (!matched) { Status = BuildStepStatus.Failed; return; }
        Status = BuildStepStatus.Verified;
        Index++;
        if (!Finished) Status = BuildStepStatus.Pending;
    }
    public void RetryFailed() {
        if (Status != BuildStepStatus.Failed)
            throw new InvalidOperationException("Only failed steps can be retried.");
        Status = BuildStepStatus.Pending;
    }
    public void Cancel() { Canceled = true; }
}
