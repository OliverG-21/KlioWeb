using Klio.Infrastructure.Services;

namespace Klio.Tests.Services;

[Trait("Category", "Unit")]
public class NullTaggingServiceTests
{
    private readonly NullTaggingService _sut = new();

    [Fact]
    public async Task TagImageAsync_AnyInput_ReturnsEmptyList()
    {
        // Arrange
        const string anyPath = "/some/image.jpg";

        // Act
        var result = await _sut.TagImageAsync(anyPath);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task IsAvailableAsync_Always_ReturnsFalse()
    {
        // Arrange / Act
        var result = await _sut.IsAvailableAsync();

        // Assert
        Assert.False(result);
    }
}
