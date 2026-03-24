using Klio.Core.Models;
using Klio.Infrastructure.Repositories;
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
}
