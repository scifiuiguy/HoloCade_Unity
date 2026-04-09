// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.Collections;

namespace HoloCade.HoloCadeAI
{
    /// <summary>
    /// OpenAI-Compatible LLM Provider
    /// 
    /// Implements ILLMProvider for OpenAI-compatible APIs.
    /// Supports:
    /// - NVIDIA NIM (containerized, hot-swappable)
    /// - vLLM
    /// - OpenAI API
    /// - Claude API (if OpenAI-compatible)
    /// - Any other OpenAI-compatible service
    /// 
    /// **NVIDIA NIM Hot-Swapping:**
    /// NIM runs as Docker containers, enabling easy model swapping:
    /// - Each model container exposes OpenAI-compatible API on port 8000
    /// - Swap models by changing endpoint URL to different container port
    /// - No code changes required - just update config
    /// </summary>
    public class LLMProviderOpenAICompatible : MonoBehaviour, ILLMProvider
    {
        public string endpointURL = "http://localhost:8000";
        public string apiKey = "";
        private AIHTTPClient httpClient;
        private bool isInitialized = false;

        private void Start()
        {
            if (httpClient == null)
                httpClient = gameObject.AddComponent<AIHTTPClient>();
            isInitialized = true;
        }

        public void Initialize(string inEndpointURL, string inAPIKey = "")
        {
            endpointURL = inEndpointURL;
            apiKey = inAPIKey;
            isInitialized = true;
        }

        public void RequestResponse(LLMRequest request, Action<LLMResponse> callback)
        {
            if (!isInitialized || httpClient == null)
            {
                callback?.Invoke(new LLMResponse { errorMessage = "Provider not initialized" });
                return;
            }

            StartCoroutine(SendOpenAIRequest(request, callback));
        }

        private IEnumerator SendOpenAIRequest(LLMRequest request, Action<LLMResponse> callback)
        {
            string url = $"{endpointURL}/v1/chat/completions";
            
            var messages = new List<OpenAIMessage>();
            messages.Add(new OpenAIMessage { role = "system", content = request.systemPrompt });
            
            foreach (var history in request.conversationHistory)
            {
                if (history.StartsWith("Player:"))
                    messages.Add(new OpenAIMessage { role = "user", content = history.Substring(7).Trim() });
                else if (history.StartsWith("AI:"))
                    messages.Add(new OpenAIMessage { role = "assistant", content = history.Substring(3).Trim() });
            }
            
            messages.Add(new OpenAIMessage { role = "user", content = request.playerInput });

            var requestBody = new OpenAIRequest
            {
                model = request.modelName,
                messages = messages.ToArray(),
                temperature = request.temperature,
                max_tokens = request.maxTokens
            };

            string jsonString = JsonUtility.ToJson(requestBody);
            var headers = new Dictionary<string, string> { { "Content-Type", "application/json" } };
            if (!string.IsNullOrEmpty(apiKey))
                headers["Authorization"] = $"Bearer {apiKey}";

            bool completed = false;
            LLMResponse response = new LLMResponse();

            httpClient.PostJSON(url, jsonString, headers, (result) =>
            {
                if (result.success)
                {
                    try
                    {
                        var jsonResponse = JsonUtility.FromJson<OpenAIResponse>(result.responseBody);
                        if (jsonResponse.choices != null && jsonResponse.choices.Length > 0)
                        {
                            response.responseText = jsonResponse.choices[0].message.content ?? "";
                            response.isComplete = true;
                        }
                        else
                        {
                            response.errorMessage = "No response in API result";
                        }
                    }
                    catch (Exception e)
                    {
                        response.errorMessage = $"Failed to parse response: {e.Message}";
                    }
                }
                else
                {
                    response.errorMessage = result.errorMessage;
                }
                completed = true;
            });

            yield return new WaitUntil(() => completed);
            callback?.Invoke(response);
        }

        public bool IsAvailable()
        {
            return isInitialized && httpClient != null;
        }

        public string GetProviderName()
        {
            return "OpenAI-Compatible";
        }

        public List<string> GetSupportedModels()
        {
            // TODO: Query API for available models
            return new List<string> { "llama-3.2-3b-instruct", "mistral-7b-instruct", "phi-3-mini" };
        }

        [Serializable]
        private class OpenAIRequest
        {
            public string model;
            public OpenAIMessage[] messages;
            public float temperature;
            public int max_tokens;
        }

        [Serializable]
        private class OpenAIMessage
        {
            public string role;
            public string content;
        }

        [Serializable]
        private class OpenAIResponse
        {
            public Choice[] choices;
        }

        [Serializable]
        private class Choice
        {
            public Message message;
        }

        [Serializable]
        private class Message
        {
            public string content;
        }
    }
}

