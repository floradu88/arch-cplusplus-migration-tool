using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SolutionDependencyMapper.Utils;

/// <summary>
/// Utility class for detecting and installing missing Microsoft.Build packages in project files.
/// Handles automatic package installation and NuGet restore for projects that fail due to missing dependencies.
/// </summary>
public static class PackageInstaller
{
    // Common Microsoft.Build packages that projects might need
    private static readonly Dictionary<string, string> CommonMsBuildPackages = new()
    {
        { "Microsoft.Build", "15.1.548" },
        { "Microsoft.Build.Framework", "15.1.548" },
        { "Microsoft.Build.Utilities.Core", "15.1.548" },
        { "Microsoft.Build.Tasks.Core", "15.1.548" },
        { "Microsoft.Build.Engine", "15.1.548" }
    };

    /// <summary>
    /// Detects if an exception is related to missing Microsoft.Build packages.
    /// </summary>
    public static bool IsMissingPackageError(Exception ex)
    {
        return GetMissingPackagesFromError(ex).Count > 0;
    }

    /// <summary>
    /// Returns the list of Microsoft.Build-related NuGet packages to install based on the error contents.
    /// This is intentionally conservative: it only triggers on clear Microsoft.Build* assembly/type load failures.
    /// </summary>
    public static IReadOnlyCollection<string> GetMissingPackagesFromError(Exception ex)
    {
        if (ex == null) return Array.Empty<string>();

        var packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in EnumerateExceptionChain(ex))
        {
            var msg = (e.Message ?? string.Empty);
            var lower = msg.ToLowerInvariant();

            // Only trigger on typical runtime load failures (not generic "could not find" noise)
            var looksLikeLoadFailure =
                lower.Contains("could not load file or assembly") ||
                lower.Contains("fileloadexception") ||
                lower.Contains("filenotfoundexception") ||
                lower.Contains("could not load type") ||
                lower.Contains("typeinitializationexception") ||
                lower.Contains("the type or namespace name") ||
                lower.Contains("could not resolve type") ||
                lower.Contains("could not resolve assembly") ||
                lower.Contains("could not load assembly");

            if (!looksLikeLoadFailure)
                continue;

            // Only consider errors that mention Microsoft.Build
            if (!lower.Contains("microsoft.build"))
                continue;

            // Always include base Microsoft.Build when we detect a Microsoft.Build* load failure.
            packages.Add("Microsoft.Build");

            // Heuristic: detect which specific assembly is missing and install the matching package(s)
            // (Most of these map 1:1 to NuGet packages).
            AddIfMentioned(packages, lower, "microsoft.build.framework", "Microsoft.Build.Framework");
            AddIfMentioned(packages, lower, "microsoft.build.utilities.core", "Microsoft.Build.Utilities.Core");
            AddIfMentioned(packages, lower, "microsoft.build.tasks.core", "Microsoft.Build.Tasks.Core");
            AddIfMentioned(packages, lower, "microsoft.build.engine", "Microsoft.Build.Engine");

            // Sometimes message contains short names or types that imply dependencies
            if (lower.Contains("microsoft.build.evaluation") || lower.Contains("microsoft.build.execution"))
            {
                packages.Add("Microsoft.Build"); // already
                packages.Add("Microsoft.Build.Framework");
            }
        }

