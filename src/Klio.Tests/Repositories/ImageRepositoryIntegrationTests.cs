using Klio.Core.Models;
using Klio.Infrastructure.Repositories;
using Klio.Infrastructure.Data;
using Klio.Tests.Infrastructure;

namespace Klio.Tests.Repositories;

[Trait("Category", "Integration")]
public class ImageRepositoryIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public ImageRepositoryIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private static Image MakeImage(string? hash = null) => new Image
    {
        OriginalFilename = "photo.jpg",
        StoredFilename = "photo.jpg",
        FilePath = "/tmp/photo.jpg",
        Hash = hash ?? Guid.NewGuid().ToString("N"),
        MimeType = "image/jpeg",
        ProcessedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task AddAsync_PersistsImage_CanBeRetrievedById()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new ImageRepository(ctx);

        var image = MakeImage();
        var added = await repo.AddAsync(image);

        Assert.True(added.Id > 0);

        var fetched = await repo.GetByIdAsync(added.Id);
        Assert.NotNull(fetched);
        Assert.Equal(added.Id, fetched.Id);
        Assert.Equal(image.Hash, fetched.Hash);
    }

    [Fact]
    public async Task GetByHashAsync_ExistingHash_ReturnsImage()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new ImageRepository(ctx);

        var hash = Guid.NewGuid().ToString("N");
        var image = MakeImage(hash);
        await repo.AddAsync(image);

        var found = await repo.GetByHashAsync(hash);
        Assert.NotNull(found);
        Assert.Equal(hash, found.Hash);
    }

    [Fact]
    public async Task ExistsByHashAsync_NonExistentHash_ReturnsFalse()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new ImageRepository(ctx);

        var exists = await repo.ExistsByHashAsync(Guid.NewGuid().ToString("N"));
        Assert.False(exists);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllImages()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new ImageRepository(ctx);

        var img1 = await repo.AddAsync(MakeImage());
        var img2 = await repo.AddAsync(MakeImage());

        var all = (await repo.GetAllAsync()).ToList();
        Assert.Contains(all, i => i.Id == img1.Id);
        Assert.Contains(all, i => i.Id == img2.Id);
    }

    [Fact]
    public async Task GetWithGpsAsync_OnlyReturnsImagesWithGpsData()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new ImageRepository(ctx);

        var withGps = MakeImage();
        withGps.GpsLatitude = 48.1351;
        withGps.GpsLongitude = 11.5820;
        var added = await repo.AddAsync(withGps);

        var noGps = MakeImage();
        await repo.AddAsync(noGps);

        var gpsImages = (await repo.GetWithGpsAsync()).ToList();
        Assert.Contains(gpsImages, i => i.Id == added.Id);
        Assert.DoesNotContain(gpsImages, i => i.Id == noGps.Id);
    }

    [Fact]
    public async Task DeleteAsync_RemovesImage()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new ImageRepository(ctx);

        var image = await repo.AddAsync(MakeImage());
        var id = image.Id;

        await repo.DeleteAsync(id);

        var found = await repo.GetByIdAsync(id);
        Assert.Null(found);
    }

    [Fact]
    public async Task GetByTopicIdAsync_ReturnsOnlyImagesInTopic()
    {
        await using var ctx = _fixture.CreateContext();

        // Create a topic first
        var topicRepo = new TopicRepository(ctx);
        var topic = await topicRepo.AddAsync(new Topic
        {
            Name = "TestTopic_" + Guid.NewGuid().ToString("N")[..6],
            Description = "for filter test",
            CreatedAt = DateTime.UtcNow
        });

        var repo = new ImageRepository(ctx);
        var inTopic  = MakeImage();
        inTopic.TopicId = topic.Id;
        var added    = await repo.AddAsync(inTopic);
        var outside  = await repo.AddAsync(MakeImage()); // no topic

        var result = (await repo.GetByTopicIdAsync(topic.Id)).ToList();

        Assert.Contains(result, i => i.Id == added.Id);
        Assert.DoesNotContain(result, i => i.Id == outside.Id);
    }

    [Fact]
    public async Task GetCountsByTopicAsync_ReturnsCorrectCounts()
    {
        await using var ctx = _fixture.CreateContext();

        var topicRepo = new TopicRepository(ctx);
        var topic = await topicRepo.AddAsync(new Topic
        {
            Name = "CountTopic_" + Guid.NewGuid().ToString("N")[..6],
            Description = "count test",
            CreatedAt = DateTime.UtcNow
        });

        var repo = new ImageRepository(ctx);
        var img1 = MakeImage(); img1.TopicId = topic.Id;
        var img2 = MakeImage(); img2.TopicId = topic.Id;
        await repo.AddAsync(img1);
        await repo.AddAsync(img2);

        var counts = await repo.GetCountsByTopicAsync();

        Assert.True(counts.ContainsKey(topic.Id));
        Assert.True(counts[topic.Id] >= 2);
    }

    [Fact]
    public async Task GetTotalCountAsync_ReturnsCorrectCount()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new ImageRepository(ctx);

        var before = await repo.GetTotalCountAsync();
        await repo.AddAsync(MakeImage());
        await repo.AddAsync(MakeImage());
        var after = await repo.GetTotalCountAsync();

        Assert.Equal(before + 2, after);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new ImageRepository(ctx);

        var image = await repo.AddAsync(MakeImage());
        image.OriginalFilename = "updated.jpg";
        await repo.UpdateAsync(image);

        await using var ctx2 = _fixture.CreateContext();
        var repo2 = new ImageRepository(ctx2);
        var fetched = await repo2.GetByIdAsync(image.Id);
        Assert.NotNull(fetched);
        Assert.Equal("updated.jpg", fetched.OriginalFilename);
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsCorrectPage()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new ImageRepository(ctx);

        // Add 3 images, then verify paging returns only 2 when take=2
        var img1 = await repo.AddAsync(MakeImage());
        var img2 = await repo.AddAsync(MakeImage());
        var img3 = await repo.AddAsync(MakeImage());

        var page1 = (await repo.GetPagedAsync(skip: 0, take: 2)).ToList();
        var page2 = (await repo.GetPagedAsync(skip: 2, take: 2)).ToList();

        Assert.True(page1.Count <= 2);
        // Page 1 and page 2 should have no overlap
        var ids1 = page1.Select(i => i.Id).ToHashSet();
        var ids2 = page2.Select(i => i.Id).ToHashSet();
        Assert.Empty(ids1.Intersect(ids2));
    }
}
