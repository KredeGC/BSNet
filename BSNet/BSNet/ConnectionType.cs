namespace BSNet
{
    public static class ConnectionType
    {
        // Message types
        public const byte CONNECT = 0x00;
        public const byte MESSAGE = 0x01;
        public const byte HEARTBEAT = 0x02;
        public const byte DISCONNECT = 0x03;
    }
}
