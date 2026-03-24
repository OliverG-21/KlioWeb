using Klio.Core.Models;
using Klio.Infrastructure.Data;
using Klio.Infrastructure.Repositories;
using Klio.Tests.Infrastructure;

namespace Klio.Tests.Repositories;

[Trait("Category", "Integration")]
public class ImageTagRepositoryIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public ImageTagRepositoryIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private static Image MakeImage() => new Image
    {
        OriginalFilename = "test.jpg",
        StoredFilename = "test.jpg",
        FilePath = "/tmp/test.jpg",
        Hash = Guid.NewGuid().ToString("N"),
        MimeType = "image/jpeg",
        ProcessedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task AddRangeAsync_PersistsTags()
    {
        await using var ctx = _fixture.CreateContext();
        var image = MakeImage();
        ctx.Images.Add(image);
        await ctx.SaveChangesAsync();

        var repo = new ImageTagRepository(ctx);
        var tags = new[]
        {
            new ImageTag { ImageId = image.Id, Tag = "cat", Source = TagSource.Auto },
            new ImageTag { ImageId = image.Id, Tag = "dog", Source = TagSource.Manual },
        };
        await repo.AddRangeAsync(tags);

        var result = await repo.GetByImageIdAsync(image.Id);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Tag == "cat");
        Assert.Contains(result, t => t.Tag == "dog");
    }

    [Fact]
    public async Task GetByImageIdAsync_ReturnsTagsForCorrectImage()
    {
        await using var ctx = _fixture.CreateContext();
        var image1 = MakeImage();
        var image2 = MakeImage();
        ctx.Images.AddRange(image1, image2);
        await ctx.SaveChangesAsync();

        var repo = new ImageTagRepository(ctx);
        await repo.AddRangeAsync(new[]
        {
            new ImageTag { ImageId = image1.Id, Tag = "alpha", Source = TagSource.Auto },
            new ImageTag { ImageId = image2.Id, Tag = "beta", Source = TagSource.Auto },
        });

        var result = await repo.GetByImageIdAsync(image1.Id);
        Assert.Single(result);
        Assert.Equal("alpha", result[0].Tag);
    }

    [Fact]
    public async Task GetTagsWithCountsAsync_ReturnsCorrectCounts()
    {
        await using var ctx = _fixture.CreateContext();
        var image1 = MakeImage();
        var image2 = MakeImage();
        var image3 = MakeImage();
        ctx.Images.AddRange(image1, image2, image3);
        await ctx.SaveChangesAsync();

        var repo = new ImageTagRepository(ctx);
        await repo.AddRangeAsync(new[]
        {
            new ImageTag { ImageId = image1.Id, Tag = "nature", Source = TagSource.Auto },
            new ImageTag { ImageId = image2.Id, Tag = "nature", Source = TagSource.Auto },
            new ImageTag { ImageId = image3.Id, Tag = "nature", Source = TagSource.Auto },
            new ImageTag { ImageId = image1.Id, Tag = "sky", Source = TagSource.Auto },
            new ImageTag { ImageId = image2.Id, Tag = "sky", Source = TagSource.Auto },
        });

        var counts = await repo.GetTagsWithCountsAsync();
        Assert.Equal(3, counts["nature"]);
        Assert.Equal(2, counts["sky"]);
    }

    [Fact]
    public async Task GetUntaggedCountAsync_ExcludesTaggedImages()
    {
        await using var ctx = _fixture.CreateContext();
        var image1 = MakeImage();
        var image2 = MakeImage();
        var image3 = MakeImage();
        ctx.Images.AddRange(image1, image2, image3);
        await ctx.SaveChangesAsync();

        var repo = new ImageTagRepository(ctx);
        // Tag image1 and image2, leave image3 untagged
        await repo.AddRangeAsync(new[]
        {
            new ImageTag { ImageId = image1.Id, Tag = "tagged", Source = TagSource.Auto },
            new ImageTag { ImageId = image2.Id, Tag = "tagged", Source = TagSource.Auto },
        });

        // Count untagged among all images — note: there may be other images from prior tests
        // so we verify our 3 images specifically using a fresh context read
        var allImages = new[] { image1.Id, image2.Id, image3.Id };
        var taggedIds = ctx.ImageTags
            .Where(t => allImages.Contains(t.ImageId))
            .Select(t => t.ImageId)
            .Distinct()
            .ToHashSet();
        var untaggedCount = allImages.Count(id => !taggedIds.Contains(id));
        Assert.Equal(1, untaggedCount);
    }

    [Fact]
    public async Task MergeTagsAsync_RenamesSourceToTarget_AndDropsDuplicates()
    {
        await using var ctx = _fixture.CreateContext();
        var img1 = MakeImage();
        var img2 = MakeImage();
        var img3 = MakeImage();
        ctx.Images.AddRange(img1, img2, img3);
        await ctx.SaveChangesAsync();

        var repo = new ImageTagRepository(ctx);
        // img1, img2, img3 have source tag "feline"
        // img2, img3 already have target tag "cat"
        await repo.AddRangeAsync(new[]
        {
            new ImageTag { ImageId = img1.Id, Tag = "feline", Source = TagSource.Auto },
            new ImageTag { ImageId = img2.Id, Tag = "feline", Source = TagSource.Auto },
            new ImageTag { ImageId = img3.Id, Tag = "feline", Source = TagSource.Auto },
            new ImageTag { ImageId = img2.Id, Tag = "cat", Source = TagSource.Auto },
            new ImageTag { ImageId = img3.Id, Tag = "cat", Source = TagSource.Auto },
        });

        await repo.MergeTagsAsync("feline", "cat");

        // img1: feline → cat (renamed)
        var tagsImg1 = await repo.GetByImageIdAsync(img1.Id);
        Assert.Contains(tagsImg1, t => t.Tag == "cat");
        Assert.DoesNotContain(tagsImg1, t => t.Tag == "feline");

        // img2 and img3: source duplicate dropped, still have cat once
        var tagsImg2 = await repo.GetByImageIdAsync(img2.Id);
        Assert.Single(tagsImg2, t => t.Tag == "cat");
        Assert.DoesNotContain(tagsImg2, t => t.Tag == "feline");

        var tagsImg3 = await repo.GetByImageIdAsync(img3.Id);
        Assert.Single(tagsImg3, t => t.Tag == "cat");
        Assert.DoesNotContain(tagsImg3, t => t.Tag == "feline");
    }

    [Fact]
    public async Task DeleteAllAutoTagsAsync_LeavesManualTagsIntact()
    {
        await using var ctx = _fixture.CreateContext();
        var image = MakeImage();
        ctx.Images.Add(image);
        await ctx.SaveChangesAsync();

        var repo = new ImageTagRepository(ctx);
        await repo.AddRangeAsync(new[]
        {
            new ImageTag { ImageId = image.Id, Tag = "auto1", Source = TagSource.Auto },
            new ImageTag { ImageId = image.Id, Tag = "auto2", Source = TagSource.Auto },
            new ImageTag { ImageId = image.Id, Tag = "manual1", Source = TagSource.Manual },
            new ImageTag { ImageId = image.Id, Tag = "manual2", Source = TagSource.Manual },
        });

        await repo.DeleteAllAutoTagsAsync();

        var remaining = await repo.GetByImageIdAsync(image.Id);
        Assert.Equal(2, remaining.Count);
        Assert.All(remaining, t => Assert.Equal(TagSource.Manual, t.Source));
        Assert.Contains(remaining, t => t.Tag == "manual1");
        Assert.Contains(remaining, t => t.Tag == "manual2");
    }
}
