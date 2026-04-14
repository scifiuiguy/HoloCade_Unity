// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HoloCade.Core
{
    /// <summary>
    /// Experience state definition
    /// </summary>
    [Serializable]
    public class ExperienceState
    {
        [Tooltip("Unique identifier for this state")]
        public string stateName = "";

        [Tooltip("Human-readable description")]
        public string description = "";

        [Tooltip("Can this state be skipped forward?")]
        public bool canSkipForward = true;

        [Tooltip("Can this state be rewound backward?")]
        public bool canSkipBackward = true;

        [Tooltip("Duration of this state in seconds (0 = infinite)")]
        public float duration = 0f;

        [Tooltip("Optional audio cue for this state")]
        public string audioCue = "";

        public ExperienceState() { }

        public ExperienceState(string name, string desc)
        {
            stateName = name;
            description = desc;
        }
    }

    /// <summary>
    /// Event for state changes
    /// </summary>
    [Serializable]
    public class StateChangedEvent : UnityEvent<string, string, int> { }

    /// <summary>
    /// Experience Loop State Machine
    /// 
    /// Manages the progression of an LBE experience through discrete states.
    /// Live actors use wrist-mounted buttons to advance/retreat through the experience.
    /// 
    /// Example states: Intro -> Tutorial -> Act1 -> Act2 -> Finale -> Credits
    /// </summary>
    public class ExperienceStateMachine : MonoBehaviour
    {
        [Header("State Machine")]
        [SerializeField] private List<ExperienceState> states = new List<ExperienceState>();
        [SerializeField] private int currentStateIndex = 0;
        [SerializeField] private bool isRunning = false;

        [Header("Events")]
        public StateChangedEvent onStateChanged = new StateChangedEvent();

        /// <summary>
        /// Current state index (read-only)
        /// </summary>
        public int CurrentStateIndex => currentStateIndex;

        /// <summary>
        /// Is the experience running?
        /// </summary>
        public bool IsRunning => isRunning;

        /// <summary>
        /// All states in this experience
        /// </summary>
        public List<ExperienceState> States => states;

        /// <summary>
        /// Initialize the state machine with states
        /// </summary>
        public void Initialize(List<ExperienceState> newStates)
        {
            states = new List<ExperienceState>(newStates);
            currentStateIndex = 0;
            isRunning = false;

            if (states.Count > 0)
            {
                Debug.Log($"[HoloCade] ExperienceStateMachine: Initialized with {states.Count} states");
            }
            else
            {
                Debug.LogWarning("[HoloCade] ExperienceStateMachine: Initialized with no states");
            }
        }

        /// <summary>
        /// Start the experience from the first state
        /// </summary>
        public void StartExperience()
        {
            if (states.Count == 0)
            {
                Debug.LogError("[HoloCade] ExperienceStateMachine: Cannot start - no states defined");
                return;
            }

            currentStateIndex = 0;
            isRunning = true;

            string initialState = states[0].stateName;
            Debug.Log($"[HoloCade] ExperienceStateMachine: Started at state '{initialState}'");

            BroadcastStateChange("", initialState);
        }

        /// <summary>
        /// Advance to the next state
        /// </summary>
        public bool AdvanceState()
        {
            if (!isRunning)
            {
                Debug.LogWarning("[HoloCade] ExperienceStateMachine: Cannot advance - experience not running");
                return false;
            }

            if (!CanAdvance())
            {
                Debug.LogWarning("[HoloCade] ExperienceStateMachine: Cannot advance from current state");
                return false;
            }

            string oldState = GetCurrentStateName();
            currentStateIndex++;
            string newState = GetCurrentStateName();

            Debug.Log($"[HoloCade] ExperienceStateMachine: Advanced from '{oldState}' to '{newState}' (Index {currentStateIndex})");

            BroadcastStateChange(oldState, newState);
            return true;
        }

        /// <summary>
        /// Retreat to the previous state
        /// </summary>
        public bool RetreatState()
        {
            if (!isRunning)
            {
                Debug.LogWarning("[HoloCade] ExperienceStateMachine: Cannot retreat - experience not running");
                return false;
            }

            if (!CanRetreat())
            {
                Debug.LogWarning("[HoloCade] ExperienceStateMachine: Cannot retreat from current state");
                return false;
            }

            string oldState = GetCurrentStateName();
            currentStateIndex--;
            string newState = GetCurrentStateName();

            Debug.Log($"[HoloCade] ExperienceStateMachine: Retreated from '{oldState}' to '{newState}' (Index {currentStateIndex})");

            BroadcastStateChange(oldState, newState);
            return true;
        }

        /// <summary>
        /// Jump to a specific state by name
        /// </summary>
        public bool JumpToState(string stateName)
        {
            for (int i = 0; i < states.Count; i++)
            {
                if (states[i].stateName == stateName)
                {
                    return JumpToStateIndex(i);
                }
            }

            Debug.LogWarning($"[HoloCade] ExperienceStateMachine: State '{stateName}' not found");
            return false;
        }

        /// <summary>
        /// Jump to a specific state by index
        /// </summary>
        public bool JumpToStateIndex(int stateIndex)
        {
            if (stateIndex < 0 || stateIndex >= states.Count)
            {
                Debug.LogError($"[HoloCade] ExperienceStateMachine: Invalid state index {stateIndex}");
                return false;
            }

            string oldState = GetCurrentStateName();
            currentStateIndex = stateIndex;
            string newState = GetCurrentStateName();

            Debug.Log($"[HoloCade] ExperienceStateMachine: Jumped from '{oldState}' to '{newState}' (Index {currentStateIndex})");

            BroadcastStateChange(oldState, newState);
            return true;
        }

        /// <summary>
        /// Get the current state
        /// </summary>
        public ExperienceState GetCurrentState()
        {
            if (currentStateIndex >= 0 && currentStateIndex < states.Count)
            {
                return states[currentStateIndex];
            }

            return null;
        }

        /// <summary>
        /// Get the current state name
        /// </summary>
        public string GetCurrentStateName()
        {
            var state = GetCurrentState();
            return state != null ? state.stateName : "";
        }

        /// <summary>
        /// Check if we can advance from current state
        /// </summary>
        public bool CanAdvance()
        {
            if (currentStateIndex < 0 || currentStateIndex >= states.Count)
            {
                return false;
            }

            // Check if we're at the last state
            if (currentStateIndex >= states.Count - 1)
            {
                return false;
            }

            // Check if current state allows skipping forward
            return states[currentStateIndex].canSkipForward;
        }

        /// <summary>
        /// Check if we can retreat from current state
        /// </summary>
        public bool CanRetreat()
        {
            if (currentStateIndex < 0 || currentStateIndex >= states.Count)
            {
                return false;
            }

            // Check if we're at the first state
            if (currentStateIndex <= 0)
            {
                return false;
            }

            // Check if current state allows skipping backward
            return states[currentStateIndex].canSkipBackward;
        }

        /// <summary>
        /// Reset to the first state
        /// </summary>
        public void ResetExperience()
        {
            string oldState = GetCurrentStateName();
            currentStateIndex = 0;
            string newState = GetCurrentStateName();

            Debug.Log($"[HoloCade] ExperienceStateMachine: Reset to initial state '{newState}'");

            if (isRunning)
            {
                BroadcastStateChange(oldState, newState);
            }
        }

        /// <summary>
        /// Stop the experience
        /// </summary>
        public void StopExperience()
        {
            isRunning = false;
            Debug.Log($"[HoloCade] ExperienceStateMachine: Experience stopped at state '{GetCurrentStateName()}'");
        }

        private void BroadcastStateChange(string oldState, string newState)
        {
            onStateChanged?.Invoke(oldState, newState, currentStateIndex);
        }
    }
}



