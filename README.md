# BSNet
Lightweight UDP-based, transport layer for games.
Mostly based on reading [Glenn Fiedler's articles](https://gafferongames.com).
Should NOT be used with sensitive data as no encryption occurs, whatsoever.

## Features
* Connection-based, built on top of UDP with keep-alive packets.
* Supports both reliable and unreliable packets over UDP.
* Reliability layer with acknowledgements redundantly sent in every packet.
* 20 bytes (and 3 bits) of packet overhead.
* CRC32 to protect against old clients and accidental connections.
* Bitpacking mostly based on [BitPackerTools](https://github.com/LazyBui/BitPackerTools), with some improvements.
* Quantization of floats, Vectors and Quaternions, from [NetStack](https://github.com/nxrighthere/NetStack).
* Made for .NET Framework 4.6.1 and Unity.

### What this protects against
* IP spoofing (A challenge packet is sent when connections are requested).
* Replay attacks (All packets have unique sequence numbers, and can't be used multiple times).
* DDoS amplification (Connection packets enforce padding in messages).
* Old clients and accidental connections.

### What this doesn't protect against
* Man in the middle attacks (Proper encryption would be needed for that).
* Zombie clients.

### Future ideas
* Some sort of packet fragmentation.

# Usage
## P2P example
This is an example of a simple P2P architecture, where both ends connect to eachother and send a message when connected.
```csharp
public class Client : BSSocket
{
    public override byte[] ProtocolVersion => new byte[] { 0x00, 0x00, 0x00, 0x01 };
    
    protected Encoding encoding = new ASCIIEncoding();
    
    public Client(int localPort, string peerIP, int peerPort) : base(localPort)
    {
        // Construct peer endpoint
        IPAddress peerAddress = IPAddress.Parse(peerIP);
        IPEndPoint peerEndPoint = new IPEndPoint(peerAddress, peerPort);

        // Send a request to connect
        Connect(peerEndPoint);
    }

    // For error logging
    protected override void Log(object obj, LogLevel level)
    {
        Console.WriteLine(obj);
    }

    // Called when a connection has been established with this IPEndPoint
    protected override void OnConnect(IPEndPoint endPoint)
    {
        Log($"{endPoint.ToString()} connected", LogLevel.Info);

        // Send a message to the connected IPEndPoint
        SendMessageReliable(endPoint, writer =>
        {
            writer.SerializeString(encoding, "Hello network!");
        });
    }
	
    // Called when a connection has been lost with this IPEndPoint
    protected override void OnDisconnect(IPEndPoint endPoint)
    {
        Log($"{endPoint.ToString()} disconnected", LogLevel.Info);
    }
	
    // Called when we receive a message from this IPEndPoint
    protected override void OnReceiveMessage(IPEndPoint endPoint, IBSStream reader)
    {
        // Receive the message, "Hello network!", from the other end
        string message = reader.SerializeString(encoding);
        Log(message, LogLevel.Info);
    }
}
```

## Bitpacking
The readers and writers have built-in functionality for packing bits as tight as you want.
They can also quantize floats, halfs, Vectors and Quaternions, to keep the bits low.
```csharp
// Create a new BoundedRange, with a minimum value of 0, a maximum of 1 and 0.01 in precision
// This range will crunch a float into just 7 bits
BoundedRange range = new BoundedRange(0, 1, 0.01f);

// Create a new BSWriter
byte[] bytes;
using (BSWriter writer = BSWriter.GetWriter()) {
    // Write a couple of floats to the writer
    writer.SerializeFloat(range, 0.23167f);
    writer.SerializeFloat(range, 0.55f);
    
    // Return the bytes in a packed array
    bytes = writer.ToArray();
    
    Console.WriteLine($"Total bits used: {writer.TotalBits}"); // Prints 14
    Console.WriteLine($"Total length of byte array: {bytes.Length}"); // Prints 2
}

// Create a new BSReader from the byte array
using (BSReader reader = BSReader.GetReader(bytes)) {
    // Read the 2 floats
    float firstFloat = reader.SerializeFloat(range);
    float secondFloat = reader.SerializeFloat(range);
    Console.WriteLine($"Floats read: {firstFloat}, {secondFloat}"); // Prints 0.23 and 0.55
}
```