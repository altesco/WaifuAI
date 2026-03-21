using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WaifuAI.ViewModels;
using WaifuAI.Models;
using Lucide.Avalonia;
using System.Threading.Tasks;
using Avalonia.Controls.Shapes;
using Avalonia.Xaml.Interactions.Custom;
using WebViewControl;
using System.Diagnostics;
using WaifuAI.Services;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Messaging;

namespace WaifuAI.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            _lastLeftBarWidth = MainGrid.ColumnDefinitions[1].Width;
            _lastRightBarWidth = MainGrid.ColumnDefinitions[3].Width;
            ChatButton.IsChecked = true;
            ModelButton.IsChecked = true;            
            WeakReferenceMessenger.Default.Register<ExecuteScriptMessage>(this, (_, m) =>
            {
                MyWebView.ExecuteScript(m.Value);
            });
            WeakReferenceMessenger.Default.Register<MainWindow, EvaluateScriptMessage>(this, (r, m) =>
            {
                var evalTask = Task.Run(async () =>
                {
                    try 
                    {
                        var result = await MyWebView.EvaluateScript<int>(m.Script);
                        Console.WriteLine(result);
                        return result;
                    }
                    catch 
                    {
                        return -1;
                    }
                });
                m.Reply(evalTask);
            });
        }

        private GridLength _lastLeftBarWidth;
        private GridLength _lastRightBarWidth;

        

        private void OnBackgroundPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            RootPanel.Focus();
        }

        private void LeftThumb_OnDragCompleted(object? sender, VectorEventArgs e)
        {
            _lastLeftBarWidth = MainGrid.ColumnDefinitions[1].Width;
        }

        private void RightThumb_OnDragCompleted(object? sender, VectorEventArgs e)
        {
            _lastRightBarWidth = MainGrid.ColumnDefinitions[3].Width;
        }

        private void ChatOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems == null)
                return;
            Dispatcher.UIThread.Post(() => MessageList.ScrollIntoView(e.NewItems[0]));
        }

        private void Window_OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not MainVM mainVM)
                return;
            mainVM.Chat.CollectionChanged += ChatOnCollectionChanged;
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            if (VoiceService.PythonProcess is null || VoiceService.PythonProcess.HasExited)
                return;
            VoiceService.PythonProcess.Kill(entireProcessTree: true);
            VoiceService.PythonProcess.Dispose();
        }

        private void LeftToggleButton_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton btn || btn.Parent is not Panel panel)
                return;
            switch (btn.IsChecked)
            {
                case true:
                    foreach (var child in panel.Children)
                        if (child is ToggleButton toggleButton && toggleButton != btn)
                            toggleButton.IsChecked = false;
                    MainGrid.ColumnDefinitions[1].MinWidth = 256;
                    MainGrid.ColumnDefinitions[1].Width = _lastLeftBarWidth;
                    break;
                case false:
                    MainGrid.ColumnDefinitions[1].MinWidth = 0;
                    MainGrid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Pixel);
                    break;
            }
        }

        private void RightToggleButton_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton btn || btn.Parent is not Panel panel)
                return;
            switch (btn.IsChecked)
            {
                case true:
                    foreach (var child in panel.Children)
                        if (child is ToggleButton toggleButton && toggleButton != btn)
                            toggleButton.IsChecked = false;
                    MainGrid.ColumnDefinitions[3].MinWidth = 256;
                    MainGrid.ColumnDefinitions[3].Width = _lastRightBarWidth;
                    break;
                case false:
                    MainGrid.ColumnDefinitions[3].MinWidth = 0;
                    MainGrid.ColumnDefinitions[3].Width = new GridLength(0, GridUnitType.Pixel);
                    break;
            }
        }

        private void PART_SelectableTextBlock_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Control control)
                return;
            var item = control.FindAncestorOfType<ListBoxItem>();
            if (item is null)
                return;
            item.IsSelected = true;
        }

        private void PART_SelectableTextBlock_Initialized(object? sender, EventArgs e)
        {
            (sender as SelectableTextBlock)?.AddHandler(
                PointerPressedEvent,
                PART_SelectableTextBlock_PointerPressed,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                true
            );
        }

        private async void CopyErrorButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Content is not LucideIcon oldIcon)
                return;
            btn.IsHitTestVisible = false;
            btn.Content = new LucideIcon
            {
                Kind = LucideIconKind.Check,
                Size = 16,
                StrokeWidth = oldIcon.StrokeWidth
            };
            await Task.Delay(1500);
            btn.Content = new LucideIcon
            {
                Kind = LucideIconKind.Copy,
                Size = 14,
                StrokeWidth = oldIcon.StrokeWidth
            };
            btn.IsHitTestVisible = true;
        }
    }
}