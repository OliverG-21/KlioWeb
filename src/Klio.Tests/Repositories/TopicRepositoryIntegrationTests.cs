using Klio.Core.Models;
using Klio.Infrastructure.Repositories;
using Klio.Tests.Infrastructure;

namespace Klio.Tests.Repositories;

[Trait("Category", "Integration")]
public class TopicRepositoryIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public TopicRepositoryIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private static Topic MakeTopic(string name = "My Album") => new Topic
    {
        Name = name,
        Description = "Test description",
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task AddAsync_PersistsTopic()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new TopicRepository(ctx);

        var topic = MakeTopic("Persist Test " + Guid.NewGuid());
        var added = await repo.AddAsync(topic);

        Assert.True(added.Id > 0);

        var fetched = await repo.GetByIdAsync(added.Id);
        Assert.NotNull(fetched);
        Assert.Equal(added.Name, fetched.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTopic()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new TopicRepository(ctx);

        var topic = await repo.AddAsync(MakeTopic("GetById Test " + Guid.NewGuid()));

        var found = await repo.GetByIdAsync(topic.Id);
        Assert.NotNull(found);
        Assert.Equal(topic.Id, found.Id);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesName()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new TopicRepository(ctx);

        var topic = await repo.AddAsync(MakeTopic("Original " + Guid.NewGuid()));
        var newName = "Updated " + Guid.NewGuid();
        topic.Name = newName;
        await repo.UpdateAsync(topic);

        var updated = await repo.GetByIdAsync(topic.Id);
        Assert.NotNull(updated);
        Assert.Equal(newName, updated.Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTopic()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new TopicRepository(ctx);

        var topic = await repo.AddAsync(MakeTopic("Delete Test " + Guid.NewGuid()));
        var id = topic.Id;

        await repo.DeleteAsync(id);

        var found = await repo.GetByIdAsync(id);
        Assert.Null(found);
    }

    [Fact]
    public async Task GetByFolderIdAsync_ReturnsTopicsInFolder()
    {
        await using var ctx = _fixture.CreateContext();
        var folderRepo = new AlbumFolderRepository(ctx);
        var topicRepo = new TopicRepository(ctx);

        var folder = new AlbumFolder { Name = "Folder " + Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        await folderRepo.AddAsync(folder);

        var topicInFolder = MakeTopic("In Folder " + Guid.NewGuid());
        topicInFolder.FolderId = folder.Id;
        var topicNoFolder = MakeTopic("No Folder " + Guid.NewGuid());

        await topicRepo.AddAsync(topicInFolder);
        await topicRepo.AddAsync(topicNoFolder);

        var results = (await topicRepo.GetByFolderIdAsync(folder.Id)).ToList();
        Assert.Contains(results, t => t.Id == topicInFolder.Id);
        Assert.DoesNotContain(results, t => t.Id == topicNoFolder.Id);
    }
}
