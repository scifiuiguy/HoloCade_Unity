// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;

namespace HoloCade
{
    /// <summary>
    /// Optional one-line purpose text shown at the top of the Unity inspector for this component
    /// (see <c>HoloCade.Editor.InspectorPurposeDrawer</c> and per-type custom editors).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class InspectorPurposeAttribute : Attribute
    {
        public string Text { get; }

        public InspectorPurposeAttribute(string text) => Text = text;
    }
}
