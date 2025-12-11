using System.Diagnostics;

namespace SolutionDependencyMapper.Utils;

/// <summary>
/// Utility class to find Visual Studio tools, CMake, and other C++ build tools
/// in various locations: project root, environment PATH, and common Windows installation paths.
/// </summary>
public static class ToolFinder
{
    /// <summary>
    /// Represents a found tool with its path and source location.
    /// </summary>
    public class FoundTool
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public ToolSource Source { get; set; }
        public string? Version { get; set; }
    }

    /// <summary>
    /// Indicates where the tool was found.
    /// </summary>
    public enum ToolSource
    {
        ProjectRoot,      // Found in project root directory
        EnvironmentPath,  // Found in PATH environment variable
        CommonLocation,   // Found in common installation location
        Vswhere           // Found using vswhere.exe
    }

    /// <summary>
    /// Finds all Visual Studio tools, CMake, and other C++ tools.
    /// </summary>
    /// <param name="projectRoot">Optional project root directory to search in</param>
    /// <returns>Dictionary of tool names to lists of found instances</returns>
    public static Dictionary<string, List<FoundTool>> FindAllTools(string? projectRoot = null)
    {
        return FindAllTools(projectRoot, parallel: false, maxParallelism: null);
    }

    /// <summary>
    /// Finds all Visual Studio tools, CMake, and other C++ tools, optionally in parallel.
    /// </summary>
    public static Dictionary<string, List<FoundTool>> FindAllTools(string? projectRoot, bool parallel, int? maxParallelism)
    {
        // Define tools to search for
        var toolsToFind = new[]
        {
            "msbuild.exe",
            "cmake.exe",
            "cl.exe",           // MSVC compiler
            "link.exe",         // MSVC linker
            "clang.exe",        // Clang compiler
            "clang++.exe",      // Clang++ compiler
            "gcc.exe",          // GCC compiler (MinGW)
            "g++.exe",          // G++ compiler (MinGW)
            "ninja.exe",        // Ninja build system
            "vswhere.exe",      // Visual Studio installer tool
            "devenv.exe",       // Visual Studio IDE
            "vcvarsall.bat",    // Visual Studio environment setup
            "dumpbin.exe",      // Binary dump utility
            "lib.exe",          // Library manager
            "nmake.exe"         // NMAKE build tool
        };

        if (!parallel)
        {
            var results = new Dictionary<string, List<FoundTool>>();
            foreach (var toolName in toolsToFind)
            {
                var found = FindTool(toolName, projectRoot);
                if (found.Count > 0)
                {
                    results[toolName] = found;
                }
            }
            return results;
        }

        var concurrent = new System.Collections.Concurrent.ConcurrentDictionary<string, List<FoundTool>>(StringComparer.OrdinalIgnoreCase);
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxParallelism.HasValue ? Math.Max(1, maxParallelism.Value) : Environment.ProcessorCount
        };

        Parallel.ForEach(toolsToFind, options, toolName =>
        {
            var found = FindTool(toolName, projectRoot);
            if (found.Count > 0)
            {
                concurrent[toolName] = found;
            }
        });

        return concurrent.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Finds a specific tool by name.
    /// </summary>
    /// <param name="toolName">Name of the tool to find (e.g., "msbuild.exe", "cmake.exe")</param>
    /// <param name="projectRoot">Optional project root directory to search in</param>
    /// <returns>List of found tool instances</returns>
    public static List<FoundTool> FindTool(string toolName, string? projectRoot = null)
    {
        var results = new List<FoundTool>();

        // 1. Search in project root directory
        if (!string.IsNullOrEmpty(projectRoot) && Directory.Exists(projectRoot))
        {
            var projectRootResults = SearchInDirectory(projectRoot, toolName, ToolSource.ProjectRoot, recursive: true);
            results.AddRange(projectRootResults);
        }

        // 2. Search in PATH environment variable
        var pathResults = SearchInPath(toolName);
        results.AddRange(pathResults);

        // 3. Search in common Visual Studio locations
        if (toolName.Contains("msbuild", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("cl.exe", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("link.exe", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("devenv.exe", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("vcvarsall.bat", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("dumpbin.exe", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("lib.exe", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("nmake.exe", StringComparison.OrdinalIgnoreCase))
        {
            var vsResults = SearchVisualStudioLocations(toolName);
            results.AddRange(vsResults);
        }

        // 4. Search in common CMake locations
        if (toolName.Contains("cmake", StringComparison.OrdinalIgnoreCase))
        {
            var cmakeResults = SearchCmakeLocations(toolName);
            results.AddRange(cmakeResults);
        }

        // 5. Search in common MinGW/GCC locations
        if (toolName.Contains("gcc", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("g++", StringComparison.OrdinalIgnoreCase))
        {
            var mingwResults = SearchMinGwLocations(toolName);
            results.AddRange(mingwResults);
        }

        // 6. Search in common Clang locations
        if (toolName.Contains("clang", StringComparison.OrdinalIgnoreCase))
        {
            var clangResults = SearchClangLocations(toolName);
            results.AddRange(clangResults);
        }

        // Remove duplicates (same path)
        return results
            .GroupBy(t => t.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>
    /// Searches for a tool in a specific directory.
    /// </summary>
    private static List<FoundTool> SearchInDirectory(
        string directory,
        string toolName,
        ToolSource source,
        bool recursive = false)
    {
        var results = new List<FoundTool>();

        try
        {
            if (recursive)
            {
                var files = Directory.GetFiles(directory, toolName, SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    results.Add(new FoundTool
                    {
                        Name = toolName,
                        Path = file,
                        Source = source
                    });
                }
            }
            else
            {
                var file = Path.Combine(directory, toolName);
                if (File.Exists(file))
                {
                    results.Add(new FoundTool
                    {
                        Name = toolName,
                        Path = file,
                        Source = source
                    });
                }
            }
        }
        catch
        {
            // Ignore errors (permissions, etc.)
        }

        return results;
    }

    /// <summary>
    /// Searches for a tool in the PATH environment variable.
    /// </summary>
    private static List<FoundTool> SearchInPath(string toolName)
    {
        var results = new List<FoundTool>();
        var pathEnv = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrEmpty(pathEnv))
            return results;

        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var path in paths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    var fullPath = Path.Combine(path, toolName);
                    if (File.Exists(fullPath))
                    {
                        results.Add(new FoundTool
                        {
                            Name = toolName,
                            Path = fullPath,
                            Source = ToolSource.EnvironmentPath
                        });
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
        }

        return results;
    }

    /// <summary>
    /// Searches for Visual Studio tools in common installation locations.
    /// </summary>
    private static List<FoundTool> SearchVisualStudioLocations(string toolName)
    {
        var results = new List<FoundTool>();

        // Try using vswhere.exe first (most reliable)
        var vswhereResults = SearchUsingVswhere(toolName);
        results.AddRange(vswhereResults);

        // Common Visual Studio installation paths
        var programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
        var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";

        var vsPaths = new[]
        {
            // VS 2026 (64-bit) - Future version
            Path.Combine(programFiles, "Microsoft Visual Studio", "2026", "Enterprise"),
            Path.Combine(programFiles, "Microsoft Visual Studio", "2026", "Professional"),
            Path.Combine(programFiles, "Microsoft Visual Studio", "2026", "Community"),
            Path.Combine(programFiles, "Microsoft Visual Studio", "2026", "BuildTools"),

            // VS 2025 (64-bit)
            Path.Combine(programFiles, "Microsoft Visual Studio", "2025", "Enterprise"),
            Path.Combine(programFiles, "Microsoft Visual Studio", "2025", "Professional"),
            Path.Combine(programFiles, "Microsoft Visual Studio", "2025", "Community"),
            Path.Combine(programFiles, "Microsoft Visual Studio", "2025", "BuildTools"),

            // VS 2022 (64-bit)
            Path.Combine(programFiles, "Microsoft Visual Studio", "2022", "Enterprise"),
            Path.Combine(programFiles, "Microsoft Visual Studio", "2022", "Professional"),
            Path.Combine(programFiles, "Microsoft Visual Studio", "2022", "Community"),
            Path.Combine(programFiles, "Microsoft Visual Studio", "2022", "BuildTools"),

            // VS 2019 (32-bit)
            Path.Combine(programFilesX86, "Microsoft Visual Studio", "2019", "Enterprise"),
            Path.Combine(programFilesX86, "Microsoft Visual Studio", "2019", "Professional"),
            Path.Combine(programFilesX86, "Microsoft Visual Studio", "2019", "Community"),
            Path.Combine(programFilesX86, "Microsoft Visual Studio", "2019", "BuildTools"),

            // VS 2017 (32-bit)
            Path.Combine(programFilesX86, "Microsoft Visual Studio", "2017", "Enterprise"),
            Path.Combine(programFilesX86, "Microsoft Visual Studio", "2017", "Professional"),
            Path.Combine(programFilesX86, "Microsoft Visual Studio", "2017", "Community"),
            Path.Combine(programFilesX86, "Microsoft Visual Studio", "2017", "BuildTools"),

            // VS 2015 (32-bit)
            Path.Combine(programFilesX86, "Microsoft Visual Studio", "14.0"),
        };

        foreach (var vsPath in vsPaths)
        {
            if (!Directory.Exists(vsPath))
                continue;

            // MSBuild locations
            if (toolName.Equals("msbuild.exe", StringComparison.OrdinalIgnoreCase))
            {
                var msbuildPaths = new[]
                {
                    Path.Combine(vsPath, "MSBuild", "Current", "Bin", "MSBuild.exe"),
                    Path.Combine(vsPath, "MSBuild", "15.0", "Bin", "MSBuild.exe"),
                    Path.Combine(vsPath, "MSBuild", "Bin", "MSBuild.exe"),
                };

                foreach (var msbuildPath in msbuildPaths)
                {
                    if (File.Exists(msbuildPath))
                    {
                        results.Add(new FoundTool
                        {
                            Name = toolName,
                            Path = msbuildPath,
                            Source = ToolSource.CommonLocation
                        });
                    }
                }
            }

            // VC++ Tools locations (cl.exe, link.exe, etc.)
            if (toolName.Equals("cl.exe", StringComparison.OrdinalIgnoreCase) ||
                toolName.Equals("link.exe", StringComparison.OrdinalIgnoreCase) ||
                toolName.Equals("dumpbin.exe", StringComparison.OrdinalIgnoreCase) ||
                toolName.Equals("lib.exe", StringComparison.OrdinalIgnoreCase) ||
                toolName.Equals("nmake.exe", StringComparison.OrdinalIgnoreCase))
            {
                var vcToolsPaths = new[]
                {
                    Path.Combine(vsPath, "VC", "Tools", "MSVC"),
                    Path.Combine(vsPath, "VC", "bin"),
                };

                foreach (var vcToolsPath in vcToolsPaths)
                {
                    if (Directory.Exists(vcToolsPath))
                    {
                        var found = SearchInDirectory(vcToolsPath, toolName, ToolSource.CommonLocation, recursive: true);
                        results.AddRange(found);
                    }
                }
            }

            // devenv.exe location
            if (toolName.Equals("devenv.exe", StringComparison.OrdinalIgnoreCase))
            {
                var devenvPath = Path.Combine(vsPath, "Common7", "IDE", "devenv.exe");
                if (File.Exists(devenvPath))
                {
                    results.Add(new FoundTool
                    {
                        Name = toolName,
                        Path = devenvPath,
                        Source = ToolSource.CommonLocation
                    });
                }
            }

            // vcvarsall.bat location
            if (toolName.Equals("vcvarsall.bat", StringComparison.OrdinalIgnoreCase))
            {
                var vcvarsallPath = Path.Combine(vsPath, "VC", "Auxiliary", "Build", "vcvarsall.bat");
                if (File.Exists(vcvarsallPath))
                {
                    results.Add(new FoundTool
                    {
                        Name = toolName,
                        Path = vcvarsallPath,
                        Source = ToolSource.CommonLocation
                    });
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Uses vswhere.exe to find Visual Studio tools.
    /// </summary>
    private static List<FoundTool> SearchUsingVswhere(string toolName)
    {
        var results = new List<FoundTool>();

        var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";
        var vswherePath = Path.Combine(programFilesX86, "Microsoft Visual Studio", "Installer", "vswhere.exe");

        if (!File.Exists(vswherePath))
            return results;

        try
        {
            // Find MSBuild using vswhere
            if (toolName.Equals("msbuild.exe", StringComparison.OrdinalIgnoreCase))
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = vswherePath,
                        Arguments = "-latest -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    var paths = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var path in paths)
                    {
                        var trimmedPath = path.Trim();
                        if (File.Exists(trimmedPath))
                        {
                            results.Add(new FoundTool
                            {
                                Name = toolName,
                                Path = trimmedPath,
                                Source = ToolSource.Vswhere
                            });
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return results;
    }

    /// <summary>
    /// Searches for CMake in common installation locations.
    /// </summary>
    private static List<FoundTool> SearchCmakeLocations(string toolName)
    {
        var results = new List<FoundTool>();

        var programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
        var localAppData = Environment.GetEnvironmentVariable("LocalAppData") ?? 
                          Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

        var cmakePaths = new[]
        {
            Path.Combine(programFiles, "CMake", "bin", toolName),
            Path.Combine(programFiles, "CMake", toolName),
            Path.Combine(localAppData, "Programs", "CMake", "bin", toolName),
        };

        foreach (var cmakePath in cmakePaths)
        {
            if (File.Exists(cmakePath))
            {
                results.Add(new FoundTool
                {
                    Name = toolName,
                    Path = cmakePath,
                    Source = ToolSource.CommonLocation
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Searches for MinGW/GCC tools in common installation locations.
    /// </summary>
    private static List<FoundTool> SearchMinGwLocations(string toolName)
    {
        var results = new List<FoundTool>();

        var programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
        var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";

        var mingwPaths = new[]
        {
            Path.Combine(programFiles, "mingw-w64"),
            Path.Combine(programFiles, "MinGW"),
            Path.Combine(programFilesX86, "mingw-w64"),
            Path.Combine(programFilesX86, "MinGW"),
            Path.Combine(programFiles, "TDM-GCC-64"),
            Path.Combine(programFiles, "TDM-GCC-32"),
        };

        foreach (var mingwBase in mingwPaths)
        {
            if (Directory.Exists(mingwBase))
            {
                var found = SearchInDirectory(mingwBase, toolName, ToolSource.CommonLocation, recursive: true);
                results.AddRange(found);
            }
        }

        return results;
    }

    /// <summary>
    /// Searches for Clang tools in common installation locations.
    /// </summary>
    private static List<FoundTool> SearchClangLocations(string toolName)
    {
        var results = new List<FoundTool>();

        var programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
        var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";
        var localAppData = Environment.GetEnvironmentVariable("LocalAppData") ?? 
                          Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

        var clangPaths = new[]
        {
            Path.Combine(programFiles, "LLVM", "bin"),
            Path.Combine(programFilesX86, "LLVM", "bin"),
            Path.Combine(localAppData, "Programs", "LLVM", "bin"),
        };

        foreach (var clangBase in clangPaths)
        {
            if (Directory.Exists(clangBase))
            {
                var found = SearchInDirectory(clangBase, toolName, ToolSource.CommonLocation, recursive: false);
                results.AddRange(found);
            }
        }

        return results;
    }

    /// <summary>
    /// Gets version information for a tool (if available).
    /// </summary>
    public static string? GetToolVersion(string toolPath)
    {
        try
        {
            if (!File.Exists(toolPath))
                return null;

            var versionInfo = FileVersionInfo.GetVersionInfo(toolPath);
            if (versionInfo != null && !string.IsNullOrEmpty(versionInfo.FileVersion))
            {
                return versionInfo.FileVersion;
            }
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    /// <summary>
    /// Prints all found tools to the console in a formatted way.
    /// </summary>
    public static void PrintFoundTools(Dictionary<string, List<FoundTool>> tools)
    {
        if (tools.Count == 0)
        {
            Console.WriteLine("No tools found.");
            return;
        }

        Console.WriteLine("=== Found Tools ===");
        Console.WriteLine();

        foreach (var kvp in tools.OrderBy(k => k.Key))
        {
            Console.WriteLine($"{kvp.Key}:");
            foreach (var tool in kvp.Value)
            {
                // Get version if not already set
                if (string.IsNullOrEmpty(tool.Version))
                {
                    tool.Version = GetToolVersion(tool.Path);
                }

                var versionStr = !string.IsNullOrEmpty(tool.Version) ? $" (v{tool.Version})" : "";
                var sourceStr = tool.Source switch
                {
                    ToolSource.ProjectRoot => "[Project Root]",
                    ToolSource.EnvironmentPath => "[PATH]",
                    ToolSource.CommonLocation => "[Common Location]",
                    ToolSource.Vswhere => "[vswhere]",
                    _ => "[Unknown]"
                };

                Console.WriteLine($"  {sourceStr} {tool.Path}{versionStr}");
            }
            Console.WriteLine();
        }
    }
}

