using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace Parchwell.Enrichment.SeedF04;

public sealed class SeedF04Worker(IOptions<SeedF04Options> options,
    Func<IProducer<byte[], byte[]>> producerFactory, TimeProvider clock)
{
    public async Task<SeedF04Result> ExecuteAsync(CancellationToken cancellationToken)
    {
        SeedF04Options configuration = options.Value;
        configuration.Validate();
        IProducer<byte[], byte[]>? producer = null;
        bool delivered = false;
        bool cleanupCompleted = true;
        SeedF04Error? error = null;
        bool construction = true;
        try
        {
            producer = producerFactory();
            construction = false;
            var terminal = new TaskCompletionSource<(DeliveryReport<byte[], byte[]> Report, TimeSpan Elapsed)>(TaskCreationOptions.RunContinuationsAsynchronously);
            long actionStarted = clock.GetTimestamp();
            using var actionDeadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(configuration.ActionWindowMs), clock);
            using var actionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, actionDeadline.Token);
            // One native Produce call, no application retries. Callback data is the measured terminal result.
            producer.Produce(new TopicPartition("document-state", new Partition(0)), new Message<byte[], byte[]>
            {
                Key = Encoding.UTF8.GetBytes("f04-seed:" + configuration.RunId),
                Value = Encoding.ASCII.GetBytes(new string('s', 1000))
            }, report => terminal.TrySetResult((report, clock.GetElapsedTime(actionStarted))));
            var observed = await terminal.Task.WaitAsync(actionCancellation.Token).ConfigureAwait(false);
            if (observed.Elapsed <= TimeSpan.FromMilliseconds(configuration.ActionWindowMs))
            {
                DeliveryReport<byte[], byte[]> report = observed.Report;
                delivered = !report.Error.IsError;
                if (report.Error.IsError) error = MeasuredError(report.Error, false);
            }
        }
        catch (KafkaException exception)
        {
            error = MeasuredError(exception.Error, construction);
        }
        catch (Exception)
        {
            // Cancellation and code-less failures cannot manufacture a measured native error tuple.
        }
        finally
        {
            if (producer is not null)
            {
                using var cleanupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(configuration.CleanupSeconds), clock);
                try
                {
                    await Task.Run(() => producer.Flush(cleanupDeadline.Token), CancellationToken.None)
                        .WaitAsync(cleanupDeadline.Token).ConfigureAwait(false);
                }
                catch (Exception) { cleanupCompleted = false; }
                try
                {
                    await Task.Run(producer.Dispose, CancellationToken.None).WaitAsync(cleanupDeadline.Token).ConfigureAwait(false);
                }
                catch (Exception) { cleanupCompleted = false; }
            }
        }
        return new SeedF04Result(delivered, error, delivered && error is null && cleanupCompleted ? 0 : 1, cleanupCompleted);
    }

    private static SeedF04Error MeasuredError(Error error, bool construction) => new(
        (int)error.Code, error.IsFatal, construction ? "construction" : error.IsLocalError ? "local" : "broker", error.Reason);
}
