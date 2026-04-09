// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using System;
using System.Net;
using System.Threading;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HoloCade.Core;

namespace HoloCade.Retail
{
    /// <summary>
    /// Payment Provider Types
    /// </summary>
    public enum PaymentProvider
    {
        Embed,
        Nayax,
        Intercard,
        CoreCashless,
        Cantaloupe
    }

    /// <summary>
    /// Payment Configuration
    /// </summary>
    [System.Serializable]
    public class PaymentConfig
    {
        public PaymentProvider provider = PaymentProvider.Embed;
        public string apiKey = "";
        public string baseUrl = "";
        public string webhookPath = "";
        public string cardId = "";
    }

    /// <summary>
    /// Payment Webhook Payload
    /// </summary>
    [System.Serializable]
    public class PaymentPayload
    {
        public string cardId;
        public float amount;
        public float newBalance;
        public string transactionId;
        public string stationId;
    }

    /// <summary>
    /// Arcade Payment Manager - Handles cashless tap card payment interface for VR tap-to-play capability.
    /// 
    /// Supports multiple payment providers (Embed, Nayax, Intercard, Core Cashless, Cantaloupe) and provides
    /// webhook server for receiving payment confirmations and API methods for checking balances and allocating tokens.
    /// </summary>
    public class ArcadePaymentManager : MonoBehaviour
    {
        [Header("Payment Configuration")]
        [Tooltip("Payment provider configuration")]
        public PaymentConfig config = new PaymentConfig();

        [Header("Runtime Status")]
        [Tooltip("Whether the webhook server is currently running")]
        public bool isServerRunning = false;

        private HttpListener listener;
        private Thread listenerThread;
        private string localIP => GetLocalIPAddress();

        void Start()
        {
            StartWebhookServer();
            StartCoroutine(PollBalanceOnStartup());
        }

        void StartWebhookServer()
        {
            if (isServerRunning) return;

            try
            {
                listener = new HttpListener();
                string prefix = $"http://{localIP}:8080/{GetWebhookPath()}/";
                listener.Prefixes.Add(prefix);
                listener.Start();
                isServerRunning = true;

                listenerThread = new Thread(ListenLoop);
                listenerThread.IsBackground = true;
                listenerThread.Start();

                Debug.Log($"[HoloCade.Payment] Webhook server started: {prefix}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HoloCade.Payment] Failed to start webhook server: {ex.Message}");
                isServerRunning = false;
            }
        }

        void ListenLoop()
        {
            while (isServerRunning && listener != null && listener.IsListening)
            {
                try
                {
                    var context = listener.GetContext();
                    var req = context.Request;
                    var resp = context.Response;

                    if (req.HttpMethod == "POST" && req.Url.AbsolutePath.Contains(GetWebhookPath()))
                    {
                        string json = new System.IO.StreamReader(req.InputStream, req.ContentEncoding).ReadToEnd();
                        
                        // Parse JSON using Unity's JsonUtility (no external dependencies)
                        PaymentPayload payload = JsonUtility.FromJson<PaymentPayload>(json);

                        if (payload != null)
                        {
                            // Use UnityMainThreadDispatcher if available, otherwise use Unity's main thread
                            if (UnityMainThreadDispatcher.Instance != null)
                            {
                                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                                {
                                    OnPaymentConfirmed(payload);
                                });
                            }
                            else
                            {
                                // Fallback: schedule on main thread via coroutine
                                StartCoroutine(ProcessPaymentOnMainThread(payload));
                            }
                        }
                    }

                    byte[] buffer = Encoding.UTF8.GetBytes("OK");
                    resp.ContentLength64 = buffer.Length;
                    resp.OutputStream.Write(buffer, 0, buffer.Length);
                    resp.Close();
                }
                catch (Exception ex)
                {
                    // Log error but continue listening
                    Debug.LogWarning($"[HoloCade.Payment] Error in webhook listener: {ex.Message}");
                }
            }
        }

        IEnumerator ProcessPaymentOnMainThread(PaymentPayload payload)
        {
            yield return null; // Wait one frame to ensure we're on main thread
            OnPaymentConfirmed(payload);
        }

