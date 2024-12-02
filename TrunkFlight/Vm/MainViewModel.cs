using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using ObservableCollections;
using R3;
using Serilog;
using Serilog.Events;
using TrunkFlight.Core;

namespace TrunkFlight.Vm;

public class MainViewModel : IDisposable
{
    private DisposableBag _disposable;

    public void Dispose()
    {
        TearDown(TeardownOptions.SandboxDirs);
        _disposable.Dispose();
    }

    // HACK
    // 1. load first project
    // 2. load that project's git repo
    // 3. run fetch
    // 4. load that repo's first commit
    // 5.

    public MainViewModel(IClipboard clipboard)
    {
        var config = new ConfigurationBuilder().AddDefaultConfig().AddEnvironmentVariables().Build();

        var conn = new SqliteConnection(config.GetConnectionString("db"))
            .AddTo(ref _disposable);
        conn.Open();
        var db = new Db(conn);
        db.EnsureCreated();

        var logger = Log.ForContext<MainViewModel>();

        logger.Information(AppData.Default.UserAppDataDir.FullName);

        var repos = new ObservableList<GitRepo>(db
            .RepoCommands()
            .Select(x => x.GitRepo)
            .Where(x => x is not null)
            .Cast<GitRepo>()
            .ToHashSet());
        RepoOptions = repos.ToNotifyCollectionChangedSlim();
        RepoSelected = new BindableReactiveProperty<GitRepo?>(repos.FirstOrDefault()).AddTo(ref _disposable);

        var commands = new ObservableList<RepoCommand>(RepoSelected.Value?.RepoCommands ?? []);
        CommandOptions = commands.ToNotifyCollectionChangedSlim();
        CommandSelected = new BindableReactiveProperty<RepoCommand?>(commands.FirstOrDefault()).AddTo(ref _disposable);

        ProcessOutput = new BindableReactiveProperty<string>();
        SandboxPath = new BindableReactiveProperty<string?>();

        var branches = new ObservableList<string>();
        GitBranchOptions = branches.ToNotifyCollectionChangedSlim();
        GitBranchSelected = new BindableReactiveProperty<string?>();

        var commits = new ObservableList<string>();
        GitCommitOptions = commits.ToNotifyCollectionChangedSlim();
        GitCommitSelected = new BindableReactiveProperty<string?>();

        RepoSelected.Subscribe(repo =>
        {
            if (repo is null)
            {
                CommandSelected.Value = null;
                commands.Clear();
                return;
            }

            var rcSelected = CommandSelected.Value;

            commands.Clear();
            commands.AddRange(repo.RepoCommands ??
                              throw new Exception(
                                  $"Null {nameof(repo.RepoCommands)} for {repo.GetType()} {repo.GitRepoId}"));

            if (rcSelected is not null
                && commands.FirstOrDefault(x => x.Command.Equals(rcSelected.Command)) is { } found)
            {
                // Might be a different command record, but if the command text
                // itself is identical, feels good to create the illusion that
                // the selected command record did not change.
                CommandSelected.Value = found;
            }

            var git = new Git(AppData.Default, repo);
            UpdateBranchOptions(branches, git);
            UpdateCommitOptions(commits, git);
        });

        ProjectImportCommand = new ReactiveCommand();
        ProjectImportCommand.SubscribeExclusiveAwait(async (_, ct) =>
        {
            logger.Information("Command: " + nameof(ProjectImportCommand));

            // clipboard.GetFormatsAsync()
            //     .ContinueWith(formats => { });

            await Task.Run(async () =>
            {
                // var formats = await clipboard.GetFormatsAsync();
                // logger.Information(string.Join(Environment.NewLine, formats));
                var data = await clipboard.GetTextAsync();
                if (data is not null)
                {
                    UpsertGitRepoAndProject(data, db);

                    // maybe it's a little much to do some set calculations
                    // to make sure our in-memory list has everything from the
                    // db. then again, why not? but ask.. how would the db have
                    // more than a single new record?

                    var commandsFromDb = db.RepoCommands();

                    var notLocally = commandsFromDb.Except(commands);
                    var notInDb = commands.Except(commandsFromDb);
                    commands.AddRange(notLocally);
                    foreach (var nid in notInDb)
                    {
                        commands.Remove(nid);
                    }
                }
            });
        });

        ProjectUpdateCommand = new ReactiveCommand();
        ProjectUpdateCommand.SubscribeExclusiveAwait(async (_, ct) =>
        {
            logger.Information("Command: " + nameof(ProjectUpdateCommand));

            var gr = RepoSelected.Value;
            if (gr is null) return;

            var feelings = Task.Delay(500);
            var work = Task.Run(async () =>
            {
                bool connected;
                var uri = new Uri(gr.GitUrl);
                if (uri.Scheme.StartsWith("http"))
                {
                    CancellationTokenSource cts = new(1000);
                    using var tcp = new TcpClient();
                    try
                    {
                        await tcp.ConnectAsync(uri.Host, uri.Port, cts.Token);
                        connected = true;
                    }
                    catch (Exception)
                    {
                        connected = false;
                    }
                }
                else
                {
                    connected = true;
                }

                if (!connected)
                {
                    logger.Information("Basic connection failed, aborting git fetch.");
                    return;
                }

                var git = new Git(AppData.Default, gr);
                git.Fetch();
                UpdateBranchOptions(branches, git);
                UpdateCommitOptions(commits, git);
            });
            await Task.WhenAll(work, feelings);
        });

        GitBranchSelected.Subscribe(branchName =>
            {
                if (branchName is null) return;

                var p = CommandSelected.Value;
                if (p is null) return;
                if (p.GitRepo is null) return;

                var git = new Git(AppData.Default, p.GitRepo);
                UpdateCommitOptions(commits, git);
            })
            .AddTo(ref _disposable);

        SandboxCreateCommand = new ReactiveCommand(_ =>
        {
            logger.Information("Command: " + nameof(SandboxCreateCommand));

            var p = CommandSelected.Value;
            if (p is null) return;
            if (p.GitRepo is null) return;
            if (GitCommitSelected.Value is not { } commit) return;

            var git = new Git(AppData.Default, p.GitRepo);

            var tmpDir = Directory.CreateTempSubdirectory("merviche.trunkflight.");
            var tmpDirPath = tmpDir.FullName;
            // lol. lmao.
            // libgit2sharp requires an empty dir
            // but the csharp api to create a temp dir _path_ is private
            tmpDir.Delete(); // this is ridiculous

            git.Worktree.Add(tmpDirPath, commit.Split(' ')[0]); // TODO hack
            SandboxPath.Value = tmpDirPath;
        });

        SandboxDestroyCommand = new ReactiveCommand(_ =>
        {
            logger.Information("Command: " + nameof(SandboxDestroyCommand));
            if (SandboxPath.Value is not { } path) return;

            var p = CommandSelected.Value;
            if (p is null) return;
            if (p.GitRepo is null) return;

            var git = new Git(AppData.Default, p.GitRepo);
            git.Worktree.Remove(path);
            SandboxPath.Value = null;
            ProcessOutput.Value = string.Empty;
        });

        SandboxDestroyAllCommand = new ReactiveCommand(_ =>
        {
            TearDown(TeardownOptions.SandboxDirs);
        });

        SandboxRunAppCommand = new ReactiveCommand();
        SandboxRunAppCommand.SubscribeExclusiveAwait(async (_, ct) =>
        {
            logger.Information("Command: " + nameof(SandboxRunAppCommand));

            if (CommandSelected.Value is not { } proj) return;
            if (SandboxPath.Value is not { } path) return;

            var firstSpace = proj.Command.IndexOf(' ');

            Process proc = new Process();
            proc.AddTo(ref _disposable);

            // set up the command
            proc.StartInfo.WorkingDirectory = path;
            proc.StartInfo.FileName = firstSpace > 0
                ? proj.Command[..firstSpace]
                : proj.Command;
            if (firstSpace > 0) proc.StartInfo.Arguments = proj.Command[firstSpace..];

            // get ready to capture stdout
            proc.StartInfo.RedirectStandardOutput = true;
            Observable.FromEvent<DataReceivedEventHandler, DataReceivedEventArgs>(
                    h => (sender, e) => h(e),
                    e => proc.OutputDataReceived += e,
                    e => proc.OutputDataReceived -= e)
                .Subscribe(args =>
                {
                    ProcessOutput.Value += args.Data + Environment.NewLine;
                });

            proc.Start();
            proc.BeginOutputReadLine();
            await proc.WaitForExitAsync(ct);
        });

        InitRepo = new ReactiveCommand();
        InitRepo.SubscribeExclusiveAwait(async (_, ct) =>
        {
            logger.Information("Command: " + nameof(InitRepo));

            var p = CommandSelected.Value;
            if (p is null) return;
            if (p.GitRepo is null) return;

            await Task.Run(() =>
            {
                try
                {
                    var git = new Git(AppData.Default, p.GitRepo);
                    git.Clone();
                }
                catch (TaskCanceledException)
                {
                    // copypasta.. not sure what could be canceling right now
                }
                catch (Exception ex)
                {
                    logger.Information(ex, "Clone failed.");
                }
            });
        }).AddTo(ref _disposable);

        DeleteRepo = new ReactiveCommand(_ =>
        {
            logger.Information("Command: " + nameof(DeleteRepo));
            TearDown(TeardownOptions.BareGitRepo | TeardownOptions.SandboxDirs);
        });

        View = App.LogsSink.Logs.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

        TintOpacity = new BindableReactiveProperty<decimal>(1m).AddTo(ref _disposable);
        MaterialOpacity = new BindableReactiveProperty<decimal>(1m).AddTo(ref _disposable);
    }

