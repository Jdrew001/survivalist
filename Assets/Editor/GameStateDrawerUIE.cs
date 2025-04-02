using Assets.Game.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(GameState), true)]
public class GameStateDrawerUIE : PropertyDrawer
{
    private static List<Type> _subclassTypes;

    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        // 'property' is the serialized property of type [SerializeReference] GameState
        // We'll create a container to hold everything
        var container = new VisualElement();

        // Ensure we have a cached list of all non-abstract GameState subclasses
        if (_subclassTypes == null)
        {
            _subclassTypes = GetAllConcreteSubclassesOf<GameState>();
        }

        // 1) Build a dropdown to choose the current subclass
        var dropdown = new PopupField<string>("Game State Type");
        container.Add(dropdown);

        // Convert list of types to their names (or you could store full names)
        var typeNames = _subclassTypes.Select(t => t.Name).ToList();

        // We'll track the currently assigned type
        Type currentType = null;
        if (property.managedReferenceValue != null)
        {
            currentType = property.managedReferenceValue.GetType();
        }

        // If there's no assigned type yet, we might default to the first in the list or show "None"
        var currentTypeName = currentType != null ? currentType.Name : "None";

        // Fill dropdown choices
        dropdown.choices.Clear();
        dropdown.choices.Add("None");
        dropdown.choices.AddRange(typeNames);

        // Set the initial value in the dropdown
        dropdown.value = typeNames.Contains(currentTypeName) ? currentTypeName : "None";

        // 2) We'll make a container to display the actual subclass fields
        var subclassFieldContainer = new VisualElement();
        container.Add(subclassFieldContainer);

        // If there's already a valid type assigned, display its fields
        if (currentType != null)
        {
            subclassFieldContainer.Add(CreateSubFields(property));
        }

        // 3) Handle user picking a different type from the dropdown
        dropdown.RegisterValueChangedCallback(evt =>
        {
            var chosen = evt.newValue;
            if (chosen == "None")
            {
                // Clear out the reference
                property.managedReferenceValue = null;
            }
            else
            {
                // Find the actual Type from our list
                var selectedType = _subclassTypes.FirstOrDefault(t => t.Name == chosen);
                if (selectedType == null)
                {
                    Debug.LogWarning($"No subclass found for {chosen}");
                    return;
                }

                // If we already have an instance of that type, keep it
                // Otherwise create a new instance
                var currentObj = property.managedReferenceValue;
                if (currentObj == null || currentObj.GetType() != selectedType)
                {
                    var newInstance = Activator.CreateInstance(selectedType);
                    property.managedReferenceValue = newInstance;
                }
            }

            // Apply changes so the property is updated
            property.serializedObject.ApplyModifiedProperties();

            // Rebuild the subclass fields container
            subclassFieldContainer.Clear();
            if (property.managedReferenceValue != null)
            {
                subclassFieldContainer.Add(CreateSubFields(property));
            }
        });

        return container;
    }

    private VisualElement CreateSubFields(SerializedProperty property)
    {
        // This will create a default property field for all fields in the current subclass
        // i.e. 'stateName', or 'welcomeMessage', etc.
        // The key is to create a PropertyField for 'property' itself, but not create an infinite loop.
        var subField = new PropertyField(property, "Subclass Fields");
        // We don't want the default label "Subclass Fields", so you could pass null or an empty string
        subField.Bind(property.serializedObject);
        return subField;
    }

    private static List<Type> GetAllConcreteSubclassesOf<T>()
    {
        var baseType = typeof(T);
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(asm =>
            {
                // Some assemblies can throw reflection errors, so catch them
                try { return asm.GetTypes(); }
                catch { return new Type[0]; }
            })
            .Where(type => baseType.IsAssignableFrom(type))
            .Where(type => !type.IsAbstract)
            .Where(type => !type.IsInterface)
            .Where(type => type != baseType)
            .ToList();
    }
}
