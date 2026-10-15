# Code comments

Code comments are written for an engineer who is new to this repository and has no other context. They explain what project terms mean and why the code does what it does, and they link to the decision for the history. Comment quality is guidance for the author, not a review gate.

Status: proposed in the pull request for [issue #63](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/63); binding once Hari merges it.

## Readers

Two readers matter. One reads in an IDE, where hovering a type shows its XML `<summary>`. The other reads a pull request diff, which shows only changed lines and a few lines around them. Agent sessions read files cold and are the same reader.

## Rules

1. A comment lets the reader understand the code without leaving the file.
2. Meaning is local. History and rationale are a link to an ADR or a blueprint section, not a copy. Write `ADR-014` or `docs/blueprint.md section 12`; the decision may change, and one copy is easier to keep true.
3. Define each project term once, in the XML documentation of the type that embodies it: a short `<summary>`, with any detail in `<remarks>`. This applies to `internal` types too. An IDE then shows it at every use, so call sites do not repeat it. The definition must agree with docs/blueprint.md, which is the glossary (CONTEXT.md).
4. At a call site, add a short gloss only where a project term decides the logic: a guard, a validation rule, a branch. A diff reader sees that line without the type's summary. A plain use of the same type needs nothing.
5. In tests, write one line per test class or test method that names the rule it protects and why the case matters. Do not narrate arrange, act and assert. The test name already states the behaviour; the comment adds what the name cannot.
6. Do not restate the code. `// Increment the count` above `count++` is noise.
7. A comment that states an Azure, Kafka or librdkafka behaviour is a claim. AGENTS.md "Truth and verification" applies: cite the source or link to the blueprint row that cites it.

`Parchwell.Kafka` also follows docs/engineering/dotnet.md: every public member documented. The XML documentation on `KafkaSettingKeys` is checked by the verification entry point after the build (docs/engineering/dotnet-verification.md); keep its structure.

## Review

Codex review and the cold review do not flag the presence, absence or style of comments (AGENTS.md "Code Review Rules"). Two cases are still findings. A comment that states something false about the code or a source misleads every later reader. A public member of `Parchwell.Kafka` without documentation breaks docs/engineering/dotnet.md.

## Examples

A term defined once, on its type (rule 3):

```csharp
/// <summary>Provides membership checks for the repository's approved frozen inventory.</summary>
/// <remarks>
/// The frozen inventory is the fixed list of work item IDs that v1 may test or trace, such as A01
/// or K10. ...
/// Source of the list: docs/blueprint.md section 12, frozen inventory and amendment 1.
/// </remarks>
public static class FrozenInventory
```

A call-site gloss where the term decides the logic (rule 4):

```csharp
// One definition file exists per inventory item (ADR-014). A file whose id differs from
// the requested item would run the wrong experiment, so it is rejected.
if (!StringComparer.Ordinal.Equals(id, requestedId)) validation.Error(...);
```

A test comment (rule 5):

```csharp
// The loader must reject an ID outside the frozen inventory before it reads any file,
// so a typo or a path such as "../A01" can never open an unrelated source.
public async Task Given_UnknownInventoryId_When_Loading_Then_RejectsBeforeOpeningAnySource(string id)
```