    private void UpdateBranchOptions(ObservableList<string> branches, Git git)
    {
        var gbs = GitBranchSelected.Value;

        // The ObservableList::Clear is synchronized!
        //
        // I think the ui render doesn't happen until the Command function
        // exits. But the ui controls seem to be notified and take their
        // action immediately. The ListBox nulls out the selected item, which
        // is bound to the selected branch. Adter the Clear, the selected
        // branch is null. So we capture that value before clear, then sort out
        // if it's still useful.

        var branchNames = git.RemoteBranchNames();
        branches.Clear();
        branches.AddRange(branchNames);

        if (gbs is null)
        {
            GitBranchSelected.Value = branchNames.FirstOrDefault();
        }
        else if (branchNames.Contains(gbs))
        {
            GitBranchSelected.Value = gbs;
        }
        else
        {
            GitBranchSelected.Value = branchNames.FirstOrDefault();
        }
    }

    private void UpdateCommitOptions(ObservableList<string> commits, Git git)
    {
        var branchName = GitBranchSelected.Value;
        if (branchName is null) return;

        var gcs = GitCommitSelected.Value;

        // it's important to capture the selected commit before calling clear
        // cf UpdateBranchOptions

        var latestCommits = git
            .LatestCommits(branchName)
            .Select(x1 => x1.ShaShort + " " + x1.MessageShort);
        commits.Clear();
        commits.AddRange(latestCommits);

        if (gcs is null)
        {
            GitCommitSelected.Value = commits.FirstOrDefault();
        }
        else if (commits.Contains(gcs))
        {
            GitCommitSelected.Value = gcs;
        }
        else
        {
            GitCommitSelected.Value = commits.FirstOrDefault();
        }
    }

