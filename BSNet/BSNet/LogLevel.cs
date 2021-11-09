namespace BSNet
{
    /// <summary>
    /// The level of severity when receiving or sending a log message
    /// <para/>In order of least to most severe: Info, Warning, Error
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Unused, but can be called by child applications
        /// </summary>
        Info,
        /// <summary>
        /// Returned on general user warnings like potential disconnects, packet losses and failed checksums
        /// </summary>
        Warning,
        /// <summary>
        /// Returned on SocketException and Exception along with the stacktrace
        /// </summary>
        Error
    }
}
