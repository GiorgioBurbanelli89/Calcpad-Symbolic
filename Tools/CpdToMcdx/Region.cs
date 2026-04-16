namespace CpdToMcdx;

/// <summary>Types of regions in a document</summary>
enum RegionType { Text, Heading, Math, DisplayEq, Plot, Map, Comment, Directive }

/// <summary>A document region (text, math, or plot)</summary>
record Region(RegionType Type, string Content, string? Extra = null)
{
    /// <summary>For Math: variable name (left side of =)</summary>
    public string? VarName { get; init; }
    /// <summary>For Math: expression (right side of =)</summary>
    public string? Expression { get; init; }
    /// <summary>For Math: function arguments (e.g., "x;y")</summary>
    public string? FuncArgs { get; init; }
    /// <summary>For Text: heading level (1-6, 0=normal text)</summary>
    public int HeadingLevel { get; init; }
}
