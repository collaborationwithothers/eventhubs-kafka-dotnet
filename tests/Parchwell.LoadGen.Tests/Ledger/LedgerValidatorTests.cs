using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Ledger;
using Xunit;
#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

namespace Parchwell.LoadGen.Tests.Ledger;

public sealed class LedgerValidatorTests
{
    private static readonly RunIdentity Identity = new("a01", "default", "run-1", "instance-1", "instance-1", "client-1");
    private static readonly AttemptEntry Entry = new("instance-1", "attempt-1", 0, "tenant-1", "d0", "tenant-1-d0", 0, "created", 100, 110);

    [Fact]
    public void Given_valid_row_When_serialized_Then_schema_uses_snake_case_and_round_trips()
    {
        var row = CallbackRow();
        var bytes = LedgerValidator.SerializeRow(row);
        var json = Encoding.UTF8.GetString(bytes);

        Assert.Contains("\"schema_version\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"completion_source\":\"callback\"", json, StringComparison.Ordinal);
        Assert.Equal(row, LedgerValidator.DeserializeRow(bytes));
    }

    [Theory]
    [InlineData("\"schema_version\":1", "\"schema_version\":1,\"schema_version\":1")]
    [InlineData("\"client_id\":\"client-1\"", "\"client_id\":\"client-1\",\"unknown\":true")]
    [InlineData("\"key\":\"tenant-1-d0\",", "")]
    public void Given_malformed_row_envelope_When_decoded_Then_rejected(string original, string replacement)
    {
        var json = Encoding.UTF8.GetString(LedgerValidator.SerializeRow(CallbackRow()));
        var malformed = json.Replace(original, replacement, StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => LedgerValidator.DeserializeRow(Encoding.UTF8.GetBytes(malformed)));
    }

    [Fact]
    public void Given_callback_without_native_status_When_validated_Then_missing_provenance_is_preserved()
    {
        var row = CallbackRow() with { PersistenceStatus = null, PersistenceProvenance = "missing" };
        Assert.Equal(row, LedgerValidator.DeserializeRow(LedgerValidator.SerializeRow(row)));
    }

    [Fact]
    public void Given_inventory_with_duplicate_attempt_or_ordinal_When_registered_Then_rejected()
    {
        var inventory = new AttemptInventory(Identity, 2);
        inventory.Register(Entry);

        Assert.Throws<InvalidOperationException>(() => inventory.Register(Entry with { AttemptId = "attempt-2" }));
        Assert.Throws<InvalidOperationException>(() => inventory.Register(Entry with { Ordinal = 1 }));
    }

    [Fact]
    public void Given_rejected_duplicate_ordinal_When_reusing_attempt_id_at_new_ordinal_Then_registration_succeeds()
    {
        var inventory = new AttemptInventory(Identity, 3);
        inventory.Register(Entry);

        Assert.Throws<InvalidOperationException>(() => inventory.Register(Entry with { AttemptId = "attempt-2", Ordinal = 0 }));
        inventory.Register(Entry with { AttemptId = "attempt-2", Ordinal = 1 });

        Assert.Equal(2, inventory.Seal().Length);
    }

    [Fact]
    public void Given_sealed_inventory_When_registration_attempted_Then_snapshot_remains_unchanged()
    {
        var inventory = new AttemptInventory(Identity, 1);
        inventory.Register(Entry);
        var sealedEntries = inventory.Seal();

        Assert.Single(sealedEntries);
        Assert.Throws<InvalidOperationException>(() => inventory.Register(Entry with { AttemptId = "later", Ordinal = 1 }));
        Assert.Single(inventory.Seal());
    }

    [Fact]
    public void Given_ledger_row_not_equal_to_independent_inventory_When_reconciled_Then_rejected()
    {
        var manifest = Manifest([Entry]);
        var wrongRow = CallbackRow() with { Attempt = Entry with { Key = "wrong-key" } };

        Assert.Throws<InvalidDataException>(() => LedgerValidator.Validate([wrongRow], manifest, Identity));
        Assert.Throws<InvalidDataException>(() => LedgerValidator.Validate([], manifest, Identity));
    }

    [Fact]
    public void Given_manifest_with_unknown_nested_identity_property_When_decoded_Then_rejected()
    {
        var json = Encoding.UTF8.GetString(LedgerValidator.SerializeManifest(Manifest([Entry])));
        json = json.Replace("\"client_id\":\"client-1\"", "\"client_id\":\"client-1\",\"other\":false", StringComparison.Ordinal);

        Assert.Throws<JsonException>(() => LedgerValidator.DeserializeManifest(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void Given_manifest_inventory_above_entry_budget_When_decoded_Then_rejected_before_model_creation()
    {
        var bytes = LedgerValidator.SerializeManifest(Manifest([Entry]));

        Assert.Throws<InvalidDataException>(() => LedgerValidator.DeserializeManifest(bytes, 0));
    }

    internal static LedgerRow CallbackRow() => new(1, Identity, Entry, "callback", true, 0, null, 2,
        "native_callback", 150, 0.04, null);

    [Fact]
    public void Given_UnverifiedBookkeepingOrGenerator_When_ValidatingCompleteManifest_Then_Rejects()
    {
        var manifest = Manifest([Entry]);
        Assert.Throws<InvalidDataException>(() => LedgerValidator.ValidateManifest(manifest with { BookkeepingComplete = false }));
        Assert.Throws<InvalidDataException>(() => LedgerValidator.ValidateManifest(manifest with { GeneratorVersion = "unknown" }));
    }

    [Fact]
    public void Given_SkippedInvocationOrdinal_When_Registered_Then_RejectsWithoutReservingTheAttempt()
    {
        var inventory = new AttemptInventory(Identity, 3);
        inventory.Register(Entry);
        Assert.Throws<InvalidOperationException>(() => inventory.Register(Entry with { AttemptId = "attempt-2", Ordinal = 2 }));
        inventory.Register(Entry with { AttemptId = "attempt-2", Ordinal = 1 });
        Assert.Equal(new long[] { 0, 1 }, inventory.Seal().Select(item => item.Ordinal));
    }

    // A stopped schedule preserves the next unexecuted opportunity independently of ledger rows.
    [Theory]
    [InlineData(null)]
    [InlineData(4L)]
    public void Given_InconsistentUnexecutedSuffix_When_ValidatingManifest_Then_Rejects(long? firstUnexecuted)
    {
        var manifest = Manifest([Entry]) with
        {
            PlannedAttemptCount = 5,
            WorkerStatus = "incomplete",
            FirstUnexecutedOrdinal = firstUnexecuted
        };
        Assert.Throws<InvalidDataException>(() => LedgerValidator.ValidateManifest(manifest));
    }

    [Fact]
    public void Given_ContiguousPartialInventory_When_ValidatingManifest_Then_PreservesExactSuffix()
    {
        var manifest = Manifest([Entry]) with
        {
            PlannedAttemptCount = 5,
            WorkerStatus = "incomplete",
            FirstUnexecutedOrdinal = 1
        };
        Assert.Equal([CallbackRow()], LedgerValidator.Validate([CallbackRow()], manifest, Identity));
        var sparse = manifest with { Inventory = [Entry with { Ordinal = 1 }] };
        Assert.Throws<InvalidDataException>(() => LedgerValidator.ValidateManifest(sparse));
    }

    internal static RunManifest Manifest(ImmutableArray<AttemptEntry> entries) => new(1, Identity, "1", entries.Length,
        entries.Length, entries, 1_000_000, 0, "complete", true, null, [],
        ImmutableDictionary<string, string>.Empty, ImmutableDictionary<string, string>.Empty, []);
}
