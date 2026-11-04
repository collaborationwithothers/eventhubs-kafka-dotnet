using System.Text;
using System.Text.Json;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Ledger;
using Xunit;
using static Parchwell.LoadGen.Tests.Restart.RestartFixture;

namespace Parchwell.LoadGen.Tests.Restart;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed class RestartManifestTests
{
    [Fact]
    public void Given_RestartManifest_When_SerializedAndParsed_Then_RestartObjectIsLastAndRoundTrips()
    {
        byte[] bytes = LedgerValidator.SerializeManifest(Restarted());

        string json = Encoding.UTF8.GetString(bytes);
        Assert.Contains(",\"restart\":{\"predecessor_instance_id\":\"pod-a\",\"planned\":[{\"ordinal\":0,\"predecessor_attempt_id\":\"pod-a-5\",\"tenant_id\":\"tenant\",", json, StringComparison.Ordinal);
        Assert.EndsWith("\"event_type\":\"classified\",\"planned_microseconds\":3000}]}}", json, StringComparison.Ordinal);
        RunManifest parsed = LedgerValidator.DeserializeManifest(bytes);
        Assert.Equal("pod-a", parsed.Restart!.PredecessorInstanceId);
        Assert.Equal(Plan().Planned, parsed.Restart.Planned);
        Assert.Equal(bytes, LedgerValidator.SerializeManifest(parsed));
    }

    [Fact]
    public void Given_FirstInstanceManifest_When_SerializedAndParsed_Then_HasNoRestartField()
    {
        byte[] bytes = LedgerValidator.SerializeManifest(Manifest(First, [Attempt(First, 0, 0, 0)], 1));
        string json = Encoding.UTF8.GetString(bytes);

        Assert.EndsWith("\"guard_observations\":[]}", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"restart\"", json, StringComparison.Ordinal);
        Assert.Null(LedgerValidator.DeserializeManifest(bytes).Restart);
        Assert.Throws<JsonException>(() => LedgerValidator.DeserializeManifest(Encoding.UTF8.GetBytes(
            json.Replace("\"guard_observations\":[]}", "\"guard_observations\":[],\"restart\":null}", StringComparison.Ordinal))));
    }

    [Theory]
    [InlineData("\"restart\":{", "\"restart\":{\"other\":1,")]
    [InlineData("\"predecessor_instance_id\":\"pod-a\",", "")]
    [InlineData("\"planned_microseconds\":3000}", "\"planned_microseconds\":3000,\"other\":1}")]
    public void Given_MalformedRestartObject_When_Parsed_Then_Rejected(string original, string replacement)
    {
        string json = Encoding.UTF8.GetString(LedgerValidator.SerializeManifest(Restarted()));

        Assert.Throws<JsonException>(() => LedgerValidator.DeserializeManifest(Encoding.UTF8.GetBytes(
            json.Replace(original, replacement, StringComparison.Ordinal))));
    }

    [Theory]
    [InlineData("A01")]
    [InlineData("A02")]
    [InlineData("A03")]
    [InlineData("A04")]
    [InlineData("B01")]
    [InlineData("B03")]
    [InlineData("B04")]
    [InlineData("B05")]
    [InlineData("B06")]
    [InlineData("B07")]
    [InlineData("B08")]
    [InlineData("C08")]
    [InlineData("C12")]
    [InlineData("E01")]
    [InlineData("F01")]
    [InlineData("F02")]
    [InlineData("F04")]
    [InlineData("F06")]
    [InlineData("b10")]
    public void Given_RestartManifestOutsideB10_When_Validated_Then_Rejected(string scenarioId)
    {
        RunManifest manifest = Restarted() with { Identity = Second with { ScenarioId = scenarioId } };

        Assert.Equal("restart_manifest_requires_b10", Assert.Throws<InvalidDataException>(() =>
            LedgerValidator.ValidateManifest(manifest)).Message);
    }

