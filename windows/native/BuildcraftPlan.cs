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
public record BuildCommand(BuildAction Action, int X1, int Y1, int X2, int Y2, string Asset = "");
public sealed class BuildPlan {
    public List<BuildCommand> Commands { get; } = new();
    public IReadOnlyList<BuildCommand> ForStage(BuildAction action) =>
        Commands.Where(x => x.Action == action).ToList();
    public static BuildPlan Rectangle(int width, int depth) {
        if (width < 2 || depth < 2 || width > 100 || depth > 100)
            throw new ArgumentOutOfRangeException(nameof(width), "Room dimensions must be 2–100 tiles.");
        var plan = new BuildPlan();
        plan.Commands.AddRange(new[] {
            new BuildCommand(BuildAction.Wall, 0, 0, width, 0),
            new BuildCommand(BuildAction.Wall, width, 0, width, depth),
            new BuildCommand(BuildAction.Wall, width, depth, 0, depth),
            new BuildCommand(BuildAction.Wall, 0, depth, 0, 0),
            new BuildCommand(BuildAction.Floor, 0, 0, width, depth)
        });
        return plan;
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
