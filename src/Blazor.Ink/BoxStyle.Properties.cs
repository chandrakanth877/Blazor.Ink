using Facebook.Yoga;

namespace Blazor.Ink;

public sealed partial record BoxStyle
{
    public TextWrap? TextWrap { get; init; }
    public YGPositionType? Position { get; init; }
    public Length? Top { get; init; }
    public Length? Right { get; init; }
    public Length? Bottom { get; init; }
    public Length? Left { get; init; }
    public float? ColumnGap { get; init; }
    public float? RowGap { get; init; }
    public float? Gap { get; init; }
    public float? Margin { get; init; }
    public float? MarginX { get; init; }
    public float? MarginY { get; init; }
    public float? MarginTop { get; init; }
    public float? MarginBottom { get; init; }
    public float? MarginLeft { get; init; }
    public float? MarginRight { get; init; }
    public float? Padding { get; init; }
    public float? PaddingX { get; init; }
    public float? PaddingY { get; init; }
    public float? PaddingTop { get; init; }
    public float? PaddingBottom { get; init; }
    public float? PaddingLeft { get; init; }
    public float? PaddingRight { get; init; }
    public float? FlexGrow { get; init; }
    public float? FlexShrink { get; init; }
    public YGFlexDirection? FlexDirection { get; init; }
    public Length? FlexBasis { get; init; }
    public YGWrap? FlexWrap { get; init; }
    public YGAlign? AlignItems { get; init; }
    public YGAlign? AlignSelf { get; init; }
    public YGAlign? AlignContent { get; init; }
    public YGJustify? JustifyContent { get; init; }
    public Length? Width { get; init; }
    public Length? Height { get; init; }
    public Length? MinWidth { get; init; }
    public Length? MinHeight { get; init; }
    public Length? MaxWidth { get; init; }
    public Length? MaxHeight { get; init; }
    public float? AspectRatio { get; init; }
    public YGDisplay? Display { get; init; }
    public BorderStyle? BorderStyle { get; init; }
    public bool? BorderTop { get; init; }
    public bool? BorderBottom { get; init; }
    public bool? BorderLeft { get; init; }
    public bool? BorderRight { get; init; }
    public string? BorderColor { get; init; }
    public string? BorderTopColor { get; init; }
    public string? BorderBottomColor { get; init; }
    public string? BorderLeftColor { get; init; }
    public string? BorderRightColor { get; init; }
    public bool? BorderDimColor { get; init; }
    public bool? BorderTopDimColor { get; init; }
    public bool? BorderBottomDimColor { get; init; }
    public bool? BorderLeftDimColor { get; init; }
    public bool? BorderRightDimColor { get; init; }
    public string? BorderBackgroundColor { get; init; }
    public string? BorderTopBackgroundColor { get; init; }
    public string? BorderBottomBackgroundColor { get; init; }
    public string? BorderLeftBackgroundColor { get; init; }
    public string? BorderRightBackgroundColor { get; init; }
    public Overflow? Overflow { get; init; }
    public Overflow? OverflowX { get; init; }
    public Overflow? OverflowY { get; init; }
    public float? ContentOffsetX { get; init; }
    public float? ContentOffsetY { get; init; }
    public string? BackgroundColor { get; init; }

