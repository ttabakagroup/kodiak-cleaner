using System.IO;
using System.IO.Pipes;
using System.Text;

namespace KodiakCleaner.App;

/// <summary>Keeps one Kodiak Cleaner window: a second launch (e.g. from the jump list) hands its arguments to the first.</summary>
public static class SingleInstance
{
    private static Mutex? _mutex;
    private static readonly string Name = $"{AppInfo.Id}-{Environment.UserName}-{System.Diagnostics.Process.GetCurrentProcess().SessionId}";

    /// <summary>True when a Kodiak Cleaner window is already open (a new launch only hands over its arguments).</summary>
    public static bool IsRunning
    {
        get
        {
            try { return Mutex.TryOpenExisting(@"Local\" + Name, out var m) && DisposeAndTrue(m); }
            catch (UnauthorizedAccessException) { return true; } // an elevated copy owns it
        }
    }

    private static bool DisposeAndTrue(Mutex m) { m.Dispose(); return true; }

    /// <param name="waitForPrevious">After Restart as Administrator: give the closing copy a few seconds to let go first.</param>
    /// <returns>true if this is the first instance.</returns>
    public static bool Claim(bool waitForPrevious = false)
    {
        try
        {
            if (!waitForPrevious)
            {
                _mutex = new Mutex(initiallyOwned: true, @"Local\" + Name, out bool created);
                return created;
            }
            _mutex = new Mutex(initiallyOwned: false, @"Local\" + Name);
            try { return _mutex.WaitOne(TimeSpan.FromSeconds(10)); }
            catch (AbandonedMutexException) { return true; } // the previous copy exited without releasing it
        }
        catch (UnauthorizedAccessException)
        {
            // An elevated copy created it, and a normal launch can't open it: that copy is the first instance.
            return false;
        }
    }

    public static void SendToFirst(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", Name, PipeDirection.Out);
            client.Connect(2000);
            var bytes = Encoding.UTF8.GetBytes(string.Join("\n", args));
            client.Write(bytes, 0, bytes.Length);
        }
        catch (Exception e) when (e is IOException or TimeoutException or UnauthorizedAccessException) { }
    }

    /// <summary>Listens for later launches and passes their arguments to <paramref name="onArgs"/> on the UI thread.</summary>
    public static void Listen(Action<string[]> onArgs)
    {
        var dispatcher = System.Windows.Application.Current.Dispatcher;
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(Name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync();
                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var text = await reader.ReadToEndAsync();
                    var args = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    _ = dispatcher.BeginInvoke(() => onArgs(args));
                }
                catch (IOException) { await Task.Delay(500); }
            }
        });
    }
}
