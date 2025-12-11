using SolutionDependencyMapper.Utils;
using Xunit;

namespace SolutionDependencyMapper.Tests.Utils;

public class PackageInstallerTests
{
    [Fact]
    public void GetMissingPackagesFromError_ReturnsEmpty_WhenErrorIsNotMsBuildLoadFailure()
    {
        var ex = new Exception("Could not find file 'foo.txt' in directory.");
        var packages = PackageInstaller.GetMissingPackagesFromError(ex);
        Assert.Empty(packages);
        Assert.False(PackageInstaller.IsMissingPackageError(ex));
    }

    [Fact]
    public void GetMissingPackagesFromError_ReturnsEmpty_WhenMicrosoftBuildIsMentionedButNotLoadFailure()
    {
        var ex = new Exception("Microsoft.Build is referenced but something else failed.");
        var packages = PackageInstaller.GetMissingPackagesFromError(ex);
        Assert.Empty(packages);
        Assert.False(PackageInstaller.IsMissingPackageError(ex));
    }

    [Fact]
    public void GetMissingPackagesFromError_ReturnsEmpty_WhenLoadFailureButNotMicrosoftBuild()
    {
        var ex = new Exception("Could not load file or assembly 'Some.Other.Assembly, Version=1.0.0.0'.");
        var packages = PackageInstaller.GetMissingPackagesFromError(ex);
        Assert.Empty(packages);
        Assert.False(PackageInstaller.IsMissingPackageError(ex));
    }

    [Fact]
    public void GetMissingPackagesFromError_ReturnsMicrosoftBuild_WhenMessageMentionsMicrosoftBuildAssemblyLoadFailure()
    {
        var ex = new Exception("Could not load file or assembly 'Microsoft.Build, Version=15.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'.");
        var packages = PackageInstaller.GetMissingPackagesFromError(ex);
        Assert.Contains("Microsoft.Build", packages);
        Assert.True(PackageInstaller.IsMissingPackageError(ex));
    }

    [Fact]
    public void GetMissingPackagesFromError_ReturnsSpecificPackages_WhenUtilitiesCoreIsMentioned()
    {
        var ex = new Exception("Could not load file or assembly 'Microsoft.Build.Utilities.Core, Version=15.1.0.0'.");
        var packages = PackageInstaller.GetMissingPackagesFromError(ex);
        Assert.Contains("Microsoft.Build", packages);
        Assert.Contains("Microsoft.Build.Utilities.Core", packages);
    }

    [Fact]
    public void GetMissingPackagesFromError_UsesInnerExceptionChain()
    {
        var ex = new Exception(
            "Top level parse failure",
            new FileNotFoundException("Could not load file or assembly 'Microsoft.Build.Framework, Version=15.1.0.0'.")
        );

        var packages = PackageInstaller.GetMissingPackagesFromError(ex);
        Assert.Contains("Microsoft.Build", packages);
        Assert.Contains("Microsoft.Build.Framework", packages);
        Assert.True(PackageInstaller.IsMissingPackageError(ex));
    }
}


