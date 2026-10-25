namespace Parchwell.Verifier.Common.Storage;

using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;

/// <summary>Publishes the final immutable worker receipt after host disposal.</summary>
internal interface ICompletionReceiptPublisher
{
    Task PublishAsync(VerifierHostOptions options,string container,string name,byte[] bytes,CancellationToken cancellationToken);
}

internal sealed class BlobCompletionReceiptPublisher : ICompletionReceiptPublisher
{
    public Task PublishAsync(VerifierHostOptions options,string container,string name,byte[] bytes,CancellationToken cancellationToken)
    {
        var store=new BlobVerifierArtifactStore(Program.CreateBlobContainer(options,container),PhaseWireCodec.MaximumBytes,PhaseWireCodec.MaximumBytes);
        return store.CreateAsync(name,bytes,cancellationToken);
    }
}
