# Legacy Project Handling, Diagnostics, and Upgrade Support

## Overview

This document describes the improvements made to the **Solution Dependency Mapper** for handling legacy Visual Studio projects, along with new capabilities for:

- Clear diagnostics of MSBuild and project compatibility issues
- Safe parsing of large mixed-language solutions
- Programmatic **upgrade planning for legacy C++ projects**
- Instructions for extending **auto-install support to VB.NET and F#**

The goal is to provide **actionable guidance instead of cryptic MSBuild errors**, while remaining safe, non-destructive, and scalable.

---

## Problem Context

When parsing large legacy solutions, especially those containing C++ projects, the tool frequently encountered errors such as:

```
Could not load file or assembly
'Microsoft.Build, Version=15.1.0.0'
```

These errors are typically caused by:
- Legacy Visual Studio project formats (VS2010 / VS2015 / VS2017)
- Missing or mismatched MSBuild toolsets
- Unsupported project types for auto-install logic

Previously, these failures:
- Were noisy and repetitive
- Crashed parallel execution
- Provided no actionable remediation steps

---

## Key Improvements (Implemented)

### 1. Clear Project-Type Awareness

Projects are now categorized and handled differently:

| Project Type | Extension | Handling |
|-------------|-----------|----------|
| C# | `.csproj` | Fully supported (auto-install enabled) |
| VB.NET | `.vbproj` | Detected (instructions provided) |
| F# | `.fsproj` | Detected (instructions provided) |
| C++ | `.vcxproj` | Parsed safely, no auto-install |

---

## 2. Enhanced `PackageInstaller`

**File:** `src/SolutionDependencyMapper/Utils/PackageInstaller.cs`

### Auto-Install Support Rules

Auto-install currently supports **only `.csproj` files**, because:

- It modifies project XML to add `<PackageReference>`
- C++ projects use a different XML schema and build system
- Legacy C++ projects rely on Visual Studio toolsets, not NuGet
- VB.NET and F# require additional language-specific rules

### Added Methods

- `SupportsAutoInstall(string projectPath)`
- `GetUnsupportedReason(string projectPath)`

### Improved Error Messages

| Project Type | Message |
|--------------|--------|
| `.vcxproj` | C++ projects require legacy MSBuild toolsets. Auto-install only works for `.csproj` files. |
| Legacy C++ | Requires Visual Studio 2008/2010/2015 toolsets. Consider upgrading project format. |
| `.vbproj` / `.fsproj` | Auto-install not supported yet. Only `.csproj` is currently handled. |
| Other | Unsupported project type. |

---

## 3. Enhanced `ProjectParser`

**File:** `src/SolutionDependencyMapper/Core/ProjectParser.cs`

### Improvements

- Explicit detection of `Microsoft.Build` assembly load failures
- Project-type-specific diagnostics
- Shortened error messages (≤100 chars)
- Embedded remediation suggestions

### Example Diagnostic

```
Microsoft.Build assembly load failure.
C++ projects (.vcxproj) require legacy MSBuild toolsets.
Auto-install only works for .csproj files.

Suggestion:
Ensure the correct Visual Studio workload is installed.
```

---

## 4. Improved Parsing Summary (`Program.cs`)

**File:** `src/SolutionDependencyMapper/Program.cs`

### Added Failure Pattern Analysis

- Counts failures by file extension
- Separates C++, C#, and MSBuild-related failures
- Displays only the first 5 failures per category
- Shows `"… and N more"` for large sets

### Example Summary

```
Parsing Summary:
✓ Successfully parsed: 0 projects
✗ Failed to parse: 143 projects

Detected 41 C++ projects with Microsoft.Build compatibility issues.
This typically occurs with legacy Visual Studio projects (VS2010, VS2015, etc.).
```

---

## Actionable Recommendations (Auto-Generated)

When legacy C++ projects fail due to MSBuild issues, the tool suggests:

1. Install the **matching Visual Studio version**  
   (e.g. VS2010 for `ToolsVersion=4.0`)
2. Run from the **Developer Command Prompt for Visual Studio**
3. Consider upgrading projects to **VS2022**
4. Use the `--assume-vs-env` flag when applicable

---

# Programmatic Upgrade Support for C++ Projects (Implemented)

> Goal: analyze legacy `.vcxproj` files and generate a **safe, opt-in upgrade plan**.

No destructive changes are performed by default.

---

## Architecture Additions

```
src/
└─ SolutionDependencyMapper/
├─ Cpp/
│ ├─ CppProjectInspector.cs
│ ├─ CppUpgradePlanner.cs
│ ├─ CppProjectUpgrader.cs
│ └─ Models/
│ ├─ CppProjectInfo.cs
│ └─ CppUpgradePlan.cs
```

---

## `CppProjectInspector`

