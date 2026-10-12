namespace CUE4Parse_Conversion.Relationships;

public enum AssetReferenceType
{
    Unknown,
    HardReference,
    SoftReference,
    Skeleton,
    SkeletalMeshComponentMesh,
    AnimationClass,
    AnimationBlueprintReference,
    AnimationSegment,
    RetargetSourceAsset,
    PreviewSkeletalMesh,
    VariantParent
}

public enum MeshAnimationRelationshipLevel
{
    DirectReference,
    StrongCandidate,
    WeakCandidate,
    SkeletonOnly,
    Unresolved
}

public enum MeshAnimationCompatibilityStatus
{
    Compatible,
    Incompatible,
    Unknown
}
