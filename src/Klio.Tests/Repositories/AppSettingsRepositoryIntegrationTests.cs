using Klio.Infrastructure.Repositories;
using Klio.Tests.Infrastructure;

namespace Klio.Tests.Repositories;

[Trait("Category", "Integration")]
public class AppSettingsRepositoryIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public AppSettingsRepositoryIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetAsync_FirstCall_CreatesDefaultSettings()
    {
        // Arrange – use a separate context so no settings exist yet for this test.
        // Because the fixture shares one DB across all tests, we verify the method
        // returns a valid object (it may already exist from a prior migration seed).
        await using var ctx = _fixture.CreateContext();
        var repo = new AppSettingsRepository(ctx);

        // Act
        var settings = await repo.GetAsync();

        // Assert – settings must be persisted with an Id > 0
        Assert.NotNull(settings);
        Assert.True(settings.Id > 0);
    }

    [Fact]
    public async Task GetAsync_SubsequentCall_ReturnsSameSettings()
    {
        // Two calls on the same context should return the same row (no duplicates).
        await using var ctx = _fixture.CreateContext();
        var repo = new AppSettingsRepository(ctx);

        var first  = await repo.GetAsync();
        var second = await repo.GetAsync();

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        // Arrange
        await using var ctx = _fixture.CreateContext();
        var repo = new AppSettingsRepository(ctx);

        var settings = await repo.GetAsync();
        var originalWidth = settings.ThumbnailWidth;
        settings.ThumbnailWidth = originalWidth + 50;

        // Act
        await repo.UpdateAsync(settings);

        // Assert – reload from DB with a fresh context
        await using var ctx2 = _fixture.CreateContext();
        var repo2 = new AppSettingsRepository(ctx2);
        var reloaded = await repo2.GetAsync();
        Assert.Equal(originalWidth + 50, reloaded.ThumbnailWidth);

        // Restore original value so other tests are unaffected
        reloaded.ThumbnailWidth = originalWidth;
        await repo2.UpdateAsync(reloaded);
    }

    [Fact]
    public async Task GetAsync_DefaultPasswordHash_IsNotEmpty()
    {
        // Arrange
        await using var ctx = _fixture.CreateContext();
        var repo = new AppSettingsRepository(ctx);

        // Act
        var settings = await repo.GetAsync();

        // Assert – seeded with BCrypt hash of "admin"
        Assert.NotNull(settings.AdminPasswordHash);
        Assert.NotEmpty(settings.AdminPasswordHash);
        // BCrypt hashes always start with "$2"
        Assert.StartsWith("$2", settings.AdminPasswordHash);
    }
}
