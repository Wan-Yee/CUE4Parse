using System.Collections.Generic;
using System.Linq;
using CUE4Parse_Conversion.Relationships;

namespace CUE4Parse.Tests.Relationships;

public class MeshAnimationRelationshipResolverTests
{
    private const string Skeleton = "/Game/Common/SK_Human";

    [Fact]
    public void SameSkeletonMultipleCharactersKeepDirectAnimationsSeparate()
    {
        var meshA = Mesh("/Game/Characters/A/Mesh_A");
        var meshB = Mesh("/Game/Characters/B/Mesh_B");
        var animA = Animation("/Game/Animations/A/Anim_A");
        var animB = Animation("/Game/Animations/B/Anim_B");
        var graph = new AssetReferenceGraph();

        AddEdge(graph, "/Game/Characters/A/Character_A", meshA.AssetPath, AssetReferenceType.SkeletalMeshComponentMesh);
        AddEdge(graph, "/Game/Characters/A/Character_A", "/Game/Characters/A/AnimBP_A", AssetReferenceType.AnimationClass);
        AddEdge(graph, "/Game/Characters/A/AnimBP_A", animA.AssetPath, AssetReferenceType.AnimationBlueprintReference);

        AddEdge(graph, "/Game/Characters/B/Character_B", meshB.AssetPath, AssetReferenceType.SkeletalMeshComponentMesh);
        AddEdge(graph, "/Game/Characters/B/Character_B", "/Game/Characters/B/AnimBP_B", AssetReferenceType.AnimationClass);
        AddEdge(graph, "/Game/Characters/B/AnimBP_B", animB.AssetPath, AssetReferenceType.AnimationBlueprintReference);

        var resolver = new MeshAnimationRelationshipResolver();

        var meshAResults = ToDictionary(resolver.Resolve(meshA, new[] { animA, animB }, graph));
        var meshBResults = ToDictionary(resolver.Resolve(meshB, new[] { animA, animB }, graph));

        Assert.Equal(MeshAnimationRelationshipLevel.DirectReference, meshAResults[animA.AssetPath].RelationshipLevel);
        Assert.Equal(MeshAnimationRelationshipLevel.SkeletonOnly, meshAResults[animB.AssetPath].RelationshipLevel);
        Assert.Equal(MeshAnimationRelationshipLevel.DirectReference, meshBResults[animB.AssetPath].RelationshipLevel);
        Assert.Equal(MeshAnimationRelationshipLevel.SkeletonOnly, meshBResults[animA.AssetPath].RelationshipLevel);
    }

    [Fact]
    public void FacialMeshDoesNotPromoteUnreferencedSkeletonAnimation()
    {
        var mesh = Mesh("/Game/Characters/NA/961/CH_M_NA_961_Facial");
        var animation = Animation("/Game/World/Interactions/cancollection_cancabinet_eve");

        var relationship = new MeshAnimationRelationshipResolver()
            .Resolve(mesh, new[] { animation }, new AssetReferenceGraph())[0];

        Assert.Equal(MeshAnimationRelationshipLevel.SkeletonOnly, relationship.RelationshipLevel);
        Assert.Equal(MeshAnimationCompatibilityStatus.Compatible, relationship.CompatibilityStatus);
        Assert.Contains(relationship.Evidence, evidence => evidence.Type == "SKELETON_COMPATIBILITY");
    }

    [Fact]
    public void ExplicitVariantParentInheritsAsStrongCandidate()
    {
        var variantMesh = Mesh("/Game/Characters/A/Mesh_A_Variant");
        var inheritedAnim = Animation("/Game/Animations/A/Anim_A");
        var graph = new AssetReferenceGraph();

        AddEdge(graph, "/Game/Characters/A/Character_A_Variant", variantMesh.AssetPath, AssetReferenceType.SkeletalMeshComponentMesh);
        AddEdge(graph, "/Game/Characters/A/Character_A_Variant", "/Game/Characters/A/Character_A", AssetReferenceType.VariantParent);
        AddEdge(graph, "/Game/Characters/A/Character_A", "/Game/Characters/A/AnimBP_A", AssetReferenceType.AnimationClass);
        AddEdge(graph, "/Game/Characters/A/AnimBP_A", inheritedAnim.AssetPath, AssetReferenceType.AnimationBlueprintReference);

        var relationship = new MeshAnimationRelationshipResolver()
            .Resolve(variantMesh, new[] { inheritedAnim }, graph)[0];

        Assert.Equal(MeshAnimationRelationshipLevel.StrongCandidate, relationship.RelationshipLevel);
        Assert.Contains(relationship.Evidence, evidence => evidence.Type == "INHERITED_REFERENCE_CHAIN");
    }

