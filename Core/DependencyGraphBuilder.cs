using SolutionDependencyMapper.Models;
using SolutionDependencyMapper.Utils;

namespace SolutionDependencyMapper.Core;

/// <summary>
/// Builds and analyzes the dependency graph from parsed projects.
/// </summary>
public class DependencyGraphBuilder
{
    /// <summary>
    /// Builds a complete dependency graph from a list of project nodes.
    /// </summary>
    /// <param name="projects">List of parsed project nodes</param>
    /// <returns>Complete dependency graph with edges, layers, and cycles</returns>
    public static DependencyGraph BuildGraph(List<ProjectNode> projects)
    {
        var graph = new DependencyGraph
        {
            Nodes = new Dictionary<string, ProjectNode>(),
            Edges = new List<DependencyEdge>(),
            BuildLayers = new List<BuildLayer>(),
            Cycles = new List<List<string>>()
        };

        // Add all nodes to the graph
        foreach (var project in projects)
        {
            graph.Nodes[project.Path] = project;
        }

        // Build edges from project dependencies
        foreach (var project in projects)
        {
            foreach (var depPath in project.ProjectDependencies)
            {
                // Only add edges for projects that exist in our graph
                if (graph.Nodes.ContainsKey(depPath))
                {
                    var edge = new DependencyEdge
                    {
                        FromProject = project.Path,
                        ToProject = depPath,
                        DependencyType = "ProjectReference"
                    };
                    graph.Edges.Add(edge);
                }
            }
        }

        // Detect cycles
        graph.Cycles = CycleDetector.DetectCycles(graph);

        // Build layers using topological sort
        graph.BuildLayers = TopologicalSorter.SortIntoLayers(graph);

        // Calculate migration scores for all projects
        foreach (var project in graph.Nodes.Values)
        {
            var score = MigrationScorer.CalculateScore(project, graph);
            project.MigrationScore = score.TotalScore;
            project.MigrationDifficultyLevel = score.DifficultyLevel;
        }

        return graph;
    }
}

