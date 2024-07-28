using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace BTModMerger.Core.Utils;

// Mostly from https://stackoverflow.com/a/74948883/6078677

public class ParallelExecutionException<T> : Exception
{
    internal ParallelExecutionException(T item, Exception innerException) : base(innerException.Message, innerException)
    {
        Item = item;
    }

    public T Item { get; }
}

public static class AsyncEnumerableExtensions
{
    private static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return await Task.FromResult(item);
        }
    }

    public static IAsyncEnumerable<TInput> AsParallelAsync<TInput>(this IEnumerable<TInput> source, int maxDegreeOfParallelism, Func<TInput, CancellationToken, Task> body, bool aggregateException = true, CancellationToken cancellationToken = default)
    {
        return source.AsAsyncEnumerable().AsParallelAsync(maxDegreeOfParallelism, body, aggregateException, cancellationToken);
    }

    public static IAsyncEnumerable<TOutput> AsParallelAsync<TInput, TOutput>(this IEnumerable<TInput> source, int maxDegreeOfParallelism, Func<TInput, CancellationToken, Task<TOutput>> body, bool aggregateException = true, CancellationToken cancellationToken = default)
    {
        return source.AsAsyncEnumerable().AsParallelAsync(maxDegreeOfParallelism, body, aggregateException, cancellationToken);
    }

    public static IAsyncEnumerable<TInput> AsParallelAsync<TInput>(this IAsyncEnumerable<TInput> source, int maxDegreeOfParallelism, Func<TInput, CancellationToken, Task> body, bool aggregateException = true, CancellationToken cancellationToken = default)
    {
        return source.AsParallelAsync(maxDegreeOfParallelism, async (e, ct) => { await body(e, ct); return e; }, aggregateException, cancellationToken);
    }

    public static async IAsyncEnumerable<TOutput> AsParallelAsync<TInput, TOutput>(this IAsyncEnumerable<TInput> source, int maxDegreeOfParallelism, Func<TInput, CancellationToken, Task<TOutput>> body, bool aggregateException = true, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDegreeOfParallelism, 1);

        var channelOptions = new BoundedChannelOptions(maxDegreeOfParallelism) { SingleReader = true };
        var channel = Channel.CreateBounded<TOutput>(channelOptions);

        var parallelExecutionTask = Task.Run(async () =>
        {
            var exceptions = new List<Exception>();
            var writer = channel.Writer;
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                CancellationToken = cancellationToken,
            };

            try
            {
                await Parallel.ForEachAsync(source, parallelOptions, async (item, ct) =>
                {
                    try
                    {
                        var result = await body(item, ct);
                        await writer.WriteAsync(result, ct);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        var parallelExecutionException = new ParallelExecutionException<TInput>(item, exception);
                        if (aggregateException)
                        {
                            exceptions.Add(parallelExecutionException);
                        }
                        else
                        {
                            writer.Complete(parallelExecutionException);
                        }
                    }
                });
                writer.Complete(exceptions.Count > 0 ? new AggregateException(exceptions) : null);
            }
            catch (OperationCanceledException exception)
            {
                writer.Complete(exception);
            }
        }, cancellationToken);

        await foreach (var result in channel.Reader.ReadAllAsync(CancellationToken.None))
        {
            yield return result;
        }

        await parallelExecutionTask;
    }
}
