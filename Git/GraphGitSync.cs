using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AgentFlow.Backend.Core.Graph;
using AgentFlow.Backend.Api;

namespace AgentFlow.Backend.Git;

public sealed record DeployResult(string CommitSha, string Branch, DateTimeOffset DeployedAt, string? PrUrl = null);

public sealed class GraphGitSync : IDisposable
{
    private readonly string _repoPath;
    private readonly string _authorName;
    private readonly string _authorEmail;
    private readonly ILogger<GraphGitSync> _log;
    private readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

    public GraphGitSync(IConfiguration config, ILogger<GraphGitSync> log)
    {
        _repoPath = config["Git:RepoPath"] ?? Environment.GetEnvironmentVariable("AGENTFLOW_GIT_REPO") ?? "./git-graphs";
        _authorName = config["Git:AuthorName"] ?? "AgentFlow Bot";
        _authorEmail = config["Git:AuthorEmail"] ?? "bot@agentflow.io";
        _log = log;

        EnsureRepository();
    }

    private void EnsureRepository()
    {
        if (!System.IO.Directory.Exists(_repoPath))
        {
            System.IO.Directory.CreateDirectory(_repoPath);
            Repository.Init(_repoPath);
            _log.LogInformation("Initialized new git repository at {Path}", _repoPath);
        }
    }

    public async Task<DeployResult> DeployAsync(AgentFlow.Backend.Core.Graph.GraphDefinition graph, AgentFlow.Backend.Api.DeployOptions opts, CancellationToken ct = default)
    {
        await Task.CompletedTask;

        using var repo = new Repository(_repoPath);

        var branch = opts.Branch;
        var graphJson = JsonSerializer.Serialize(graph, _jsonOpts);
        var fileName = $"{graph.Id}.json";
        var filePath = System.IO.Path.Combine(_repoPath, fileName);

        await System.IO.File.WriteAllTextAsync(filePath, graphJson, ct);

        Commands.Stage(repo, fileName);

        var message = opts.CommitMessage ?? $"deploy: graph {graph.Id} ({graph.Name}) → {opts.TargetEnvironment}";
        var sig = new Signature(_authorName, _authorEmail, DateTimeOffset.UtcNow);

        LibGit2Sharp.Commit? commit = null;
        try
        {
            commit = repo.Commit(message, sig, sig);
            _log.LogInformation("Committed graph {GraphId} as {CommitSha}", graph.Id, commit.Sha[..8]);
        }
        catch (EmptyCommitException)
        {
            commit = repo.Head.Tip;
            _log.LogDebug("No changes detected for graph {GraphId}, reusing commit {CommitSha}", graph.Id, commit.Sha[..8]);
        }

        return new DeployResult(commit.Sha, branch, DateTimeOffset.UtcNow);
    }

    public Task<IReadOnlyList<string>> GetHistoryAsync(string graphId, int limit = 10, CancellationToken ct = default)
    {
        var results = new List<string>();
        using var repo = new Repository(_repoPath);
        var filter = new CommitFilter { SortBy = CommitSortStrategies.Time };

        int count = 0;
        foreach (var commit in repo.Commits.QueryBy(filter))
        {
            if (count++ >= limit) break;
            if (commit.Message.Contains(graphId))
            {
                results.Add($"{commit.Sha[..8]} | {commit.Author.When:u} | {commit.MessageShort}");
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(results);
    }

    public Task<string?> GetGraphVersionAsync(string graphId, string commitSha, CancellationToken ct = default)
    {
        using var repo = new Repository(_repoPath);
        var commit = repo.Lookup<LibGit2Sharp.Commit>(commitSha);
        if (commit is null) return Task.FromResult<string?>(null);

        var entry = commit.Tree[$"{graphId}.json"];
        if (entry is null) return Task.FromResult<string?>(null);

        var blob = (Blob)entry.Target;
        return Task.FromResult<string?>(blob.GetContentText());
    }

    public void Dispose() { }
}
