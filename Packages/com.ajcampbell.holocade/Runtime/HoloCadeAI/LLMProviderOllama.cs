// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.Collections;

namespace HoloCade.HoloCadeAI
{
    /// <summary>
    /// Ollama LLM Provider
    /// 
    /// Implements ILLMProvider for Ollama API.
    /// Supports local Ollama instances and custom LoRA models.
    /// </summary>
    public class LLMProviderOllama : MonoBehaviour, ILLMProvider
    {
        public string endpointURL = "http://localhost:11434";
        private AIHTTPClient httpClient;
        private bool isInitialized = false;

        private void Start()
        {
            if (httpClient == null)
                httpClient = gameObject.AddComponent<AIHTTPClient>();
            isInitialized = true;
        }

        public void Initialize(string inEndpointURL)
        {
            endpointURL = inEndpointURL;
            isInitialized = true;
        }

        public void RequestResponse(LLMRequest request, Action<LLMResponse> callback)
        {
            if (!isInitialized || httpClient == null)
            {
                callback?.Invoke(new LLMResponse { errorMessage = "Provider not initialized" });
                return;
            }

            StartCoroutine(SendOllamaRequest(request, callback));
        }

        private IEnumerator SendOllamaRequest(LLMRequest request, Action<LLMResponse> callback)
        {
            string url = $"{endpointURL}/api/generate";
            
            var jsonBody = new OllamaRequest
            {
                model = request.modelName,
                prompt = BuildPrompt(request),
                stream = false,
                options = new OllamaOptions
                {
                    temperature = request.temperature,
                    num_predict = request.maxTokens
                }
            };

            string jsonString = JsonUtility.ToJson(jsonBody);
            var headers = new Dictionary<string, string> { { "Content-Type", "application/json" } };

            bool completed = false;
            LLMResponse response = new LLMResponse();

            httpClient.PostJSON(url, jsonString, headers, (result) =>
            {
                if (result.success)
                {
                    try
                    {
                        var jsonResponse = JsonUtility.FromJson<OllamaResponse>(result.responseBody);
                        response.responseText = jsonResponse.response ?? "";
                        response.isComplete = true;
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

        private string BuildPrompt(LLMRequest request)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine(request.systemPrompt);
            prompt.AppendLine();
            
            foreach (var history in request.conversationHistory)
                prompt.AppendLine(history);
            
            prompt.AppendLine($"User: {request.playerInput}");
            prompt.AppendLine("Assistant:");
            
            return prompt.ToString();
        }

        public bool IsAvailable()
        {
            return isInitialized && httpClient != null;
        }

        public string GetProviderName()
        {
            return "Ollama";
        }

        public List<string> GetSupportedModels()
        {
            // TODO: Query Ollama API for available models
            return new List<string> { "llama3.2", "mistral", "phi3" };
        }

        [Serializable]
        private class OllamaResponse
        {
            public string response;
            public bool done;
        }

        [Serializable]
        private class OllamaRequest
        {
            public string model;
            public string prompt;
            public bool stream;
            public OllamaOptions options;
        }

        [Serializable]
        private class OllamaOptions
        {
            public float temperature;
            public int num_predict;
        }
    }
}

