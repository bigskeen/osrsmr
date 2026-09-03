using System;

namespace OsrsMr.Core.Scripting
{
    /// <summary>
    /// Attribute to mark public properties on a BotScript that can be configured by the user via the UI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class ScriptSettingAttribute : Attribute
    {
        public string Label { get; }
        public string Description { get; }
        public string Group { get; }
        public object? DefaultValue { get; set; }
        public int Order { get; set; }
        public string[]? Options { get; set; }

        public ScriptSettingAttribute(
            string label,
            string description = "",
            string group = "General")
        {
            Label = label;
            Description = description;
            Group = group;
        }
    }
}
