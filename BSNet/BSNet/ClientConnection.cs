using System;
using System.Net;

namespace BSNet
{
    public class ClientConnection
    {
        /// <summary>
        /// The address and port of this connection
        /// </summary>
        public IPEndPoint AddressPoint { private set; get; }

        /// <summary>
        /// The estimated round-trip-time
        /// <para/>Note: Uses an exponentially smoothed average
        /// </summary>
        public double RTT { private set; get; }

        /// <summary>
        /// The estimated packet loss
        /// <para/>Note: Uses an exponentially smoothed average
        /// </summary>
        public double PacketLoss { private set; get; }

        /// <summary>
        /// The sequence number of the last sent packet
        /// </summary>
        public ushort LocalSequence { private set; get; }

        /// <summary>
        /// The sequence number of the last received packet
        /// </summary>
        public ushort RemoteSequence { private set; get; }

        /// <summary>
        /// The bitfield of the last 32 received packets
        /// </summary>
        public uint AckBits { private set; get; }

        /// <summary>
        /// The last time we sent this client a message
        /// </summary>
        public double LastSent { private set; get; }

        /// <summary>
        /// The last time we received a message from this client
        /// </summary>
        public double LastReceived { private set; get; }
        

        /// <summary>
        /// The locally generated token
        /// </summary>
        public ulong LocalToken { private set; get; }

        /// <summary>
        /// The remotely received token
        /// </summary>
        public ulong RemoteToken { private set; get; }

        /// <summary>
        /// Whether this client has been authenticated or not
        /// <para/>Messages can only be sent after authentication
        /// </summary>
        public bool Authenticated { private set; get; }

        /// <summary>
        /// The shared token, used in every message after authentication
        /// </summary>
        public ulong Token { get { return LocalToken ^ RemoteToken; } }

        private ushort[] acknowledgements = new ushort[BSUtility.RTT_BUFFER_SIZE];
        private double[] roundTrips = new double[BSUtility.RTT_BUFFER_SIZE];

        public ClientConnection(IPEndPoint endPoint, double time, ulong localToken, ulong remoteToken)
        {
            AddressPoint = endPoint;
            RTT = 0;
            PacketLoss = 0;
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
        /// Increments the local sequence and updates the timer and estimated packet loss
        /// </summary>
        /// <param name="time">The current time</param>
        public void IncrementSequence(double time)
        {
            // Calculate packet loss
            int packetsLost = 0;
            for (int i = 0; i < BSUtility.RTT_BUFFER_SIZE; i++)
            {
                ushort seq = (ushort)Math.Max(LocalSequence - i, 0);

                if (acknowledgements[seq % BSUtility.RTT_BUFFER_SIZE] != seq) // If we haven't received an acknowledgement for this packet yet
                {
                    double timeSinceSent = time - roundTrips[seq % BSUtility.RTT_BUFFER_SIZE];
                    
                    // Compare to round-trip time
                    if (timeSinceSent >= RTT)
                        packetsLost++;
                }
            }

            double pl = packetsLost / (double)(BSUtility.RTT_BUFFER_SIZE - 1);
            PacketLoss = PacketLoss * 0.9d + pl * 0.1d;

            // Increment
            LocalSequence++;
            LastSent = time;
            roundTrips[LocalSequence % BSUtility.RTT_BUFFER_SIZE] = time;
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
            if (roundTrips[sequence % BSUtility.RTT_BUFFER_SIZE] > 0)
            {
                double rtt = time - roundTrips[sequence % BSUtility.RTT_BUFFER_SIZE];
                RTT = RTT * 0.9d + rtt * 0.1d;
                roundTrips[sequence % BSUtility.RTT_BUFFER_SIZE] = 0;
            }
        }

        /// <summary>
        /// Returns whether or not we have received an acknowledgement for this sequence number and buffers it
        /// </summary>
        /// <param name="sentSequence">The sequence number to check against, and subsequently buffer</param>
        /// <returns>Whether we have already received an acknowledgement for this sequence number</returns>
        public bool HasReceivedAcknowledgement(ushort sentSequence, double tickRate)
        {
            bool hasReceived = acknowledgements[sentSequence % BSUtility.RTT_BUFFER_SIZE] == sentSequence;

            acknowledgements[sentSequence % BSUtility.RTT_BUFFER_SIZE] = sentSequence;

            return hasReceived;
        }

        /// <summary>
        /// Saves the received sequence number in the acknowledged bits, sending it with next packets
        /// </summary>
        /// <param name="receivedSequence">The received sequence number to acknowledge</param>
        public void Acknowledge(ushort receivedSequence)
        {
            // ackBits = 0110;1
            // remoteSequence = 5;
            // sequence = 6;
            // expected = 0111;1

            if (RemoteSequence == receivedSequence) return;
            if (BSUtility.IsGreaterThan(receivedSequence, RemoteSequence))
            {
                int shift = receivedSequence - RemoteSequence - 1;
                if (shift < 0)
                    shift += 65536 + 1;

                AckBits = (AckBits << 1) + 1; // Add old received
                AckBits <<= shift; // Shift more if necessary

                RemoteSequence = receivedSequence;
            }
            else
            {
                int shift = RemoteSequence - receivedSequence - 1;
                if (shift < 0)
                    shift += 65536 + 1;

                // Add acknowledgement to bitfield
                uint add = 1U << shift;
                AckBits |= add;
            }
        }

        /// <summary>
        /// Returns whether the given received sequence number has been acknowledged previously
        /// </summary>
        /// <param name="receivedSequence">The sequence to test</param>
        /// <returns>Whether the sequence number was acknowledged</returns>
        public bool IsAcknowledged(ushort receivedSequence) => BSUtility.IsAcknowledged(AckBits, RemoteSequence, receivedSequence);
    }
}
