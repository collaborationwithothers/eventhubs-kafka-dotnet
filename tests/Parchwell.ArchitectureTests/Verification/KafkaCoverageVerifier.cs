using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Xml;
using System.Xml.Linq;

namespace Parchwell.ArchitectureTests.Verification;

public static class KafkaCoverageVerifier
{
    private const string MappingNamespace = "Parchwell.Kafka.Configuration.";

    public static int Verify(string coverageReport, string kafkaAssembly)
    {
        var expected = ExecutableMappingTypes(kafkaAssembly);
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        XDocument document;
        using (var reader = XmlReader.Create(coverageReport, settings)) document = XDocument.Load(reader);
        if (document.Root == null || document.Root.Name != "coverage")
            throw new InvalidDataException("Expected a Cobertura coverage report.");
        var packages = document.Root.Descendants("package").Where(package => (string?)package.Attribute("name") == "Parchwell.Kafka").ToArray();
        if (packages.Length != 1) throw new InvalidDataException("Expected the instrumented Parchwell.Kafka assembly.");
        var classes = packages[0].Descendants("class")
            .Where(type => Normalize((string?)type.Attribute("name")).StartsWith(MappingNamespace, StringComparison.Ordinal)).ToArray();
        var reported = new HashSet<string>(classes.Where(type => type.Elements("lines").Elements("line").Any())
            .Select(type => Normalize((string?)type.Attribute("name"))), StringComparer.Ordinal);
        var missing = expected.Except(reported, StringComparer.Ordinal).ToArray();
        if (missing.Length != 0) throw new InvalidDataException("Missing coverage for " + string.Join(", ", missing) + ".");
        var lines = classes.SelectMany(type => type.Elements("lines").Elements("line")).ToArray();
        if (lines.Length == 0) throw new InvalidDataException("Coverage report contains no mapping lines.");
        foreach (var line in lines)
        {
            if (!int.TryParse((string?)line.Attribute("number"), out var number) || number <= 0
                || !int.TryParse((string?)line.Attribute("hits"), out var hits) || hits <= 0)
                throw new InvalidDataException("Uncovered or invalid mapping line: " + (string?)line.Attribute("number") + ".");
        }
        var sourceLines = classes.SelectMany(type => type.Elements("lines").Elements("line")
            .Select(line => (string?)type.Attribute("filename") + ":" + (string?)line.Attribute("number"))).Distinct().Count();
        return sourceLines;
    }

    private static HashSet<string> ExecutableMappingTypes(string kafkaAssembly)
    {
        using (var assemblyStream = File.OpenRead(kafkaAssembly))
        using (var pe = new PEReader(assemblyStream))
        using (var pdbStream = File.OpenRead(Path.ChangeExtension(kafkaAssembly, ".pdb")))
        using (var provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream))
        {
            var metadata = pe.GetMetadataReader();
            var pdb = provider.GetMetadataReader();
            var codeView = pe.ReadDebugDirectory().Single(entry => entry.Type == DebugDirectoryEntryType.CodeView);
            if (pe.ReadCodeViewDebugDirectoryData(codeView).Guid != new Guid((pdb.DebugMetadataHeader ?? throw new InvalidDataException("Missing portable PDB header.")).Id.Take(16).ToArray()))
                throw new InvalidDataException("The portable PDB does not match the mapping assembly.");
            string TypeName(TypeDefinitionHandle handle, bool includeGenericArguments = true)
            {
                var type = metadata.GetTypeDefinition(handle);
                var name = metadata.GetString(type.Name);
                var arity = name.IndexOf('`');
                if (arity >= 0) name = name[..arity];
                if (includeGenericArguments && type.GetGenericParameters().Count > 0)
                {
                    name += "<" + string.Join(",", type.GetGenericParameters()
                        .Select(parameter => metadata.GetString(metadata.GetGenericParameter(parameter).Name))) + ">";
                }
                var parent = type.GetDeclaringType();
                return parent.IsNil ? metadata.GetString(type.Namespace) + "." + name : TypeName(parent, false) + "." + name;
            }
            var expected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var handle in metadata.TypeDefinitions)
            {
                var name = Normalize(TypeName(handle));
                if (!name.StartsWith(MappingNamespace, StringComparison.Ordinal)) continue;
                if (metadata.GetTypeDefinition(handle).GetMethods().Any(method => pdb
                    .GetMethodDebugInformation(MetadataTokens.MethodDebugInformationHandle(MetadataTokens.GetRowNumber(method)))
                    .GetSequencePoints().Any(point => !point.IsHidden))) expected.Add(name);
            }
            if (expected.Count == 0) throw new InvalidDataException("Assembly/PDB contain no executable mapping types.");
            return expected;
        }
    }

    private static string Normalize(string? name) => (name ?? "").Replace("+", ".", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
}
