using Microsoft.Build.Locator;
using SolutionDependencyMapper.Utils;

namespace SolutionDependencyMapper.Core;

public static class MsBuildBootstrapper
{
    public static bool EnsureRegistered(bool assumeVsEnv, ToolsContext? toolsContext)
    {
        if (assumeVsEnv)
        {
            Console.WriteLine("ℹ️  Skipping MSBuildLocator (--assume-vs-env flag is set)");
            Console.WriteLine("   Assuming VS Developer Command Prompt environment is configured.");
            Console.WriteLine();
            return true;
        }

        if (MSBuildLocator.IsRegistered)
        {
            Console.WriteLine("ℹ️  MSBuildLocator is already registered (likely by another component)");
            Console.WriteLine();
            return true;
        }

        Console.WriteLine("Locating MSBuild using MSBuildLocator...");

        var instances = MSBuildLocator.QueryVisualStudioInstances(VisualStudioInstanceQueryOptions.Default).ToList();
        if (instances.Count == 0)
        {
            // Sometimes the registry lookup needs a moment.
            Console.WriteLine("  No instances found with default query, retrying...");
            System.Threading.Thread.Sleep(100);
            instances = MSBuildLocator.QueryVisualStudioInstances(VisualStudioInstanceQueryOptions.Default).ToList();
        }

        if (instances.Count > 0)
        {
            var instance = instances.OrderByDescending(i => i.Version).First();
            MSBuildLocator.RegisterInstance(instance);

            Console.WriteLine("✓ MSBuildLocator found and registered MSBuild:");
            Console.WriteLine($"    Path: {instance.MSBuildPath}");
            Console.WriteLine($"    Version: {instance.Version}");
            Console.WriteLine($"    Visual Studio: {instance.VisualStudioRootPath ?? "N/A"}");
            Console.WriteLine();
            return true;
        }

        // Fallback: ToolFinder may still have found msbuild.exe (useful for script generation)
        if (toolsContext != null && toolsContext.HasTool("msbuild.exe"))
        {
            var msbuildPath = toolsContext.GetMSBuildPath();
            if (msbuildPath != null && File.Exists(msbuildPath))
            {
                Console.WriteLine("⚠️  Warning: MSBuildLocator could not find Visual Studio instances.");
                Console.WriteLine($"   However, ToolFinder found MSBuild at: {msbuildPath}");
                Console.WriteLine();
                Console.WriteLine("   MSBuildLocator requires Visual Studio to be properly registered in the system.");
                Console.WriteLine("   The tool can still generate build scripts, but project parsing may fail.");
                Console.WriteLine();
                Console.WriteLine("   Continuing anyway (build scripts will use discovered MSBuild path)...");
                Console.WriteLine();
                return true;
            }
        }

        Console.WriteLine("❌ Error: No MSBuild instances found.");
        Console.WriteLine();
        Console.WriteLine("MSBuildLocator searched for Visual Studio instances but found none.");
        Console.WriteLine("ToolFinder also could not locate MSBuild in PATH or common locations.");
        Console.WriteLine();
        Console.WriteLine("Possible solutions:");
        Console.WriteLine("1. Install Visual Studio Build Tools or Visual Studio");
        Console.WriteLine("   Download: https://visualstudio.microsoft.com/downloads/");
        Console.WriteLine();
        Console.WriteLine("2. For Build Tools, ensure 'MSBuild' workload is installed");
        Console.WriteLine();
        Console.WriteLine("3. Try running 'Developer Command Prompt for VS' or 'Developer PowerShell for VS'");
        Console.WriteLine("   These set up the environment correctly for MSBuild");
        Console.WriteLine();
        Console.WriteLine("4. If Visual Studio is installed, try repairing the installation");
        Console.WriteLine("   (Visual Studio Installer > Modify > Repair)");
        Console.WriteLine();
        Console.WriteLine("5. Use --assume-vs-env flag if running from VS Developer Command Prompt");
        return false;
    }
}


