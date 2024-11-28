using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace TrunkFlight.Core;

public class Project
{
    public int ProjectId { get; set; }

    public required int GitRepoId { get; set; }
    public GitRepo? GitRepo { get; set; }

    public required string Name { get; init; }
    public required string Command { get; set; }

    protected bool Equals(Project other) => ProjectId == other.ProjectId;

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((Project)obj);
    }

    public override int GetHashCode() => ProjectId;
}

public class GitRepo
{
    public int GitRepoId { get; set; }

    /// relative to <see cref="AppData.UserAppDataDir"/>
    public required string RepoPath { get; init; }

    public required string GitUrl { get; init; }
    public string? Username { get; set; }
    public string? Password { get; set; }
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
        var file = new SqliteConnectionStringBuilder(_connection.ConnectionString).DataSource;
        Log.ForContext<Db>().Warning($"Failed to delete database: {file}");
        return false;
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

                             create table if not exists projects
                             (
                                 project_id  INTEGER not null
                                     constraint pk_projects
                                         primary key autoincrement,
                                 git_repo_id INTEGER not null
                                     constraint fk_projects_git_repos_git_repo_id
                                         references git_repos
                                         on delete cascade,
                                 name        TEXT    not null,
                                 command     TEXT    not null
                             );

                             create unique index if not exists ak_git_url
                                 on git_repos (git_url);

                             create unique index if not exists ak_git_repo_id_name
                                 on projects (git_repo_id, name);

                             create index if not exists ix_projects_git_repo_id
                                 on projects (git_repo_id);
                             """;

        create.ExecuteNonQuery();
        return false;
    }

    public Project? LatestProject()
    {
        using var select = _connection.CreateCommand();
        select.CommandText = """
                             select p.*, gr.*
                             from projects p
                             join git_repos gr on gr.git_repo_id = p.git_repo_id
                             order by p.project_id desc 
                             limit 1
                             """;

        using var reader = select.ExecuteReader(CommandBehavior.SingleResult | CommandBehavior.SingleRow);
        if (!reader.Read()) return null;
        var project = new Project
        {
            ProjectId = reader.GetInt32(0),
            GitRepoId = reader.GetInt32(1),
            Name = reader.GetString(2),
            Command = reader.GetString(3),
            GitRepo = new GitRepo
            {
                GitRepoId = reader.GetInt32(4),
                RepoPath = reader.GetString(5),
                GitUrl = reader.GetString(6),
                Username = reader.GetString(7),
                Password = reader.GetString(8),
            },
        };

        // don't inline, be kind to debuggers
        return project;
    }

    public List<Project> Projects()
    {
        using var select = _connection.CreateCommand();
        select.CommandText = """
                             select p.*, gr.*
                             from projects p
                             join git_repos gr on gr.git_repo_id = p.git_repo_id
                             order by p.project_id desc 
                             """;

        using var reader = select.ExecuteReader(CommandBehavior.Default);
        List<Project> projects = [];
        while (reader.Read())
        {
            var project = new Project
            {
                ProjectId = reader.GetInt32(0),
                GitRepoId = reader.GetInt32(1),
                Name = reader.GetString(2),
                Command = reader.GetString(3),
                GitRepo = new GitRepo
                {
                    GitRepoId = reader.GetInt32(4),
                    RepoPath = reader.GetString(5),
                    GitUrl = reader.GetString(6),
                    Username = reader.GetString(7),
                    Password = reader.GetString(8),
                },
            };
            projects.Add(project);
        }

        return projects;
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
        upsert.Parameters.AddWithValue("$username", gr.Username);
        upsert.Parameters.AddWithValue("$password", gr.Password);
        using (var reader = upsert.ExecuteReader(CommandBehavior.SingleResult | CommandBehavior.SingleRow))
        {
            if (reader.Read()) gr.GitRepoId = reader.GetInt32(0);
        }

        var count = upsert.ExecuteNonQuery();
        Debug.Assert(count == 1, "Should've inserted one row.");
    }

    public void Save(Project p)
    {
        using var upsertProj = _connection.CreateCommand();
        upsertProj.CommandText = """
                                 insert into projects
                                 (git_repo_id, name, command)
                                 values
                                 ($git_repo_id, $name, $command)
                                 on conflict(git_repo_id, name) do update set
                                     command=excluded.command
                                 returning project_id
                                 """;
        upsertProj.Parameters.AddWithValue("$git_repo_id", p.GitRepoId);
        upsertProj.Parameters.AddWithValue("$name", p.Name);
        upsertProj.Parameters.AddWithValue("$command", p.Command);
        using (var reader = upsertProj.ExecuteReader(CommandBehavior.SingleResult | CommandBehavior.SingleRow))
        {
            if (reader.Read()) p.ProjectId = reader.GetInt32(0);
        }

        var count = upsertProj.ExecuteNonQuery();
        Debug.Assert(count == 1, "Should've inserted one row.");
    }
}
