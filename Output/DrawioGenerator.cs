using System.Xml.Linq;
using SolutionDependencyMapper.Models;

namespace SolutionDependencyMapper.Output;

/// <summary>
/// Generates Draw.io XML diagram format.
/// </summary>
public class DrawioGenerator
{
    private const int NodeWidth = 140;
    private const int NodeHeight = 60;
    private const int HorizontalSpacing = 200;
    private const int VerticalSpacing = 150;
    private const int StartX = 100;
    private const int StartY = 100;

    /// <summary>
    /// Generates a Draw.io XML diagram file.
    /// </summary>
    /// <param name="graph">The dependency graph to visualize</param>
    /// <param name="outputPath">Path to the output .drawio file</param>
    public static void Generate(DependencyGraph graph, string outputPath)
    {
        var doc = new XDocument(
            new XElement("mxfile",
                new XAttribute("host", "app.diagrams.net"),
                new XElement("diagram",
                    new XAttribute("id", "dependency-graph"),
                    new XAttribute("name", "Dependency Graph"),
                    CreateGraphModel(graph)
                )
            )
        );

        doc.Save(outputPath);
    }

    private static XElement CreateGraphModel(DependencyGraph graph)
    {
        var root = new XElement("mxGraphModel",
            new XAttribute("dx", "1422"),
            new XAttribute("dy", "794"),
            new XAttribute("grid", "1"),
            new XAttribute("gridSize", "10"),
            new XAttribute("guides", "1"),
            new XAttribute("tooltips", "1"),
            new XAttribute("connect", "1"),
            new XAttribute("arrows", "1"),
            new XAttribute("fold", "1"),
            new XAttribute("page", "1"),
            new XAttribute("pageScale", "1"),
            new XAttribute("pageWidth", "1169"),
            new XAttribute("pageHeight", "827"),
            new XAttribute("math", "0"),
            new XAttribute("shadow", "0"),
            new XElement("root",
                new XElement("mxCell", new XAttribute("id", "0")),
                new XElement("mxCell", new XAttribute("id", "1"), new XAttribute("parent", "0"))
            )
        );

        var rootElement = root.Element("root");
        if (rootElement == null) return root;

        // Calculate positions based on build layers
        var nodePositions = CalculateNodePositions(graph);

        int cellId = 2;
        var nodeIdMap = new Dictionary<string, int>();

        // Create nodes
        foreach (var node in graph.Nodes.Values)
        {
            var nodeId = cellId++;
            nodeIdMap[node.Path] = nodeId;

            var position = nodePositions[node.Path];
            var label = $"{node.Name}\n({node.OutputType})";
            var style = GetNodeStyle(node.OutputType);

            var cell = new XElement("mxCell",
                new XAttribute("id", nodeId.ToString()),
                new XAttribute("value", label),
                new XAttribute("style", style),
                new XAttribute("vertex", "1"),
                new XAttribute("parent", "1"),
                new XElement("mxGeometry",
                    new XAttribute("x", position.X.ToString()),
                    new XAttribute("y", position.Y.ToString()),
                    new XAttribute("width", NodeWidth.ToString()),
                    new XAttribute("height", NodeHeight.ToString()),
                    new XAttribute("as", "geometry")
                )
            );

            rootElement.Add(cell);
        }

        // Create edges
        foreach (var edge in graph.Edges)
        {
            if (nodeIdMap.TryGetValue(edge.FromProject, out var fromId) &&
                nodeIdMap.TryGetValue(edge.ToProject, out var toId))
            {
                var edgeCell = new XElement("mxCell",
                    new XAttribute("id", cellId++.ToString()),
                    new XAttribute("value", ""),
                    new XAttribute("style", "edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;"),
                    new XAttribute("edge", "1"),
                    new XAttribute("parent", "1"),
                    new XAttribute("source", fromId.ToString()),
                    new XAttribute("target", toId.ToString()),
                    new XElement("mxGeometry",
                        new XAttribute("relative", "1"),
                        new XAttribute("as", "geometry")
                    )
                );

                rootElement.Add(edgeCell);
            }
        }

        return root;
    }

    private static Dictionary<string, (int X, int Y)> CalculateNodePositions(DependencyGraph graph)
    {
        var positions = new Dictionary<string, (int X, int Y)>();
        
        // Group nodes by build layer
        var layerGroups = new Dictionary<int, List<string>>();
        foreach (var layer in graph.BuildLayers)
        {
            layerGroups[layer.LayerNumber] = layer.ProjectPaths;
        }

        // Position nodes layer by layer
        int currentY = StartY;
        foreach (var layerNum in layerGroups.Keys.OrderBy(k => k))
        {
            var projectsInLayer = layerGroups[layerNum];
            int currentX = StartX;
            int maxHeight = 0;

            foreach (var projectPath in projectsInLayer)
            {
                positions[projectPath] = (currentX, currentY);
                currentX += NodeWidth + HorizontalSpacing;
                maxHeight = Math.Max(maxHeight, NodeHeight);
            }

            currentY += maxHeight + VerticalSpacing;
        }

        // Position nodes not in any layer
        foreach (var node in graph.Nodes.Values)
        {
            if (!positions.ContainsKey(node.Path))
            {
                positions[node.Path] = (StartX, currentY);
                currentY += NodeHeight + VerticalSpacing;
            }
        }

        return positions;
    }

    private static string GetNodeStyle(string outputType)
    {
        var baseStyle = "rounded=0;whiteSpace=wrap;html=1;";
        var colorStyle = outputType switch
        {
            "Exe" => "fillColor=#ff6b6b;strokeColor=#c92a2a;",
            "DynamicLibrary" => "fillColor=#51cf66;strokeColor=#2f9e44;",
            "StaticLibrary" => "fillColor=#339af0;strokeColor=#1c7ed6;",
            _ => "fillColor=#e1e1e1;strokeColor=#666666;"
        };
        return baseStyle + colorStyle;
    }
}

