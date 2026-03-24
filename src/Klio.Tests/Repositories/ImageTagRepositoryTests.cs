namespace Klio.Tests.Repositories;

/// <summary>
/// Unit tests for ImageTagRepository.
///
/// NOTE: ImageTagRepository depends directly on AppDbContext (EF Core + PostgreSQL).
/// Meaningful unit tests would require either:
///   a) A real PostgreSQL instance (integration tests), or
///   b) EF Core InMemory provider — but that provider does not support all
///      query patterns used here (e.g. GroupBy with ToDictionaryAsync, raw SQL).
///
/// Integration tests should be added in a separate test class (e.g.
/// ImageTagRepositoryIntegrationTests) that spins up the database via
/// Testcontainers (https://testcontainers.com/guides/getting-started-with-testcontainers-for-dotnet/).
///
/// The smoke tests below document the intended coverage and verify that the
/// types compile and are accessible from this assembly.
/// </summary>
public class ImageTagRepositoryTests
{
    [Fact]
    public void ImageTagRepository_TypeIsPublic()
    {
        // Smoke test: confirms the type is accessible from the test project.
        var type = typeof(Klio.Infrastructure.Repositories.ImageTagRepository);
        Assert.NotNull(type);
        Assert.True(type.IsPublic);
    }

    [Fact]
    public void ImageTag_CanBeConstructed()
    {
        // Smoke test: confirms the domain model is usable.
        var tag = new Klio.Core.Models.ImageTag
        {
            ImageId = 1,
            Tag     = "hund",
            Source  = Klio.Core.Models.TagSource.Auto
        };

        Assert.Equal(1, tag.ImageId);
        Assert.Equal("hund", tag.Tag);
        Assert.Equal(Klio.Core.Models.TagSource.Auto, tag.Source);
    }

    [Fact]
    public void IImageTagRepository_InterfaceExistsWithExpectedMembers()
    {
        // Smoke test: confirms the interface contract is intact.
        var iface = typeof(Klio.Core.Interfaces.IImageTagRepository);
        Assert.NotNull(iface);
        Assert.NotNull(iface.GetMethod("AddRangeAsync"));
        Assert.NotNull(iface.GetMethod("GetByImageIdAsync"));
        Assert.NotNull(iface.GetMethod("GetAllDistinctTagsAsync"));
        Assert.NotNull(iface.GetMethod("DeleteByIdAsync"));
        Assert.NotNull(iface.GetMethod("MergeTagsAsync"));
        Assert.NotNull(iface.GetMethod("DeleteAllAutoTagsAsync"));
    }

    // TODO: Integration tests (require PostgreSQL / Testcontainers):
    //   - AddRangeAsync_PersistsTags
    //   - GetByImageIdAsync_ReturnsTagsForCorrectImage
    //   - GetTagsWithCountsAsync_ReturnsCorrectCounts
    //   - GetUntaggedCountAsync_ExcludesTaggedImages
    //   - MergeTagsAsync_RenamesSourceToTarget_AndDropsDuplicates
    //   - DeleteAllAutoTagsAsync_LeavesManualTagsIntact
}
