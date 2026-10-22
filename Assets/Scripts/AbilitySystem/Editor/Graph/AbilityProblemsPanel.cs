using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace SimpleRPG.Abilities.Editor
{
    /// <summary>
    /// The list of everything wrong with the ability, as the compiler reports it. A problem that belongs to a node can be clicked to select and
    /// show that node. Also shows the reason when the editor refuses an edit.
    /// </summary>
    public sealed class AbilityProblemsPanel : VisualElement
    {
        private readonly Action<string> selectNode;

        private readonly Label summary = new Label();

        private readonly Label notice = new Label();

        private readonly VisualElement list = new VisualElement();

        public AbilityProblemsPanel(Action<string> selectNode)
        {
            this.selectNode = selectNode;
            style.maxHeight = 140;
            style.flexShrink = 0;
            style.borderTopWidth = 1;
            style.borderTopColor = new Color(0f, 0f, 0f, 0.4f);
            style.paddingLeft = 8;
            style.paddingRight = 8;
            style.paddingTop = 3;

            summary.style.unityFontStyleAndWeight = FontStyle.Bold;
            notice.style.color = new Color(0.9f, 0.65f, 0.15f);
            notice.style.display = DisplayStyle.None;
            Add(summary);
            Add(notice);
            ScrollView scroll = new ScrollView();
            scroll.Add(list);
            Add(scroll);
        }

        /// <summary>The summary line, for example "2 errors, 1 warning".</summary>
        public string Summary => summary.text;

        /// <summary>The message shown for the last refused edit, empty if none.</summary>
        public string Notice => notice.style.display == DisplayStyle.None ? string.Empty : notice.text;

        /// <summary>One row per problem.</summary>
        public IReadOnlyList<Button> Rows => list.Query<Button>().ToList();

        /// <summary>
        /// Shows a compile result.
        /// </summary>
        public void Show(CompileResult result)
        {
            list.Clear();
            List<ValidationMessage> messages = result.Report.Messages
                .OrderByDescending(m => m.Severity)
                .ToList();
            int errors = messages.Count(m => m.Severity == ValidationSeverity.Error);
            int warnings = messages.Count - errors;

            summary.text = messages.Count == 0
                ? "No problems. The ability compiles."
                : Count(errors, "error") + (errors > 0 && warnings > 0 ? ", " : string.Empty) + Count(warnings, "warning") + (errors > 0 ? ". The ability will not run." : ". The ability compiles.");
            summary.style.color = errors > 0 ? new Color(0.9f, 0.35f, 0.35f) : warnings > 0 ? new Color(0.9f, 0.65f, 0.15f) : new Color(0.45f, 0.8f, 0.5f);

            foreach (ValidationMessage message in messages)
            {
                string nodeId = message.NodeId;
                ValidationMessage chosen = message;
                string where = nodeId == null ? string.Empty : "[" + nodeId + (message.PortId == null ? string.Empty : "." + message.PortId) + "] ";
                Button row = new Button(() => Choose(chosen))
                {
                    text = (message.Severity == ValidationSeverity.Error ? "Error: " : "Warning: ") + where + message.Text,
                };
                row.style.unityTextAlign = TextAnchor.MiddleLeft;
                row.style.whiteSpace = WhiteSpace.Normal;
                row.style.marginLeft = 0;
                row.style.marginRight = 0;
                row.style.color = message.Severity == ValidationSeverity.Error ? new Color(0.9f, 0.45f, 0.45f) : new Color(0.9f, 0.7f, 0.3f);
                row.userData = message;
                list.Add(row);
            }
        }

        /// <summary>
        /// What clicking a problem does: select its node. A problem with the whole ability has no node, so nothing happens.
        /// </summary>
        public void Choose(ValidationMessage message)
        {
            if (message != null && message.NodeId != null) selectNode(message.NodeId);
        }

        /// <summary>
        /// Shows why an edit was refused, until the next edit.
        /// </summary>
        public void ShowNotice(string text)
        {
            notice.text = text;
            notice.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private static string Count(int count, string word) => count == 0 ? string.Empty : count + " " + word + (count == 1 ? string.Empty : "s");
    }
}