    [Fact]
    public void MissingBlueprintPreservesSkeletonOnlyCandidate()
    {
        var relationship = new MeshAnimationRelationshipResolver()
            .Resolve(Mesh("/Game/Characters/A/Mesh_A"), new[] { Animation("/Game/Animations/A/Anim_A") }, new AssetReferenceGraph())[0];

        Assert.Equal(MeshAnimationRelationshipLevel.SkeletonOnly, relationship.RelationshipLevel);
        Assert.Equal(MeshAnimationCompatibilityStatus.Compatible, relationship.CompatibilityStatus);
    }

    [Fact]
    public void SoftReferenceChainIsStrongCandidate()
    {
        var mesh = Mesh("/Game/Characters/A/Mesh_A");
        var animation = Animation("/Game/Animations/A/Anim_A");
        var graph = new AssetReferenceGraph();

        AddEdge(graph, "/Game/Characters/A/Character_A", mesh.AssetPath, AssetReferenceType.SkeletalMeshComponentMesh);
        AddEdge(graph, "/Game/Characters/A/Character_A", "/Game/Characters/A/AnimBP_A", AssetReferenceType.AnimationClass, isSoft: true);
        AddEdge(graph, "/Game/Characters/A/AnimBP_A", animation.AssetPath, AssetReferenceType.AnimationBlueprintReference);

        var relationship = new MeshAnimationRelationshipResolver()
            .Resolve(mesh, new[] { animation }, graph)[0];

        Assert.Equal(MeshAnimationRelationshipLevel.StrongCandidate, relationship.RelationshipLevel);
        Assert.Contains(relationship.Evidence, evidence => evidence.Type == "EXPLICIT_REFERENCE_CHAIN");
    }

    [Fact]
    public void SameNameDifferentPackagesAreNotMerged()
    {
        var mesh = Mesh("/Game/Characters/A/Mesh_A");
        var referencedAnim = Animation("/Game/Animations/A/AN_Idle");
        var unrelatedSameName = Animation("/Game/Animations/B/AN_Idle");
        var graph = new AssetReferenceGraph();

        AddEdge(graph, "/Game/Characters/A/Character_A", mesh.AssetPath, AssetReferenceType.SkeletalMeshComponentMesh);
        AddEdge(graph, "/Game/Characters/A/Character_A", "/Game/Characters/A/AnimBP_A", AssetReferenceType.AnimationClass);
        AddEdge(graph, "/Game/Characters/A/AnimBP_A", referencedAnim.AssetPath, AssetReferenceType.AnimationBlueprintReference);

        var results = ToDictionary(new MeshAnimationRelationshipResolver()
            .Resolve(mesh, new[] { referencedAnim, unrelatedSameName }, graph));

        Assert.Equal(MeshAnimationRelationshipLevel.DirectReference, results[referencedAnim.AssetPath].RelationshipLevel);
        Assert.Equal(MeshAnimationRelationshipLevel.SkeletonOnly, results[unrelatedSameName.AssetPath].RelationshipLevel);
    }

    [Fact]
    public void SkeletonMismatchIsUnresolved()
    {
        var mesh = Mesh("/Game/Characters/A/Mesh_A");
        var animation = Animation("/Game/Animations/B/Anim_B", "/Game/Common/SK_Creature");

        var relationship = new MeshAnimationRelationshipResolver()
            .Resolve(mesh, new[] { animation }, new AssetReferenceGraph())[0];

        Assert.Equal(MeshAnimationRelationshipLevel.Unresolved, relationship.RelationshipLevel);
        Assert.Equal(MeshAnimationCompatibilityStatus.Incompatible, relationship.CompatibilityStatus);
    }

    private static SkeletalMeshProfile Mesh(string path, string skeleton = Skeleton) => new()
    {
        AssetPath = path,
        AssetName = AssetReferenceGraph.GetAssetName(path),
        SkeletonPath = skeleton,
        BoneCount = 120,
        LodCount = 1,
        ExtractionStatus = "complete"
    };

    private static AnimationMetadata Animation(string path, string skeleton = Skeleton) => new()
    {
        AssetPath = path,
        AssetName = AssetReferenceGraph.GetAssetName(path),
        SkeletonPath = skeleton,
        FrameCount = 30,
        ExtractionStatus = "complete"
    };

    private static void AddEdge(
        AssetReferenceGraph graph,
        string source,
        string target,
        AssetReferenceType type,
        bool isSoft = false)
    {
        graph.AddEdge(new AssetEdge
        {
            SourceAsset = source,
            TargetAsset = target,
            ReferenceType = type,
            EvidenceSource = "test",
            IsSoftReference = isSoft
        });
    }

    private static Dictionary<string, MeshAnimationRelationship> ToDictionary(IEnumerable<MeshAnimationRelationship> relationships) =>
        relationships.ToDictionary(x => x.AnimationAssetPath);
}
