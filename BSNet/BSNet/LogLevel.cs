namespace BSNet
{
    /// <summary>
    /// The level of severity when receiving or sending a log message
    /// <para/>In order of least to most severe: Info, Warning, Error
    /// </summary>
    public enum LogLevel
    {
        Info,       // Generic info messages
        Warning,    // Warnings about potential disconnect, packet losses etc.
        Error       // Exceptions etc.
    }
}
