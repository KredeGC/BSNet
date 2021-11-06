namespace BSNet
{
    /// <summary>
    /// Different connection types for sending messages
    /// </summary>
    public enum ConnectionType
    {
        Connect, // Initial connection request, may contain payload
        Message, // Message containing payload
        Heartbeat, // Heartbeat containing no payload
        Disconnect // Disconnection, may contain payload
    }
    
    /*public static class ConnectionType
    {
        // Message types
        public const byte CONNECT = 0x00; // Initial connection request, may contain payload
        public const byte MESSAGE = 0x01; // Message containing payload
        public const byte HEARTBEAT = 0x02; // Heartbeat containing no payload
        public const byte DISCONNECT = 0x03; // Disconnection, may contain payload
    }*/
}
