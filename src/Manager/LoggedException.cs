using System;

namespace ShindenAPI;

// Keep diagnostic text and sanitized inner snapshots without copying exception Data.
internal sealed class LoggedException : Exception
{
    private readonly string _details;
    public string OriginalExceptionType { get; }
    public override string StackTrace { get; }

    internal LoggedException(Exception original, Func<string, string> redact)
        : base(redact(original.Message), original.InnerException is null ? null : new LoggedException(original.InnerException, redact))
    {
        OriginalExceptionType = redact(original.GetType().FullName);
        StackTrace = redact(original.StackTrace);
        _details = redact(original.ToString());
    }

    public override string ToString() => _details;
}