    private static BoxStyle Set(BoxStyle style, string name, string value) => name switch
    {
        "textWrap" => style with { TextWrap = EnumValue<TextWrap>(value) },
        "position" => style with { Position = EnumValue<YGPositionType>(value) },
        "top" => style with { Top = Length.Parse(value) },
        "right" => style with { Right = Length.Parse(value) },
        "bottom" => style with { Bottom = Length.Parse(value) },
        "left" => style with { Left = Length.Parse(value) },
        "columnGap" => style with { ColumnGap = Number(value) },
        "rowGap" => style with { RowGap = Number(value) },
        "gap" => style with { Gap = Number(value) },
        "margin" => style with { Margin = Number(value) },
        "marginX" => style with { MarginX = Number(value) },
        "marginY" => style with { MarginY = Number(value) },
        "marginTop" => style with { MarginTop = Number(value) },
        "marginBottom" => style with { MarginBottom = Number(value) },
        "marginLeft" => style with { MarginLeft = Number(value) },
        "marginRight" => style with { MarginRight = Number(value) },
        "padding" => style with { Padding = Number(value) },
        "paddingX" => style with { PaddingX = Number(value) },
        "paddingY" => style with { PaddingY = Number(value) },
        "paddingTop" => style with { PaddingTop = Number(value) },
        "paddingBottom" => style with { PaddingBottom = Number(value) },
        "paddingLeft" => style with { PaddingLeft = Number(value) },
        "paddingRight" => style with { PaddingRight = Number(value) },
        "flexGrow" => style with { FlexGrow = Number(value) },
        "flexShrink" => style with { FlexShrink = Number(value) },
        "flexDirection" => style with { FlexDirection = EnumValue<YGFlexDirection>(value) },
        "flexBasis" => style with { FlexBasis = Length.Parse(value) },
        "flexWrap" => style with { FlexWrap = EnumValue<YGWrap>(value) },
        "alignItems" => style with { AlignItems = EnumValue<YGAlign>(value) },
        "alignSelf" => style with { AlignSelf = EnumValue<YGAlign>(value) },
        "alignContent" => style with { AlignContent = EnumValue<YGAlign>(value) },
        "justifyContent" => style with { JustifyContent = EnumValue<YGJustify>(value) },
        "width" => style with { Width = Length.Parse(value) },
        "height" => style with { Height = Length.Parse(value) },
        "minWidth" => style with { MinWidth = Length.Parse(value) },
        "minHeight" => style with { MinHeight = Length.Parse(value) },
        "maxWidth" => style with { MaxWidth = Length.Parse(value) },
        "maxHeight" => style with { MaxHeight = Length.Parse(value) },
        "aspectRatio" => style with { AspectRatio = Number(value) },
        "display" => style with { Display = EnumValue<YGDisplay>(value) },
        "borderStyle" => style with { BorderStyle = EnumValue<BorderStyle>(value) },
        "borderTop" => style with { BorderTop = bool.Parse(value) },
        "borderBottom" => style with { BorderBottom = bool.Parse(value) },
        "borderLeft" => style with { BorderLeft = bool.Parse(value) },
        "borderRight" => style with { BorderRight = bool.Parse(value) },
        "borderColor" => style with { BorderColor = value },
        "borderTopColor" => style with { BorderTopColor = value },
        "borderBottomColor" => style with { BorderBottomColor = value },
        "borderLeftColor" => style with { BorderLeftColor = value },
        "borderRightColor" => style with { BorderRightColor = value },
        "borderDimColor" => style with { BorderDimColor = bool.Parse(value) },
        "borderTopDimColor" => style with { BorderTopDimColor = bool.Parse(value) },
        "borderBottomDimColor" => style with { BorderBottomDimColor = bool.Parse(value) },
        "borderLeftDimColor" => style with { BorderLeftDimColor = bool.Parse(value) },
        "borderRightDimColor" => style with { BorderRightDimColor = bool.Parse(value) },
        "borderBackgroundColor" => style with { BorderBackgroundColor = value },
        "borderTopBackgroundColor" => style with { BorderTopBackgroundColor = value },
        "borderBottomBackgroundColor" => style with { BorderBottomBackgroundColor = value },
        "borderLeftBackgroundColor" => style with { BorderLeftBackgroundColor = value },
        "borderRightBackgroundColor" => style with { BorderRightBackgroundColor = value },
        "overflow" => style with { Overflow = EnumValue<Overflow>(value) },
        "overflowX" => style with { OverflowX = EnumValue<Overflow>(value) },
        "overflowY" => style with { OverflowY = EnumValue<Overflow>(value) },
        "contentOffsetX" => style with { ContentOffsetX = Number(value) },
        "contentOffsetY" => style with { ContentOffsetY = Number(value) },
        "backgroundColor" => style with { BackgroundColor = value },
        _ => style
    };

