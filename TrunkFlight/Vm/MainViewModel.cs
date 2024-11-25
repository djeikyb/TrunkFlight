using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

    public MainViewModel()
    {
        var config = new ConfigurationBuilder().AddDefaultConfig().AddEnvironmentVariables().Build();
        var db = new DataContext(config);
        db.Database.EnsureCreated();
        var projects = new ProjectService(AppData.Default, db);
        var logger = Log.ForContext<MainViewModel>();

        Project = new BindableReactiveProperty<Project?>().AddTo(ref _disposable);

        ProcessOutput = new BindableReactiveProperty<string>();
        SandboxPath = new BindableReactiveProperty<string?>();

        var branches = new ObservableList<string>();
        GitBranchOptions = branches.ToNotifyCollectionChangedSlim();
        GitBranchSelected = new BindableReactiveProperty<string?>("main");

        var commits = new ObservableList<string>();
        GitCommitOptions = commits.ToNotifyCollectionChangedSlim();
        GitCommitSelected = new BindableReactiveProperty<string>("HEAD");

        ProjectLoadCommand = new ReactiveCommand();
        ProjectLoadCommand.SubscribeAwait((async (_, ct) =>
        {
            logger.Information("Command: " + nameof(ProjectLoadCommand));
            var p = await projects.First(ct);
            Project.Value = p;
            if (p?.GitRepo is not null)
            {
                var git = new Git(AppData.Default, p.GitRepo);
                UpdateBranchOptions(branches, git);
            }
        }));

        ProjectUnloadCommand = new ReactiveCommand(_ =>
        {
            logger.Information("Command: " + nameof(ProjectUnloadCommand));
            Project.Value = null;
            commits.Clear();
            branches.Clear();
        });

        ProjectUpdateCommand = new ReactiveCommand(_ =>
        {
            logger.Information("Command: " + nameof(ProjectUpdateCommand));

            var p = Project.Value;
            if (p is null) return;
            if (p.GitRepo is null) return;

            var git = new Git(AppData.Default, p.GitRepo);
            git.Fetch();
            UpdateBranchOptions(branches, git);
        });

        GitBranchSelected.Subscribe(branchName =>
            {
                if (branchName is null) return;

                var p = Project.Value;
                if (p is null) return;
                if (p.GitRepo is null) return;

                var git = new Git(AppData.Default, p.GitRepo);
                commits.Clear();
                var latestCommits = git.LatestCommits(branchName)
                    .Select(x1 => x1.ShaShort + " " + x1.MessageShort);
                commits.AddRange(latestCommits);
            })
            .AddTo(ref _disposable);

        SandboxCreateCommand = new ReactiveCommand(_ =>
        {
            logger.Information("Command: " + nameof(SandboxCreateCommand));

            var p = Project.Value;
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

            var p = Project.Value;
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

            if (Project.Value is not { } proj) return;
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
            var p = Project.Value;
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
        var branchNames = git.RemoteBranchNames();
        branches.Clear();
        branches.AddRange(branchNames);
        if (GitBranchSelected.Value is not { } b || !branchNames.Contains(b))
        {
            GitBranchSelected.Value = branchNames.FirstOrDefault();
        }
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
            && Project.Value?.GitRepo?.RepoPath is { } repoPath)
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

    public BindableReactiveProperty<string?> GitBranchSelected { get; }
    public INotifyCollectionChangedSynchronizedViewList<string> GitBranchOptions { get; }

    public BindableReactiveProperty<string> GitCommitSelected { get; }
    public INotifyCollectionChangedSynchronizedViewList<string> GitCommitOptions { get; }

    public ReactiveCommand<Unit> ProjectLoadCommand { get; }
    public ReactiveCommand<Unit> ProjectUnloadCommand { get; }
    public ReactiveCommand<Unit> ProjectUpdateCommand { get; }


    public ReactiveCommand<Unit> SandboxCreateCommand { get; }
    public ReactiveCommand<Unit> SandboxDestroyCommand { get; }
    public ReactiveCommand<Unit> SandboxDestroyAllCommand { get; }
    public ReactiveCommand<Unit> SandboxRunAppCommand { get; }

    public ReactiveCommand<Unit> InitRepo { get; }
    public ReactiveCommand<Unit> DeleteRepo { get; }


    public BindableReactiveProperty<Project?> Project { get; }

    public BindableReactiveProperty<decimal> TintOpacity { get; }
    public BindableReactiveProperty<decimal> MaterialOpacity { get; }


    public INotifyCollectionChangedSynchronizedViewList<LogEvent> View { get; }
}
