using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace TrunkFlight.Core;

public class RepoCommand
{
    public int RepoCommandId { get; set; }

    public required int GitRepoId { get; set; }
    public GitRepo? GitRepo { get; set; }
    public required string Command { get; set; }

    protected bool Equals(RepoCommand other) => RepoCommandId == other.RepoCommandId;

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((RepoCommand)obj);
    }

    public override int GetHashCode() => RepoCommandId;
}

public class GitRepo
{
    public int GitRepoId { get; set; }

    public List<RepoCommand>? RepoCommands { get; set; }

    /// relative to <see cref="AppData.UserAppDataDir"/>
    public required string RepoPath { get; init; }

    public required string GitUrl { get; init; }
    public string? Username { get; set; }
    public string? Password { get; set; }

    protected bool Equals(GitRepo other)
    {
        return GitRepoId == other.GitRepoId;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((GitRepo)obj);
    }

    public override int GetHashCode()
    {
        return GitRepoId;
    }
}

public class Db : IDisposable
{
    private readonly SqliteConnection _connection;

    public Db(SqliteConnection connection)
    {
        _connection = connection;
    }

    public Db(IConfiguration config)
    {
        var cs = config.GetConnectionString("db");
        _connection = new SqliteConnection(cs);
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    public string GetConnectionString() => _connection.ConnectionString;

    public bool EnsureDeleted()
    {
        using var drop = _connection.CreateCommand();
        drop.CommandText = """
                           drop table if exists projects;
                           drop table if exists git_repos;
                           drop table if exists repo_command;
                           """;
        drop.ExecuteNonQuery();
        return true;
    }

    public bool EnsureCreated()
    {
        using var create = _connection.CreateCommand();
        create.CommandText = """
                             create table if not exists git_repos
                             (
                                 git_repo_id INTEGER not null
                                     constraint pk_git_repos
                                         primary key autoincrement,
                                 repo_path   TEXT    not null,
                                 git_url     TEXT    not null,
                                 username    TEXT,
                                 password    TEXT
                             );

                             create table if not exists repo_command
                             (
                                 repo_command_id  INTEGER not null
                                     constraint pk_repo_command
                                         primary key autoincrement,
                                 git_repo_id INTEGER not null
                                     constraint fk_repo_command_git_repos_git_repo_id
                                         references git_repos
                                         on delete cascade,
                                 command     TEXT    not null
                             );

                             create unique index if not exists ak_git_url
                                 on git_repos (git_url);

                             create index if not exists ix_repo_command_git_repo_id
                                 on repo_command (git_repo_id);

                             create unique index if not exists ak_git_repo_id_command
                                 on repo_command (git_repo_id, command);
                             """;

        create.ExecuteNonQuery();
        return false;
    }

    public RepoCommand? LatestRepoCommand()
    {
        using var select = _connection.CreateCommand();
        select.CommandText = """
                             select rc.*, gr.*
                             from repo_command rc
                             join git_repos gr on gr.git_repo_id = rc.git_repo_id
                             order by rc.repo_command_id desc 
                             limit 1
                             """;

        using var reader = select.ExecuteReader(CommandBehavior.SingleResult | CommandBehavior.SingleRow);
        if (!reader.Read()) return null;
        var rc = new RepoCommand
        {
            RepoCommandId = reader.GetInt32(0),
            GitRepoId = reader.GetInt32(1),
            Command = reader.GetString(2),
            GitRepo = new GitRepo
            {
                GitRepoId = reader.GetInt32(3),
                RepoPath = reader.GetString(4),
                GitUrl = reader.GetString(5),
                Username = reader.GetString(6),
                Password = reader.GetString(7),
            },
        };

        // don't inline, be kind to debuggers
        return rc;
    }

    public List<RepoCommand> RepoCommands()
    {
        using var select = _connection.CreateCommand();
        select.CommandText = """
                             select rc.*, gr.*
                             from repo_command rc
                             join git_repos gr on gr.git_repo_id = rc.git_repo_id
                             order by rc.repo_command_id desc 
                             """;

        using var reader = select.ExecuteReader(CommandBehavior.Default);
        List<RepoCommand> rcs = [];
        while (reader.Read())
        {
            var rc = new RepoCommand
            {
                RepoCommandId = reader.GetInt32(0),
                GitRepoId = reader.GetInt32(1),
                Command = reader.GetString(2),
                GitRepo = new GitRepo
                {
                    GitRepoId = reader.GetInt32(3),
                    RepoPath = reader.GetString(4),
                    GitUrl = reader.GetString(5),
                    Username = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Password = reader.IsDBNull(7) ? null : reader.GetString(7),
                },
            };
            rcs.Add(rc);
        }

        var lookup = rcs.ToLookup(x => x.GitRepoId, x => x);
        foreach (var lk in lookup)
        {
            var repo = lk.First().GitRepo;
            repo.RepoCommands = lk.ToList();
        }

        return rcs;
    }

    public void Save(GitRepo gr)
    {
        using var upsert = _connection.CreateCommand();
        upsert.CommandText = """
                             insert into git_repos
                             (repo_path, git_url, username, password)
                             values
                             ($repo_path, $git_url, $username, $password)
                             on conflict(git_url) do update set
                                 repo_path=excluded.repo_path,
                                  username=excluded.username,
                                  password=excluded.password
                             returning git_repo_id
                             """;
        upsert.Parameters.AddWithValue("$repo_path", gr.RepoPath);
        upsert.Parameters.AddWithValue("$git_url", gr.GitUrl);
        upsert.Parameters.AddWithValue("$username", (object?)gr.Username ?? DBNull.Value);
        upsert.Parameters.AddWithValue("$password", (object?)gr.Password ?? DBNull.Value);
        using (var reader = upsert.ExecuteReader(CommandBehavior.SingleResult | CommandBehavior.SingleRow))
        {
            if (reader.Read()) gr.GitRepoId = reader.GetInt32(0);
        }

        var count = upsert.ExecuteNonQuery();
        Debug.Assert(count == 1, "Should've inserted one row.");
    }

    public void Save(RepoCommand rc)
    {
        using var upsertRc = _connection.CreateCommand();
        upsertRc.CommandText = """
                               insert into repo_command
                               (git_repo_id, command)
                               values
                               ($git_repo_id, $command)
                               on conflict(git_repo_id, command) do nothing 
                               returning repo_command_id
                               """;
        upsertRc.Parameters.AddWithValue("$git_repo_id", rc.GitRepoId);
        upsertRc.Parameters.AddWithValue("$command", rc.Command);
        using var reader = upsertRc.ExecuteReader(CommandBehavior.SingleResult | CommandBehavior.SingleRow);
        if (reader.Read()) rc.RepoCommandId = reader.GetInt32(0);
        reader.Close();
        // RecordsAffected may be unreliable until reader is closed
        // https://learn.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqldatareader?view=net-8.0-pp&redirectedfrom=MSDN
        var count = reader.RecordsAffected;
        Debug.Assert(count == 1, $"Should've inserted one row, but was {count}.");
    }
}
