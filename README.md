**MiniUDP: A Simple UDP Layer for Shipping and Receiving Byte Arrays**

Alexander Shoulson, Ph.D. - http://ashoulson.com

---

Based loosely on [MassiveNet](https://github.com/jakevn/MassiveNet), and [LiteNetLib](https://github.com/RevenantX/LiteNetLib).

---

**Branch Status:** Functional but undocumented. Demos and unit tests are wildly out of date and should not be trusted.

Supported Networking Tasks:
- UDP traffic I/O for byte[] arrays with very little overhead
- Connection establishment and time-out detection
- Reliable-ordered and unreliable-sequenced message channels
- Traffic statistic collection for ping, remote/local packet loss, and remote/local packet drop
- Noise-based bidirectional latency and loss simulation with configurable parameters

Three delivery modes:
- **Connection Token:** A custom string attached with an opening connection request. Will be delivered to the host when the connection is established and made available via an event. Intended for session token authorization.
- **Payloads:** Sent immediately upon request as unreliable-sequenced. MiniUDP will not re-send but will drop payloads on arrival if they're older than the latest. Intended for synchronizing delta-encoded game state data.
- **Notifications:** Queued and sent at regular intervals as reliable-ordered. MiniUDP will ensure the arrival of these messages and will re-send if necessary. Intended for extra-game messages like chat, authentication, and other bookkeeping tasks.

Wishlist:
- Encryption and authentication

Not Supported:
- Fragmentation/reassembly (MiniUDP enforces a hard MTU for its payload size)
- Data serialization (MiniUDP expects a byte[] array and int length for all data)
- RPCs

Usage Notes:
- MiniUDP is not inherently multithreaded. This library is designed to work within the game loop and is optimized for server performance on cheap single-vCore VPS deployments.
