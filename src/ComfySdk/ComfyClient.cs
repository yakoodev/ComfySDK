using ComfySdk.Models;
using ComfySdk.Options;

namespace ComfySdk;

/// <summary>Top-level SDK client for submitting workflows and tracking execution.</summary>
public class ComfyClient
{
    private readonly ComfyClientOptions _options;

    /// <summary>Creates client with explicit options.</summary>
    public ComfyClient(ComfyClientOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Streams normalized run events for a workflow parameters object.</summary>
    public async IAsyncEnumerable<RunEvent> RunStreamAsync(
        object parameters,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        yield return new RunEvent(RunEventType.Connected, "Client scaffold initialized.");
        await Task.CompletedTask;
        _ = cancellationToken;
    }

    /// <summary>Runs workflow and returns final outputs once terminal state is reached.</summary>
    public Task<RunResult> RunAsync(object parameters, CancellationToken cancellationToken = default)
    {
        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        _ = cancellationToken;
        throw new NotImplementedException("Run pipeline is planned in tasks A2-A3/B/C and is not implemented in repository scaffold.");
    }
}
