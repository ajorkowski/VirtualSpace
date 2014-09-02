using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace VirtualSpace.Platform.Common.Plex
{
    public class PlexNetworkServiceAdvertiser
    {
        private const string BindAddress = "0.0.0.0";
        private const string BroadcastAddress = "239.0.0.250";
        private const int ClientPort = 32450;

        private async Task<Socket> CreateSocket()
        {
            // Create Socket
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            // Create listening endpoint
            var listenEndpoint = new IPEndPoint(IPAddress.Parse(BindAddress), ClientPort);
            socket.Bind(listenEndpoint);
            socket.SetSocketOption(SocketOptionLevel.Udp, SocketOptionName.ReuseAddress, true);

            // Enable loopback
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);

            // Setup membership
            var groupAddress = IPAddress.Parse(BroadcastAddress);
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(groupAddress));
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);

            // Start listening
            var sendTo = new IPEndPoint(groupAddress, ClientPort + 1);
            await Task.Factory.FromAsync((c, o) => socket.BeginConnect(sendTo, c, o), socket.EndConnect, null);

            return socket;
        }
    }
}
