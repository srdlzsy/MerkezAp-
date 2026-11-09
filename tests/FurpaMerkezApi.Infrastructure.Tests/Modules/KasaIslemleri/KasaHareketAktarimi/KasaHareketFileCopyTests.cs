using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaHareketAktarimi;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.KasaIslemleri.KasaHareketAktarimi;

public sealed class KasaHareketFileCopyTests : IDisposable
{
    private readonly string testDirectory = Path.Combine(
        Path.GetTempPath(),
        $"furpa-kasa-copy-{Guid.NewGuid():N}");

    [Fact]
    public async Task CopyFileAtomicallyAsync_PublishesCompleteFileAfterCopy()
    {
        Directory.CreateDirectory(testDirectory);
        var sourcePath = Path.Combine(testDirectory, "source.dat");
        var destinationPath = Path.Combine(testDirectory, "target", "movement.dat");
        var content = Enumerable.Range(0, 10_000)
            .Select(index => (byte)(index % 251))
            .ToArray();
        await File.WriteAllBytesAsync(sourcePath, content);

        var copiedBytes = await KasaHareketAktarimiService.CopyFileAtomicallyAsync(
            sourcePath,
            destinationPath,
            overwrite: false,
            CancellationToken.None);

        Assert.Equal(content.LongLength, copiedBytes);
        Assert.Equal(content, await File.ReadAllBytesAsync(destinationPath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(destinationPath)!, "*.tmp"));
    }

    [Fact]
    public async Task CopyFileAtomicallyAsync_WhenCancelled_KeepsExistingDestinationAndRemovesTemporaryFile()
    {
        Directory.CreateDirectory(testDirectory);
        var sourcePath = Path.Combine(testDirectory, "source.dat");
        var destinationDirectory = Path.Combine(testDirectory, "target");
        var destinationPath = Path.Combine(destinationDirectory, "movement.dat");
        Directory.CreateDirectory(destinationDirectory);
        await File.WriteAllBytesAsync(sourcePath, new byte[128 * 1024]);
        await File.WriteAllTextAsync(destinationPath, "existing-complete-file");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            KasaHareketAktarimiService.CopyFileAtomicallyAsync(
                sourcePath,
                destinationPath,
                overwrite: true,
                cancellation.Token));

        Assert.Equal("existing-complete-file", await File.ReadAllTextAsync(destinationPath));
        Assert.Empty(Directory.GetFiles(destinationDirectory, "*.tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }
}
