#pragma warning disable CA1707 // Repository-required Given/When/Then test names.
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Runner.Run.Deployment;
using Xunit;

namespace Parchwell.Runner.Tests.Deployment;

public sealed class DeploymentRuntimeLimitsTests
{
    public static TheoryData<string, string> RequiredFields
    {
        get
        {
            var cases = new TheoryData<string, string>();
            foreach ((string section, JsonObject fields) in Sections())
                foreach (string propertyName in fields.Select(pair => pair.Key)) cases.Add(section, propertyName);
            return cases;
        }
    }

    public static TheoryData<string> RequiredSections
    {
        get
        {
            var cases = new TheoryData<string>();
            foreach ((string section, _) in Sections()) cases.Add(section);
            return cases;
        }
    }

    public static TheoryData<string, string> NumericalFields
    {
        get
        {
            var cases = new TheoryData<string, string>();
            foreach ((string section, JsonObject fields) in Sections())
                foreach ((string propertyName, JsonNode? value) in fields)
                    if (value!.GetValueKind() == JsonValueKind.Number) cases.Add(section, propertyName);
            return cases;
        }
    }

    public static TheoryData<string, string> DurationFields
    {
        get
        {
            var cases = new TheoryData<string, string>();
            foreach ((string section, JsonObject fields) in Sections())
                foreach ((string propertyName, JsonNode? value) in fields)
                    if (value!.GetValueKind() == JsonValueKind.String && TimeSpan.TryParse(value.GetValue<string>(), CultureInfo.InvariantCulture, out _))
                        cases.Add(section, propertyName);
            return cases;
        }
    }

    public static TheoryData<string, string, string> IntegerFields
    {
        get
        {
            var cases = new TheoryData<string, string, string>();
            string[] longFields = ["MaxLedgerReadbackBytes", "MaxArtifactBytes", "MaxAggregateArtifactBytes", "MaxOutputBytes",
                "MaxConsumedRecords", "MaxConsumedBytes", "MaxTotalInputBytes", "MaxRowsPerArtifact", "MaxInventoryEntries", "phase_evidence_bytes"];
            foreach ((string section, JsonObject fields) in Sections())
                foreach ((string propertyName, JsonNode? value) in fields)
                    if (value!.GetValueKind() == JsonValueKind.Number && propertyName != "item_limit_gbp")
                        cases.Add(section, propertyName, (longFields.Contains(propertyName, StringComparer.Ordinal) ? long.MaxValue : int.MaxValue).ToString(CultureInfo.InvariantCulture));
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(RequiredFields))]
    public void Given_AnyMissingDeploymentField_When_Validated_Then_ItCannotDefaultSilently(string section, string field)
    {
        JsonObject limits = Limits();
        limits[section]!.AsObject().Remove(field);
        Assert.Throws<InvalidDataException>(() => Validate(limits));
    }

    [Theory]
    [MemberData(nameof(RequiredFields))]
    public void Given_AnyDeploymentFieldWithWrongType_When_Validated_Then_ItIsRejected(string section, string field)
    {
        JsonObject limits = Limits();
        limits[section]![field] = new JsonObject();
        Assert.Throws<InvalidDataException>(() => Validate(limits));
        limits[section]![field] = null;
        Assert.Throws<InvalidDataException>(() => Validate(limits));
    }

    [Theory]
    [MemberData(nameof(RequiredSections))]
    public void Given_AnyMissingOrMalformedDeploymentSection_When_Validated_Then_ItIsRejected(string section)
    {
        JsonObject limits = Limits();
        limits.Remove(section);
        Assert.Throws<InvalidDataException>(() => Validate(limits));
        limits[section] = 1;
        Assert.Throws<InvalidDataException>(() => Validate(limits));
        limits[section] = new JsonObject();
        Assert.Throws<InvalidDataException>(() => Validate(limits));
    }

    [Theory]
    [MemberData(nameof(NumericalFields))]
    public void Given_NumericalLimitBelowAtOrAboveZero_When_Validated_Then_PositiveBoundsAndZeroReplacementsArePreserved(string section, string field)
    {
        JsonObject limits = Limits();
        limits[section]![field] = -1;
        Assert.Throws<InvalidDataException>(() => Validate(limits));
        limits[section]![field] = 0;
        if (field == "replacement_repeats") Validate(limits);
        else Assert.Throws<InvalidDataException>(() => Validate(limits));
        limits[section]![field] = 1;
        Validate(limits);
        limits[section]![field] = "1";
        Assert.Throws<InvalidDataException>(() => Validate(limits));
        if (field == "item_limit_gbp") return;
        limits[section]![field] = 1.5;
        Assert.Throws<InvalidDataException>(() => Validate(limits));
    }

