using System.ComponentModel;

namespace CUE4Parse_Conversion.Options;

public enum EAnimationRelationshipExportMode
{
    [Description("Legacy Skeleton Export")]
    LegacySkeletonExport,

    [Description("Referenced Animations Only")]
    ReferencedAnimationsOnly,

    [Description("Referenced + Strong Candidates")]
    ReferencedAndStrongCandidates,

    [Description("All Candidates + Manifest")]
    AllCandidatesAndManifest
}
