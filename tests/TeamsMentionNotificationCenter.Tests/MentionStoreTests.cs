using System.IO;
using TeamsMentionNotificationCenter.Core;

namespace TeamsMentionNotificationCenter.Tests;

public class MentionStoreTests
{
    private static MentionStore NewStore() =>
        new(Path.Combine(Path.GetTempPath(), "tmnc-tests", Guid.NewGuid() + ".json"));

    [Fact]
    public void AddIfNew_Creates_Entry_And_Respects_Repeat_Window()
    {
        var s = NewStore();
        var t = DateTime.Now;
        Assert.NotNull(s.AddIfNew("Anna Schmidt", t, repeatMinutes: 3));
        Assert.Null(s.AddIfNew("Anna Schmidt", t.AddMinutes(2), repeatMinutes: 3));   // zu früh
        Assert.NotNull(s.AddIfNew("Anna Schmidt", t.AddMinutes(4), repeatMinutes: 3)); // Abstand erreicht
        Assert.NotNull(s.AddIfNew("Ben Weber", t.AddSeconds(10), repeatMinutes: 3));   // andere Person sofort
        Assert.Equal(3, s.Items.Count);
    }

    [Fact]
    public void Repeat_Window_Matches_Name_Variants()
    {
        var s = NewStore();
        var t = DateTime.Now;
        s.AddIfNew("Robert H. Kieschke", t, 3);
        Assert.Null(s.AddIfNew("Robert Kieschke", t.AddMinutes(1), 3)); // gleiche Person trotz Mittelinitiale
    }

    [Fact]
    public void TickSnoozes_Reopens_Due_Entries()
    {
        var s = NewStore();
        var e = s.AddIfNew("Anna", DateTime.Now, 0)!;
        s.Snooze(e.Id, 1);
        Assert.Equal(MentionStatus.Snoozed, s.Items[0].Status);
        Assert.Empty(s.TickSnoozes(DateTime.Now));                       // noch nicht fällig
        var due = s.TickSnoozes(DateTime.Now.AddMinutes(2));
        Assert.Single(due);
        Assert.Equal(MentionStatus.Open, s.Items[0].Status);
    }

    [Fact]
    public void PersonSeen_Reopens_Only_Waiting_Entries_Of_That_Person()
    {
        var s = NewStore();
        var a = s.AddIfNew("Anna Schmidt", DateTime.Now, 0)!;
        var b = s.AddIfNew("Ben Weber", DateTime.Now, 0)!;
        s.WaitForPerson(a.Id);
        s.WaitForPerson(b.Id);
        var reopened = s.PersonSeen("Anna Schmidt");
        Assert.Single(reopened);
        Assert.Equal(MentionStatus.Open, s.Items.First(m => m.Id == a.Id).Status);
        Assert.Equal(MentionStatus.WaitingForPerson, s.Items.First(m => m.Id == b.Id).Status);
    }

    [Fact]
    public void Done_And_Reopen_And_Delete_Work()
    {
        var s = NewStore();
        var e = s.AddIfNew("Anna", DateTime.Now, 0)!;
        s.MarkDone(e.Id);
        Assert.Equal(MentionStatus.Done, s.Items[0].Status);
        Assert.Equal(0, s.UnfinishedCount);
        s.Reopen(e.Id);
        Assert.Equal(MentionStatus.Open, s.Items[0].Status);
        s.Delete(e.Id);
        Assert.Empty(s.Items);
    }

    [Fact]
    public void Cleanup_Removes_Entries_Older_Than_Retention()
    {
        var s = NewStore();
        s.AddIfNew("Alt", DateTime.Now.AddDays(-40), 0);
        s.AddIfNew("Neu", DateTime.Now, 0);
        s.Cleanup(retentionDays: 30);
        Assert.Single(s.Items);
        Assert.Equal("Neu", s.Items[0].Speaker);
    }

    [Fact]
    public void Store_Persists_Across_Instances()
    {
        var path = Path.Combine(Path.GetTempPath(), "tmnc-tests", Guid.NewGuid() + ".json");
        var s1 = new MentionStore(path);
        s1.AddIfNew("Anna", DateTime.Now, 0);
        var s2 = new MentionStore(path);
        s2.Load(retentionDays: 30);
        Assert.Single(s2.Items);
        Assert.Equal("Anna", s2.Items[0].Speaker);
    }
}
