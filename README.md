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