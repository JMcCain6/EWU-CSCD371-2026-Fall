namespace Assignment8;

public sealed class InvalidPersonException : Exception
{
    public InvalidPersonException(string message, int lineNumber, string rawLine,
        Exception innerException) : base(message, innerException)
    {
        LineNumber = lineNumber;
        RawLine = rawLine;
    }

    public int LineNumber { get; }
    public string RawLine { get; }
    public string RawText => RawLine;
}
