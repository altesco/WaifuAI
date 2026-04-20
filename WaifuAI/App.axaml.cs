using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using WaifuAI.Services;
using WaifuAI.ViewModels;
using WaifuAI.Views;
using WebViewControl;

namespace WaifuAI
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                WebView.Settings.AddCommandLineSwitch("autoplay-policy", "no-user-gesture-required");
                DisableAvaloniaDataAnnotationValidation();
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainVM()
                };
                InputElement.KeyDownEvent.AddClassHandler(
                    typeof(TextBox), 
                    GlobalCutHandler, 
                    RoutingStrategies.Tunnel);
                ActualThemeVariantChanged += (_, _) => 
                {
                    if (SettingsVM.Instance.IsLoading)
                        return;
                    ModelService.SetBackground();
                    Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await Task.Delay(10);
                        WeakReferenceMessenger.Default.Send(new SnapshotMessage(true));
                    });
                };
            }

            var culture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            base.OnFrameworkInitializationCompleted();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }

        private void GlobalCutHandler(object? sender, RoutedEventArgs e)
        {
            if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
                desktop.MainWindow?.DataContext is not MainVM mainVM)
                return;
            if (e is KeyEventArgs keyArgs && keyArgs.Key == Key.X && keyArgs.KeyModifiers == KeyModifiers.Control)
                mainVM.ManualCutCommand.Execute(sender);
        }
    }
}