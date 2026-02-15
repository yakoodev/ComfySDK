namespace ComfySdk.Abstractions;

/// <summary>Applies authentication data to outgoing HTTP requests.</summary>
public interface IAuthProvider
{
    /// <summary>Mutates request with auth headers/cookies/query.</summary>
    ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}
