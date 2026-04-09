// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace HoloCade.Core.Networking
{
    /// <summary>
    /// Base UDP Transport (Non-MonoBehaviour)
    /// 
    /// Provides raw UDP socket management for protocol-agnostic UDP communication.
    /// This is the foundation for all UDP-based transports in HoloCade.
    /// 
    /// Used by:
    /// - HoloCadeUDPTransport (adds HoloCade binary protocol)
    /// - ArtNetTransport (adds Art-Net protocol)
    /// - Any future UDP-based protocols
    /// 
    /// This class handles:
    /// - Socket creation and lifecycle
    /// - IP address parsing
    /// - Send/Receive operations
    /// - Non-blocking I/O
    /// 
    /// Protocol-specific logic (packet building, parsing) is handled by subclasses.
    /// </summary>
    public class UDPTransportBase
    {
        protected UdpClient udpSocket;
        protected IPEndPoint remoteEndPoint;
        protected bool isConnected = false;

        public UDPTransportBase()
        {
        }

        ~UDPTransportBase()
        {
            ShutdownUDPConnection();
        }

        /// <summary>
        /// Initialize UDP socket connection
        /// </summary>
        /// <param name="remoteIP">IP address of the remote device</param>
        /// <param name="remotePort">UDP port</param>
        /// <param name="socketName">Name for the socket (for debugging)</param>
        /// <param name="enableBroadcast">If true, enables broadcast (for Art-Net, etc.)</param>
        /// <returns>True if initialization successful</returns>
        public bool InitializeUDPConnection(string remoteIP, int remotePort, string socketName = "HoloCade_UDP", bool enableBroadcast = false)
        {
            try
            {
                // Parse IP address
                IPAddress ipAddress;
                if (!IPAddress.TryParse(remoteIP, out ipAddress))
                {
                    Debug.LogError($"{socketName}: Invalid IP address: {remoteIP}");
                    return false;
                }

                remoteEndPoint = new IPEndPoint(ipAddress, remotePort);

                // Create UDP client
                udpSocket = new UdpClient();
                udpSocket.Client.Blocking = false;  // Non-blocking
                udpSocket.Client.ReceiveBufferSize = 8192;
                udpSocket.EnableBroadcast = enableBroadcast;

                isConnected = true;
                Debug.Log($"{socketName}: UDP connection initialized (target: {remoteIP}:{remotePort}, broadcast: {enableBroadcast})");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{socketName}: Failed to initialize UDP connection - {ex.Message}");
                ShutdownUDPConnection();
                return false;
            }
        }

        /// <summary>
        /// Shutdown UDP connection
        /// </summary>
        public void ShutdownUDPConnection()
        {
            if (udpSocket != null)
            {
                try
                {
                    udpSocket.Close();
                    udpSocket.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"UDPTransportBase: Error during shutdown - {ex.Message}");
                }
                udpSocket = null;
            }

            remoteEndPoint = null;
            isConnected = false;
        }

        /// <summary>
        /// Check if UDP connection is active
        /// </summary>
        public bool IsUDPConnected() => isConnected && udpSocket != null && remoteEndPoint != null;

        /// <summary>
        /// Send raw data via UDP
        /// </summary>
        /// <param name="data">Raw byte array to send</param>
        /// <returns>True if send was successful</returns>
        public bool SendUDPData(byte[] data)
        {
            if (!IsUDPConnected() || data == null || data.Length == 0)
                return false;

            try
            {
                int bytesSent = udpSocket.Send(data, data.Length, remoteEndPoint);
                return bytesSent == data.Length;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"UDPTransportBase: Send failed - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Receive raw data via UDP (non-blocking)
        /// </summary>
        /// <param name="outData">Output buffer for received data</param>
        /// <param name="outBytesRead">Number of bytes actually read</param>
        /// <param name="outSenderAddr">Address of the sender</param>
        /// <returns>True if data was received</returns>
        public bool ReceiveUDPData(out byte[] outData, out int outBytesRead, out IPEndPoint outSenderAddr)
        {
            outData = null;
            outBytesRead = 0;
            outSenderAddr = null;

            if (!IsUDPConnected())
                return false;

            try
            {
                if (udpSocket.Available > 0)
                {
                    outData = udpSocket.Receive(ref outSenderAddr);
                    outBytesRead = outData != null ? outData.Length : 0;
                    return outBytesRead > 0;
                }
            }
            catch (SocketException ex)
            {
                // Non-blocking socket will throw if no data available (expected)
                if (ex.SocketErrorCode != SocketError.WouldBlock)
                {
                    Debug.LogWarning($"UDPTransportBase: Receive error - {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"UDPTransportBase: Receive failed - {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Get the remote address
        /// </summary>
        public IPEndPoint GetRemoteAddress() => remoteEndPoint;

        /// <summary>
        /// Get the UDP socket (for advanced use cases)
        /// </summary>
        public UdpClient GetSocket() => udpSocket;
    }
}

