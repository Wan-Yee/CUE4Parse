using System.Text.RegularExpressions;

namespace CUE4Parse_Conversion.Relationships;

public sealed class MeshAnimationRelationshipResolver
{
    private static readonly Regex TokenSplitter = new("[^A-Za-z0-9]+", RegexOptions.Compiled);

    public IReadOnlyList<MeshAnimationRelationship> Resolve(
        SkeletalMeshProfile mesh,
        IEnumerable<AnimationMetadata> animations,
        AssetReferenceGraph graph)
    {
        var results = new List<MeshAnimationRelationship>();
        foreach (var animation in animations)
        {
            results.Add(ResolveOne(mesh, animation, graph));
        }
        return results;
    }

    private static MeshAnimationRelationship ResolveOne(
        SkeletalMeshProfile mesh,
        AnimationMetadata animation,
        AssetReferenceGraph graph)
    {
        var compatibility = GetCompatibility(mesh.SkeletonPath, animation.SkeletonPath);
        var evidence = new List<RelationshipEvidence>();

        if (compatibility != MeshAnimationCompatibilityStatus.Compatible)
        {
            evidence.Add(new RelationshipEvidence
            {
                Type = compatibility == MeshAnimationCompatibilityStatus.Incompatible ? "SKELETON_MISMATCH" : "SKELETON_UNKNOWN",
                Source = mesh.AssetPath,
                Target = animation.AssetPath,
                Detail = $"mesh_skeleton={mesh.SkeletonPath ?? "null"}; animation_skeleton={animation.SkeletonPath ?? "null"}"
            });
            return new MeshAnimationRelationship
            {
                MeshAssetPath = mesh.AssetPath,
                AnimationAssetPath = animation.AssetPath,
                RelationshipLevel = MeshAnimationRelationshipLevel.Unresolved,
                CompatibilityStatus = compatibility,
                Score = 0,
                Evidence = evidence
            };
        }

        if (TryFindCommonReferenceEvidence(mesh.AssetPath, animation.AssetPath, graph, hardOnly: true, out var directEvidence))
        {
            evidence.AddRange(directEvidence);
            evidence.Add(SkeletonCompatibilityEvidence(mesh, animation));
            return new MeshAnimationRelationship
            {
                MeshAssetPath = mesh.AssetPath,
                AnimationAssetPath = animation.AssetPath,
                RelationshipLevel = MeshAnimationRelationshipLevel.DirectReference,
                CompatibilityStatus = compatibility,
                Score = 100,
                Evidence = evidence
            };
        }

        if (TryFindVariantInheritedEvidence(mesh.AssetPath, animation.AssetPath, graph, out var inheritedEvidence))
        {
            evidence.AddRange(inheritedEvidence);
            evidence.Add(SkeletonCompatibilityEvidence(mesh, animation));
            return new MeshAnimationRelationship
            {
                MeshAssetPath = mesh.AssetPath,
                AnimationAssetPath = animation.AssetPath,
                RelationshipLevel = MeshAnimationRelationshipLevel.StrongCandidate,
                CompatibilityStatus = compatibility,
                Score = 65,
                Evidence = evidence
            };
        }

        if (TryFindCommonReferenceEvidence(mesh.AssetPath, animation.AssetPath, graph, hardOnly: false, out var strongEvidence))
        {
            evidence.AddRange(strongEvidence);
            evidence.Add(SkeletonCompatibilityEvidence(mesh, animation));
            return new MeshAnimationRelationship
            {
                MeshAssetPath = mesh.AssetPath,
                AnimationAssetPath = animation.AssetPath,
                RelationshipLevel = MeshAnimationRelationshipLevel.StrongCandidate,
                CompatibilityStatus = compatibility,
                Score = 75,
                Evidence = evidence
            };
        }

        var nameScore = GetNameAndPathScore(mesh.AssetPath, animation.AssetPath);
        if (nameScore >= 35)
        {
            evidence.Add(SkeletonCompatibilityEvidence(mesh, animation));
            evidence.Add(new RelationshipEvidence
            {
                Type = "NAMING_OR_PACKAGE_PROXIMITY",
                Source = mesh.AssetPath,
                Target = animation.AssetPath,
                Detail = $"score={nameScore}",
                IsInference = true
            });
            return new MeshAnimationRelationship
            {
                MeshAssetPath = mesh.AssetPath,
                AnimationAssetPath = animation.AssetPath,
                RelationshipLevel = MeshAnimationRelationshipLevel.WeakCandidate,
                CompatibilityStatus = compatibility,
                Score = nameScore,
                Evidence = evidence
            };
        }

        evidence.Add(SkeletonCompatibilityEvidence(mesh, animation));
        return new MeshAnimationRelationship
        {
            MeshAssetPath = mesh.AssetPath,
            AnimationAssetPath = animation.AssetPath,
            RelationshipLevel = MeshAnimationRelationshipLevel.SkeletonOnly,
            CompatibilityStatus = compatibility,
            Score = 10,
            Evidence = evidence
        };
    }

