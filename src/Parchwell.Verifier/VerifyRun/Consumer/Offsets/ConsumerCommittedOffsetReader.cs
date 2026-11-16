using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Verifier.Common.Consumer;

namespace Parchwell.Verifier.VerifyRun.Consumer.Offsets;

/// <summary>Observes the explicit run partition set once without joining the consumer group.</summary>
/// <remarks>
/// Confluent.Kafka at 56ef87df3402861f0dc72ef921fcfac702ae1f16:
/// Consumer.cs Committed(IEnumerable, TimeSpan) delegates directly to SafeKafkaHandle.Committed;
/// Impl/SafeKafkaHandle.cs maps request and partition errors to KafkaException and
/// TopicPartitionOffsetException respectively. Sources:
/// https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/Consumer.cs#L541-L544
/// https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/Impl/SafeKafkaHandle.cs#L1104-L1134
/// </remarks>
internal sealed class ConsumerCommittedOffsetReader
{
    private readonly Func<DateTimeOffset, CancellationToken, ConsumerLease> create;
    private readonly TimeProvider clock;

    public ConsumerCommittedOffsetReader(KafkaConfigurationFactory configurations, KafkaAuthenticationFactory authentication,
        IOptions<VerifierHostOptions> configured, ConsumerVerificationPlan plan, TimeProvider clock)
        : this(configurations,authentication,configured,plan,clock,(config,session)=>authentication.CreateConsumerBuilder<byte[],byte[]>(config,session)
            .SetKeyDeserializer(Deserializers.ByteArray).SetValueDeserializer(Deserializers.ByteArray).Build())
    {
    }

    internal ConsumerCommittedOffsetReader(KafkaConfigurationFactory configurations, KafkaAuthenticationFactory authentication,
        IOptions<VerifierHostOptions> configured, ConsumerVerificationPlan plan, TimeProvider clock,
        Func<ConsumerConfig,KafkaOAuthBearerSession,IConsumer<byte[],byte[]>> build)
    {
        this.clock=clock;
        create=(deadline, token)=>
        {
            VerifierHostOptions options=configured.Value;
            var acquisition=CancellationTokenSource.CreateLinkedTokenSource(token);
            IConsumer<byte[],byte[]>? consumer=null;
            try
            {
                TimeSpan remaining=deadline-clock.GetUtcNow();
                if(remaining<=TimeSpan.Zero)throw new KafkaException(new Error(ErrorCode.Local_TimedOut));
                TimeSpan installBudget=remaining<options.TokenTimeout!.Value?remaining:options.TokenTimeout.Value;
                using var installDeadline=new CancellationTokenSource(installBudget,clock);
                using CancellationTokenRegistration cancellation=installDeadline.Token.Register(acquisition.Cancel);
                KafkaOAuthBearerSession session=authentication.CreateSession(new KafkaAuthenticationIdentity(options.ScenarioId!,
                    options.RunId!,KafkaClientRole.VerifierConsumer,options.VerifierClientId!),acquisition.Token);
                ConsumerConfig config=ConfigurationForRead(configurations.CreateVerifierConsumer().Configuration,options,plan);
                consumer=build(config,session);
                session.InstallToken(consumer);
                token.ThrowIfCancellationRequested();
                if(installDeadline.IsCancellationRequested||session.InitialFailure is not null||session.InstalledTokenExpiresOn is null)
                    throw new KafkaException(new Error(ErrorCode.Local_TimedOut));
                return new ConsumerLease(consumer,acquisition);
            }
            catch
            {
                try{consumer?.Dispose();}finally{acquisition.Dispose();}
                throw;
            }
        };
    }

    internal ConsumerCommittedOffsetReader(Func<CancellationToken,IConsumer<byte[],byte[]>> create,TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(create);
        this.clock=clock;
        this.create=(_,token)=>new ConsumerLease(create(token),null);
    }

    internal static ConsumerConfig ConfigurationForRead(ConsumerConfig baseline,VerifierHostOptions options,ConsumerVerificationPlan plan)
    {
        var config=new ConsumerConfig(baseline.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal))
        {GroupId=ConsumerPlanValidation.GroupId(options),EnableAutoCommit=false};
        config.Set("isolation.level",plan.SubjectSettings["isolation.level"]);
        return config;
    }

    internal Task<ConsumerOffsetRead> ReadAsync(IReadOnlyList<int> partitions,DateTimeOffset deadline,CancellationToken token)
    {
        int[] requested=partitions.Order().ToArray();
        if(deadline<=clock.GetUtcNow())return Task.FromResult(new ConsumerOffsetRead("not_attempted",requested,null,null,null,[]));
        DateTimeOffset started=clock.GetUtcNow();
        string outcome="success";int? error=0;IReadOnlyList<ConsumerObservedOffset> offsets=[];
        try
        {
            token.ThrowIfCancellationRequested();
            using ConsumerLease lease=create(deadline,token);
            TimeSpan remaining=deadline-clock.GetUtcNow();
            if(remaining<=TimeSpan.Zero)throw new KafkaException(new Error(ErrorCode.Local_TimedOut));
            List<TopicPartitionOffset> returned=lease.Consumer.Committed(requested.Select(partition=>new TopicPartition("document-events",partition)),remaining);
            offsets=returned.Select(value=>new ConsumerObservedOffset(value.Partition.Value,value.Offset.Value,0)).ToArray();
            if(returned.Count!=requested.Length||returned.Any(value=>value.Topic!="document-events")||
                !returned.Select(value=>value.Partition.Value).Order().SequenceEqual(requested))outcome="invalid_response";
        }
        catch(TopicPartitionOffsetException exception)
        {
            outcome="partition_exception";error=(int)exception.Error.Code;
            offsets=exception.Results.Select(value=>new ConsumerObservedOffset(value.Partition.Value,value.Offset.Value,(int)value.Error.Code)).ToArray();
        }
        catch(KafkaException exception){outcome="kafka_exception";error=(int)exception.Error.Code;}
        catch(OperationCanceledException)when(!token.IsCancellationRequested){outcome="kafka_exception";error=(int)ErrorCode.Local_TimedOut;}
        return Task.FromResult(new ConsumerOffsetRead(outcome,requested,started,clock.GetUtcNow(),error,offsets));
    }

    private sealed class ConsumerLease(IConsumer<byte[],byte[]> consumer,CancellationTokenSource? acquisition):IDisposable
    {
        internal IConsumer<byte[],byte[]> Consumer {get;}=consumer;
        public void Dispose(){try{Consumer.Dispose();}finally{acquisition?.Dispose();}}
    }
}
