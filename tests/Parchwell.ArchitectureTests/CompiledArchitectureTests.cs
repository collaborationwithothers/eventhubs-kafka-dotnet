using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Extensions;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Loader;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

// The repository requires Given_When_Then test names (docs/engineering/dotnet.md).
#pragma warning disable CA1707

namespace Parchwell.ArchitectureTests;

public sealed class CompiledArchitectureTests
{
    private static readonly Lazy<Architecture> Architecture = new(() =>
    {
        var assemblies = ProjectRules.SourceNames.Select(System.Reflection.Assembly.Load).ToArray();
        Assert.Equal(ProjectRules.SourceNames, assemblies.Select(assembly => assembly.GetName().Name));
        return new ArchLoader().LoadAssemblies(assemblies).Build();
    });

    [Fact]
    public void Given_SourceAssemblies_When_CompiledDependenciesAreChecked_Then_KafkaHasNoParchwellDependencies()
    {
        var architecture = Architecture.Value;
        var kafka = Types().That().ResideInAssembly("Parchwell.Kafka");
        var otherParchwellTypes = architecture.Types.Concat(architecture.ReferencedTypes)
            .Where(type => type.Assembly.Name.StartsWith("Parchwell.", StringComparison.Ordinal) && type.Assembly.Name != "Parchwell.Kafka");
        Check(kafka.Should().NotDependOnAny(otherParchwellTypes), architecture);
    }

    [Fact]
    public void Given_ServiceAssemblies_When_CompiledDependenciesAreChecked_Then_ServicesDoNotDependOnEachOther()
    {
        var architecture = Architecture.Value;
        foreach (var service in ProjectRules.ServiceNames)
        {
            var source = Types().That().ResideInAssembly(service);
            var others = architecture.Types.Concat(architecture.ReferencedTypes)
                .Where(type => type.Assembly.Name != service && ProjectRules.ServiceNames.Contains(type.Assembly.Name, StringComparer.Ordinal));
            Check(source.Should().NotDependOnAny(others), architecture);
        }
    }

    [Fact]
    public void Given_ServiceFeatureNamespaces_When_CompiledDependenciesAreChecked_Then_FeatureSlicesAreIsolated()
    {
        var architecture = Architecture.Value;
        foreach (var service in ProjectRules.ServiceNames)
        {
            var sliceTypes = architecture.Types.Where(type => type.Assembly.Name == service && type.Namespace.FullName.StartsWith(service + ".", StringComparison.Ordinal))
                .GroupBy(type => type.Namespace.FullName[(service.Length + 1)..].Split('.')[0], StringComparer.Ordinal)
                .Where(group => group.Key != "Common").ToArray();
            foreach (var slice in sliceTypes)
            {
                var current = Types().That().Are(slice);
                var otherSlices = Types().That().Are(sliceTypes.Where(group => group.Key != slice.Key).SelectMany(group => group));
                Check(current.Should().NotDependOnAny(otherSlices), architecture);
            }
        }
    }

    [Fact]
    public void Given_ProductionMethods_When_ClockCallsAreChecked_Then_DirectDateTimeNowAndUtcNowGettersAreForbidden()
    {
        var architecture = Architecture.Value;
        var production = Types().That().Are(architecture.Types);
        foreach (var getter in new[] { "get_Now()", "get_UtcNow()" })
        {
            // Read method-call targets directly: referenced-only method stubs need not appear
            // in the declaring type's Members or MethodMembers()'s source-type provider.
            var forbidden = architecture.Types.SelectMany(type => type.GetCalledMethods())
                .Where(method => method.DeclaringType.FullName == "System.DateTime" && method.Name == getter);
            Check(production.Should().NotCallAny(forbidden), architecture);
        }
    }

    private static void Check(TypesShouldConjunction rule, Architecture architecture)
    {
        // Empty class-library scaffolds have no types. Assembly identity is checked above;
        // allowing an empty selection still evaluates and rejects every future violation.
        var scaffoldAwareRule = rule.WithoutRequiringPositiveResults();
        Assert.True(scaffoldAwareRule.HasNoViolations(architecture), string.Join(Environment.NewLine, scaffoldAwareRule.Evaluate(architecture)));
    }
}
