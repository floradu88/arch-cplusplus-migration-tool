using SolutionDependencyMapper.Utils;
using Xunit;

namespace SolutionDependencyMapper.Tests.Utils;

public class GacScannerTests
{
    [Fact]
    public void ExtractMicrosoftBuildLines_FiltersAndSorts()
    {
        var output = @"
Microsoft (R) .NET Global Assembly Cache Utility.  Version 4.0.30319.0
Microsoft.Build, Version=15.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
Microsoft.Build.Framework, Version=15.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
Some.Other.Assembly, Version=1.0.0.0
MICROSOFT.BUILD.Utilities.Core, Version=15.1.0.0
";

        var lines = GacScanner.ExtractMicrosoftBuildLines(output);

        Assert.DoesNotContain(lines, l => l.Contains("Some.Other.Assembly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(lines, l => l.Contains("Microsoft.Build,", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(lines, l => l.Contains("Microsoft.Build.Framework", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(lines, l => l.Contains("MICROSOFT.BUILD.Utilities.Core", StringComparison.OrdinalIgnoreCase));
    }
}