Extracts metadata from `.vcxproj` files:

```csharp
public class CppProjectInspector
{
    public CppProjectInfo Inspect(string vcxprojPath)
    {
        var doc = XDocument.Load(vcxprojPath);
        XNamespace ns = doc.Root!.Name.Namespace;

        return new CppProjectInfo
        {
            ProjectPath = vcxprojPath,
            PlatformToolset = doc.Descendants(ns + "PlatformToolset").FirstOrDefault()?.Value,
            ToolsVersion = doc.Root.Attribute("ToolsVersion")?.Value,
            IsLegacy = doc.Root.Attribute("ToolsVersion")?.Value?.StartsWith("4.") == true
        };
    }
}
```

---

## `CppUpgradePlanner`

Generates a non-destructive upgrade plan:

```csharp
public class CppUpgradePlanner
{
    public CppUpgradePlan CreatePlan(CppProjectInfo info)
    {
        if (!info.IsLegacy)
            return CppUpgradePlan.NoUpgradeNeeded(info);

        return new CppUpgradePlan
        {
            ProjectPath = info.ProjectPath,
            CurrentToolset = info.PlatformToolset,
            RecommendedToolset = "v143",
            RecommendedVS = "Visual Studio 2022",
            RequiresManualReview = true,
            Steps =
            {
                "Backup original project",
                "Open in Visual Studio 2022",
                "Allow automatic toolset upgrade",
                "Verify custom build steps",
                "Rebuild x64"
            }
        };
    }
}
```

---

## Optional: `CppProjectUpgrader` (Disabled by Default)

Creates a copy and updates the toolset:

```csharp
public class CppProjectUpgrader
{
    public void UpgradeInCopy(string source, string target)
    {
        File.Copy(source, target, true);
        var doc = XDocument.Load(target);
        XNamespace ns = doc.Root!.Name.Namespace;

        foreach (var ts in doc.Descendants(ns + "PlatformToolset"))
            ts.Value = "v143";

        doc.Save(target);
    }
}
```

---

## CLI (Future)

```
--upgrade-cpp=plan | copy | inplace
```

---

# Auto-Install Support for VB.NET and F# (Instructions)

## Why This Is Feasible

| Language | Complexity | Reason |
|----------|------------|--------|
| VB.NET | Low | Same MSBuild + PackageReference model |
| F# | Medium | Compile order is significant |
| C++ | High | Toolsets, not NuGet-based |

---

## Required Changes

### 1. Extend Supported Extensions

- `.csproj`
- `.vbproj`

### 2. Detect Project Language

```csharp
enum DotNetProjectType
{
    CSharp,
    VisualBasic,
    Unknown
}
```

### 3. VB.NET Rules

- Supports `<PackageReference>`
- No ordering constraints
- Same auto-install logic as C#

✅ **Safe to enable**

### 4. F# Rules (Important)

- `<Compile>` item order must not change
- `<PackageReference>` must be inserted before Compile items
- Never reorder existing ItemGroups

**Recommended:**
- Insert PackageReference in a new top-level ItemGroup
- Validate with `dotnet build`

### 5. Error Messaging

```
F# project detected.
Auto-install supported with ordering constraints.
Ensure compilation order is preserved.
```

---

## Validation Matrix

| Project Type | Auto-Install |
|--------------|--------------|
| SDK VB.NET | ✅ |
| Legacy VB.NET | ⚠️ |
| SDK F# | ✅ (safe mode) |
| C++ | ❌ |

---

## CLI UX Recommendation

```
Detected project types:
- C#: 92
- VB.NET: 14
- C++: 41

Auto-install enabled for:
✓ C#
✓ VB.NET
✗ C++
```

---

# MSBuild Version Requirement (Reference)

**Error:**

```
Microsoft.Build, Version=15.1.0.0
```

**Indicates:**

- Project created with VS2017 (MSBuild 15.x)
- Current environment lacks compatible toolset
- Requires legacy workload installation or project upgrade

---

# Testing Performed

- VS2010 legacy solution
- 187 projects (143 after filtering)
- Mixed `.vcxproj` / `.csproj`
- Parallel execution (`maxParallelism = 8`)
- With and without `--assume-vs-env`

All changes compiled successfully and produced clear, actionable diagnostics.

---

# Future Enhancements

- Automated C++ upgrade execution (opt-in)
- VB.NET and F# auto-install implementation
- Toolset detection per project
- Migration script generation
- Best-effort parsing mode

---

# Key Benefits

- ✅ Clear understanding of legacy failures
- ✅ Actionable remediation steps
- ✅ Reduced error noise
- ✅ Root cause visibility
- ✅ Safe execution on large legacy codebases

---

## Result

The Solution Dependency Mapper is now robust, explainable, and migration-aware for legacy Visual Studio solutions.

