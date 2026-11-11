using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Simcraft.Buildcraft;

/// <summary>
/// Conservative prompt-to-plan preview. Reference images are preserved as inputs but
/// are NOT interpreted as geometry. No Sims 4 mouse, keyboard, or camera input occurs.
/// </summary>
public sealed record BuildPlanDraft(
    BuildPlan Plan, int Width, int Depth, IReadOnlyList<string> ReferenceIds,
    IReadOnlyList<string> Warnings);

public static class BuildPromptPlanner {
    private static readonly Regex Dimensions = new(
        @"\b(?<w>\d{1,3})\s*(?:x|×|by)\s*(?<d>\d{1,3})\s*(?:tiles?|squares?)?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Generate a bounded, rectangular footprint preview. Unrecognized descriptions
    /// are NOT silently converted into inferred rooms, doors, or furnishings.
    /// </summary>
    public static BuildPlanDraft Preview(string? prompt, int lotWidth, int lotDepth,
        IReadOnlyList<BuildReference>? references = null) {
        if (lotWidth < 2 || lotDepth < 2 || lotWidth > 100 || lotDepth > 100)
            throw new ArgumentOutOfRangeException(nameof(lotWidth), "Lot must be between 2 and 100 tiles per axis.");
        prompt ??= "";
        if (prompt.Length > 10000)
            throw new ArgumentException("Prompt must be at most 10,000 characters.", nameof(prompt));
        var warnings = new List<string>();
        int width = Math.Min(lotWidth, 12);
        int depth = Math.Min(lotDepth, 10);
        var match = Dimensions.Match(prompt);
        if (match.Success) {
            width = int.Parse(match.Groups["w"].Value);
            depth = int.Parse(match.Groups["d"].Value);
            if (width < 2 || depth < 2 || width > lotWidth || depth > lotDepth)
                throw new ArgumentOutOfRangeException(nameof(prompt),
                    "Requested footprint must fit within the selected lot and be at least 2×2 tiles.");
        } else {
            warnings.Add("No explicit tile dimensions found; using a conservative starter footprint.");
        }
        if (references is { Count: > 0 })
            warnings.Add("Reference images are attached for future interpretation; their contents are not analyzed.");
        warnings.Add("Preview only: rooms, doors, windows, roofs, furniture and actual lot boundaries are not inferred.");
        warnings.Add("No game input or camera movement is authorized by this plan.");
        var ids = new List<string>();
        if (references != null) foreach (var item in references)
            if (!string.IsNullOrWhiteSpace(item.Id) && !ids.Contains(item.Id))
                ids.Add(item.Id);
        return new BuildPlanDraft(BuildPlan.Rectangle(width, depth), width, depth, ids, warnings);
    }
}
