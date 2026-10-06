using System.Security.Cryptography;
using KodiakCleaner.Core.Supporter;

// Ted's supporter code tool. Never shipped in a release.
//   make-key                     creates keys/supporter-private.pem and keys/supporter-public.pem (once)
//   make-code "Name" [--test]    prints a supporter code signed with Ted's key (or the test key)

var root = FindRoot();
var keys = Path.Combine(root, "keys");
var privatePath = Path.Combine(keys, "supporter-private.pem");
var publicPath = Path.Combine(keys, "supporter-public.pem");
var testPrivatePath = Path.Combine(root, "tools", "SupporterCodes", "supporter-test-private.pem");

switch (args.FirstOrDefault())
{
    case "make-key":
        if (File.Exists(privatePath))
        {
            Console.Error.WriteLine($"{privatePath} already exists. Making a new key would stop every code you've issued from working.");
            return 1;
        }
        using (var key = ECDsa.Create(ECCurve.NamedCurves.nistP256))
        {
            Directory.CreateDirectory(keys);
            File.WriteAllText(privatePath, key.ExportPkcs8PrivateKeyPem());
            File.WriteAllText(publicPath, key.ExportSubjectPublicKeyInfoPem());
        }
        Console.WriteLine($"Private key: {privatePath}");
        Console.WriteLine("  Keep it secret and back it up somewhere safe. It is ignored by git and must never be shared.");
        Console.WriteLine($"Public key:  {publicPath}");
        Console.WriteLine("  Commit this one. The next build of Kodiak Cleaner uses it to check supporter codes.");
        return 0;

    case "make-code" when args.Length >= 2:
        var test = args.Contains("--test");
        var keyPath = test ? testPrivatePath : privatePath;
        if (!File.Exists(keyPath))
        {
            Console.Error.WriteLine($"{keyPath} not found. Run make-key first (or add --test for a test code).");
            return 1;
        }
        var license = new SupporterLicense(Guid.NewGuid().ToString("N")[..12], args[1], DateOnly.FromDateTime(DateTime.Today));
        Console.WriteLine($"Code for {license.Name} (ID {license.Id}{(test ? ", TEST KEY" : "")}):");
        Console.WriteLine(SupporterCode.Create(license, File.ReadAllText(keyPath)));
        return 0;

    default:
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project tools/SupporterCodes -- make-key");
        Console.WriteLine("  dotnet run --project tools/SupporterCodes -- make-code \"Name\" [--test]");
        return 1;
}

static string FindRoot()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "version.json"))) return dir.FullName;
    return Directory.GetCurrentDirectory();
}
