// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.ProLighting
{
    /// <summary>Stub USB DMX transport (placeholder - not yet implemented)</summary>
    public class USBDMXTransport : IDMXTransport
    {
        private readonly string comPort;
        private readonly int baudRate;
        private bool connected;

        public bool IsConnected => connected;

        public USBDMXTransport(string comPort, int baudRate)
        {
            this.comPort = comPort;
            this.baudRate = baudRate;
        }

        public bool Initialize()
        {
            Debug.LogWarning($"USBDMXTransport: Initialize called (stub). USB DMX not implemented yet. COM={comPort}, Baud={baudRate}");
            connected = false;
            return connected;
        }

        public void Shutdown()
        {
            Debug.Log("USBDMXTransport: Shutdown (stub)");
            connected = false;
        }

        public void SendDMX(int universe, byte[] dmxData)
        {
            Debug.LogWarning("USBDMXTransport: SendDMX called (stub). USB DMX not implemented yet.");
        }
    }
}

