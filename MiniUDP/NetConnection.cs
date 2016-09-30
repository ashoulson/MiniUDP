/*
 *  MiniUDP - A Simple UDP Layer for Shipping and Receiving Byte Arrays
 *  Copyright (c) 2016 - Alexander Shoulson - http://ashoulson.com
 *
 *  This software is provided 'as-is', without any express or implied
 *  warranty. In no event will the authors be held liable for any damages
 *  arising from the use of this software.
 *  Permission is granted to anyone to use this software for any purpose,
 *  including commercial applications, and to alter it and redistribute it
 *  freely, subject to the following restrictions:
 *  
 *  1. The origin of this software must not be misrepresented; you must not
 *     claim that you wrote the original software. If you use this software
 *     in a product, an acknowledgment in the product documentation would be
 *     appreciated but is not required.
 *  2. Altered source versions must be plainly marked as such, and must not be
 *     misrepresented as being the original software.
 *  3. This notice may not be removed or altered from any source distribution.
*/

using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MiniUDP
{
  public class NetConnection
  {
    // The NetConnection keeps its own socket wrapper for instantly sending
    // payloads on the main thread rather than passing them to the controller
    private readonly NetSocket socket;
    private readonly NetController controller;
    private Thread controllerThread;

    public NetConnection(NetVersion version)
    {
      Socket socket = NetSocket.CreateRawSocket();
      this.socket = new NetSocket(socket);
      this.controller = new NetController(socket, version, true);
    }

    public void Start(int port = 0)
    {
      this.socket.Bind(port);
      this.controllerThread = 
        new Thread(new ThreadStart(this.controller.Start));
      this.controllerThread.IsBackground = true;
      this.controllerThread.Start();
    }

    public void Stop()
    {
      this.controller.Stop();
    }

    public void Connect(IPEndPoint endpoint)
    {
      this.controller.BeginConnect(endpoint);
    }

    public void Cleanup()
    {
      this.controller.Stop();
      this.controllerThread.Join();
      this.socket.Close();
    }

    public bool IsRunning()
    {
      return this.controllerThread.IsAlive;
    }
  }
}