    [Theory]
    [MemberData(nameof(IntegerFields))]
    public void Given_IntegerLimitAroundItsRepresentationMaximum_When_Validated_Then_OnlyRepresentableValuesAreAccepted(string section, string field, string maximum)
    {
        JsonObject limits = Limits();
        BigInteger bound = BigInteger.Parse(maximum, CultureInfo.InvariantCulture);
        foreach (int offset in new[] { -1, 0, 1 })
        {
            limits[section]![field] = JsonNode.Parse((bound + offset).ToString(CultureInfo.InvariantCulture));
            if (offset <= 0) Validate(limits);
            else Assert.Throws<InvalidDataException>(() => Validate(limits));
        }
    }

    [Theory]
    [MemberData(nameof(DurationFields))]
    public void Given_DurationBelowAtOrAboveZero_When_Validated_Then_OnlyScheduleLatenessMayBeZero(string section, string field)
    {
        JsonObject limits = Limits();
        foreach (long ticks in new long[] { -1, 0, 1 })
        {
            limits[section]![field] = TimeSpan.FromTicks(ticks).ToString("c", CultureInfo.InvariantCulture);
            if (ticks > 0 || (ticks == 0 && field == "MaxScheduleLateness")) Validate(limits);
            else Assert.Throws<InvalidDataException>(() => Validate(limits));
        }
        limits[section]![field] = "not-a-duration";
        Assert.Throws<InvalidDataException>(() => Validate(limits));
    }

    [Theory]
    [MemberData(nameof(DurationFields))]
    public void Given_DurationAroundExistingPreflightMaximum_When_Validated_Then_TheTimeoutRepresentationIsPreserved(string section, string field)
    {
        JsonObject limits = Limits();
        long maximumTicks = (long)int.MaxValue * TimeSpan.TicksPerMillisecond;
        foreach (long offset in new long[] { -1, 0, 1 })
        {
            limits[section]![field] = TimeSpan.FromTicks(maximumTicks + offset).ToString("c", CultureInfo.InvariantCulture);
            if (offset <= 0) Validate(limits);
            else Assert.Throws<InvalidDataException>(() => Validate(limits));
        }
    }

    [Theory]
    [InlineData("[]", false)]
    [InlineData("[-1]", false)]
    [InlineData("[0]", false)]
    [InlineData("[1]", true)]
    [InlineData("[1,0]", false)]
    [InlineData("[2147483647]", true)]
    [InlineData("[2147483648]", false)]
    [InlineData("[1.5]", false)]
    [InlineData("[\"1\"]", false)]
    public void Given_ReadDelayArray_When_Validated_Then_EachDelayRequiresAPositiveRepresentableBound(string json, bool valid)
    {
        JsonObject limits = Limits();
        limits["execution"]!["read_delay_seconds"] = JsonNode.Parse(json);
        if (valid) Validate(limits);
        else Assert.Throws<InvalidDataException>(() => Validate(limits));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"schema_version\":null}")]
    [InlineData("{\"schema_version\":\"1\"}")]
    [InlineData("{\"schema_version\":0}")]
    [InlineData("{\"schema_version\":2}")]
    public void Given_MissingOrInvalidSchema_When_Validated_Then_ItIsRejected(string json)
    {
        using JsonDocument limits = JsonDocument.Parse(json);
        Assert.Throws<InvalidDataException>(() => DeploymentRuntimeLimits.Validate(limits.RootElement));
    }

    private static JsonObject Limits() => JsonNode.Parse(File.ReadAllText(Path.Combine(DeploymentFixture.RepositoryRoot, "docs/operations/a01-runtime-limits.json")))!.AsObject();
    private static IEnumerable<(string Section, JsonObject Fields)> Sections() => Limits()
        .Where(pair => pair.Value is JsonObject).Select(pair => (pair.Key, pair.Value!.AsObject()));
    private static void Validate(JsonObject limits) => DeploymentRuntimeLimits.Validate(JsonSerializer.SerializeToElement(limits));
}
