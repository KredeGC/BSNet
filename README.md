# BSNet
Lightweight UDP-based, transport layer for games.
Mostly based on reading [Glenn Fiedler's articles](https://gafferongames.com).
Should NOT be used with sensitive data as no encryption occurs, whatsoever.

## Features
* Connection-based, built on UDP with built-in keep-alive packets.
* Supports both reliable and unreliable packets over UDP.
* Reliability layer with acknowledgements redundantly sent in each packet.
* 16 bytes (and 3 bits) of packet overhead.
* Bitpacking mostly based on [BitPackerTools](https://github.com/LazyBui/BitPackerTools), with some improvements.
* Quantization of floats, Vectors and Quaternions, from [NetStack](https://github.com/nxrighthere/NetStack).
* Built for .NET Framework 4.6.1 and Unity.

## What this protects against
* IP spoofing (A challenge packet is sent when connections are requested).
* Replay attacks (All packets have unique sequence numbers, and can't be used multiple times).
* DDoS amplification (Connection packets enforce padding in messages).

## What this doesn't protect against
* Man in the middle attacks (Proper encryption would be needed for that).
* Zombie clients.

## Usage
### P2P architecture
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
            writer.WriteString("Hello network!", encoding);
        });
    }
	
	// Called when a connection has been lost with this IPEndPoint
	protected override void OnDisconnect(IPEndPoint endPoint)
    {
        Log($"{endPoint.ToString()} disconnected");
    }
	
	// Called when we receive a message from this IPEndPoint
	protected override void OnReceiveMessage(IPEndPoint endPoint, BSReader reader)
    {
        // Receive the message from the other end
        string message = reader.ReadString(encoding);
        Log(message);
	}
}
```