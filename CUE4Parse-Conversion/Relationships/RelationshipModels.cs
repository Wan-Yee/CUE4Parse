namespace CUE4Parse_Conversion.Relationships;

public sealed class AssetRelationshipIndex
{
    public List<SkeletalMeshProfile> SkeletalMeshes { get; } = [];
    public List<AnimationMetadata> Animations { get; } = [];
    public AssetReferenceGraph ReferenceGraph { get; } = new();

    private IReadOnlyDictionary<string, SkeletalMeshProfile>? _skeletalMeshByPath;
    public IReadOnlyDictionary<string, SkeletalMeshProfile> SkeletalMeshByPath =>
        _skeletalMeshByPath ??= SkeletalMeshes.ToDictionary(x => x.AssetPath, StringComparer.OrdinalIgnoreCase);

    private IReadOnlyDictionary<string, AnimationMetadata>? _animationByPath;
    public IReadOnlyDictionary<string, AnimationMetadata> AnimationByPath =>
        _animationByPath ??= Animations.ToDictionary(x => x.AssetPath, StringComparer.OrdinalIgnoreCase);
}

public sealed class SkeletalMeshProfile
{
    public string AssetPath { get; init; } = "";
    public string AssetName { get; init; } = "";
    public string AssetType { get; init; } = "SkeletalMesh";
    public string? SkeletonPath { get; set; }
    public string? MeshRole { get; set; }
    public bool MeshRoleInferred { get; set; }
    public int? BoneCount { get; set; }
    public int? LodCount { get; set; }
    public List<string> References { get; } = [];
    public string? SourcePackage { get; init; }
    public string ExtractionStatus { get; set; } = "partial";
    public List<ReferenceBoneInfo> ReferenceSkeleton { get; } = [];
    public WeightedBoneProfile? WeightedBoneProfile { get; set; }
}

public sealed class ReferenceBoneInfo
{
    public string Name { get; init; } = "";
    public int ParentIndex { get; init; }
}

public sealed class WeightedBoneProfile
{
    public string ProfileStatus { get; init; } = "unavailable";
    public string? UnavailableReason { get; init; }
    public int SkeletonBones { get; init; }
    public Dictionary<string, WeightedBoneInfo> EffectiveBones { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class WeightedBoneInfo
{
    public int VertexCount { get; set; }
    public double WeightSum { get; set; }
}

public sealed class AnimationMetadata
{
    public string AssetPath { get; init; } = "";
    public string AssetName { get; init; } = "";
    public string AssetType { get; init; } = "Animation";
    public string? SkeletonPath { get; set; }
    public double? Duration { get; set; }
    public int? FrameCount { get; set; }
    public string? RetargetSource { get; set; }
    public string? RetargetSourceAsset { get; set; }
    public List<string> References { get; } = [];
    public List<string> ReferencedBy { get; } = [];
    public List<string> TrackBoneNames { get; } = [];
    public string ExtractionStatus { get; set; } = "partial";
}

public sealed class RelationshipEvidence
{
    public string Type { get; init; } = "";
    public string Source { get; init; } = "";
    public string Target { get; init; } = "";
    public string? Detail { get; init; }
    public bool IsInference { get; init; }
}

public sealed class MeshAnimationRelationship
{
    public string MeshAssetPath { get; init; } = "";
    public string AnimationAssetPath { get; init; } = "";
    public MeshAnimationRelationshipLevel RelationshipLevel { get; init; }
    public MeshAnimationCompatibilityStatus CompatibilityStatus { get; init; }
    public int Score { get; init; }
    public List<RelationshipEvidence> Evidence { get; init; } = [];
}