        return packages.Count == 0 ? Array.Empty<string>() : packages.ToList();
    }

    /// <summary>
    /// Attempts to fix a project by installing missing packages inferred from the exception and restoring.
    /// </summary>
    public static bool FixProjectPackagesForError(string projectPath, Exception ex)
    {
        if (!File.Exists(projectPath))
            return false;

        var packagesToInstall = GetMissingPackagesFromError(ex);
        if (packagesToInstall.Count == 0)
            return false;

        bool packagesFixed = false;

        if (InstallMsBuildPackages(projectPath, packagesToInstall))
        {
            packagesFixed = true;
        }

        // Always try to restore packages after edits
        if (RestorePackages(projectPath))
        {
            packagesFixed = true;
        }

        return packagesFixed;
    }

    private static IEnumerable<Exception> EnumerateExceptionChain(Exception ex)
    {
        for (var cur = ex; cur != null; cur = cur.InnerException!)
        {
            yield return cur;
            if (cur.InnerException == null) break;
        }
    }

    private static void AddIfMentioned(HashSet<string> packages, string lowerMessage, string needle, string packageName)
    {
        if (lowerMessage.Contains(needle))
        {
            packages.Add(packageName);
        }
    }

    /// <summary>
    /// Installs missing Microsoft.Build packages to a .csproj file.
    /// </summary>
    /// <param name="projectPath">Path to the .csproj file</param>
    /// <param name="packagesToInstall">Optional list of specific packages to install. If null, installs common packages.</param>
    /// <returns>True if packages were installed, false if the file couldn't be modified or isn't a .csproj</returns>
    public static bool InstallMsBuildPackages(string projectPath, IEnumerable<string>? packagesToInstall = null)
    {
        if (!File.Exists(projectPath))
            return false;

        // Only process .csproj files
        if (!projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var packages = packagesToInstall?.ToList() ?? CommonMsBuildPackages.Keys.ToList();
            var doc = XDocument.Load(projectPath);
            
            // Check if this is an SDK-style project (no namespace)
            var project = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Project");
            if (project == null)
            {
                return false;
            }

            // Determine if SDK-style or traditional
            // SDK-style projects have no namespace or have Sdk attribute
            bool isSdkStyle = project.Attribute("Sdk") != null || 
                             project.Name.Namespace == XNamespace.None ||
                             project.Elements().Any(e => e.Name.LocalName == "PropertyGroup" && e.Name.Namespace == XNamespace.None);

            XElement? itemGroup;
            
            if (isSdkStyle)
            {
                // SDK-style project - no namespace
                itemGroup = project.Elements()
                    .FirstOrDefault(e => e.Name.LocalName == "ItemGroup" && 
                                        e.Elements().Any(el => el.Name.LocalName == "PackageReference"));
                
                if (itemGroup == null)
                {
                    itemGroup = new XElement("ItemGroup");
                    project.Add(itemGroup);
                }
            }
            else
            {
                // Traditional project - with namespace
                var ns = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");
                itemGroup = doc.Descendants(ns + "ItemGroup")
                    .FirstOrDefault(g => g.Elements(ns + "PackageReference").Any());
                
                if (itemGroup == null)
                {
                    itemGroup = new XElement(ns + "ItemGroup");
                    var nsProject = doc.Descendants(ns + "Project").FirstOrDefault();
                    if (nsProject != null)
                    {
                        nsProject.Add(itemGroup);
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            bool packagesAdded = false;

            foreach (var packageName in packages)
            {
                // Check if package already exists
                XElement? existingPackage;
                
                if (isSdkStyle)
                {
                    existingPackage = itemGroup.Elements()
                        .FirstOrDefault(e => e.Name.LocalName == "PackageReference" &&
                            (e.Attribute("Include")?.Value.Equals(packageName, StringComparison.OrdinalIgnoreCase) == true ||
                             e.Attribute("Update")?.Value.Equals(packageName, StringComparison.OrdinalIgnoreCase) == true));
                }
                else
                {
                    var ns = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");
                    existingPackage = itemGroup.Elements(ns + "PackageReference")
                        .FirstOrDefault(p => 
                            p.Attribute("Include")?.Value.Equals(packageName, StringComparison.OrdinalIgnoreCase) == true ||
                            p.Attribute("Update")?.Value.Equals(packageName, StringComparison.OrdinalIgnoreCase) == true);
                }

                if (existingPackage == null)
                {
                    // Add new package reference
                    var version = CommonMsBuildPackages.ContainsKey(packageName) 
                        ? CommonMsBuildPackages[packageName] 
                        : "15.1.548";

                    XElement packageRef;
                    if (isSdkStyle)
                    {
                        packageRef = new XElement("PackageReference",
                            new XAttribute("Include", packageName),
                            new XElement("Version", version));
                    }
                    else
                    {
                        var ns = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");
                        packageRef = new XElement(ns + "PackageReference",
                            new XAttribute("Include", packageName),
                            new XElement(ns + "Version", version));
                    }

                    itemGroup.Add(packageRef);
                    packagesAdded = true;
                }
            }

            if (packagesAdded)
            {
                // Save the modified project file
                doc.Save(projectPath);
                Console.WriteLine($"  ✓ Installed Microsoft.Build packages to: {Path.GetFileName(projectPath)}");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️  Warning: Could not install packages to {Path.GetFileName(projectPath)}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Restores NuGet packages for a project using dotnet restore.
    /// </summary>
    /// <param name="projectPath">Path to the project file</param>
    /// <returns>True if restore succeeded, false otherwise</returns>
    public static bool RestorePackages(string projectPath)
    {
        if (!File.Exists(projectPath))
            return false;

        try
        {
            var projectDir = Path.GetDirectoryName(projectPath) ?? ".";
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"restore \"{projectPath}\"",
                    WorkingDirectory = projectDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                Console.WriteLine($"  ✓ Restored packages for: {Path.GetFileName(projectPath)}");
                return true;
            }
            else
            {
                Console.WriteLine($"  ⚠️  Warning: Package restore failed for {Path.GetFileName(projectPath)}");
                if (!string.IsNullOrWhiteSpace(error))
                {
                    Console.WriteLine($"     Error: {error.Trim()}");
                }
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️  Warning: Could not restore packages for {Path.GetFileName(projectPath)}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Attempts to fix a project by installing missing packages and restoring.
    /// </summary>
    /// <param name="projectPath">Path to the project file</param>
    /// <returns>True if fixes were applied, false otherwise</returns>
    public static bool FixProjectPackages(string projectPath)
    {
        if (!File.Exists(projectPath))
            return false;

        bool packagesFixed = false;

        // Try installing packages first
        if (InstallMsBuildPackages(projectPath))
        {
            packagesFixed = true;
        }

        // Always try to restore packages
        if (RestorePackages(projectPath))
        {
            packagesFixed = true;
        }

        return packagesFixed;
    }
}

