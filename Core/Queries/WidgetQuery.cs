using System;
using System.Collections.Generic;
using System.Linq;

namespace OsrsMr.Core.Queries
{
    /// <summary>
    /// Fluent query builder for searching and interacting with game widgets/interfaces.
    /// </summary>
    public class WidgetQuery : EntityQuery<WidgetSnapshot, WidgetQuery>
    {
        public WidgetQuery(IEnumerable<WidgetSnapshot> source) : base(source)
        {
        }

        public WidgetQuery InGroup(int groupId) => Filter(w => w.GroupId == groupId);

        public WidgetQuery WithChildId(int childId) => Filter(w => w.ChildId == childId);

        public WidgetQuery WithId(int id) => Filter(w => w.Id == id);

        public WidgetQuery VisibleOnly() => Filter(w => !w.IsHidden && w.BoundsWidth > 0 && w.BoundsHeight > 0);

        public WidgetQuery WithText(string text, bool exact = false)
        {
            if (string.IsNullOrWhiteSpace(text)) return this;
            return Filter(w => !string.IsNullOrEmpty(w.Text) &&
                (exact ? string.Equals(w.Text, text, StringComparison.OrdinalIgnoreCase)
                       : w.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        public WidgetQuery WithItem(int itemId) => Filter(w => w.ItemId == itemId);

        public WidgetQuery WithAction(string action)
        {
            if (string.IsNullOrWhiteSpace(action)) return this;
            return Filter(w => w.Actions != null && w.Actions.Any(a => a.IndexOf(action, StringComparison.OrdinalIgnoreCase) >= 0));
        }
    }
}
