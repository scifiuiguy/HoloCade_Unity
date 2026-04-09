// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using UnityEngine.InputSystem;
using HoloCade.ExperienceTemplates;

namespace HoloCade.Core.Input
{
    /// <summary>
    /// Optional helper class that bridges Unity's Input System to the HoloCadeInputAdapter.
    /// This allows developers to use standard gamepads, keyboards, and mice for testing LBE experiences
    /// without physical hardware (ESP32, VR controllers, etc.).
    /// 
    /// Usage:
    /// 1. Create an Input Action Asset in the editor
    /// 2. Define actions (Button0, Button1, Axis0, etc.)
    /// 3. Assign the Input Action Asset to this component
    /// 4. Input will automatically route to the experience's InputAdapter
    /// 
    /// Typical Use Cases:
    /// - Development testing with gamepad before hardware is available
    /// - Listen server hosts using keyboard/gamepad instead of VR controllers
    /// - Rapid prototyping without ESP32/Arduino setup
    /// 
    /// Production Deployment:
    /// - In production LBE venues, the dedicated server reads directly from ESP32 via InputAdapter
    /// - This controller is optional and only used for development/testing
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class HoloCadePlayerController : MonoBehaviour
    {
        // ========================================
        // CONFIGURATION
        // ========================================

        [Header("Experience Reference")]
        [Tooltip("Reference to the current experience (auto-assigned in Start if not set)")]
        public HoloCadeExperienceBase currentExperience;

        [Tooltip("If true, automatically finds and assigns currentExperience in Start")]
        public bool autoFindExperience = true;

        [Header("Input Actions")]
        [Tooltip("Input Action Asset containing button and axis actions")]
        public InputActionAsset inputActions;

        [Tooltip("If true, logs input events for debugging")]
        public bool debugLogInput = false;

        // ========================================
        // INPUT ACTION REFERENCES
        // ========================================

        private InputAction button0Action;
        private InputAction button1Action;
        private InputAction button2Action;
        private InputAction button3Action;
        private InputAction button4Action;
        private InputAction button5Action;
        private InputAction button6Action;
        private InputAction button7Action;

        private InputAction axis0Action;
        private InputAction axis1Action;
        private InputAction axis2Action;
        private InputAction axis3Action;

        private PlayerInput playerInput;

        // ========================================
        // LIFECYCLE
        // ========================================

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();

            if (inputActions == null && playerInput.actions != null)
            {
                inputActions = playerInput.actions;
            }

            if (inputActions != null)
            {
                BindInputActions();
            }
            else
            {
                Debug.LogWarning("[HoloCadePlayerController] No Input Action Asset assigned. Create one and assign it.");
            }
        }

        private void Start()
        {
            // Auto-find experience if not already assigned
            if (autoFindExperience && currentExperience == null)
            {
                currentExperience = FindFirstObjectByType<HoloCadeExperienceBase>();
                if (currentExperience != null)
                {
                    Debug.Log($"[HoloCadePlayerController] Auto-assigned currentExperience: {currentExperience.name}");
                }
                else
                {
                    Debug.LogWarning("[HoloCadePlayerController] No experience found in scene. Input will not work.");
                }
            }
        }

        private void OnEnable()
        {
            EnableInputActions();
        }

        private void OnDisable()
        {
            DisableInputActions();
        }

        // ========================================
        // INPUT ACTION BINDING
        // ========================================

