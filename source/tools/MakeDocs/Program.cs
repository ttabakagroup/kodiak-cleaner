using KodiakCleaner.Core.About;

// dotnet run --project tools/MakeDocs -- <output-folder> <dev|beta|stable>
if (args.Length != 2 || !Enum.TryParse<ReleaseChannel>(args[1], ignoreCase: true, out var channel))
{
    Console.Error.WriteLine("Usage: MakeDocs <output-folder> <dev|beta|stable>");
    return 1;
}
var repo = AppContext.BaseDirectory;
while (!File.Exists(Path.Combine(repo, "version.json"))) repo = Path.GetDirectoryName(repo) ?? throw new DirectoryNotFoundException("version.json not found");
DocsFolder.Write(repo, args[0], channel);
foreach (var file in DocsFolder.Files) Console.WriteLine($"Docs: {Path.Combine(args[0], file)}");
return 0;