        void OnPaymentConfirmed(PaymentPayload p)
        {
            Debug.Log($"[HoloCade.Payment:{config.provider}] Player {p.cardId} paid ${p.amount:F2}. New balance: ${p.newBalance:F2}");
            
            // TODO: Integrate with HoloCade experience system
            // Example: Find experience and start session
            // var experience = FindObjectOfType<HoloCadeExperienceBase>();
            // experience?.StartSession(p.cardId, p.newBalance);
        }

        IEnumerator PollBalanceOnStartup()
        {
            yield return new WaitForSeconds(2f);
            yield return StartCoroutine(CheckBalance(config.cardId, (balance) =>
            {
                Debug.Log($"[HoloCade.Payment] Initial balance for {config.cardId}: ${balance:F2}");
            }));
        }

        /// <summary>
        /// Check the balance for a card ID (async coroutine)
        /// </summary>
        public IEnumerator CheckBalance(string cardId, Action<float> callback = null)
        {
            if (string.IsNullOrEmpty(cardId))
            {
                Debug.LogWarning("[HoloCade.Payment] CheckBalance called with empty cardId");
                callback?.Invoke(0f);
                yield break;
            }

            string url = BuildEndpoint("balance", cardId);
            using (var www = UnityEngine.Networking.UnityWebRequest.Get(url))
            {
                www.SetRequestHeader("Authorization", "Bearer " + config.apiKey);
                yield return www.SendWebRequest();

                if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    string json = www.downloadHandler.text;
                    float balance = ExtractBalance(json);
                    callback?.Invoke(balance);
                }
                else
                {
                    Debug.LogWarning($"[HoloCade.Payment] CheckBalance failed: {www.error}");
                    callback?.Invoke(0f);
                }
            }
        }

        /// <summary>
        /// Allocate tokens/credits for gameplay (async coroutine)
        /// </summary>
        public IEnumerator AllocateTokens(string stationId, float amount, Action<bool> callback)
        {
            if (string.IsNullOrEmpty(stationId))
            {
                Debug.LogWarning("[HoloCade.Payment] AllocateTokens called with empty stationId");
                callback?.Invoke(false);
                yield break;
            }

            string url = BuildEndpoint("allocate", stationId, amount.ToString());
            
            // Create JSON payload using Unity's JsonUtility
            var payload = new { cardId = config.cardId, amount = amount, stationId = stationId };
            string jsonBody = JsonUtility.ToJson(payload);

            using (var www = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
            {
                byte[] body = Encoding.UTF8.GetBytes(jsonBody);
                www.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(body);
                www.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("Authorization", "Bearer " + config.apiKey);

                yield return www.SendWebRequest();

                bool success = www.result == UnityEngine.Networking.UnityWebRequest.Result.Success;
                callback?.Invoke(success);
                
                if (!success)
                {
                    Debug.LogWarning($"[HoloCade.Payment] AllocateTokens failed: {www.error}");
                }
            }
        }

        string BuildEndpoint(string action, params string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return config.baseUrl;
            }

            return config.provider switch
            {
                PaymentProvider.Embed => action switch
                {
                    "balance" => $"{config.baseUrl}/balance/{parts[0]}",
                    "allocate" => $"{config.baseUrl}/allocate/{parts[0]}/{parts[1]}",
                    _ => config.baseUrl
                },
                PaymentProvider.Nayax => action switch
                {
                    "balance" => $"{config.baseUrl}/v1/card/balance?card_id={parts[0]}",
                    "allocate" => $"{config.baseUrl}/credit/allocate",
                    _ => config.baseUrl
                },
                PaymentProvider.Intercard => action switch
                {
                    "balance" => $"{config.baseUrl}/api/player/balance?card={parts[0]}",
                    "allocate" => $"{config.baseUrl}/game/play",
                    _ => config.baseUrl
                },
                PaymentProvider.CoreCashless => action switch
                {
                    "balance" => $"{config.baseUrl}/balances/{parts[0]}",
                    "allocate" => $"{config.baseUrl}/allocate/tokens",
                    _ => config.baseUrl
                },
                PaymentProvider.Cantaloupe => action switch
                {
                    "balance" => $"{config.baseUrl}/device/balance?device_id={parts[0]}",
                    "allocate" => $"{config.baseUrl}/play/allocate",
                    _ => config.baseUrl
                },
                _ => config.baseUrl
            };
        }

        string GetWebhookPath()
        {
            return config.provider switch
            {
                PaymentProvider.Embed => "embed",
                PaymentProvider.Nayax => "nayax",
                PaymentProvider.Intercard => "intercard",
                PaymentProvider.CoreCashless => "core",
                PaymentProvider.Cantaloupe => "cantaloupe",
                _ => "unknown"
            };
        }

        float ExtractBalance(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return 0f;
            }

            // Provider-specific balance field names
            // Note: This is a simplified extraction - actual API responses may vary
            string balanceField = config.provider switch
            {
                PaymentProvider.Embed => "balance",
                PaymentProvider.Nayax => "credits",
                PaymentProvider.Intercard => "tokens",
                PaymentProvider.CoreCashless => "balance",
                PaymentProvider.Cantaloupe => "balance",
                _ => "balance"
            };

            try
            {
                // Simple string extraction - look for the balance field
                int fieldIndex = json.IndexOf($"\"{balanceField}\"", StringComparison.OrdinalIgnoreCase);
                if (fieldIndex >= 0)
                {
                    int colonIndex = json.IndexOf(':', fieldIndex);
                    if (colonIndex >= 0)
                    {
                        int startIndex = colonIndex + 1;
                        // Skip whitespace
                        while (startIndex < json.Length && char.IsWhiteSpace(json[startIndex]))
                            startIndex++;
                        
                        int endIndex = startIndex;
                        // Find end of number (comma, }, or whitespace)
                        while (endIndex < json.Length && 
                               (char.IsDigit(json[endIndex]) || json[endIndex] == '.' || json[endIndex] == '-' || json[endIndex] == '+'))
                            endIndex++;
                        
                        if (endIndex > startIndex)
                        {
                            string valueStr = json.Substring(startIndex, endIndex - startIndex);
                            if (float.TryParse(valueStr, out float balance))
                            {
                                return balance;
                            }
                        }
                    }
                }
                
                Debug.LogWarning($"[HoloCade.Payment] Could not extract {balanceField} from JSON: {json}");
                return 0f;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HoloCade.Payment] Failed to parse balance from JSON: {ex.Message}");
                return 0f;
            }
        }

        string GetLocalIPAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HoloCade.Payment] Failed to get local IP: {ex.Message}");
            }
            return "127.0.0.1";
        }

        void OnDestroy()
        {
            isServerRunning = false;
            
            if (listener != null && listener.IsListening)
            {
                listener.Stop();
                listener.Close();
            }
            
            if (listenerThread != null && listenerThread.IsAlive)
            {
                listenerThread.Join(1000);
            }
        }
    }

    /// <summary>
    /// Simple main thread dispatcher for Unity (to process webhook callbacks on main thread)
    /// Reuses the one from ProAudio module if available, otherwise creates its own
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher instance;
        private static readonly Queue<Action> actionQueue = new Queue<Action>();
        private static readonly object lockObject = new object();

        public static UnityMainThreadDispatcher Instance
        {
            get
            {
                if (instance == null && UnityEngine.Application.isPlaying)
                {
                    // Check if one already exists from another module
                    var existing = FindFirstObjectByType<UnityMainThreadDispatcher>();
                    if (existing != null)
                    {
                        instance = existing;
                    }
                    else
                    {
                        GameObject go = new GameObject("UnityMainThreadDispatcher");
                        instance = go.AddComponent<UnityMainThreadDispatcher>();
                        UnityEngine.Object.DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        public void Enqueue(Action action)
        {
            if (action == null) return;
            
            lock (lockObject)
            {
                actionQueue.Enqueue(action);
            }
        }

        void Update()
        {
            lock (lockObject)
            {
                while (actionQueue.Count > 0)
                {
                    Action action = actionQueue.Dequeue();
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[HoloCade.Payment] Error in main thread dispatcher: {ex.Message}");
                    }
                }
            }
        }
    }
}
