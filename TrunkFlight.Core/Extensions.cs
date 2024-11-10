using System;
using System.Collections.Generic;
using System.IO;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace TrunkFlight.Core;

public static class Extensions
{
    public static ConfigurationBuilder AddDefaultConfig(
        this ConfigurationBuilder manager)
    {
        var dir = AppData.Default.UserAppDataDir;
        var dbfilepath = Path.Combine(dir.FullName, "merviche.trunkflight.db");

        var b = new SqliteConnectionStringBuilder();
        b.Mode = SqliteOpenMode.ReadWriteCreate;
        b.DataSource = dbfilepath;

        manager.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:db", b.ConnectionString)
        ]);

        return manager;
    }

    public static CredentialsHandler Creds(this GitRepo gr)
    {
        return (url, usernameFromUrl, types) =>
            new UsernamePasswordCredentials()
            {
                Username = gr.Username,
                Password = gr.Password,
            };
    }

    public static DirectoryInfo Realpath(this DirectoryInfo info)
    {
        var parts = info.FullName.Split(Path.DirectorySeparatorChar);
        string rebuilt = Path.GetPathRoot(info.FullName)
                         ?? throw new Exception("No path root for: " + info.FullName);
        for (int i = 1; i < parts.Length; i++)
        {
            var p = parts[i];
            rebuilt = Path.Combine(rebuilt, p);
            var resolved = Directory.ResolveLinkTarget(rebuilt, true)?.FullName;
            if (resolved is not null) rebuilt = resolved;
        }

        return new DirectoryInfo(rebuilt);
    }
}