    private static void UpsertGitRepoAndProject(string projectData, Db db)
    {
        // SAMPLE
        // string projectData = """
        //                 git.repo=https://gitlab.eikongroup.io/Eikon/dcp/noetik.git
        //                 git.user=redacted
        //                 git.pass=redacted
        //                 project.name=some project name
        //                 project.command=dotnet run --project Prototype.Reels
        //                 """;

        var logger = Log.ForContext<MainViewModel>();
        Dictionary<string, string> d = new();
        try
        {
            foreach (var ln in projectData.Split(Environment.NewLine))
            {
                if (string.Empty.Equals(ln)) continue;
                var kv = ln.Split('=', count: 2); // split exactly once
                d[kv[0]] = kv[1];
            }

            if (!d.ContainsKey("git.repo")) throw new Exception("Invalid project data: missing key: git.repo");

            if (d["git.repo"].StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
            {
                if (!d.ContainsKey("git.user")) throw new Exception("Invalid project data: missing key: git.user");
                if (!d.ContainsKey("git.pass")) throw new Exception("Invalid project data: missing key: git.pass");
            }

            if (!d.ContainsKey("project.name")) throw new Exception("Invalid project data: missing key: project.name");
            if (!d.ContainsKey("project.command"))
                throw new Exception("Invalid project data: missing key: project.command");
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to parse config.");
            return;
        }

        var gitRepo = new GitRepo
        {
            RepoPath = AppData.Default.GenerateRepoPath(d["git.repo"]),
            GitUrl = d["git.repo"]
        };
        if (d.TryGetValue("git.user", out var user))
            gitRepo.Username = user;
        if (d.TryGetValue("git.pass", out var pass))
            gitRepo.Password = pass;
        db.Save(gitRepo);

        var proj = new RepoCommand
        {
            GitRepoId = gitRepo.GitRepoId,
            Command = d["project.command"],
        };
        db.Save(proj);
    }

    [Flags]
    enum TeardownOptions
    {
        BareGitRepo = 1 << 0,
        SandboxDirs = 1 << 1,
    }

    private void TearDown(TeardownOptions options)
    {
        if (options.HasFlag(TeardownOptions.BareGitRepo)
            && CommandSelected.Value?.GitRepo?.RepoPath is { } repoPath)
        {
            var hack = Path.Combine(AppData.Default.UserAppDataDir.FullName, repoPath);
            try
            {
                Directory.Delete(hack, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // whatever man
            }
        }

        if (options.HasFlag(TeardownOptions.SandboxDirs))
        {
            var myTempDirRoot = "/private/var/folders/9m/25nqg0sd5b51pm8dz78j31740000gn"; // TODO hack
            var tempDirs = Directory.EnumerateDirectories(myTempDirRoot, "merviche.trunkflight.*",
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    MatchCasing = MatchCasing.CaseSensitive,
                    MatchType = MatchType.Simple,
                    MaxRecursionDepth = 2,
                    ReturnSpecialDirectories = false,
                    IgnoreInaccessible = true,
                });

            foreach (var dir in tempDirs)
            {
                if (!Path.IsPathRooted(dir)) continue;
                if (!Path.IsPathFullyQualified(dir)) continue;
                var dirname = Path.GetFileName(dir);
                if (!dirname.StartsWith("merviche.trunkflight.")) continue;
                Directory.Delete(dir, true);
            }

            SandboxPath.Value = null;
        }
    }

    public BindableReactiveProperty<string?> SandboxPath { get; }
    public BindableReactiveProperty<string> ProcessOutput { get; }

    public BindableReactiveProperty<GitRepo?> RepoSelected { get; }
    public INotifyCollectionChangedSynchronizedViewList<GitRepo> RepoOptions { get; }

    public BindableReactiveProperty<RepoCommand?> CommandSelected { get; }
    public INotifyCollectionChangedSynchronizedViewList<RepoCommand> CommandOptions { get; }

    public BindableReactiveProperty<string?> GitBranchSelected { get; }
    public INotifyCollectionChangedSynchronizedViewList<string> GitBranchOptions { get; }

    public BindableReactiveProperty<string?> GitCommitSelected { get; }
    public INotifyCollectionChangedSynchronizedViewList<string> GitCommitOptions { get; }

    public ReactiveCommand<Unit> ProjectUpdateCommand { get; }
    public ReactiveCommand<Unit> ProjectImportCommand { get; }


    public ReactiveCommand<Unit> SandboxCreateCommand { get; }
    public ReactiveCommand<Unit> SandboxDestroyCommand { get; }
    public ReactiveCommand<Unit> SandboxDestroyAllCommand { get; }
    public ReactiveCommand<Unit> SandboxRunAppCommand { get; }

    public ReactiveCommand<Unit> InitRepo { get; }
    public ReactiveCommand<Unit> DeleteRepo { get; }


    public BindableReactiveProperty<decimal> TintOpacity { get; }
    public BindableReactiveProperty<decimal> MaterialOpacity { get; }


    public INotifyCollectionChangedSynchronizedViewList<LogEvent> View { get; }
}
