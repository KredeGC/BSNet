[![License](https://img.shields.io/github/license/KredeGC/BSNet?style=flat-square)](https://github.com/KredeGC/BSNet/blob/master/LICENSE)
[![Workflow](https://img.shields.io/github/workflow/status/KredeGC/BSNet/c-cpp.yml?style=flat-square)](https://github.com/KredeGC/CPPNet/actions/workflows/c-cpp.yml)
# BSNet
Lightweight UDP-based, transport layer for games.
Mostly based on reading [Glenn Fiedler's articles](https://gafferongames.com).
Should NOT be used with sensitive data as no encryption occurs, whatsoever.

## Features
* Connection-based, built on top of UDP with keep-alive packets.
* Supports both reliable and unreliable packets over UDP.
* Reliability layer with acknowledgements redundantly sent in every packet.
* 20 bytes (and 2 bits) of packet overhead.
* CRC32 checksum to protect against old clients and accidental connections.
* Bitpacking mostly based on [BitPackerTools](https://github.com/LazyBui/BitPackerTools), with some improvements.
* Quantization of floats, Vectors and Quaternions, from [NetStack](https://github.com/nxrighthere/NetStack).
* Made for .NET Framework 4.6.1 and Unity.

### Protects against casual attacks like
* IP spoofing (A challenge packet is sent when connections are requested).
* Replay attacks (All packets have unique sequence numbers, and can't be used multiple times).
* DDoS amplification (Connection packets enforce padding in messages).
* Old clients and accidental connections.

### What this doesn't protect against
* Man in the middle attacks (Proper encryption would be needed for that).
* Zombie clients (A more sophisticated detection system would be required).

### Future ideas
* Some sort of packet fragmentation and reassembly.
* Make SendMessageUnreliable and SendMessageReliable buffer instead of sending straight away.
  * On each tick: Combine the buffered messages into one, preferring the reliable buffer.
  * Don't remove packets from the reliable buffer until it has been acknowledged.
  * Possibly use the aforementioned fragmentation, if the packets are too big.

# Usage
## P2P example
This is an example of a simple P2P architecture, where both ends connect to eachother and send a message with the connection packet.
```csharp
using BSNet.Stream;
using System;
using System.Net;
using System.Text;

public class P2PExample : BSSocket
{
  public override byte[] ProtocolVersion => new byte[] { 0xC0, 0xDE, 0xDE, 0xAD };

  public P2PExample() : base(0) { }

  // For logging
  protected override void Log(object obj, LogLevel level)
  {
    Console.WriteLine(obj);
  }

  // Called when an endPoint wishes to connect, or we wish to connect to them
  protected override void OnRequestConnect(IPEndPoint endPoint, IBSStream writer)
  {
    writer.SerializeString(Encoding.ASCII, $"Hello from {Port}!");
  }

  // Called when a connection has been established with this IPEndPoint
  protected override void OnConnect(IPEndPoint endPoint, IBSStream reader)
  {
    Log($"{endPoint.ToString()} connected", LogLevel.Info);

    Log($"Received initial message: \"{reader.SerializeString(Encoding.ASCII)}\"", LogLevel.Info);
  }

  // Called when a connection has been lost with this IPEndPoint
  protected override void OnDisconnect(IPEndPoint endPoint, IBSStream reader)
  {
    Log($"{endPoint.ToString()} disconnected", LogLevel.Info);

    // Attempt to reconnect
    Connect(endPoint);
  }

  // Called when we receive a message from this IPEndPoint
  protected override void OnReceiveMessage(IPEndPoint endPoint, ushort sequence, IBSStream reader) { }
}

public class Program {
  public static void Main(string[] args)
  {
    // Instantiate 2 BSSockets
    P2PExample peer1 = new P2PExample();
    P2PExample peer2 = new P2PExample();

    // Construct peer endpoint
    IPAddress peer2Address = IPAddress.Parse("127.0.0.1");
    IPEndPoint peer2EndPoint = new IPEndPoint(peer2Address, peer2.Port);

    // Send a request to connect
    peer1.Connect(peer2EndPoint);

    while (true)
    {
      // Continuously update the clients
      peer1.Update();
      peer2.Update();

      // Exit if the user presses Q
      if (Console.KeyAvailable)
      {
        if (Console.ReadKey(true).Key == ConsoleKey.Q)
          break;
      }
    }

    peer1.Dispose();
    peer2.Dispose();
  }
}
```

## Bitpacking
The readers and writers have built-in functionality for packing bits as tight as you want.
They can also quantize floats, halves, Vectors and Quaternions, to keep the bits low.
They can also encode strings using different TextEncodings, like ASCII or UTF-8.
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