    private static bool TryFindCommonReferenceEvidence(
        string meshPath,
        string animationPath,
        AssetReferenceGraph graph,
        bool hardOnly,
        out List<RelationshipEvidence> evidence)
    {
        static bool ReferenceEdge(AssetEdge edge) => !edge.IsSkeletonOnly && edge.ReferenceType != AssetReferenceType.VariantParent;

        var meshSources = graph.FindSourcesThatReach(meshPath, 3, ReferenceEdge);
        var animationSources = graph.FindSourcesThatReach(animationPath, 5, ReferenceEdge);
        evidence = [];

        foreach (var source in meshSources.Keys.Intersect(animationSources.Keys, StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(source, meshPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, animationPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var meshPathEvidence = meshSources[source];
            var animationPathEvidence = animationSources[source];
            if (hardOnly && (!meshPathEvidence.IsHardEvidence || !animationPathEvidence.IsHardEvidence))
            {
                continue;
            }

            evidence.Add(new RelationshipEvidence
            {
                Type = hardOnly ? "EXPLICIT_HARD_REFERENCE_CHAIN" : "EXPLICIT_REFERENCE_CHAIN",
                Source = source,
                Target = animationPath,
                Detail = $"mesh_path={meshPathEvidence.Describe()}; animation_path={animationPathEvidence.Describe()}"
            });
            return true;
        }

        return false;
    }

    private static bool TryFindVariantInheritedEvidence(
        string meshPath,
        string animationPath,
        AssetReferenceGraph graph,
        out List<RelationshipEvidence> evidence)
    {
        static bool ReferenceEdge(AssetEdge edge) => !edge.IsSkeletonOnly && edge.ReferenceType != AssetReferenceType.VariantParent;

        evidence = [];
        var meshOwners = graph.FindSourcesThatReach(meshPath, 3, ReferenceEdge);
        var variantAssets = new HashSet<string>(meshOwners.Keys, StringComparer.OrdinalIgnoreCase) { meshPath };
        var animationSources = graph.FindSourcesThatReach(animationPath, 5, ReferenceEdge);

        foreach (var variantAsset in variantAssets)
        {
            foreach (var variantEdge in graph.GetOutgoing(variantAsset).Where(edge => edge.ReferenceType == AssetReferenceType.VariantParent))
            {
                if (animationSources.TryGetValue(variantEdge.TargetAsset, out var parentAnimationPath))
                {
                    evidence.Add(new RelationshipEvidence
                    {
                        Type = "INHERITED_REFERENCE_CHAIN",
                        Source = variantAsset,
                        Target = animationPath,
                        Detail = $"variant_parent={AssetReferenceGraph.GetAssetName(variantEdge.TargetAsset)}; animation_path={parentAnimationPath.Describe()}"
                    });
                    return true;
                }

                var parentOwners = graph.FindSourcesThatReach(variantEdge.TargetAsset, 3, ReferenceEdge);
                foreach (var parentOwner in parentOwners.Keys.Intersect(animationSources.Keys, StringComparer.OrdinalIgnoreCase))
                {
                    evidence.Add(new RelationshipEvidence
                    {
                        Type = "INHERITED_REFERENCE_CHAIN",
                        Source = variantAsset,
                        Target = animationPath,
                        Detail = $"variant_parent_path={parentOwners[parentOwner].Describe()}; animation_path={animationSources[parentOwner].Describe()}"
                    });
                    return true;
                }
            }
        }

        return false;
    }

    private static MeshAnimationCompatibilityStatus GetCompatibility(string? meshSkeletonPath, string? animationSkeletonPath)
    {
        if (string.IsNullOrWhiteSpace(meshSkeletonPath) || string.IsNullOrWhiteSpace(animationSkeletonPath))
            return MeshAnimationCompatibilityStatus.Unknown;

        return string.Equals(meshSkeletonPath, animationSkeletonPath, StringComparison.OrdinalIgnoreCase)
            ? MeshAnimationCompatibilityStatus.Compatible
            : MeshAnimationCompatibilityStatus.Incompatible;
    }

    private static RelationshipEvidence SkeletonCompatibilityEvidence(SkeletalMeshProfile mesh, AnimationMetadata animation) => new()
    {
        Type = "SKELETON_COMPATIBILITY",
        Source = mesh.SkeletonPath ?? mesh.AssetPath,
        Target = animation.SkeletonPath ?? animation.AssetPath,
        Detail = "Skeleton paths match; this is compatibility evidence only, not ownership evidence."
    };

    private static int GetNameAndPathScore(string meshPath, string animationPath)
    {
        var meshTokens = Tokenize(meshPath);
        var animTokens = Tokenize(animationPath);
        if (meshTokens.Count == 0 || animTokens.Count == 0) return 0;

        var overlap = meshTokens.Intersect(animTokens, StringComparer.OrdinalIgnoreCase)
            .Where(token => token.Length > 2)
            .Count();
        var commonDirectory = GetCommonDirectoryDepth(meshPath, animationPath);
        return Math.Min(60, overlap * 12 + commonDirectory * 4);
    }

    private static HashSet<string> Tokenize(string path)
    {
        var name = AssetReferenceGraph.GetAssetName(path);
        var tokens = TokenSplitter.Split(path + "/" + name)
            .Where(token => token.Length > 1)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return tokens;
    }

    private static int GetCommonDirectoryDepth(string left, string right)
    {
        var leftParts = left.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var rightParts = right.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var max = Math.Min(leftParts.Length, rightParts.Length);
        var count = 0;
        for (var i = 0; i < max; i++)
        {
            if (!string.Equals(leftParts[i], rightParts[i], StringComparison.OrdinalIgnoreCase)) break;
            count++;
        }
        return count;
    }
}
