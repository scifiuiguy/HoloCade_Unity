// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System.Collections.Generic;

namespace HoloCade.HoloCadeAI
{
    /// <summary>
    /// Structure for container configuration.
    /// </summary>
    [System.Serializable]
    public class ContainerConfig
    {
        public string imageName;
        public string containerName;
        public int hostPort = 8000;
        public int containerPort = 8000;
        public bool requireGPU = true;
        public Dictionary<string, string> environmentVariables = new Dictionary<string, string>();
        public Dictionary<string, string> volumeMounts = new Dictionary<string, string>();
    }

    /// <summary>
    /// Interface for container management.
    /// Enables starting, stopping, and monitoring Docker containers from Unity.
    /// 
    /// **Docker CLI Approach:**
    /// Uses Docker CLI commands (not HTTP API) for simplicity and security:
    /// - No TLS required (local socket/pipe communication)
    /// - No network exposure (local Docker daemon only)
    /// - No authentication setup (Docker daemon handles permissions)
    /// 
    /// **Platform Support:**
    /// - Windows: Named pipe at `\\.\pipe\docker_engine`
    /// - Linux: Unix socket at `/var/run/docker.sock`
    /// </summary>
    public interface IContainerManager
    {
        /// <summary>
        /// Checks if a container is currently running.
        /// </summary>
        bool IsContainerRunning(string containerName);

        /// <summary>
        /// Starts a container with the given configuration.
        /// </summary>
        bool StartContainer(ContainerConfig config);

        /// <summary>
        /// Stops a running container.
        /// </summary>
        bool StopContainer(string containerName);

        /// <summary>
        /// Removes a container (must be stopped first).
        /// </summary>
        bool RemoveContainer(string containerName);

        /// <summary>
        /// Checks if Docker CLI is available and Docker daemon is running.
        /// </summary>
        bool IsDockerAvailable();

        /// <summary>
        /// Gets container status information.
        /// </summary>
        bool GetContainerStatus(string containerName, out bool isRunning, out bool exists);
    }
}

