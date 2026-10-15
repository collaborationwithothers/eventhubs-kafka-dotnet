using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;

public sealed class ValidateKafkaDocumentation : Microsoft.Build.Utilities.Task
{
    [Required] public string KafkaAssembly { get; set; }
    [Required] public string DocumentationFile { get; set; }

    public override bool Execute()
    {
        try
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            const BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var assembly = Assembly.LoadFrom(Path.GetFullPath(KafkaAssembly));
            var catalogue = assembly.GetType("Parchwell.Kafka.Configuration.BaselineSettingsCatalog", true);
            var roles = assembly.GetType("Parchwell.Kafka.Configuration.KafkaClientRole", true);
            var keyType = assembly.GetType("Parchwell.Kafka.Configuration.KafkaSettingKeys", true);
            var expected = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var role in Enum.GetValues(roles))
            {
                var entries = (IEnumerable)catalogue.GetMethod("GetEntries", flags).Invoke(null, new[] { role });
                foreach (var entry in entries)
                {
                    var type = entry.GetType();
                    var key = (string)type.GetProperty("Key", instanceFlags).GetValue(entry);
                    if (!expected.ContainsKey(key)) expected.Add(key, new HashSet<string>(StringComparer.Ordinal));
                    expected[key].UnionWith(((IEnumerable)type.GetProperty("InventoryIds", instanceFlags).GetValue(entry)).Cast<string>());
                }
            }
            if (!File.Exists(DocumentationFile))
                throw new InvalidDataException("Missing XML documentation: " + DocumentationFile);
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            XDocument document;
            using (var reader = XmlReader.Create(DocumentationFile, settings)) document = XDocument.Load(reader);
            if (document.Root == null || document.Root.Name != "doc"
                || (string)document.Root.Element("assembly")?.Element("name") != "Parchwell.Kafka"
                || document.Root.Elements("members").Count() != 1)
                throw new InvalidDataException("Expected generated Parchwell.Kafka XML documentation.");
            var members = document.Root.Elements("members").Elements("member").ToArray();
            var fields = keyType.GetFields(flags).Where(field => field.IsLiteral && field.FieldType == typeof(string)).ToArray();
            var declared = fields.Select(field => (string)field.GetRawConstantValue()).ToArray();
            if (declared.Distinct(StringComparer.Ordinal).Count() != declared.Length
                || !expected.Keys.OrderBy(key => key, StringComparer.Ordinal).SequenceEqual(declared.OrderBy(key => key, StringComparer.Ordinal)))
                throw new InvalidDataException("Setting declarations and catalogue keys disagree.");
            var memberPrefix = "F:" + keyType.FullName + ".";
            var expectedMembers = new HashSet<string>(fields.Select(field => memberPrefix + field.Name), StringComparer.Ordinal);
            var settingMembers = members.Where(member => ((string)member.Attribute("name") ?? "")
                .StartsWith(memberPrefix, StringComparison.Ordinal)).ToArray();
            if (settingMembers.Any(member => !expectedMembers.Contains((string)member.Attribute("name"))))
                throw new InvalidDataException("Unexpected XML setting declaration.");
            foreach (var field in fields)
            {
                var key = (string)field.GetRawConstantValue();
                var name = "F:" + keyType.FullName + "." + field.Name;
                var matches = members.Where(member => (string)member.Attribute("name") == name).ToArray();
                if (matches.Length != 1) throw new InvalidDataException("Missing or duplicate XML declaration for " + key + ".");
                var member = matches[0];
                var summaries = member.Elements("summary").ToArray();
                if (summaries.Length != 1 || summaries[0].Elements("c").Count() != 1
                    || (string)summaries[0].Element("c") != key)
                    throw new InvalidDataException("XML summary names the wrong key for " + key + ".");
                var links = member.Descendants("see").Where(link => ((string)link.Attribute("href") ?? "")
                    .StartsWith("https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/", StringComparison.Ordinal)
                    && ((string)link.Attribute("href")).Contains("/docs/blueprint.md#L")).ToArray();
                var ids = links.Select(link => link.Value).ToArray();
                if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length || !expected[key].SetEquals(ids))
                    throw new InvalidDataException("XML inventory IDs disagree with catalogue for " + key + ".");
            }
            Log.LogMessage(MessageImportance.High, "Parchwell.Kafka: " + fields.Length + " setting XML declarations match catalogue inventory IDs across all roles.");
            return true;
        }
        catch (Exception exception)
        {
            Log.LogError("Kafka documentation rejected: " + exception.GetBaseException().Message);
            return false;
        }
    }
}
