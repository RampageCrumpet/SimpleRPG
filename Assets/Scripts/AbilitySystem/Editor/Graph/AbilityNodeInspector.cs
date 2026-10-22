using SimpleRPG.Gameplay;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SimpleRPG.Abilities.Editor
{
    /// <summary>
    /// The settings panel of the ability editor. For a selected node it shows that node's settings; with nothing selected it shows the ability's
    /// own details (name, tooltip, icon, sound, cooldown). Fields are built from the serialized settings of the node's type, so a new node type
    /// or a new setting appears here with no editor change, and every change is a normal property edit: undoable, saved with the asset, and
    /// it makes the ability compile again.
    /// </summary>
    public sealed class AbilityNodeInspector : VisualElement
    {
        /// <summary>The attribute-key field gets a menu of the primary attributes, so a common key does not have to be typed.</summary>
        private const string AttributeKeyField = "AttributeKey";

        private readonly AbilityGraphDocument document;

        private SerializedObject serialized;

        public AbilityNodeInspector(AbilityGraphDocument document)
        {
            this.document = document;
            style.width = 300;
            style.flexShrink = 0;
            style.paddingLeft = 8;
            style.paddingRight = 8;
            style.paddingTop = 6;
            style.borderLeftWidth = 1;
            style.borderLeftColor = new Color(0f, 0f, 0f, 0.4f);
            RegisterCallback<DetachFromPanelEvent>(_ => ReleaseSerialized());

            // If the window is docked elsewhere the panel is detached and attached again; the fields need their data back.
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                if (serialized == null) Show(NodeId);
            });
            Show(null);
        }

        // A SerializedObject holds native memory, so the one being replaced is disposed rather than left to the garbage collector.
        private void ReleaseSerialized()
        {
            serialized?.Dispose();
            serialized = null;
        }

        /// <summary>The ID of the node being shown, or null when showing the ability's details.</summary>
        public string NodeId { get; private set; }

        /// <summary>The fields currently shown, for tests and tools.</summary>
        public IEnumerable<PropertyField> Fields => this.Query<PropertyField>().ToList();

        /// <summary>
        /// Shows one node's settings, or the ability's details if no node is given.
        /// </summary>
        public void Show(string nodeId)
        {
            Clear();
            ReleaseSerialized();
            NodeId = nodeId;
            serialized = new SerializedObject(document.Definition);

            if (nodeId == null)
            {
                ShowAbilityDetails();
            }
            else
            {
                ShowNode(nodeId);
            }

            this.Bind(serialized);
        }

        private void ShowAbilityDetails()
        {
            Add(Heading("Ability"));
            foreach (string field in new[] { "abilityName", "toolTip", "icon", "sound", "cooldownSeconds" })
            {
                Add(new PropertyField(serialized.FindProperty(field)));
            }

            Add(Hint("Select a node to edit its settings."));
        }

        private void ShowNode(string nodeId)
        {
            int index = IndexOf(nodeId);
            AbilityNodeRecord record = index >= 0 ? document.Definition.Nodes[index] : null;
            if (record == null)
            {
                Add(Hint("This node no longer exists."));
                return;
            }

            if (!document.Registry.TryGet(record.TypeId, out IAbilityNodeDescriptor descriptor))
            {
                Add(Heading("Unknown node"));
                Add(Hint("No node type is registered as '" + record.TypeId + "'. Delete the node."));
                return;
            }

            Add(Heading(descriptor.DisplayName));
            Add(Hint(descriptor.Description));

            SerializedProperty config = serialized.FindProperty("nodes").GetArrayElementAtIndex(index).FindPropertyRelative("Config");
            bool usable = config.propertyType == SerializedPropertyType.ManagedReference
                && config.managedReferenceValue != null
                && descriptor.ConfigType.IsInstanceOfType(config.managedReferenceValue);
            if (!usable)
            {
                Add(Hint("This node's settings are missing or are not the kind " + descriptor.DisplayName + " uses."));
                Button reset = new Button(() => document.ResetConfig(nodeId)) { text = "Reset to defaults" };
                Add(reset);
                return;
            }

            SerializedProperty child = config.Copy();
            SerializedProperty end = config.GetEndProperty();
            bool any = false;
            for (bool enter = true; child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end); enter = false)
            {
                any = true;
                Add(child.name == AttributeKeyField ? AttributeKeyRow(child.Copy()) : new PropertyField(child.Copy()));
            }

            if (!any)
            {
                Add(Hint("This node has no settings."));
            }
        }

        private VisualElement AttributeKeyRow(SerializedProperty property)
        {
            VisualElement row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.FlexEnd } };
            PropertyField field = new PropertyField(property);
            field.style.flexGrow = 1;
            row.Add(field);

            ToolbarMenu menu = new ToolbarMenu { text = "Pick" };
            foreach (AttributeId attribute in PrimaryAttributes.All)
            {
                string key = attribute.Key;
                menu.menu.AppendAction(key.Substring(key.LastIndexOf('.') + 1), _ =>
                {
                    property.stringValue = key;
                    property.serializedObject.ApplyModifiedProperties();
                });
            }

            row.Add(menu);
            return row;
        }

        private int IndexOf(string nodeId)
        {
            for (int i = 0; i < document.Definition.Nodes.Count; i++)
            {
                if (document.Definition.Nodes[i] != null && document.Definition.Nodes[i].NodeId == nodeId) return i;
            }

            return -1;
        }

        private static Label Heading(string text)
        {
            Label label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 13;
            label.style.marginBottom = 4;
            return label;
        }

        private static Label Hint(string text)
        {
            Label label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.opacity = 0.7f;
            label.style.marginBottom = 6;
            return label;
        }
    }
}
