namespace SolutionDependencyMapper.Models;

public sealed class OutputArtifactStatus
{
    // Optional context
    public string? Configuration { get; set; }
    public string? Platform { get; set; }

    public string? ExpectedPath { get; set; }    // absolute when possible
    public bool Exists { get; set; }
    public string? Details { get; set; }
}


