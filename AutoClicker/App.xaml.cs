using Microsoft.Extensions.DependencyInjection;
using System;
#if WINDOWS
using WinRT.Interop;
#endif

namespace AutoClicker
{
    public partial class App : Application
    {
        const int FixedWidth = 400;
        const int FixedHeight = 750;

        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell())
            {
                Width = FixedWidth,
                Height = FixedHeight
            };


            return window;
        }
    }
}