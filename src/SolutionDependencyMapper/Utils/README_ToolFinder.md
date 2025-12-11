# ToolFinder Utility

The `ToolFinder` utility class helps locate Visual Studio tools, CMake, and other C++ build tools across your system.

## Features

- **Multi-location search**: Searches in project root, PATH environment variable, and common Windows installation locations
- **Comprehensive tool detection**: Finds MSBuild, CMake, compilers (cl.exe, clang.exe, gcc.exe), linkers, and more
- **Version detection**: Automatically detects tool versions when available
- **Source tracking**: Identifies where each tool was found (PATH, common location, vswhere, etc.)

## Usage

### From Command Line

```bash
# Find all tools (searches PATH and common locations)
dotnet run -- --find-tools

# Find all tools including project root directory
dotnet run -- --find-tools "C:\MyProject"
```

### From Code

```csharp
using SolutionDependencyMapper.Utils;

// Find all tools
var allTools = ToolFinder.FindAllTools(projectRoot: @"C:\MyProject");

// Find a specific tool
var msbuildInstances = ToolFinder.FindTool("msbuild.exe", projectRoot: @"C:\MyProject");

// Print results
ToolFinder.PrintFoundTools(allTools);
```

## Supported Tools

The tool finder searches for:

- **Build Systems**: `msbuild.exe`, `cmake.exe`, `ninja.exe`
- **Compilers**: `cl.exe`, `clang.exe`, `clang++.exe`, `gcc.exe`, `g++.exe`
- **Linkers**: `link.exe`
- **Utilities**: `dumpbin.exe`, `lib.exe`, `nmake.exe`, `vswhere.exe`
- **IDEs**: `devenv.exe`
- **Environment**: `vcvarsall.bat`

## Search Locations

1. **Project Root** (if specified): Recursively searches the project directory
2. **PATH Environment Variable**: Searches all directories in the system PATH
3. **Common Visual Studio Locations**:
   - `C:\Program Files\Microsoft Visual Studio\2022\*`
   - `C:\Program Files (x86)\Microsoft Visual Studio\2019\*`
   - `C:\Program Files (x86)\Microsoft Visual Studio\2017\*`
   - Uses `vswhere.exe` for reliable detection
4. **Common CMake Locations**:
   - `C:\Program Files\CMake\bin\`
   - `%LocalAppData%\Programs\CMake\bin\`
5. **Common MinGW/GCC Locations**:
   - `C:\Program Files\mingw-w64\`
   - `C:\Program Files\MinGW\`
   - `C:\Program Files\TDM-GCC-64\`
6. **Common Clang/LLVM Locations**:
   - `C:\Program Files\LLVM\bin\`
   - `%LocalAppData%\Programs\LLVM\bin\`

## Example Output

```
=== Found Tools ===

cmake.exe:
  [PATH] C:\Program Files\CMake\bin\cmake.exe (v3.28.0)
  [Common Location] C:\Program Files\CMake\bin\cmake.exe (v3.28.0)

msbuild.exe:
  [vswhere] C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe (v17.8.0)
  [Common Location] C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe (v17.8.0)

cl.exe:
  [Common Location] C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.38.33130\bin\Hostx64\x64\cl.exe (v19.38.0)

=== Summary ===
Total tools found: 15
Unique tool types: 8
```

## ToolSource Enum

Each found tool includes a `Source` property indicating where it was found:

- `ProjectRoot`: Found in the specified project root directory
- `EnvironmentPath`: Found in the PATH environment variable
- `CommonLocation`: Found in a common Windows installation location
- `Vswhere`: Found using vswhere.exe (most reliable for Visual Studio tools)

## API Reference

### `FindAllTools(string? projectRoot = null)`

Finds all supported tools and returns a dictionary mapping tool names to lists of found instances.

**Returns**: `Dictionary<string, List<FoundTool>>`

### `FindTool(string toolName, string? projectRoot = null)`

Finds a specific tool by name.

**Parameters**:
- `toolName`: Name of the tool (e.g., "msbuild.exe", "cmake.exe")
- `projectRoot`: Optional project root directory to search

**Returns**: `List<FoundTool>` - List of all found instances

### `GetToolVersion(string toolPath)`

Gets version information for a tool from its file properties.

**Returns**: `string?` - Version string or null if unavailable

### `PrintFoundTools(Dictionary<string, List<FoundTool>> tools)`

Prints all found tools to the console in a formatted way.

## FoundTool Class

```csharp
public class FoundTool
{
    public string Name { get; set; }        // Tool name (e.g., "msbuild.exe")
    public string Path { get; set; }        // Full path to the tool
    public ToolSource Source { get; set; }  // Where it was found
    public string? Version { get; set; }    // Version (if available)
}
```

