namespace BSNet
{
    public class ClientConnection
    {
        public double RTT { private set; get; }
        public ushort LocalSequence { private set; get; }
        public ushort RemoteSequence { private set; get; }
        public uint AckBits { private set; get; }
        public double LastSent { private set; get; }
        public double LastReceived { private set; get; }

        public ulong LocalToken { private set; get; }
        public ulong RemoteToken { private set; get; }
        public bool Authenticated { private set; get; }
        public ulong Token { get { return LocalToken ^ RemoteToken; } }

        private double[] roundTrips = new double[256];
        //private Dictionary<ushort, double> roundTrips = new Dictionary<ushort, double>();

        public ClientConnection(double time, ulong localToken, ulong remoteToken)
        {
            LocalSequence = 0;
            RemoteSequence = 0;
            AckBits = 0;
            LastSent = time;
            LastReceived = time;

            LocalToken = localToken;
            RemoteToken = remoteToken;
            Authenticated = false;
        }

        public void UpdateLastSent(double time)
        {
            LastSent = time;
        }

        public ushort IncrementSequence()
        {
            return ++LocalSequence;
        }

        public bool Authenticate(ulong receivedToken, double time)
        {
            if (Authenticated)
            {
                if (Token == receivedToken)
                {
                    LastReceived = time;
                    return true;
                }
                return false;
            }
            else
            {
                RemoteToken = receivedToken;
                Authenticated = true;
                LastReceived = time;
                return true;
            }
        }

        public void AddRTT(ushort sequence, double time)
        {
            roundTrips[sequence % roundTrips.Length] = time;
        }

        public void UpdateRTT(ushort sequence, double time)
        {
            double rtt = time - roundTrips[sequence % roundTrips.Length];
            RTT = RTT * 0.9d + rtt * 0.1d;
        }

        public void Acknowledge(ushort sequence)
        {
            // ackBits = 0110;1
            // remoteSequence = 5;
            // sequence = 6;
            // expected = 0111;1

            if (RemoteSequence == sequence) return;
            if (BSUtility.IsGreaterThan(sequence, RemoteSequence))
            {
                int shift = sequence - RemoteSequence - 1;
                if (shift < 0)
                    shift += 65536 + 1;

                AckBits = (AckBits << 1) + 1; // Add last received
                AckBits <<= shift; // Shift more if necessary

                RemoteSequence = sequence;
            }
            else
            {
                int shift = RemoteSequence - sequence - 1;
                if (shift < 0)
                    shift += 65536 + 1;

                // Add acknowledgement to bitfield
                uint add = 1U << shift;
                AckBits |= add;
            }
        }

        public bool IsAcknowledged(ushort sequence) => BSUtility.IsAcknowledged(AckBits, RemoteSequence, sequence);
    }
}
