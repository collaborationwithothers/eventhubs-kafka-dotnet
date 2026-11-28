namespace Parchwell.Enrichment.ConsumerProcessing;

internal interface IConsumerRecordStore
{
    Task RegisterAsync(byte[] json, CancellationToken cancellationToken);
    Task AppendAsync(byte[] jsonLine, CancellationToken cancellationToken);
    Task CompleteAsync(byte[] json, CancellationToken cancellationToken);
    Task WriteStopReceiptAsync(byte[] json, CancellationToken cancellationToken);
}
