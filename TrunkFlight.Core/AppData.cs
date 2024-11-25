using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace TrunkFlight.Core;

public class AppData
{
    public static AppData Default { get; } = new();

    public DirectoryInfo UserAppDataDir
    {
        get
        {
            // TODO: cache result

            var userAppDataDir = LocalAppDataFolder();
            if (string.Empty.Equals(userAppDataDir)) throw new Exception("Base app data folder does not exist.");

            var path = Path.Combine(userAppDataDir, "merviche.trunkflight");
            var di = new DirectoryInfo(path);
            if (!di.Exists) di.Create();
            return di;
        }
    }

    private static string LocalAppDataFolder()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { } s) return s;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".local", "share");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support");
        }

        throw new NotImplementedException("Unsupported OS.");
    }

    /// Use when assigning <see cref="GitRepo.RepoPath"/>.
    /// Relative to <see cref="AppData.UserAppDataDir"/>.
    public string GenerateRepoPath(string gitUrl)
    {
        string littleEndianHost;
        string hostPath;
        string repoPath;
        if (gitUrl.StartsWith("http"))
        {
            var uri = new Uri(gitUrl);
            littleEndianHost = string.Join('.', uri.Host.Split('.').Reverse());
            hostPath = uri.AbsolutePath;
            repoPath = Path.Combine(["src", littleEndianHost, ..hostPath.Split('/')]);
        }
        else if (gitUrl.StartsWith("file://"))
        {
            var sourcePath = new Uri(gitUrl).AbsolutePath;
            string name;
            if (".git".Equals(Path.GetFileName(sourcePath)))
            {
                name = Path.GetFileName(Path.GetDirectoryName(sourcePath));
            }
            else
            {
                name = Path.GetFileName(sourcePath);
            }
            repoPath = Path.Combine(["src", "file", name]);
        }
        else
        {
            throw new Exception("Unsupported git url format or protocol.");
        }

        // avoid inline, friendly to debugger
        return repoPath;
    }
}
