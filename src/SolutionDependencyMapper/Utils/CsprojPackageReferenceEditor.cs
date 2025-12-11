using System.Xml.Linq;

namespace SolutionDependencyMapper.Utils;

internal sealed class CsprojPackageReferenceEditor
{
    private readonly string _projectPath;

    public CsprojPackageReferenceEditor(string projectPath)
    {
        _projectPath = projectPath;
    }

    public bool AddPackageReferences(IEnumerable<(string Name, string Version)> packages)
    {
        if (!File.Exists(_projectPath))
            return false;

        if (!_projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var doc = XDocument.Load(_projectPath);
            var projectEl = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Project");
            if (projectEl == null)
                return false;

            var isSdkStyle = IsSdkStyleProject(projectEl);
            var ns = isSdkStyle ? XNamespace.None : XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");

            var itemGroup = FindOrCreatePackageReferenceItemGroup(doc, projectEl, ns);
            if (itemGroup == null)
                return false;

            var packagesAdded = false;
            foreach (var (name, version) in packages)
            {
                if (HasPackageReference(itemGroup, ns, name))
                    continue;

                itemGroup.Add(CreatePackageReference(ns, name, version, isSdkStyle));
                packagesAdded = true;
            }

            if (!packagesAdded)
                return false;

            doc.Save(_projectPath);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️  Warning: Could not edit {Path.GetFileName(_projectPath)}: {ex.Message}");
            return false;
        }
    }

    private static bool IsSdkStyleProject(XElement projectEl)
    {
        return projectEl.Attribute("Sdk") != null ||
               projectEl.Name.Namespace == XNamespace.None ||
               projectEl.Elements().Any(e => e.Name.LocalName == "PropertyGroup" && e.Name.Namespace == XNamespace.None);
    }

    private static XElement? FindOrCreatePackageReferenceItemGroup(XDocument doc, XElement projectEl, XNamespace ns)
    {
        var itemGroupName = ns == XNamespace.None ? "ItemGroup" : ns + "ItemGroup";
        var packageRefName = ns == XNamespace.None ? "PackageReference" : ns + "PackageReference";

        // Prefer an existing ItemGroup that already contains PackageReference.
        var itemGroup = projectEl
            .Elements(itemGroupName)
            .FirstOrDefault(g => g.Elements(packageRefName).Any());

        if (itemGroup != null)
            return itemGroup;

        // Otherwise create a new one.
        itemGroup = ns == XNamespace.None ? new XElement("ItemGroup") : new XElement(ns + "ItemGroup");

        if (ns == XNamespace.None)
        {
            projectEl.Add(itemGroup);
            return itemGroup;
        }

        // Traditional projects should have the namespace on Project.
        var nsProject = doc.Descendants(ns + "Project").FirstOrDefault();
        if (nsProject == null)
            return null;

        nsProject.Add(itemGroup);
        return itemGroup;
    }

    private static bool HasPackageReference(XElement itemGroup, XNamespace ns, string packageName)
    {
        if (ns == XNamespace.None)
        {
            return itemGroup.Elements()
                .Any(e =>
                    e.Name.LocalName == "PackageReference" &&
                    (string.Equals(e.Attribute("Include")?.Value, packageName, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(e.Attribute("Update")?.Value, packageName, StringComparison.OrdinalIgnoreCase)));
        }

        return itemGroup.Elements(ns + "PackageReference")
            .Any(e =>
                string.Equals(e.Attribute("Include")?.Value, packageName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.Attribute("Update")?.Value, packageName, StringComparison.OrdinalIgnoreCase));
    }

    private static XElement CreatePackageReference(XNamespace ns, string packageName, string version, bool isSdkStyle)
    {
        // Keep existing behavior: SDK style uses <Version> child element.
        if (isSdkStyle)
        {
            return new XElement("PackageReference",
                new XAttribute("Include", packageName),
                new XElement("Version", version));
        }

        return new XElement(ns + "PackageReference",
            new XAttribute("Include", packageName),
            new XElement(ns + "Version", version));
    }
}


