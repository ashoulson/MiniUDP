**MiniUDP: A Simple UDP Layer for Shipping and Receiving Byte Arrays**

Alexander Shoulson, Ph.D. - http://ashoulson.com

---

Based loosely on MassiveNet: https://github.com/jakevn/MassiveNet

---

Supported Networking Tasks:
- UDP traffic I/O for byte[] arrays with very little overhead (8 bytes)
- Loose connection establishment and time-out detection
- Unreliable payload delivery
- Traffic data collection for ping, remote packet loss, and local packet loss

Wishlist:
- Encryption and authentication

Not Supported:
- Reliability
- Fragmentation/reassembly (MiniUDP enforces a hard MTU for its payload size)
- Reordering or duplicate removal (all packets received are sent to the application in receipt order)
- Data serialization (MiniUDP expects a byte[] array)
- RPCs

Primary Design Features of MiniUDP:
- **No thread requirements.** MiniUDP is designed with small, single process architectures in mind (like those of a 1-core cloud VPS). MiniUDP does not create any threads, and is designed to work in a threadless architecture, though it can be run on its own thread if desired.
- **Simplicity.** MiniUDP is designed to be simple to read and debug. This library offers as minimal a feature set as possible to keep the total source small and readable. MiniUDP is designed to work with a higher-level library for tasks like serialization and reliability layering.

Caveats:
- To use MiniUDP on its own thread, small adjustments will be needed to ensure thread-safe access to the message passing queues used for reading and writing packets.
