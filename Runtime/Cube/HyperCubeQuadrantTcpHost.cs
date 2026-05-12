// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using HoloCade;
using UnityEngine;

namespace HoloCade.Cube
{
    /// <summary>
    /// Hosts four TCP listeners (parameterizable ports). HyperCube connects as client and sends
    /// <c>uint32 big-endian length</c> + JPEG bytes per frame. Decoded frames are applied on the main thread.
    /// </summary>
    [InspectorPurpose("Hosts TCP listeners for four JPEG quadrant streams from HyperCube and queues decoded textures for the passthrough binder.")]
    [DisallowMultipleComponent]
    public class HyperCubeQuadrantTcpHost : MonoBehaviour
    {
        [SerializeField] int[] tcpPorts = { 18001, 18002, 18003, 18004 };
        [SerializeField] int maxJpegBytes = 4 * 1024 * 1024;

        readonly ConcurrentQueue<(int quadrant, byte[] jpeg)> _pending = new ConcurrentQueue<(int, byte[])>();
        Texture2D[] _textures = new Texture2D[4];
        TcpListener[] _listeners;
        Thread[] _threads;
        volatile bool _alive;

        public Texture2D GetQuadrantTexture(int index)
        {
            if (index < 0 || index >= _textures.Length)
                return null;
            return _textures[index];
        }

        void Awake()
        {
            for (int i = 0; i < 4; i++)
                _textures[i] = new Texture2D(8, 8, TextureFormat.RGB24, false);
        }

        void OnEnable()
        {
            _alive = true;
            if (tcpPorts == null || tcpPorts.Length != 4)
            {
                Debug.LogError("[HyperCubeQuadrantTcpHost] tcpPorts must have length 4.");
                return;
            }

            _listeners = new TcpListener[4];
            _threads = new Thread[4];
            for (int i = 0; i < 4; i++)
            {
                int q = i;
                int port = tcpPorts[i];
                _listeners[i] = new TcpListener(IPAddress.Any, port);
                _listeners[i].Start();
                _threads[i] = new Thread(() => AcceptLoop(q, _listeners[q]));
                _threads[i].IsBackground = true;
                _threads[i].Start();
            }
        }

        void OnDisable()
        {
            _alive = false;
            if (_listeners != null)
            {
                for (int i = 0; i < _listeners.Length; i++)
                {
                    try
                    {
                        _listeners[i]?.Stop();
                    }
                    catch (Exception) { /* ignore */ }
                }
            }

            if (_threads != null)
            {
                foreach (var t in _threads)
                {
                    try
                    {
                        t?.Join(500);
                    }
                    catch (Exception) { /* ignore */ }
                }
            }
        }

        void Update()
        {
            int budget = 8;
            while (budget-- > 0 && _pending.TryDequeue(out var item))
            {
                var (quadrant, jpeg) = item;
                if (quadrant < 0 || quadrant >= 4)
                    continue;
                try
                {
                    if (!_textures[quadrant].LoadImage(jpeg))
                        Debug.LogWarning($"[HyperCubeQuadrantTcpHost] quadrant {quadrant}: LoadImage failed");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[HyperCubeQuadrantTcpHost] quadrant {quadrant}: {e.Message}");
                }
            }
        }

        void AcceptLoop(int quadrantIndex, TcpListener listener)
        {
            while (_alive && listener != null)
            {
                TcpClient client = null;
                try
                {
                    client = listener.AcceptTcpClient();
                }
                catch (SocketException)
                {
                    if (!_alive)
                        break;
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (client == null)
                    continue;

                try
                {
                    using (client)
                    using (var stream = client.GetStream())
                    {
                        var lenBuf = new byte[4];
                        while (_alive && client.Connected)
                        {
                            if (!ReadFully(stream, lenBuf, 0, 4))
                                break;
                            int jpegLen =
                                (lenBuf[0] << 24) |
                                (lenBuf[1] << 16) |
                                (lenBuf[2] << 8) |
                                lenBuf[3];
                            if (jpegLen <= 0 || jpegLen > maxJpegBytes)
                                break;

                            var jpeg = new byte[jpegLen];
                            if (!ReadFully(stream, jpeg, 0, jpegLen))
                                break;

                            _pending.Enqueue((quadrantIndex, jpeg));
                        }
                    }
                }
                catch (Exception)
                {
                    /* disconnect; accept next */
                }
            }
        }

        static bool ReadFully(Stream stream, byte[] buffer, int offset, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = stream.Read(buffer, offset + read, count - read);
                if (n <= 0)
                    return false;
                read += n;
            }

            return true;
        }
    }
}