    internal void WriteAttributes(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        if (TextWrap is { } value1) builder.AddAttribute(1, "textWrap", Convert.ToString(value1, System.Globalization.CultureInfo.InvariantCulture));
        if (Position is { } value2) builder.AddAttribute(2, "position", Convert.ToString(value2, System.Globalization.CultureInfo.InvariantCulture));
        if (Top is { } value3) builder.AddAttribute(3, "top", Convert.ToString(value3, System.Globalization.CultureInfo.InvariantCulture));
        if (Right is { } value4) builder.AddAttribute(4, "right", Convert.ToString(value4, System.Globalization.CultureInfo.InvariantCulture));
        if (Bottom is { } value5) builder.AddAttribute(5, "bottom", Convert.ToString(value5, System.Globalization.CultureInfo.InvariantCulture));
        if (Left is { } value6) builder.AddAttribute(6, "left", Convert.ToString(value6, System.Globalization.CultureInfo.InvariantCulture));
        if (ColumnGap is { } value7) builder.AddAttribute(7, "columnGap", Convert.ToString(value7, System.Globalization.CultureInfo.InvariantCulture));
        if (RowGap is { } value8) builder.AddAttribute(8, "rowGap", Convert.ToString(value8, System.Globalization.CultureInfo.InvariantCulture));
        if (Gap is { } value9) builder.AddAttribute(9, "gap", Convert.ToString(value9, System.Globalization.CultureInfo.InvariantCulture));
        if (Margin is { } value10) builder.AddAttribute(10, "margin", Convert.ToString(value10, System.Globalization.CultureInfo.InvariantCulture));
        if (MarginX is { } value11) builder.AddAttribute(11, "marginX", Convert.ToString(value11, System.Globalization.CultureInfo.InvariantCulture));
        if (MarginY is { } value12) builder.AddAttribute(12, "marginY", Convert.ToString(value12, System.Globalization.CultureInfo.InvariantCulture));
        if (MarginTop is { } value13) builder.AddAttribute(13, "marginTop", Convert.ToString(value13, System.Globalization.CultureInfo.InvariantCulture));
        if (MarginBottom is { } value14) builder.AddAttribute(14, "marginBottom", Convert.ToString(value14, System.Globalization.CultureInfo.InvariantCulture));
        if (MarginLeft is { } value15) builder.AddAttribute(15, "marginLeft", Convert.ToString(value15, System.Globalization.CultureInfo.InvariantCulture));
        if (MarginRight is { } value16) builder.AddAttribute(16, "marginRight", Convert.ToString(value16, System.Globalization.CultureInfo.InvariantCulture));
        if (Padding is { } value17) builder.AddAttribute(17, "padding", Convert.ToString(value17, System.Globalization.CultureInfo.InvariantCulture));
        if (PaddingX is { } value18) builder.AddAttribute(18, "paddingX", Convert.ToString(value18, System.Globalization.CultureInfo.InvariantCulture));
        if (PaddingY is { } value19) builder.AddAttribute(19, "paddingY", Convert.ToString(value19, System.Globalization.CultureInfo.InvariantCulture));
        if (PaddingTop is { } value20) builder.AddAttribute(20, "paddingTop", Convert.ToString(value20, System.Globalization.CultureInfo.InvariantCulture));
        if (PaddingBottom is { } value21) builder.AddAttribute(21, "paddingBottom", Convert.ToString(value21, System.Globalization.CultureInfo.InvariantCulture));
        if (PaddingLeft is { } value22) builder.AddAttribute(22, "paddingLeft", Convert.ToString(value22, System.Globalization.CultureInfo.InvariantCulture));
        if (PaddingRight is { } value23) builder.AddAttribute(23, "paddingRight", Convert.ToString(value23, System.Globalization.CultureInfo.InvariantCulture));
        if (FlexGrow is { } value24) builder.AddAttribute(24, "flexGrow", Convert.ToString(value24, System.Globalization.CultureInfo.InvariantCulture));
        if (FlexShrink is { } value25) builder.AddAttribute(25, "flexShrink", Convert.ToString(value25, System.Globalization.CultureInfo.InvariantCulture));
        if (FlexDirection is { } value26) builder.AddAttribute(26, "flexDirection", Convert.ToString(value26, System.Globalization.CultureInfo.InvariantCulture));
        if (FlexBasis is { } value27) builder.AddAttribute(27, "flexBasis", Convert.ToString(value27, System.Globalization.CultureInfo.InvariantCulture));
        if (FlexWrap is { } value28) builder.AddAttribute(28, "flexWrap", Convert.ToString(value28, System.Globalization.CultureInfo.InvariantCulture));
        if (AlignItems is { } value29) builder.AddAttribute(29, "alignItems", Convert.ToString(value29, System.Globalization.CultureInfo.InvariantCulture));
        if (AlignSelf is { } value30) builder.AddAttribute(30, "alignSelf", Convert.ToString(value30, System.Globalization.CultureInfo.InvariantCulture));
        if (AlignContent is { } value31) builder.AddAttribute(31, "alignContent", Convert.ToString(value31, System.Globalization.CultureInfo.InvariantCulture));
        if (JustifyContent is { } value32) builder.AddAttribute(32, "justifyContent", Convert.ToString(value32, System.Globalization.CultureInfo.InvariantCulture));
        if (Width is { } value33) builder.AddAttribute(33, "width", Convert.ToString(value33, System.Globalization.CultureInfo.InvariantCulture));
        if (Height is { } value34) builder.AddAttribute(34, "height", Convert.ToString(value34, System.Globalization.CultureInfo.InvariantCulture));
        if (MinWidth is { } value35) builder.AddAttribute(35, "minWidth", Convert.ToString(value35, System.Globalization.CultureInfo.InvariantCulture));
        if (MinHeight is { } value36) builder.AddAttribute(36, "minHeight", Convert.ToString(value36, System.Globalization.CultureInfo.InvariantCulture));
        if (MaxWidth is { } value37) builder.AddAttribute(37, "maxWidth", Convert.ToString(value37, System.Globalization.CultureInfo.InvariantCulture));
        if (MaxHeight is { } value38) builder.AddAttribute(38, "maxHeight", Convert.ToString(value38, System.Globalization.CultureInfo.InvariantCulture));
        if (AspectRatio is { } value39) builder.AddAttribute(39, "aspectRatio", Convert.ToString(value39, System.Globalization.CultureInfo.InvariantCulture));
        if (Display is { } value40) builder.AddAttribute(40, "display", Convert.ToString(value40, System.Globalization.CultureInfo.InvariantCulture));
        if (BorderStyle is { } value41) builder.AddAttribute(41, "borderStyle", Convert.ToString(value41, System.Globalization.CultureInfo.InvariantCulture));
        if (BorderTop is { } value42) builder.AddAttribute(42, "borderTop", Convert.ToString(value42, System.Globalization.CultureInfo.InvariantCulture));
        if (BorderBottom is { } value43) builder.AddAttribute(43, "borderBottom", Convert.ToString(value43, System.Globalization.CultureInfo.InvariantCulture));
        if (BorderLeft is { } value44) builder.AddAttribute(44, "borderLeft", Convert.ToString(value44, System.Globalization.CultureInfo.InvariantCulture));
        if (BorderRight is { } value45) builder.AddAttribute(45, "borderRight", Convert.ToString(value45, System.Globalization.CultureInfo.InvariantCulture));
        if (BorderColor is { } value46) builder.AddAttribute(46, "borderColor", Convert.ToString(value46, System.Globalization.CultureInfo.InvariantCulture));
        if (BorderTopColor is { } value47) builder.AddAttribute(47, "borderTopColor", Convert.ToString(value47, System.Globalization.CultureInfo.InvariantCulture));
        if (BorderBottomColor is { } value48) builder.AddAttribute(48, "borderBottomColor", Convert.ToString(value48, System.Globalization.CultureInfo.InvariantCulture));
        if (BorderLeftColor is { } value49) builder.AddAttribute(49, "borderLeftColor", Convert.ToString(value49, System.Globalization.CultureInfo.InvariantCulture));
        if (BorderRightColor is { } value50) builder.AddAttribute(50, "borderRightColor", Convert.ToString(value50, System.Globalization.CultureInfo.InvariantCulture));
        if (BorderDimColor is { } value51) builder.AddAttribute(51, "borderDimColor", Convert.ToString(value51, System.Globalization.CultureInfo.InvariantCulture));
        if (BorderTopDimColor is { } value52) builder.AddAttribute(52, "borderTopDimColor", Convert.ToString(value52, System.Globalization.CultureInfo.InvariantCulture));
        if (BorderBottomDimColor is { } value53) builder.AddAttribute(53, "borderBottomDimColor", Convert.ToString(value53, System.Globalization.CultureInfo.InvariantCulture));
        if (BorderLeftDimColor is { } value54) builder.AddAttribute(54, "borderLeftDimColor", Convert.ToString(value54, System.Globalization.CultureInfo.InvariantCulture));
        if (BorderRightDimColor is { } value55) builder.AddAttribute(55, "borderRightDimColor", Convert.ToString(value55, System.Globalization.CultureInfo.InvariantCulture));
        if (BorderBackgroundColor is { } value56) builder.AddAttribute(56, "borderBackgroundColor", Convert.ToString(value56, System.Globalization.CultureInfo.InvariantCulture));
        if (BorderTopBackgroundColor is { } value57) builder.AddAttribute(57, "borderTopBackgroundColor", Convert.ToString(value57, System.Globalization.CultureInfo.InvariantCulture));
        if (BorderBottomBackgroundColor is { } value58) builder.AddAttribute(58, "borderBottomBackgroundColor", Convert.ToString(value58, System.Globalization.CultureInfo.InvariantCulture));
        if (BorderLeftBackgroundColor is { } value59) builder.AddAttribute(59, "borderLeftBackgroundColor", Convert.ToString(value59, System.Globalization.CultureInfo.InvariantCulture));
        if (BorderRightBackgroundColor is { } value60) builder.AddAttribute(60, "borderRightBackgroundColor", Convert.ToString(value60, System.Globalization.CultureInfo.InvariantCulture));
        if (Overflow is { } value61) builder.AddAttribute(61, "overflow", Convert.ToString(value61, System.Globalization.CultureInfo.InvariantCulture));
        if (OverflowX is { } value62) builder.AddAttribute(62, "overflowX", Convert.ToString(value62, System.Globalization.CultureInfo.InvariantCulture));
        if (OverflowY is { } value63) builder.AddAttribute(63, "overflowY", Convert.ToString(value63, System.Globalization.CultureInfo.InvariantCulture));
        if (ContentOffsetX is { } value64) builder.AddAttribute(64, "contentOffsetX", Convert.ToString(value64, System.Globalization.CultureInfo.InvariantCulture));
        if (ContentOffsetY is { } value65) builder.AddAttribute(65, "contentOffsetY", Convert.ToString(value65, System.Globalization.CultureInfo.InvariantCulture));
        if (BackgroundColor is { } value66) builder.AddAttribute(66, "backgroundColor", Convert.ToString(value66, System.Globalization.CultureInfo.InvariantCulture));
    }
}
