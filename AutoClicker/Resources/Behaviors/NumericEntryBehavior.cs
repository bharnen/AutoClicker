using System;
using System.Collections.Generic;
using System.Text;

namespace AutoClicker.Resources.Behaviors
{
    public class NumericEntryBehavior : Behavior<Entry>
    {
        protected override void OnAttachedTo(Entry entry)
        {
            entry.TextChanged += OnTextChanged;
            base.OnAttachedTo(entry);
        }

        protected override void OnDetachingFrom(Entry entry)
        {
            entry.TextChanged -= OnTextChanged;
            base.OnDetachingFrom(entry);
        }

        private void OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender == null)
                return;
            var entry = (Entry)sender;
            string newText = new string(e.NewTextValue.Where(c => char.IsDigit(c) || c == '.').ToArray());

            if (entry.Text != newText)
                entry.Text = newText;
        }
    }

}
