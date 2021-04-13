# BSNet
Lightweight UDP-based, transport layer for games.
Mostly based on reading [Glenn Fiedler's articles](https://gafferongames.com).
Should NOT be used with sensitive data as no encryption occurs, whatsoever.

## Features
* Connection-based, built on top of UDP with keep-alive packets.
* Supports both reliable and unreliable packets over UDP.
* Reliability layer with acknowledgements redundantly sent in every packet.
* 16 bytes (and 3 bits) of packet overhead.
* Bitpacking mostly based on [BitPackerTools](https://github.com/LazyBui/BitPackerTools), with some improvements.
* Quantization of floats, Vectors and Quaternions, from [NetStack](https://github.com/nxrighthere/NetStack).
* Made for .NET Framework 4.6.1 and Unity.

### What this protects against
* IP spoofing (A challenge packet is sent when connections are requested).
* Replay attacks (All packets have unique sequence numbers, and can't be used multiple times).
* DDoS amplification (Connection packets enforce padding in messages).

### What this doesn't protect against
* Man in the middle attacks (Proper encryption would be needed for that).
* Zombie clients.

### Future ideas
* CRC32 to protect against old clients and accidental connections.
* Some sort of packet fragmentation.

# Usage
## P2P example
This is an example of a simple P2P architecture, where both ends connect to eachother and send a message when connected.
```csharp
public class Client : BSSocket
{
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
    protected override void Log(object obj)
    {
        Console.WriteLine(obj);
    }

    // Called when a connection has been established with this IPEndPoint
    protected override void OnConnect(IPEndPoint endPoint)
    {
        Log($"{endPoint.ToString()} connected");

        // Send a message to the connected IPEndPoint
        SendMessageReliable(endPoint, writer =>
        {
            writer.SerializeString("Hello network!", encoding);
        });
    }
	
    // Called when a connection has been lost with this IPEndPoint
    protected override void OnDisconnect(IPEndPoint endPoint)
    {
        Log($"{endPoint.ToString()} disconnected");
    }
	
    // Called when we receive a message from this IPEndPoint
    protected override void OnReceiveMessage(IPEndPoint endPoint, IBSStream reader)
    {
        // Receive the message, "Hello network!", from the other end
        string message = reader.SerializeString(null, encoding);
        Log(message);
    }
}
```

## Bitpacking
The readers and writers have built-in functionality for packing bits as tight as you want.
They can also quantize floats, halfs, Vectors and Quaternions, to keep the bits low.
```csharp
// Create a new BSWriter
BSWriter writer = new BSWriter();

// Create a new BoundedRange, with a minimum value of 0, a maximum of 1 and 0.01 in precision
// This range will crunch a float into just 7 bits
BoundedRange range = new BoundedRange(0, 1, 0.01f);

// Write a couple of floats to the writer
writer.SerializeFloat(0.23167f, range);
writer.SerializeFloat(0.55f, range);

// Return the bytes in a packed array
byte[] bytes = writer.ToArray();
Console.WriteLine($"Total bits used: {writer.TotalBits}"); // Prints 14
Console.WriteLine($"Total length of byte array: {bytes.Length}"); // Prints 2

// Create a new BSReader from the byte array
BSReader reader = new BSReader(bytes);

// Read the 2 floats
float firstFloat = reader.SerializeFloat(0, range);
float secondFloat = reader.SerializeFloat(0, range);
Console.WriteLine($"Floats read: {firstFloat}, {secondFloat}"); // Prints 0.23 and 0.55
```