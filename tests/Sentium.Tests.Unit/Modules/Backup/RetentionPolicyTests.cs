using Sentium.Backup.Archives;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Backup;

public sealed class RetentionPolicyTests
{
    private static readonly DateTimeOffset Newest = new(2026, 11, 15, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NoArchives_DeletesNothing()
    {
        Assert.Empty(RetentionPolicy.SelectForDeletion([], keepDaily: 7, keepWeekly: 4));
    }

    [Fact]
    public void NightlyBackupsForTwoMonths_KeepSevenDailyAndFourWeekly()
    {
        var archives = Enumerable.Range(0, 60).Select(day => Archive(Newest.AddDays(-day))).ToList();

        var deleted = RetentionPolicy.SelectForDeletion(archives, keepDaily: 7, keepWeekly: 4);
        var kept = archives.Except(deleted).OrderByDescending(a => a.CreatedAt).ToList();

        // 2026-11-15 is a Sunday: the newest backup of each of the last four ISO weeks is a Sunday, and the most
        // recent of those weeks is already covered by the seven daily ones.
        Assert.Equal(new int[] { 15, 14, 13, 12, 11, 10, 9, 8, 1, 25 }, kept.Select(a => a.CreatedAt.Day));
    }

    [Fact]
    public void SeveralBackupsOnOneDay_KeepOnlyTheNewestOfThatDay()
    {
        var morning = Archive(Newest);
        var noon = Archive(Newest.AddHours(9));
        var evening = Archive(Newest.AddHours(17));

        var deleted = RetentionPolicy.SelectForDeletion([morning, noon, evening], keepDaily: 7, keepWeekly: 4);

        Assert.Equivalent(new[] { morning, noon }, deleted);
    }

    [Fact]
    public void NewestArchive_SurvivesEvenWithNothingToKeep()
    {
        var newest = Archive(Newest);
        var older = Archive(Newest.AddDays(-1));

        var deleted = RetentionPolicy.SelectForDeletion([older, newest], keepDaily: 0, keepWeekly: 0);

        Assert.Equal(new BackupArchive[] { older }, deleted);
    }

    [Fact]
    public void WeeklyDisabled_KeepsOnlyTheDailyWindow()
    {
        var archives = Enumerable.Range(0, 10).Select(day => Archive(Newest.AddDays(-day))).ToList();

        var deleted = RetentionPolicy.SelectForDeletion(archives, keepDaily: 3, keepWeekly: 0);

        Assert.Equal(7, deleted.Count);
        Assert.All(deleted, a => Assert.True(a.CreatedAt < Newest.AddDays(-2)));
    }

    private static BackupArchive Archive(DateTimeOffset createdAt)
        => new(BackupArchiveName.Format(createdAt, encrypted: false), createdAt, 1, Encrypted: false);
}
