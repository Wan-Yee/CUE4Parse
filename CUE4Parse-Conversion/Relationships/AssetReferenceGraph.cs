namespace CUE4Parse_Conversion.Relationships;

public sealed class AssetNode
{
    public string AssetPath { get; init; } = "";
    public string AssetName { get; init; } = "";
    public string AssetType { get; init; } = "";
}

public sealed class AssetEdge
{
    public string SourceAsset { get; init; } = "";
    public string TargetAsset { get; init; } = "";
    public AssetReferenceType ReferenceType { get; init; }
    public string EvidenceSource { get; init; } = "";
    public string? PropertyPath { get; init; }
    public bool IsSoftReference { get; init; }

    public bool IsSkeletonOnly => ReferenceType == AssetReferenceType.Skeleton;
}

public sealed class AssetReferenceGraph
{
    private readonly Dictionary<string, AssetNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<AssetEdge>> _outgoing = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<AssetEdge>> _incoming = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<AssetNode> Nodes => _nodes.Values;
    public IEnumerable<AssetEdge> Edges => _outgoing.Values.SelectMany(x => x);

    public void AddNode(string assetPath, string assetType)
    {
        if (string.IsNullOrWhiteSpace(assetPath)) return;
        if (_nodes.ContainsKey(assetPath)) return;
        _nodes[assetPath] = new AssetNode
        {
            AssetPath = assetPath,
            AssetName = GetAssetName(assetPath),
            AssetType = assetType
        };
    }

    public void AddEdge(AssetEdge edge)
    {
        if (string.IsNullOrWhiteSpace(edge.SourceAsset) ||
            string.IsNullOrWhiteSpace(edge.TargetAsset) ||
            string.Equals(edge.SourceAsset, edge.TargetAsset, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AddNode(edge.SourceAsset, "Unknown");
        AddNode(edge.TargetAsset, "Unknown");

        if (_outgoing.TryGetValue(edge.SourceAsset, out var existing) &&
            existing.Any(x =>
                string.Equals(x.TargetAsset, edge.TargetAsset, StringComparison.OrdinalIgnoreCase) &&
                x.ReferenceType == edge.ReferenceType &&
                string.Equals(x.PropertyPath, edge.PropertyPath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _outgoing.GetValueOrDefault(edge.SourceAsset)?.Add(edge);
        if (!_outgoing.ContainsKey(edge.SourceAsset)) _outgoing[edge.SourceAsset] = [edge];

        _incoming.GetValueOrDefault(edge.TargetAsset)?.Add(edge);
        if (!_incoming.ContainsKey(edge.TargetAsset)) _incoming[edge.TargetAsset] = [edge];
    }

    public IReadOnlyList<AssetEdge> GetOutgoing(string assetPath) =>
        _outgoing.TryGetValue(assetPath, out var edges) ? edges : [];

    public IReadOnlyList<AssetEdge> GetIncoming(string assetPath) =>
        _incoming.TryGetValue(assetPath, out var edges) ? edges : [];

    public IReadOnlyDictionary<string, ReferencePath> FindSourcesThatReach(
        string targetAsset,
        int maxDepth,
        Func<AssetEdge, bool>? edgePredicate = null)
    {
        var results = new Dictionary<string, ReferencePath>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { targetAsset };
        var queue = new Queue<(string Node, List<AssetEdge> ReverseEdges)>();
        queue.Enqueue((targetAsset, []));

        while (queue.Count > 0)
        {
            var (node, reverseEdges) = queue.Dequeue();
            if (reverseEdges.Count >= maxDepth) continue;

            foreach (var edge in GetIncoming(node))
            {
                if (edgePredicate != null && !edgePredicate(edge)) continue;
                if (!visited.Add(edge.SourceAsset)) continue;

                var nextReverse = new List<AssetEdge>(reverseEdges.Count + 1) { edge };
                nextReverse.AddRange(reverseEdges);
                var forward = nextReverse.AsEnumerable().Reverse().ToList();
                results[edge.SourceAsset] = new ReferencePath(edge.SourceAsset, targetAsset, forward);
                queue.Enqueue((edge.SourceAsset, nextReverse));
            }
        }

        return results;
    }

    public static string GetAssetName(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        var leaf = path[(path.LastIndexOf('/') + 1)..];
        var dot = leaf.LastIndexOf('.');
        return dot >= 0 ? leaf[(dot + 1)..] : leaf;
    }
}

public sealed record ReferencePath(string SourceAsset, string TargetAsset, IReadOnlyList<AssetEdge> Edges)
{
    public bool IsHardEvidence => Edges.Count > 0 && Edges.All(edge => !edge.IsSoftReference);

    public string Describe() => string.Join(" -> ", Edges.Select(edge =>
        $"{AssetReferenceGraph.GetAssetName(edge.SourceAsset)}[{edge.ReferenceType}]{AssetReferenceGraph.GetAssetName(edge.TargetAsset)}"));
}
