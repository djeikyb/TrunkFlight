using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TrunkFlight.Core;

public class ProjectService(AppData appData, DataContext db)
{
    public async Task<Project?> First(CancellationToken ct = default)
        => await db.Projects.Include(x => x.GitRepo).FirstOrDefaultAsync(ct);

    public Task RefreshRepo(Project project, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public IEnumerable<Commit> RecentCommits(Project project, CancellationToken ct = default)
    {
        if (project.GitRepo is null) throw new Exception("Missing include.");
        var path = Path.Combine(appData.UserAppDataDir.FullName, project.GitRepo.RepoPath);
        using var repo = new Repository(path);
        return repo.Commits.Take(10);
    }

    public async Task<WorktreeProcess> RunLatestCommit(Project project, CancellationToken ct = default)
    {
        // 1. create a worktree in a tmp dir
        // 2. run a hardcoded script
        // TODO return a disposable that removes the worktree

        if (project.GitRepo is null) throw new Exception("Missing include.");
        var path = Path.Combine(appData.UserAppDataDir.FullName, project.GitRepo.RepoPath);
        using var repo = new Repository(path);

        var commit = repo.Commits.First();
        var shortsha = commit.Sha[..7];
        var tmp = Directory.CreateTempSubdirectory("merviche.trunkflight.");

        Worktree? worktree = null;
        var worktreePath = Path.Combine(tmp.FullName, "path-" + shortsha);
        worktree = repo.Worktrees.Add(
            committishOrBranchSpec: commit.Sha,
            name: "name-" + shortsha,
            path: worktreePath,

            // from `man git-worktree`
            // > If the working tree for a linked worktree is stored on a
            // > portable device or network share which is not always
            // > mounted, you can prevent its administrative files from
            // > being pruned
            //
            // i think false is good? pruning seems fine if, say, the tmp dir
            // disappears
            isLocked: false
        );

        // gitsharp creates a branch, but the git cli doesn't
        // the git cli behaviour is much better
        //
        // branch names have to be unique
        // i don't want to have to worry about a unique name,
        // for a ref i'm never going to use!
        repo.Branches.Remove("name-" + shortsha);

        // there's a bug in gitsharp
        // the checkout doesn't happen
        // there's a pr, but it's not merged
        // https://github.com/libgit2/libgit2sharp/pull/2099
        Commands.Checkout(worktree.WorktreeRepository, commit.Sha, new CheckoutOptions
        {
            CheckoutModifiers = CheckoutModifiers.Force
        });

        // var process = Run(project.Command, new ProcessTaskOptions
        // {
        //     WorkingDirectory = worktreePath,
        // });
        return new WorktreeProcess
        {
            ProcessTask = null,
            DisposeCallback = (ILogger? logger) =>
            {
                var repo = worktree.WorktreeRepository;
                try
                {
                    try
                    {
                        if (worktree is not null) repo.Worktrees.Prune(worktree);
                    }
                    catch (Exception e) { logger?.LogWarning(e, "While pruning worktree."); }

                    try
                    {
                        repo.Refs.Remove(shortsha);
                    }
                    catch (Exception e) { logger?.LogWarning(e, $"While cleaning up ref {shortsha}."); }

                    try
                    {
                        tmp.Delete(true);
                    }
                    catch (Exception e) { logger?.LogWarning(e, $"While removing temp dir {tmp.FullName}"); }
                }
                catch (Exception e)
                {
                    logger?.LogError(e, "While tryna log 😭");
                }
            }
        };
    }
}

public class WorktreeProcess : IDisposable
{
    public required object ProcessTask { get; set; }
    public required Action<ILogger?> DisposeCallback { get; set; }

    public void Dispose()
    {
        DisposeCallback.Invoke(null);
    }
}
