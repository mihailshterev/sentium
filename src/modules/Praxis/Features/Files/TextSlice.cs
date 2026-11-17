namespace Sentium.Praxis.Features.Files;

/// <summary>
/// Lines <see cref="StartLine"/> to <see cref="EndLine"/> (1-based, inclusive) of a text with <see cref="TotalLines"/>
/// lines.
/// </summary>
internal sealed record TextSlice(string Text, int StartLine, int EndLine, int TotalLines)
{
    public bool HasMore => EndLine < TotalLines;
}
