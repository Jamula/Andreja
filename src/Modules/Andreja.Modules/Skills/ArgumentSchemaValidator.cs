using Andreja.Platform.Contracts.Skills;
using System.Text.Json;

namespace Andreja.Modules.Skills;

internal static class ArgumentSchemaValidator
{
    public static SkillFailure? Validate(
        IReadOnlyList<ToolFieldSchema> schema,
        IReadOnlyDictionary<string, JsonElement> arguments)
    {
        var declared = schema.Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
        if (arguments.Keys.Any(argument => !declared.Contains(argument)))
        {
            return new("undeclared-argument", "An argument is not declared by the schema.");
        }

        foreach (var field in schema)
        {
            if (!arguments.TryGetValue(field.Name, out var value))
            {
                if (field.Required)
                {
                    return new("missing-argument", $"Required argument '{field.Name}' is missing.");
                }

                continue;
            }

            if (!Matches(field.Kind, value.ValueKind))
            {
                return new("argument-type", $"Argument '{field.Name}' has the wrong type.");
            }
        }

        return null;
    }

    private static bool Matches(ToolValueKind expected, JsonValueKind actual) =>
        expected switch
        {
            ToolValueKind.Text => actual == JsonValueKind.String,
            ToolValueKind.Numeric => actual == JsonValueKind.Number,
            ToolValueKind.Logical => actual is JsonValueKind.True or JsonValueKind.False,
            ToolValueKind.Structured => actual == JsonValueKind.Object,
            ToolValueKind.Sequence => actual == JsonValueKind.Array,
            _ => false,
        };
}
