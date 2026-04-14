# ProLighting UIToolkit Integration Guide

The ProLighting system uses native C# events (`Action<T>`) that can be subscribed to directly from UIToolkit code. UnityEvents have been removed in favor of this more flexible approach.

## Available Events

### FixtureService Events

```csharp
// Fixture intensity changed (VirtualFixtureID, Intensity 0-1)
controller.FixtureService.OnIntensityChanged += (id, intensity) => {
    // Update your UIToolkit slider/visual element
    var slider = rootVisualElement.Q<Slider>($"fixture-{id}-intensity");
    slider?.SetValueWithoutNotify(intensity);
};

// Fixture color changed (VirtualFixtureID, Red, Green, Blue)
controller.FixtureService.OnColorChanged += (id, r, g, b) => {
    // Update your UIToolkit color picker/visual element
    var colorField = rootVisualElement.Q<ColorField>($"fixture-{id}-color");
    colorField?.SetValueWithoutNotify(new Color(r, g, b));
};
```

### RDMService Events

```csharp
// Fixture discovered via RDM
controller.RDMService.OnDiscoveredEvent += (fixture) => {
    // Add to fixture list in UIToolkit
    AddFixtureToList(fixture);
};

// Fixture went offline
controller.RDMService.OnWentOfflineEvent += (virtualFixtureID) => {
    // Update status indicator in UIToolkit
    var statusIndicator = rootVisualElement.Q<VisualElement>($"fixture-{virtualFixtureID}-status");
    statusIndicator?.AddToClassList("offline");
};

// Fixture came back online
controller.RDMService.OnCameOnlineEvent += (virtualFixtureID) => {
    // Update status indicator in UIToolkit
    var statusIndicator = rootVisualElement.Q<VisualElement>($"fixture-{virtualFixtureID}-status");
    statusIndicator?.RemoveFromClassList("offline");
    statusIndicator?.AddToClassList("online");
};
```

### ArtNetManager Events

```csharp
// Art-Net node discovered
controller.ArtNetManager.OnNodeDiscovered += (node) => {
    // Add to node list in UIToolkit
    AddNodeToList(node);
};
```

## Example: UIToolkit UI Script

```csharp
using UnityEngine;
using UnityEngine.UIElements;
using HoloCade.ProLighting;

public class ProLightingUI : MonoBehaviour
{
    private ProLightingController controller;
    private UIDocument uiDocument;
    private VisualElement rootVisualElement;

    void Start()
    {
        controller = FindObjectOfType<ProLightingController>();
        uiDocument = GetComponent<UIDocument>();
        rootVisualElement = uiDocument.rootVisualElement;

        // Subscribe to events
        if (controller?.FixtureService != null)
        {
            controller.FixtureService.OnIntensityChanged += OnFixtureIntensityChanged;
            controller.FixtureService.OnColorChanged += OnFixtureColorChanged;
        }

        if (controller?.RDMService != null)
        {
            controller.RDMService.OnDiscoveredEvent += OnFixtureDiscovered;
            controller.RDMService.OnWentOfflineEvent += OnFixtureWentOffline;
            controller.RDMService.OnCameOnlineEvent += OnFixtureCameOnline;
        }

        if (controller?.ArtNetManager != null)
        {
            controller.ArtNetManager.OnNodeDiscovered += OnArtNetNodeDiscovered;
        }

        // Setup UI controls
        SetupUIControls();
    }

    void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (controller?.FixtureService != null)
        {
            controller.FixtureService.OnIntensityChanged -= OnFixtureIntensityChanged;
            controller.FixtureService.OnColorChanged -= OnFixtureColorChanged;
        }

        if (controller?.RDMService != null)
        {
            controller.RDMService.OnDiscoveredEvent -= OnFixtureDiscovered;
            controller.RDMService.OnWentOfflineEvent -= OnFixtureWentOffline;
            controller.RDMService.OnCameOnlineEvent -= OnFixtureCameOnline;
        }

        if (controller?.ArtNetManager != null)
        {
            controller.ArtNetManager.OnNodeDiscovered -= OnArtNetNodeDiscovered;
        }
    }

    void OnFixtureIntensityChanged(int id, float intensity)
    {
        var slider = rootVisualElement.Q<Slider>($"fixture-{id}-intensity");
        if (slider != null)
            slider.SetValueWithoutNotify(intensity);
    }

    void OnFixtureColorChanged(int id, float r, float g, float b)
    {
        var colorField = rootVisualElement.Q<ColorField>($"fixture-{id}-color");
        if (colorField != null)
            colorField.SetValueWithoutNotify(new Color(r, g, b));
    }

    void OnFixtureDiscovered(HoloCadeDiscoveredFixture fixture)
    {
        // Add fixture to list
        var fixtureList = rootVisualElement.Q<ListView>("fixture-list");
        // ... add fixture to list view
    }

    void OnFixtureWentOffline(int virtualFixtureID)
    {
        var statusIndicator = rootVisualElement.Q<VisualElement>($"fixture-{virtualFixtureID}-status");
        statusIndicator?.AddToClassList("offline");
    }

    void OnFixtureCameOnline(int virtualFixtureID)
    {
        var statusIndicator = rootVisualElement.Q<VisualElement>($"fixture-{virtualFixtureID}-status");
        statusIndicator?.RemoveFromClassList("offline");
        statusIndicator?.AddToClassList("online");
    }

    void OnArtNetNodeDiscovered(HoloCadeArtNetNode node)
    {
        // Add node to list
        var nodeList = rootVisualElement.Q<ListView>("artnet-node-list");
        // ... add node to list view
    }

    void SetupUIControls()
    {
        // Example: Setup intensity slider
        var intensitySlider = rootVisualElement.Q<Slider>("fixture-1-intensity");
        if (intensitySlider != null)
        {
            intensitySlider.RegisterValueChangedCallback(evt =>
            {
                // Update fixture intensity via service
                controller?.FixtureService?.SetIntensityById(1, evt.newValue);
            });
        }

        // Example: Setup color picker
        var colorField = rootVisualElement.Q<ColorField>("fixture-1-color");
        if (colorField != null)
        {
            colorField.RegisterValueChangedCallback(evt =>
            {
                var color = evt.newValue;
                controller?.FixtureService?.SetColorRGBWById(1, color.r, color.g, color.b, -1f);
            });
        }
    }
}
```

## Important Notes

1. **Always unsubscribe** from events in `OnDestroy()` to prevent memory leaks
2. **Use `SetValueWithoutNotify()`** when updating UI elements from events to avoid triggering user input callbacks
3. **Check for null** before accessing services (they may not be initialized yet)
4. **Thread safety**: Events are raised on the main thread, so UIToolkit updates are safe

## Two-Way Binding

For bidirectional sync (UI ↔ ProLighting), you need both:
1. **Event subscription** (ProLighting → UI): Subscribe to events to update UI when fixture state changes
2. **Value changed callbacks** (UI → ProLighting): Register callbacks on UIToolkit controls to send commands to fixtures

See the `SetupUIControls()` method in the example above for how to set up UI → ProLighting communication.












