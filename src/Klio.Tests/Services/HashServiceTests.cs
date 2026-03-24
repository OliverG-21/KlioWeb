using System.Text;
using Klio.Infrastructure.Services;

namespace Klio.Tests.Services;

public class HashServiceTests : IDisposable
{
    private readonly HashService _sut = new();
    private readonly List<string> _tempFiles = [];

    private string CreateTempFile(byte[] content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, content);
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public async Task ComputeAsync_SameContent_ReturnsSameHash()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("hello world");
        var file1 = CreateTempFile(content);
        var file2 = CreateTempFile(content);

        // Act
        var hash1 = await _sut.ComputeAsync(file1);
        var hash2 = await _sut.ComputeAsync(file2);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public async Task ComputeAsync_DifferentContent_ReturnsDifferentHashes()
    {
        // Arrange
        var file1 = CreateTempFile(Encoding.UTF8.GetBytes("hello world"));
        var file2 = CreateTempFile(Encoding.UTF8.GetBytes("goodbye world"));

        // Act
        var hash1 = await _sut.ComputeAsync(file1);
        var hash2 = await _sut.ComputeAsync(file2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public async Task ComputeAsync_KnownBytes_ReturnsExpectedSha256Hash()
    {
        // Arrange
        // SHA-256 of empty byte array is known
        var file = CreateTempFile([]);
        // Expected: SHA-256("") = e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
        const string expected = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        // Act
        var hash = await _sut.ComputeAsync(file);

        // Assert
        Assert.Equal(expected, hash);
    }

    [Fact]
    public async Task ComputeAsync_ResultIsLowercase()
    {
        // Arrange
        var file = CreateTempFile(Encoding.UTF8.GetBytes("test data"));

        // Act
        var hash = await _sut.ComputeAsync(file);

        // Assert
        Assert.Equal(hash, hash.ToLowerInvariant());
    }

    [Fact]
    public async Task ComputeAsync_ResultIs64Characters()
    {
        // Arrange – SHA-256 produces a 256-bit (32-byte) value → 64 hex chars
        var file = CreateTempFile(Encoding.UTF8.GetBytes("some content"));

        // Act
        var hash = await _sut.ComputeAsync(file);

        // Assert
        Assert.Equal(64, hash.Length);
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }
}
