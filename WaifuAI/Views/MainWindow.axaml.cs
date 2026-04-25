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
using System.IO;

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
            WeakReferenceMessenger.Default.Register<MainWindow, EvaluateScriptMessage>(this, (_, m) =>
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
            WeakReferenceMessenger.Default.Register<MainWindow, SnapshotMessage>(this, (_, m) =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        if (MyWebView.Bounds.Width == 0 || 
                            MyWebView.Bounds.Height == 0 ||
                            !m.Value)
                            return;
                        MyWebView.ExecuteScript("window.vrmApp.takePrintscreen()");
                        string jsCode = @"return window.vrmApp.printscreen";
                        string base64Data = string.Empty;
                        int attempts = 0;
                        while (string.IsNullOrEmpty(base64Data) && attempts < 200)
                        {
                            base64Data = await MyWebView.EvaluateScript<string>(jsCode);
                            await Task.Delay(10);
                            attempts++;
                        }
                        string base64 = base64Data.Contains(",") 
                            ? base64Data.Substring(base64Data.IndexOf(',') + 1) 
                            : base64Data;
                        byte[] bytes = Convert.FromBase64String(base64);
                        Dispatcher.UIThread.Post(() => 
                        {
                            using var ms = new MemoryStream(bytes);
                            WebViewSnapshot.Source = new Bitmap(ms);
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Snapshot failed: {ex.Message}");
                    }
                });
            });
            WeakReferenceMessenger.Default.Register<MainWindow, ScrollMessage>(this, (_, m) =>
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    if (MessageList.Items[m.Value.sourceIndex] is not MessageVM sourceMsg ||
                        MessageList.Items[m.Value.replyIndex] is not MessageVM replyMsg)
                        return;
                    MessageList.ScrollIntoView(replyMsg);
                    replyMsg.IsHighlighted = true;
                    replyMsg.SelectionStart = sourceMsg.QuoteStart;
                    replyMsg.SelectionEnd = sourceMsg.QuoteEnd;
                    await Task.Delay(500);
                    replyMsg.IsHighlighted = false;
                    replyMsg.SelectionStart = 0;
                    replyMsg.SelectionEnd = 0;
                });
            });
            AddHandler(Button.ClickEvent, (_, e) =>
            {
                if (e.Source is not Button btn || string.IsNullOrEmpty(btn.Name))
                    return;
                switch (btn.Name)
                {
                    case "PART_CloseButton":
                        Close();
                        e.Handled = true;
                        break;
                    case "PART_MinimizeButton":
                        WindowState = WindowState.Minimized;
                        e.Handled = true;
                        break;
                    case "PART_RestoreButton":
                        WindowState = WindowState == WindowState.Maximized
                            ? WindowState.Normal
                            : WindowState.Maximized;
                        (DataContext as MainVM).IsMaximized = WindowState == WindowState.Maximized;
                        Console.WriteLine((DataContext as MainVM).IsMaximized);
                        e.Handled = true;
                        break;
                }
            }, RoutingStrategies.Bubble, handledEventsToo: true);
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
                    MainGrid.ColumnDefinitions[1].MinWidth = 400;
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

        private void MyWebView_JavascriptContextCreated(string frameName)
        {
            ModelService.SetBackground();
        }

        private void Border_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed)
                return;
            BeginMoveDrag(e);
        }

        private void Border_DoubleTapped(object? sender, TappedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    }
}