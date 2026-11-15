using FluentAssertions;
using Sentium.Backup.Archives;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Backup;

public sealed class BackupArchiveNameTests
{
    [Theory]
    [InlineData(false, "sentium-20261115T030000Z.tar")]
    [InlineData(true, "sentium-20261115T030000Z.tar.age")]
    public void Format_UsesTheUtcStartTime(bool encrypted, string expected)
    {
        var startedLocal = new DateTimeOffset(2026, 11, 15, 5, 0, 0, TimeSpan.FromHours(2));

        BackupArchiveName.Format(startedLocal, encrypted).Should().Be(expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryParse_ReadsBackWhatFormatWrote(bool encrypted)
    {
        var createdAt = new DateTimeOffset(2026, 11, 15, 3, 4, 5, TimeSpan.Zero);

        BackupArchiveName.TryParse(BackupArchiveName.Format(createdAt, encrypted), out var archive).Should().BeTrue();

        archive!.CreatedAt.Should().Be(createdAt);
        archive.Encrypted.Should().Be(encrypted);
    }

    [Theory]
    [InlineData(".sentium-20261115T030000Z.tar.partial")]
    [InlineData("sentium-20261115T030000Z.tar.partial")]
    [InlineData("status.json")]
    [InlineData("sentium-notatime.tar")]
    [InlineData("other-20261115T030000Z.tar")]
    public void TryParse_RejectsAnythingThatIsNotAFinishedArchive(string fileName)
    {
        BackupArchiveName.TryParse(fileName, out _).Should().BeFalse();
    }

    [Fact]
    public void Partial_IsHiddenAndNeverParsesAsAnArchive()
    {
        var partial = BackupArchiveName.Partial("sentium-20261115T030000Z.tar");

        partial.Should().StartWith(".");
        BackupArchiveName.TryParse(partial, out _).Should().BeFalse();
    }
}
