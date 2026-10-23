using System.Text;
using KodiakCleaner.Core.Tools;
using Xunit;

namespace KodiakCleaner.Core.Tests;

public class ScheduledTaskBackupsTests
{
    private const string Xml = "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\n<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">" +
                               "<RegistrationInfo><URI>\\Vendor\\Updater &amp; Co</URI></RegistrationInfo></Task>";

    [Fact]
    public void Saved_tasks_list_A_to_Z_newest_first_and_keep_their_path()
    {
        var folder = Path.Combine(Path.GetTempPath(), "kc-tasks-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ScheduledTaskBackups(folder);
            var first = store.Save(@"\Zeta Task", Xml, "Zeta", ScheduledTaskBackups.Manual, new DateTime(2026, 10, 1, 9, 0, 0));
            store.Save(@"\Vendor\alpha", Xml, "Alpha", ScheduledTaskBackups.BeforeDelete, new DateTime(2026, 10, 1, 9, 0, 0));
            var newer = store.Save(@"\Vendor\alpha", Xml, "Alpha", ScheduledTaskBackups.Manual, new DateTime(2026, 10, 2, 9, 0, 0));

            var list = store.List();
            Assert.Equal(new[] { "alpha", "alpha", "Zeta Task" }, list.Select(b => b.Name));
            Assert.Equal(Path.GetFileName(newer), list[0].File);
            Assert.Equal(@"\Vendor", list[0].Folder);
            Assert.Equal(@"\", list[2].Folder);
            Assert.Equal(ScheduledTaskBackups.BeforeDelete, list[1].Reason);
            // The file is the task's own XML, UTF-16 with a byte order mark, as Task Scheduler's Import Task expects.
            var bytes = File.ReadAllBytes(first);
            Assert.Equal(new byte[] { 0xFF, 0xFE }, bytes[..2]);
            Assert.Equal(Xml, File.ReadAllText(first, Encoding.Unicode));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Fact]
    public void Same_task_saved_twice_in_one_second_gets_two_files()
    {
        var folder = Path.Combine(Path.GetTempPath(), "kc-tasks-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ScheduledTaskBackups(folder);
            var when = new DateTime(2026, 10, 23, 3, 0, 0);
            var a = store.Save(@"\A", Xml, "", ScheduledTaskBackups.Manual, when);
            var b = store.Save(@"\A", Xml, "", ScheduledTaskBackups.Manual, when);
            Assert.NotEqual(a, b);
            Assert.Equal(2, store.List().Count);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Fact]
    public void A_file_copied_in_by_hand_uses_the_path_in_its_XML()
    {
        var folder = Path.Combine(Path.GetTempPath(), "kc-tasks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "exported.xml"), Xml, Encoding.Unicode);
            File.WriteAllText(Path.Combine(folder, "notes.xml"), "<notes />");
            var only = Assert.Single(new ScheduledTaskBackups(folder).List());
            Assert.Equal(@"\Vendor\Updater & Co", only.TaskPath);
            Assert.Equal("Updater & Co", only.Name);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Theory]
    [InlineData(@"Vendor Update:Task?", "Vendor Update_Task_")]
    [InlineData("...", "Task")]
    [InlineData("", "Task")]
    public void File_names_are_safe(string name, string expected) => Assert.Equal(expected, ScheduledTaskBackups.SafeFileName(name));

    [Fact]
    public void Empty_task_is_not_saved()
    {
        var store = new ScheduledTaskBackups(Path.Combine(Path.GetTempPath(), "kc-tasks-" + Guid.NewGuid().ToString("N")));
        Assert.Throws<InvalidDataException>(() => store.Save(@"\A", " ", "", ScheduledTaskBackups.Manual, DateTime.Now));
    }
}
