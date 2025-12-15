using Microsoft.Maui.Handlers;

namespace AutoClicker.Platforms.Windows
{
    public class OptimizedEntryHandler : EntryHandler
    {
        protected override void ConnectHandler(Microsoft.UI.Xaml.Controls.TextBox platformView)
        {
            base.ConnectHandler(platformView);

            // Disable spell checking and text prediction for significant performance improvement
            platformView.IsSpellCheckEnabled = false;
            platformView.IsTextPredictionEnabled = false;
        }
    }
}
