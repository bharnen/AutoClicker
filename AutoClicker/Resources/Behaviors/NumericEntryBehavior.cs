using System;
using System.Collections.Generic;
using System.Text;

namespace AutoClicker.Resources.Behaviors
{
    public class NumericEntryBehavior : Behavior<Entry>
    {
        private bool _isProcessing = false;
        private string _lastProcessedText = string.Empty;
        private int _lastCursorPosition = 0;

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
            // Prevent recursive calls
            if (_isProcessing || sender == null)
                return;

            // Ignore if text hasn't actually changed (selection changes trigger TextChanged in MAUI)
            if (e.NewTextValue == _lastProcessedText)
                return;

            var entry = (Entry)sender;
            var input = e.NewTextValue ?? string.Empty;
            
            // Fast path: if string is empty or already valid, skip processing
            if (input.Length == 0 || IsValidNumericInput(input))
            {
                _lastProcessedText = input;
                return;
            }

            // Store cursor position before modification
            int cursorPos = entry.CursorPosition;

            // Filter to only digits and decimal point using allocation-free approach
            string newText = FilterNumericInput(input);

            // Only update if filtering actually changed something
            if (entry.Text != newText)
            {
                _isProcessing = true;
                
                // Batch layout updates to prevent thrashing
                entry.BatchBegin();
                try
                {
                    entry.Text = newText;
                    _lastProcessedText = newText;
                    
                    // Restore cursor position (adjust for removed characters)
                    int removedChars = input.Length - newText.Length;
                    int newCursorPos = Math.Max(0, Math.Min(cursorPos - removedChars, newText.Length));
                    entry.CursorPosition = newCursorPos;
                }
                finally
                {
                    entry.BatchCommit();
                    _isProcessing = false;
                }
            }
            else
            {
                // Update cache even if no change needed
                _lastProcessedText = newText;
            }
        }

        private static bool IsValidNumericInput(string input)
        {
            // Quick validation without allocation
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (!char.IsDigit(c) && c != '.')
                    return false;
            }
            return true;
        }

        private static string FilterNumericInput(string input)
        {
            // Use Span for allocation-free filtering
            Span<char> buffer = stackalloc char[input.Length];
            int writeIndex = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (char.IsDigit(c) || c == '.')
                {
                    buffer[writeIndex++] = c;
                }
            }

            return writeIndex == input.Length 
                ? input 
                : new string(buffer.Slice(0, writeIndex));
        }
    }
}
