using Klio.Core.Models;
using Klio.Infrastructure.Repositories;
using Klio.Tests.Infrastructure;

namespace Klio.Tests.Repositories;

[Trait("Category", "Integration")]
public class DuplicateLogRepositoryIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public DuplicateLogRepositoryIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private static DuplicateLog MakeLog(string filename = "dup.jpg") => new DuplicateLog
    {
        IncomingFilename = filename,
        DuplicatePath = "/tmp/duplicates/dup.jpg",
        ExistingImageId = 0,
        ExistingImagePath = "/tmp/images/original.jpg",
    };

    [Fact]
    public async Task AddAsync_PersistsLog()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new DuplicateLogRepository(ctx);

        var log = MakeLog("add_test_" + Guid.NewGuid() + ".jpg");
        var added = await repo.AddAsync(log);

        Assert.True(added.Id > 0);
        Assert.NotEqual(default, added.DetectedAt);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllLogs()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new DuplicateLogRepository(ctx);

        var log1 = await repo.AddAsync(MakeLog("getall1_" + Guid.NewGuid() + ".jpg"));
        var log2 = await repo.AddAsync(MakeLog("getall2_" + Guid.NewGuid() + ".jpg"));

        var all = (await repo.GetAllAsync()).ToList();
        Assert.Contains(all, l => l.Id == log1.Id);
        Assert.Contains(all, l => l.Id == log2.Id);
    }

    [Fact]
    public async Task ClearAllAsync_RemovesAllLogs()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new DuplicateLogRepository(ctx);

        await repo.AddAsync(MakeLog("clear1_" + Guid.NewGuid() + ".jpg"));
        await repo.AddAsync(MakeLog("clear2_" + Guid.NewGuid() + ".jpg"));

        await repo.ClearAllAsync();

        var remaining = (await repo.GetAllAsync()).ToList();
        Assert.Empty(remaining);
    }
}
