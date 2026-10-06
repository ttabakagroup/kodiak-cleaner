using KodiakCleaner.Core.Programs;
using Xunit;

namespace KodiakCleaner.Core.Tests;

public class ProgramHistoryTests
{
    [Fact]
    public void RecordsSurviveSavingAndUninstallsAreRecordOnly()
    {
        var folder = Path.Combine(Path.GetTempPath(), "kc-proghist-" + Guid.NewGuid().ToString("N"));
        try
        {
            var program = new InstalledProgram { Name = "Example App", Publisher = "Example", Version = "1.0", Location = new KodiakCleaner.Core.RegistryTools.RegistryLocation(Microsoft.Win32.RegistryHive.CurrentUser, Microsoft.Win32.RegistryView.Default, @"Software\Example") };
            var h = new ProgramHistory();
            var when = new DateTime(2026, 10, 4, 12, 0, 0);
            h.Add(ProgramChange.Uninstalled, program, "Removed by its own uninstaller", when, "before.reg");
            h.Add(ProgramChange.DeletedEntry, program, "Removed from the programs list", when.AddMinutes(1), "entry.reg");
            h.Add(ProgramChange.LeftoverFile, program, @"C:\Program Files\Example App", when.AddMinutes(2));
            h.Add(ProgramChange.LeftoverStartup, program, "Example (Run key)", when.AddMinutes(3));
            h.Save(folder);

            var back = ProgramHistory.Load(folder);
            Assert.Equal(4, back.Records.Count);
            Assert.Equal(ProgramChange.LeftoverStartup, back.Records[0].Change); // newest first
            Assert.False(back.Records.Single(r => r.Change == ProgramChange.Uninstalled).Restorable);
            Assert.True(back.Records.Single(r => r.Change == ProgramChange.DeletedEntry).Restorable);
            Assert.True(back.Records.Single(r => r.Change == ProgramChange.LeftoverFile).Restorable);
            Assert.False(back.Records[0].Restorable); // a startup leftover with no saved key
            Assert.Equal("Example App", back.Records[1].Program);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void DamagedFileGivesAnEmptyHistory()
    {
        var folder = Path.Combine(Path.GetTempPath(), "kc-proghist-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, ProgramHistory.FileName), "{ not json");
            Assert.Empty(ProgramHistory.Load(folder).Records);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }
}