        private void BindInputActions()
        {
            // Bind digital buttons (8 buttons)
            button0Action = TryFindAction("Button0");
            button1Action = TryFindAction("Button1");
            button2Action = TryFindAction("Button2");
            button3Action = TryFindAction("Button3");
            button4Action = TryFindAction("Button4");
            button5Action = TryFindAction("Button5");
            button6Action = TryFindAction("Button6");
            button7Action = TryFindAction("Button7");

            // Bind analog axes (4 axes)
            axis0Action = TryFindAction("Axis0");
            axis1Action = TryFindAction("Axis1");
            axis2Action = TryFindAction("Axis2");
            axis3Action = TryFindAction("Axis3");

            // Subscribe to button events (started = pressed, canceled = released)
            if (button0Action != null)
            {
                button0Action.started += ctx => OnButtonPressed(0);
                button0Action.canceled += ctx => OnButtonReleased(0);
            }
            if (button1Action != null)
            {
                button1Action.started += ctx => OnButtonPressed(1);
                button1Action.canceled += ctx => OnButtonReleased(1);
            }
            if (button2Action != null)
            {
                button2Action.started += ctx => OnButtonPressed(2);
                button2Action.canceled += ctx => OnButtonReleased(2);
            }
            if (button3Action != null)
            {
                button3Action.started += ctx => OnButtonPressed(3);
                button3Action.canceled += ctx => OnButtonReleased(3);
            }
            if (button4Action != null)
            {
                button4Action.started += ctx => OnButtonPressed(4);
                button4Action.canceled += ctx => OnButtonReleased(4);
            }
            if (button5Action != null)
            {
                button5Action.started += ctx => OnButtonPressed(5);
                button5Action.canceled += ctx => OnButtonReleased(5);
            }
            if (button6Action != null)
            {
                button6Action.started += ctx => OnButtonPressed(6);
                button6Action.canceled += ctx => OnButtonReleased(6);
            }
            if (button7Action != null)
            {
                button7Action.started += ctx => OnButtonPressed(7);
                button7Action.canceled += ctx => OnButtonReleased(7);
            }

            // Subscribe to axis events (performed = value changed)
            if (axis0Action != null)
                axis0Action.performed += ctx => OnAxisChanged(0, ctx.ReadValue<float>());
            if (axis1Action != null)
                axis1Action.performed += ctx => OnAxisChanged(1, ctx.ReadValue<float>());
            if (axis2Action != null)
                axis2Action.performed += ctx => OnAxisChanged(2, ctx.ReadValue<float>());
            if (axis3Action != null)
                axis3Action.performed += ctx => OnAxisChanged(3, ctx.ReadValue<float>());

            Debug.Log("[HoloCadePlayerController] Input actions bound successfully.");
        }

        private InputAction TryFindAction(string actionName)
        {
            if (inputActions == null) return null;

            var action = inputActions.FindAction(actionName);
            if (action == null)
            {
                Debug.LogWarning($"[HoloCadePlayerController] Input action '{actionName}' not found in Input Action Asset. Create it if you want to use it.");
            }
            return action;
        }

        private void EnableInputActions()
        {
            button0Action?.Enable();
            button1Action?.Enable();
            button2Action?.Enable();
            button3Action?.Enable();
            button4Action?.Enable();
            button5Action?.Enable();
            button6Action?.Enable();
            button7Action?.Enable();

            axis0Action?.Enable();
            axis1Action?.Enable();
            axis2Action?.Enable();
            axis3Action?.Enable();
        }

        private void DisableInputActions()
        {
            button0Action?.Disable();
            button1Action?.Disable();
            button2Action?.Disable();
            button3Action?.Disable();
            button4Action?.Disable();
            button5Action?.Disable();
            button6Action?.Disable();
            button7Action?.Disable();

            axis0Action?.Disable();
            axis1Action?.Disable();
            axis2Action?.Disable();
            axis3Action?.Disable();
        }

        // ========================================
        // INPUT CALLBACKS
        // ========================================

        private void OnButtonPressed(int buttonIndex)
        {
            if (currentExperience == null || currentExperience.inputAdapter == null)
            {
                Debug.LogWarning("[HoloCadePlayerController] Cannot inject button input: experience or InputAdapter is null.");
                return;
            }

            if (debugLogInput)
            {
                Debug.Log($"[HoloCadePlayerController] Button {buttonIndex} Pressed");
            }

            currentExperience.inputAdapter.InjectButtonPress(buttonIndex);
        }

        private void OnButtonReleased(int buttonIndex)
        {
            if (currentExperience == null || currentExperience.inputAdapter == null)
            {
                Debug.LogWarning("[HoloCadePlayerController] Cannot inject button input: experience or InputAdapter is null.");
                return;
            }

            if (debugLogInput)
            {
                Debug.Log($"[HoloCadePlayerController] Button {buttonIndex} Released");
            }

            currentExperience.inputAdapter.InjectButtonRelease(buttonIndex);
        }

        private void OnAxisChanged(int axisIndex, float value)
        {
            if (currentExperience == null || currentExperience.inputAdapter == null)
            {
                Debug.LogWarning("[HoloCadePlayerController] Cannot inject axis input: experience or InputAdapter is null.");
                return;
            }

            if (debugLogInput)
            {
                Debug.Log($"[HoloCadePlayerController] Axis {axisIndex} = {value:F2}");
            }

            currentExperience.inputAdapter.InjectAxisValue(axisIndex, value);
        }
    }
}



