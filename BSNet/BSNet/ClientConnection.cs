namespace BSNet
{
    public class ClientConnection
    {
        public const int RTT_BUFFER_SIZE = 256;

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

        private double[] roundTrips = new double[RTT_BUFFER_SIZE];

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

        /// <summary>
        /// Increments the local sequence and updates the timer
        /// </summary>
        /// <param name="time">The current time</param>
        public void IncrementSequence(double time)
        {
            LocalSequence++;
            LastSent = time;
            roundTrips[LocalSequence % RTT_BUFFER_SIZE] = time;
        }

        /// <summary>
        /// Authenticate this client using the given token
        /// </summary>
        /// <param name="receivedToken">The token to compare against</param>
        /// <param name="time">The current time</param>
        /// <returns>Whether the client was authenticated</returns>
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

        /// <summary>
        /// Update the client's Round-trip-time
        /// </summary>
        /// <param name="sequence">The sequence we have acknowledged</param>
        /// <param name="time">The current time, to compare</param>
        public void UpdateRTT(ushort sequence, double time)
        {
            if (roundTrips[sequence % RTT_BUFFER_SIZE] > 0)
            {
                double rtt = time - roundTrips[sequence % RTT_BUFFER_SIZE];
                RTT = RTT * 0.9d + rtt * 0.1d;
                roundTrips[sequence % RTT_BUFFER_SIZE] = 0;
            }
        }

        /// <summary>
        /// Saves the sequence in the acknowledged bits
        /// </summary>
        /// <param name="sequence">The sequence to acknowledge</param>
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

        /// <summary>
        /// Returns whether the given sequence has been acknowledged previously
        /// </summary>
        /// <param name="sequence">The sequence to test</param>
        /// <returns>Whether the sequence was acknowledged</returns>
        public bool IsAcknowledged(ushort sequence) => BSUtility.IsAcknowledged(AckBits, RemoteSequence, sequence);
    }
}
