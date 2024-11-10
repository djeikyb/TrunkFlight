using System;
using System.IO;
using System.Linq;

namespace TrunkFlight.Core;

public class AppData
{
    public static AppData Default { get; } = new();

    public DirectoryInfo UserAppDataDir
    {
        get
        {
            // TODO: cache result

            var userAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.Empty.Equals(userAppDataDir)) throw new Exception("Base app data folder does not exist.");

            var path = Path.Combine(userAppDataDir, "merviche.trunkflight");
            var di = new DirectoryInfo(path);
            if (!di.Exists) di.Create();
            return di;
        }
    }

    /// Use when assigning <see cref="GitRepo.RepoPath"/>.
    /// Relative to <see cref="AppData.UserAppDataDir"/>.
    public string GenerateRepoPath(string gitUrl)
    {
        string littleEndianHost;
        string hostPath;
        if (gitUrl.StartsWith("http"))
        {
            var uri = new Uri(gitUrl);
            littleEndianHost = string.Join('.', uri.Host.Split('.').Reverse());
            hostPath = uri.AbsolutePath;
        }
        else
        {
            throw new Exception("Unsupported git url format or protocol.");
        }

        var path = Path.Combine(["src", littleEndianHost, ..hostPath.Split('/')]);
        // avoid inline, friendly to debugger
        return path;
    }
}
