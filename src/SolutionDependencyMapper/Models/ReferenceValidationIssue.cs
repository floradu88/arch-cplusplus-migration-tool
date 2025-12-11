namespace SolutionDependencyMapper.Models;

public sealed class ReferenceValidationIssue
{
    public string Category { get; set; } = string.Empty;   // e.g. ProjectReference, HintPath, IncludeDirectory, HeaderFile, LibraryDirectory
    public string Reference { get; set; } = string.Empty;  // raw reference value
    public string? ResolvedPath { get; set; }              // resolved absolute path (when applicable)
    public string? Details { get; set; }                   // extra context
}


