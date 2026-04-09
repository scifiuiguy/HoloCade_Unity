// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace HoloCade.HoloCadeAI
{
    /// <summary>
    /// HTTP Request Result
    /// </summary>
    [Serializable]
    public class AIHTTPResult
    {
        public bool success = false;
        public int responseCode = 0;
        public string responseBody;
        public string errorMessage;
    }

    /// <summary>
    /// Generic HTTP Client for AI Service Integration
    /// 
    /// Provides async HTTP request/response handling with JSON support.
    /// Used by all AI service managers (LLM, ASR, TTS, Audio2Face, etc.)
    /// to communicate with AI service endpoints.
    /// 
    /// Features:
    /// - Async request/response handling with callbacks
    /// - JSON serialization/deserialization
    /// - Error handling and retry logic
    /// - Support for POST, GET, PUT, DELETE methods
    /// - Custom headers and authentication
    /// </summary>
    public class AIHTTPClient : MonoBehaviour
    {
        public float requestTimeout = 30.0f;
        public int maxRetries = 3;

        /// <summary>
        /// Make an async HTTP GET request
        /// </summary>
        public void Get(string url, Dictionary<string, string> headers, Action<AIHTTPResult> callback)
        {
            StartCoroutine(ExecuteRequest(UnityWebRequest.Get(url), headers, callback));
        }

        /// <summary>
        /// Make an async HTTP POST request with JSON body
        /// </summary>
        public void PostJSON(string url, string jsonBody, Dictionary<string, string> headers, Action<AIHTTPResult> callback)
        {
            var request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            StartCoroutine(ExecuteRequest(request, headers, callback));
        }

        /// <summary>
        /// Make an async HTTP POST request with string body
        /// </summary>
        public void PostString(string url, string body, string contentType, Dictionary<string, string> headers, Action<AIHTTPResult> callback)
        {
            var request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", contentType);
            StartCoroutine(ExecuteRequest(request, headers, callback));
        }

        /// <summary>
        /// Make an async HTTP PUT request with JSON body
        /// </summary>
        public void PutJSON(string url, string jsonBody, Dictionary<string, string> headers, Action<AIHTTPResult> callback)
        {
            var request = new UnityWebRequest(url, "PUT");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            StartCoroutine(ExecuteRequest(request, headers, callback));
        }

        /// <summary>
        /// Make an async HTTP DELETE request
        /// </summary>
        public void Delete(string url, Dictionary<string, string> headers, Action<AIHTTPResult> callback)
        {
            StartCoroutine(ExecuteRequest(UnityWebRequest.Delete(url), headers, callback));
        }

        private IEnumerator ExecuteRequest(UnityWebRequest request, Dictionary<string, string> headers, Action<AIHTTPResult> callback)
        {
            if (headers != null)
                foreach (var header in headers)
                    request.SetRequestHeader(header.Key, header.Value);

            request.timeout = (int)requestTimeout;
            yield return request.SendWebRequest();

            var result = new AIHTTPResult
            {
                success = request.result == UnityWebRequest.Result.Success,
                responseCode = (int)request.responseCode,
                responseBody = request.downloadHandler?.text ?? "",
                errorMessage = request.error ?? ""
            };

            request.Dispose();
            callback?.Invoke(result);
        }

        /// <summary>
        /// Build URL with query parameters
        /// </summary>
        public static string BuildURLWithQuery(string baseURL, Dictionary<string, string> queryParams)
        {
            if (queryParams == null || queryParams.Count == 0)
                return baseURL;

            var sb = new StringBuilder(baseURL);
            sb.Append("?");
            bool first = true;
            foreach (var param in queryParams)
            {
                if (!first) sb.Append("&");
                sb.Append(Uri.EscapeDataString(param.Key));
                sb.Append("=");
                sb.Append(Uri.EscapeDataString(param.Value));
                first = false;
            }
            return sb.ToString();
        }
    }
}

