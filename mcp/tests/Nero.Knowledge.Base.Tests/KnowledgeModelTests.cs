using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Tests;

public class KnowledgeModelTests
{
    [Fact]
    public void KnowledgeNode_WithRequiredFields_IsValid()
    {
        var node = new KnowledgeNode
        {
            Id = "global/patterns",
            Title = "Padroes globais Acme",
            Path = "knowledge/global/patterns.md",
            Scope = KnowledgeScope.Global,
            Type = KnowledgeNodeType.Pattern,
            Content = "Conteudo"
        };

        var result = node.Validate();

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(KnowledgeScope.Domain)]
    [InlineData(KnowledgeScope.Project)]
    public void KnowledgeNode_WithScopedContext_RequiresScopeSpecificFields(KnowledgeScope scope)
    {
        var node = new KnowledgeNode
        {
            Id = "scoped/node",
            Title = "Nota",
            Path = "knowledge/domains/api/index.md",
            Scope = scope,
            Type = KnowledgeNodeType.Index
        };

        var result = node.Validate();

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void KnowledgeNode_WithMissingRequiredFields_ReturnsErrors()
    {
        var node = new KnowledgeNode
        {
            Id = "",
            Title = " ",
            Path = "",
            Scope = KnowledgeScope.Global,
            Type = KnowledgeNodeType.Index
        };

        var result = node.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("Id is required.", result.Errors);
        Assert.Contains("Title is required.", result.Errors);
        Assert.Contains("Path is required.", result.Errors);
    }

    [Fact]
    public void KnowledgeNode_WithUnsupportedType_ReturnsError()
    {
        var node = new KnowledgeNode
        {
            Id = "global/index",
            Title = "Indice",
            Path = "knowledge/global/index.md",
            Scope = KnowledgeScope.Global,
            Type = (KnowledgeNodeType)999
        };

        var result = node.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("Type must be a supported value.", result.Errors);
    }

    [Fact]
    public void KnowledgeEdge_WithRequiredFields_IsValid()
    {
        var edge = new KnowledgeEdge
        {
            Id = "edge-1",
            SourceNodeId = "knowledge/projects/Acme.Mobile/context.md",
            TargetNodeId = "knowledge/domains/mobile/index.md",
            Relation = KnowledgeRelationType.BelongsToDomain,
            Confidence = 0.9m
        };

        var result = edge.Validate();

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void KnowledgeEdge_WithInvalidFields_ReturnsErrors()
    {
        var edge = new KnowledgeEdge
        {
            Id = "",
            SourceNodeId = "",
            TargetNodeId = "",
            Relation = (KnowledgeRelationType)999,
            Confidence = 1.5m
        };

        var result = edge.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("Id is required.", result.Errors);
        Assert.Contains("SourceNodeId is required.", result.Errors);
        Assert.Contains("TargetNodeId is required.", result.Errors);
        Assert.Contains("Relation must be a supported value.", result.Errors);
        Assert.Contains("Confidence must be between 0 and 1.", result.Errors);
    }
}
