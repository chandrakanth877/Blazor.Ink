using System.Globalization;
using Facebook.Yoga;

namespace Blazor.Ink;

public readonly record struct Length
{
    public float Value { get; }
    public bool IsPercent { get; }
    public Length(float value, bool isPercent = false)
    {
        if (!float.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
        IsPercent = isPercent;
    }
    public static Length Percent(float value) => new(value, true);
    public static implicit operator Length(int value) => new(value);
    public static implicit operator Length(float value) => new(value);
    public static implicit operator Length(string value) => Parse(value);
    public static Length Parse(string value) => new(
        float.Parse(value.TrimEnd('%'), CultureInfo.InvariantCulture), value.EndsWith('%'));
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture) + (IsPercent ? "%" : "");
}

public enum TextWrap { Wrap, Hard, Truncate, TruncateEnd, TruncateStart, TruncateMiddle }
public enum Overflow { Visible, Hidden }
public enum BorderStyle { Single, Double, Round, Bold, SingleDouble, DoubleSingle, Classic }

public sealed partial record BoxStyle
{
    internal static BoxStyle Read(Dictionary<string, object?> attributes)
    {
        if (attributes.TryGetValue("style", out var value) && value is BoxStyle typed) return typed;
        var style = new BoxStyle();
        foreach (var (name, item) in attributes)
        {
            if (item is null) continue;
            var text = Convert.ToString(item, CultureInfo.InvariantCulture)!;
            style = Set(style, name, text);
        }
        return style;
    }

    private static T EnumValue<T>(string value) where T : struct, Enum =>
        Enum.Parse<T>(value.Replace("-", ""), true);
    private static float Number(string value)
    {
        var number = float.Parse(value, CultureInfo.InvariantCulture);
        if (!float.IsFinite(number)) throw new ArgumentOutOfRangeException(nameof(value));
        return number;
    }
}
