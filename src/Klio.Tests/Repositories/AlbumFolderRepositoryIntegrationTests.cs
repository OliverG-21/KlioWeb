using Klio.Core.Models;
using Klio.Infrastructure.Repositories;
using Klio.Tests.Infrastructure;

namespace Klio.Tests.Repositories;

[Trait("Category", "Integration")]
public class AlbumFolderRepositoryIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public AlbumFolderRepositoryIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private static AlbumFolder MakeFolder(string? name = null, int? parentId = null) => new AlbumFolder
    {
        Name = name ?? "Folder " + Guid.NewGuid(),
        ParentFolderId = parentId,
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task AddAsync_PersistsFolder()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new AlbumFolderRepository(ctx);

        var folder = MakeFolder();
        await repo.AddAsync(folder);

        Assert.True(folder.Id > 0);

        var fetched = await repo.GetByIdAsync(folder.Id);
        Assert.NotNull(fetched);
        Assert.Equal(folder.Name, fetched.Name);
    }

    [Fact]
    public async Task GetByParentIdAsync_ReturnsChildFolders()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new AlbumFolderRepository(ctx);

        var parent = MakeFolder("Parent " + Guid.NewGuid());
        await repo.AddAsync(parent);

        var child1 = MakeFolder("Child1 " + Guid.NewGuid(), parent.Id);
        var child2 = MakeFolder("Child2 " + Guid.NewGuid(), parent.Id);
        var unrelated = MakeFolder("Unrelated " + Guid.NewGuid());
        await repo.AddAsync(child1);
        await repo.AddAsync(child2);
        await repo.AddAsync(unrelated);

        var children = (await repo.GetByParentIdAsync(parent.Id)).ToList();
        Assert.Contains(children, f => f.Id == child1.Id);
        Assert.Contains(children, f => f.Id == child2.Id);
        Assert.DoesNotContain(children, f => f.Id == unrelated.Id);
    }

    [Fact]
    public async Task HasSubFoldersAsync_ReturnsTrueWhenSubFoldersExist()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new AlbumFolderRepository(ctx);

        var parent = MakeFolder("HasSubParent " + Guid.NewGuid());
        await repo.AddAsync(parent);
        var child = MakeFolder("HasSubChild " + Guid.NewGuid(), parent.Id);
        await repo.AddAsync(child);

        var hasSubFolders = await repo.HasSubFoldersAsync(parent.Id);
        Assert.True(hasSubFolders);
    }

    [Fact]
    public async Task GetAncestorsAsync_ReturnsCorrectHierarchy()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new AlbumFolderRepository(ctx);

        // Create 3-level hierarchy: root -> mid -> leaf
        var root = MakeFolder("Root " + Guid.NewGuid());
        await repo.AddAsync(root);

        var mid = MakeFolder("Mid " + Guid.NewGuid(), root.Id);
        await repo.AddAsync(mid);

        var leaf = MakeFolder("Leaf " + Guid.NewGuid(), mid.Id);
        await repo.AddAsync(leaf);

        var ancestors = await repo.GetAncestorsAsync(leaf.Id);

        // Ancestors of leaf should be [root, mid] in order root → mid
        Assert.Equal(2, ancestors.Count);
        Assert.Equal(root.Id, ancestors[0].Id);
        Assert.Equal(mid.Id, ancestors[1].Id);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFolder()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new AlbumFolderRepository(ctx);

        var folder = MakeFolder("Delete Me " + Guid.NewGuid());
        await repo.AddAsync(folder);
        var id = folder.Id;

        await repo.DeleteAsync(id);

        var found = await repo.GetByIdAsync(id);
        Assert.Null(found);
    }
}
