using BSNet.Stream;
using System;

namespace BSNet.Datagram
{
    public class Header : IBSSerializable
    {
        public byte Type { private set; get; }
        public ushort Sequence { private set; get; }
        public ushort Ack { private set; get; }
        public uint AckBits { private set; get; }
        public ulong Token { private set; get; }

        public Header(IBSStream stream)
        {
            if (stream.Reading)
                Serialize(stream);
            else
                throw new Exception("Cannot initialize header from writable stream");
        }

        public Header(byte type, ushort sequence, ushort ack, uint ackBits, ulong token)
        {
            Type = type;
            Sequence = sequence;
            Ack = ack;
            AckBits = ackBits;
            Token = token;
        }

        public void Serialize(IBSStream stream)
        {
            Type = stream.SerializeByte(Type, 2);
            Sequence = stream.SerializeUShort(Sequence);
            Ack = stream.SerializeUShort(Ack);
            AckBits = stream.SerializeUInt(AckBits);
            Token = stream.SerializeULong(Token);
        }
    }
}
