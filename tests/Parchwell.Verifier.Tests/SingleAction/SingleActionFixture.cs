using System.Text;
using System.Text.Json;
using Parchwell.Scenarios;
using Parchwell.Kafka.Configuration;
using Parchwell.Verifier.Common.SingleAction;
using Parchwell.Verifier.VerifyRun.SingleAction;

namespace Parchwell.Verifier.Tests.SingleAction;

internal static class SingleActionFixture
{
    internal const string Run="run1", Source="1111111111111111111111111111111111111111", Pod="run1-b01-c0-single-abc", Uid="uid1";
    internal static readonly SingleActionConfiguration Declaration=new("produce_one",KafkaClientRole.LoadGenProducer,"document-events",0,1000,330000,"namespace","none");
    internal static readonly Dictionary<string,string> Settings=new(StringComparer.Ordinal){["client.id"]=Pod+"-r1",["a"]="b"};
    internal static JsonElement Json(string json)=>JsonDocument.Parse(json).RootElement.Clone();
    internal static SingleActionRegistration Registration()=>new(1,"single-action",new("B01","C0",Run,Pod),"run1-b01-c0-single",Pod,Uid,Source);
    internal static SingleActionCompletion Completion()=>new(1,"single-action",Registration().Identity,Registration().JobName,Pod,Uid,Source,5,0,true);
    internal static SingleActionRepeat Repeat(int ordinal=1)
    {
        string client=$"{Pod}-r{ordinal}";
        return new(ordinal,client,"produce_one",true,10,1000,new(StringComparer.Ordinal){["client.id"]=client,["a"]="b"},[],Json("\"success\""),Json("null"),Json("null"),[],[Statistic(client,1,1,1)],[]);
    }
    internal static SingleActionStatistic Statistic(string client,long time,long ts,int sequence,string brokers="{}",long observed=100)=>new(sequence,observed,Convert.ToBase64String(Encoding.UTF8.GetBytes($$"""{"name":"{{client}}#producer-1","client_id":"{{client}}","type":"producer","time":{{time}},"ts":{{ts}},"brokers":{{brokers}}} """)));
    internal static SingleActionCapture Capture()=>new(1,"single-action",true,Registration().Identity,Registration().JobName,Pod,Uid,Source,"2.16.0","2.16.0",Enumerable.Range(1,5).Select(Repeat).ToArray());
    internal static SingleActionRepeatProjection Judge(SingleActionRepeat repeat,SingleActionConfiguration? declaration=null)=>SingleActionRepeatJudgement.Judge(repeat,declaration??Declaration,new Dictionary<string,string>(StringComparer.Ordinal){["client.id"]=repeat.ClientId,["a"]="b"},[],Run);
    internal static SingleActionCandidate Candidate(string item="B01",string configuration="C0",string result="success",string? origin=null)
    {
        SingleActionOutcome outcome=result=="success"?new(result,null,null,null):new(result,29,false,origin??"broker");
        return new(1,"single-action",true,Run,item,configuration,Source,"2.16.0","2.16.0",new(StringComparer.Ordinal){["loadgen"]="sha256:"+new string('1',64),["verifier"]="sha256:"+new string('2',64),["enrichment"]="sha256:"+new string('3',64)},"complete_valid",Enumerable.Range(1,5).Select(i=>new SingleActionRepeatProjection(i,"complete_valid",[],outcome,0,new(1,1,1,new string('a',64)))).ToArray(),true,outcome,"not_applicable",[],false,null);
    }
    internal static SingleActionJudgeReceipt Receipt(string item="B01",string configuration="C0")=>new(1,"single-action-judge",Run,item,configuration,Source,SingleActionPaths.JudgeJob(Run,item,configuration),"judge-pod","judge-uid",SingleActionPaths.Candidate(Run,item,configuration),0,true);
    internal static SingleActionSeedReceipt Seed()=>new(1,"single-action-seed",Run,Source,"run1-f04-seed","seed-pod","seed-uid","enrichment-consumer","document-state",0,Convert.ToBase64String(Encoding.UTF8.GetBytes("f04-seed:run1")),"68fe9c9f499dd1a6bf07c6f737c2b6a8118cca37877336ee0f2674209554d3c6",true,Json("null"),0,true);
    internal static SingleActionSeedPod SeedPod()=>new(1,"run1-f04-seed","seed-pod","seed-uid","enrichment-consumer","Job","run1-f04-seed");
}
