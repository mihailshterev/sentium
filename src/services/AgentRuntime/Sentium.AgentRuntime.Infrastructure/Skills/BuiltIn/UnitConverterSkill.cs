using System.ComponentModel;
using System.Text.Json;
using Microsoft.Agents.AI;
using Sentium.AgentRuntime.Core.Skills;

namespace Sentium.AgentRuntime.Infrastructure.Skills.BuiltIn;

/// <summary>
/// Built-in skill for converting between common units of measurement.
/// </summary>
internal sealed class UnitConverterSkill : AgentClassSkill<UnitConverterSkill>
{
    internal static BuiltInSkillInfo Descriptor { get; } = new(
        "unit-converter",
        "Convert between common measurement units (distance, weight, temperature, volume). Use when the user asks to convert miles, kilometers, pounds, kilograms, Celsius, Fahrenheit, liters, or gallons.",
        """
        Use this skill when the user asks to convert between units of measurement.

        1. Identify the source unit and target unit from the user's request.
        2. Consult the conversion-tables resource for the appropriate multiplication factor.
        3. For temperature conversions use the convert-temperature script (special formula).
        4. For all other conversions use the convert script with the value and factor.
        5. Present the result clearly showing both the original and converted values with units.
        """);

    public override AgentSkillFrontmatter Frontmatter { get; } = new(
        "unit-converter",
        "Convert between common measurement units (distance, weight, temperature, volume). Use when the user asks to convert miles, kilometers, pounds, kilograms, Celsius, Fahrenheit, liters, or gallons.");

    protected override string Instructions => """
        Use this skill when the user asks to convert between units of measurement.

        1. Identify the source unit and target unit from the user's request.
        2. Consult the conversion-tables resource for the appropriate multiplication factor.
        3. For temperature conversions use the convert-temperature script (special formula).
        4. For all other conversions use the convert script with the value and factor.
        5. Present the result clearly showing both the original and converted values with units.
        """;

    [AgentSkillResource("conversion-tables")]
    [Description("Multiplication factors for common unit conversions.")]
    public string ConversionTables => """
        # Unit Conversion Tables
        Formula: result = value × factor

        ## Distance
        | From        | To          | Factor    |
        |-------------|-------------|-----------|
        | miles       | kilometers  | 1.60934   |
        | kilometers  | miles       | 0.621371  |
        | feet        | meters      | 0.3048    |
        | meters      | feet        | 3.28084   |
        | inches      | centimeters | 2.54      |
        | centimeters | inches      | 0.393701  |

        ## Weight
        | From      | To        | Factor   |
        |-----------|-----------|----------|
        | pounds    | kilograms | 0.453592 |
        | kilograms | pounds    | 2.20462  |
        | ounces    | grams     | 28.3495  |
        | grams     | ounces    | 0.035274 |

        ## Volume
        | From        | To       | Factor   |
        |-------------|----------|----------|
        | gallons(US) | liters   | 3.78541  |
        | liters      | gallons  | 0.264172 |
        | fluid oz    | ml       | 29.5735  |
        | ml          | fluid oz | 0.033814 |

        ## Temperature - use the convert-temperature script (not this table).
        """;

    [AgentSkillScript("convert")]
    [Description("Multiplies a value by a conversion factor. Returns JSON with value, factor, and result.")]
    private static string ConvertUnits(double value, double factor)
    {
        var result = Math.Round(value * factor, 4);
        return JsonSerializer.Serialize(new { value, factor, result });
    }

    [AgentSkillScript("convert-temperature")]
    [Description("Converts temperature between Celsius and Fahrenheit. Pass fromUnit as 'C' or 'F'.")]
    private static string ConvertTemperature(double value, string fromUnit)
    {
        var result = fromUnit.Equals("C", StringComparison.OrdinalIgnoreCase)
            ? Math.Round(value * 9.0 / 5.0 + 32, 2)
            : Math.Round((value - 32) * 5.0 / 9.0, 2);

        var toUnit = fromUnit.Equals("C", StringComparison.OrdinalIgnoreCase) ? "F" : "C";
        return JsonSerializer.Serialize(new { value, fromUnit, result, toUnit });
    }
}