    [Theory]
    [InlineData("predecessor_is_self", "restart_predecessor_invalid")]
    [InlineData("predecessor_has_space", "restart_predecessor_invalid")]
    [InlineData("planned_count", "restart_planned_count_mismatch")]
    [InlineData("ordinal_gap", "invalid_restart_planned_entry")]
    [InlineData("duplicate_predecessor_attempt", "invalid_restart_planned_entry")]
    [InlineData("slot_order", "invalid_restart_planned_entry")]
    [InlineData("key_mismatch", "invalid_restart_planned_entry")]
    [InlineData("negative_time", "invalid_restart_planned_entry")]
    [InlineData("inventory_mismatch", "restart_inventory_mismatch")]
    [InlineData("inventory_beyond_plan", "restart_inventory_mismatch")]
    [InlineData("complete_with_suffix", "manifest_unexecuted_suffix_mismatch")]
    public void Given_InvalidRestartManifest_When_Validated_Then_RejectedWithReason(string mutation, string reason)
    {
        RunManifest valid = Restarted();
        RestartManifest plan = Plan();
        RestartPlannedEntry first = plan.Planned[0];
        RestartPlannedEntry second = plan.Planned[1];
        RunManifest manifest = mutation switch
        {
            "predecessor_is_self" => valid with { Restart = plan with { PredecessorInstanceId = "pod-b" } },
            "predecessor_has_space" => valid with { Restart = plan with { PredecessorInstanceId = "pod a" } },
            "planned_count" => valid with { PlannedAttemptCount = 3 },
            "ordinal_gap" => valid with { Restart = plan with { Planned = [first, second with { Ordinal = 2 }] } },
            "duplicate_predecessor_attempt" => valid with { Restart = plan with { Planned = [first, second with { PredecessorAttemptId = "pod-a-5" }] } },
            "slot_order" => valid with { Restart = plan with { Planned = [second with { Ordinal = 0 }, first with { Ordinal = 1 }] } },
            "key_mismatch" => valid with { Restart = plan with { Planned = [first with { Key = "other-d0" }, second] } },
            "negative_time" => valid with { Restart = plan with { Planned = [first with { PlannedMicroseconds = -1 }, second] } },
            "inventory_mismatch" => Manifest(Second, [Resend(0, 2), Resend(1, 4)], 2, plan),
            "inventory_beyond_plan" => Manifest(Second, [Resend(0, 2), Resend(1, 3), Resend(2, 4)], 2, plan),
            "complete_with_suffix" => Manifest(Second, [Resend(0, 2)], 2, plan),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };

        Assert.Equal(reason, Assert.Throws<InvalidDataException>(() => LedgerValidator.ValidateManifest(manifest)).Message);
    }

    [Fact]
    public void Given_PartialOrEmptyRestartManifest_When_Validated_Then_Accepted()
    {
        RunManifest partial = Manifest(Second, [Resend(0, 2)], 2, Plan()) with
        {
            WorkerStatus = "incomplete",
            BookkeepingComplete = false,
            FirstUnexecutedOrdinal = 1
        };
        RunManifest empty = Manifest(Second, [], 0, new RestartManifest("pod-a", []));

        LedgerValidator.ValidateManifest(partial);
        LedgerValidator.ValidateManifest(empty);
        Assert.Equal("manifest_unexecuted_suffix_mismatch", Assert.Throws<InvalidDataException>(() =>
            LedgerValidator.ValidateManifest(empty with { Restart = null })).Message);
    }

    [Fact]
    public void Given_RestartPlan_When_RegisteringInventory_Then_AdmitsOnlyPlanEntriesInOrder()
    {
        var inventory = new AttemptInventory(Second, 4, Plan());
        var outOfOrder = new AttemptInventory(Second, 4, Plan());

        inventory.Register(Resend(0, 2));
        Assert.Equal("restart_inventory_mismatch", Assert.Throws<InvalidDataException>(() =>
            inventory.Register(Resend(1, 2))).Message);
        inventory.Register(Resend(1, 3));
        Assert.Equal("restart_inventory_mismatch", Assert.Throws<InvalidDataException>(() =>
            inventory.Register(Resend(2, 4))).Message);
        Assert.Equal(2, inventory.Seal().Length);
        Assert.Equal("restart_inventory_mismatch", Assert.Throws<InvalidDataException>(() =>
            outOfOrder.Register(Resend(0, 3))).Message);
    }
}
