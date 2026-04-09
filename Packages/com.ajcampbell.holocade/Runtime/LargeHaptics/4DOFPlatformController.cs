// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using HoloCade.Core.Networking;
using HoloCade.LargeHaptics.Models;

namespace HoloCade.LargeHaptics
{
    /// <summary>
    /// 4DOF Platform Controller
    /// 
    /// Specialized controller for 4DOF motion platforms that use:
    /// - Tilt (pitch and roll) via hydraulic actuators
    /// - Scissor lift (Y and Z translations) via scissor lift mechanism
    /// 
    /// Used by Experience Genre Templates:
    /// - GunshipExperience (4-player seated gunship)
    /// - MovingPlatformExperience (single-player standing platform)
    /// - CarSimExperience (single-player seated racing/driving simulator)
    /// 
    /// This subclass provides struct-based transmission methods for efficient
    /// UDP communication with hardware ECUs that support these data models.
    /// </summary>
    public class PlatformController4DOF : HapticPlatformController
    {
        // UDP transport is inherited from base class (HapticPlatformController)
        // No need to redeclare it here

        /// <summary>
        /// Send duration value (for use with struct packets)
        /// </summary>
        public void SendDuration(float duration, int channel = 4)
        {
            if (udpTransport != null && udpTransport.IsUDPConnected())
            {
                udpTransport.SendFloat(channel, duration);
            }
        }

        /// <summary>
        /// Send tilt state (pitch and roll only) as a struct packet
        /// </summary>
        /// <param name="tiltState">The tilt state to send</param>
        /// <param name="channel">Channel number for the struct packet (default: 100 for tilt structs)</param>
        public void SendTiltStruct(TiltState tiltState, int channel = 100)
        {
            if (!IsInitialized() || !udpTransport.IsUDPConnected())
            {
                Debug.LogWarning("[HoloCade] 4DOFPlatformController: Cannot send tilt struct - not initialized or not connected");
                return;
            }

            // Send tilt as struct packet (default Channel 100)
            udpTransport.SendStruct(channel, tiltState);
            Debug.Log($"[HoloCade] 4DOFPlatformController: Sent tilt struct on Ch{channel} - Pitch: {tiltState.Pitch:F2}, Roll: {tiltState.Roll:F2}");
        }

        /// <summary>
        /// Send scissor lift state (Y and Z translations only) as a struct packet
        /// </summary>
        /// <param name="liftState">The scissor lift state to send</param>
        /// <param name="channel">Channel number for the struct packet (default: 101 for scissor lift structs)</param>
        public void SendScissorLiftStruct(ScissorLiftState liftState, int channel = 101)
        {
            if (!IsInitialized() || !udpTransport.IsUDPConnected())
            {
                Debug.LogWarning("[HoloCade] 4DOFPlatformController: Cannot send scissor lift struct - not initialized or not connected");
                return;
            }

            // Send scissor lift state as struct packet (default Channel 101)
            udpTransport.SendStruct(channel, liftState);
            Debug.Log($"[HoloCade] 4DOFPlatformController: Sent scissor lift struct on Ch{channel} - Y: {liftState.TranslationY:F2}, Z: {liftState.TranslationZ:F2}");
        }

        /// <summary>
        /// Get current tilt state from hardware feedback (bidirectional IO)
        /// </summary>
        /// <param name="outTiltState">Output tilt state received from hardware</param>
        /// <returns>True if valid tilt state was received</returns>
        public bool GetTiltStateFeedback(out TiltState outTiltState)
        {
            outTiltState = default(TiltState);

            // Hardware sends tilt state feedback on Channel 100
            // Parse from received bytes cache
            byte[] receivedBytes = udpTransport.GetReceivedBytes(100);
            if (receivedBytes != null && receivedBytes.Length >= System.Runtime.InteropServices.Marshal.SizeOf<TiltState>())
            {
                System.Runtime.InteropServices.GCHandle handle = System.Runtime.InteropServices.GCHandle.Alloc(receivedBytes, System.Runtime.InteropServices.GCHandleType.Pinned);
                try
                {
                    outTiltState = (TiltState)System.Runtime.InteropServices.Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(TiltState));
                    return true;
                }
                finally
                {
                    handle.Free();
                }
            }
            return false;
        }

        /// <summary>
        /// Get current scissor lift state from hardware feedback (bidirectional IO)
        /// </summary>
        /// <param name="outLiftState">Output scissor lift state received from hardware</param>
        /// <returns>True if valid lift state was received</returns>
        public bool GetScissorLiftStateFeedback(out ScissorLiftState outLiftState)
        {
            outLiftState = default(ScissorLiftState);

            // Hardware sends scissor lift state feedback on Channel 101
            // Parse from received bytes cache
            byte[] receivedBytes = udpTransport.GetReceivedBytes(101);
            if (receivedBytes != null && receivedBytes.Length >= System.Runtime.InteropServices.Marshal.SizeOf<ScissorLiftState>())
            {
                System.Runtime.InteropServices.GCHandle handle = System.Runtime.InteropServices.GCHandle.Alloc(receivedBytes, System.Runtime.InteropServices.GCHandleType.Pinned);
                try
                {
                    outLiftState = (ScissorLiftState)System.Runtime.InteropServices.Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(ScissorLiftState));
                    return true;
                }
                finally
                {
                    handle.Free();
                }
            }
            return false;
        }

        /// <summary>
        /// Get gun button events from hardware (Channel 310)
        /// Used by GunshipExperience for low-latency button event handling.
        /// </summary>
        /// <param name="outButtonEvents">Output button events received from hardware</param>
        /// <returns>True if valid button events were received</returns>
        public bool GetGunButtonEvents(out GunButtonEvents outButtonEvents)
        {
            outButtonEvents = default(GunButtonEvents);

            // Hardware sends button events on Channel 310
            // Parse from received bytes cache
            byte[] receivedBytes = udpTransport.GetReceivedBytes(310);
            if (receivedBytes != null && receivedBytes.Length >= System.Runtime.InteropServices.Marshal.SizeOf<GunButtonEvents>())
            {
                System.Runtime.InteropServices.GCHandle handle = System.Runtime.InteropServices.GCHandle.Alloc(receivedBytes, System.Runtime.InteropServices.GCHandleType.Pinned);
                try
                {
                    outButtonEvents = (GunButtonEvents)System.Runtime.InteropServices.Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(GunButtonEvents));
                    return true;
                }
                finally
                {
                    handle.Free();
                }
            }
            return false;
        }

        /// <summary>
        /// Get gun telemetry from hardware (Channel 311)
        /// Used by GunshipExperience for monitoring gun system health.
        /// </summary>
        /// <param name="outTelemetry">Output telemetry received from hardware</param>
        /// <returns>True if valid telemetry was received</returns>
        public bool GetGunTelemetry(out GunTelemetry outTelemetry)
        {
            outTelemetry = default(GunTelemetry);

            // Hardware sends gun telemetry on Channel 311
            // Parse from received bytes cache
            byte[] receivedBytes = udpTransport.GetReceivedBytes(311);
            if (receivedBytes != null && receivedBytes.Length >= System.Runtime.InteropServices.Marshal.SizeOf<GunTelemetry>())
            {
                System.Runtime.InteropServices.GCHandle handle = System.Runtime.InteropServices.GCHandle.Alloc(receivedBytes, System.Runtime.InteropServices.GCHandleType.Pinned);
                try
                {
                    outTelemetry = (GunTelemetry)System.Runtime.InteropServices.Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(GunTelemetry));
                    return true;
                }
                finally
                {
                    handle.Free();
                }
            }
            return false;
        }
    }
}

