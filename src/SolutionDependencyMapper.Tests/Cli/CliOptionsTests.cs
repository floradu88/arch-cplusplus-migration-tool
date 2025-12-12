using SolutionDependencyMapper.Cli;
using Xunit;

namespace SolutionDependencyMapper.Tests.Cli;

public class CliOptionsTests
{
    [Fact]
    public void TryParse_ParallelFlags_AreParsed()
    {
        var ok = CliOptions.TryParse(new[] { "My.sln", "--no-parallel", "--max-parallelism", "3" }, out var opts, out var err);
        Assert.True(ok);
        Assert.Null(err);
        Assert.False(opts.Parallel);
        Assert.Equal(3, opts.MaxParallelism);
        Assert.Equal("My.sln", opts.SolutionPath);
    }

    [Fact]
    public void TryParse_MaxParallelism_RequiresValue()
    {
        var ok = CliOptions.TryParse(new[] { "My.sln", "--max-parallelism" }, out _, out var err);
        Assert.False(ok);
        Assert.Contains("Missing value", err);
    }

    [Fact]
    public void TryParse_MaxParallelism_MustBePositiveInt()
    {
        var ok = CliOptions.TryParse(new[] { "My.sln", "--max-parallelism", "0" }, out _, out var err);
        Assert.False(ok);
        Assert.Contains("Invalid value", err);
    }

    [Fact]
    public void TryParse_ScanGacFlag_IsParsed()
    {
        var ok = CliOptions.TryParse(new[] { "My.sln", "--scan-gac" }, out var opts, out var err);
        Assert.True(ok);
        Assert.Null(err);
        Assert.True(opts.ScanGac);
    }

    [Fact]
    public void TryParse_GenerateLayeredSlnFlag_IsParsed()
    {
        var ok = CliOptions.TryParse(new[] { "My.sln", "--generate-layered-sln" }, out var opts, out var err);
        Assert.True(ok);
        Assert.Null(err);
        Assert.True(opts.GenerateLayeredSolution);
    }
}


