using System;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WaifuAI.ViewModels;
using Lucide.Avalonia;
using System.Threading.Tasks;
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
            ChatButton.IsChecked = true;

            WeakReferenceMessenger.Default.Register<ExecuteScriptMessage>(this, (_, m) =>
            {
                MyWebView.ExecuteScript(m.Value);
            });

            WeakReferenceMessenger.Default.Register<MainWindow, EvaluateScriptMessage<int>>(this, (recipient, m) =>
            {
                m.Reply(Task.Run(async () =>
                {
                    try
                    {
                        return await recipient.MyWebView.EvaluateScript<int>(m.Script);
                    }
                    catch
                    {
                        return -1;
                    }
                }));
            });

            WeakReferenceMessenger.Default.Register<MainWindow, EvaluateScriptMessage<bool>>(this, (recipient, m) =>
            {
                m.Reply(Task.Run(async () =>
                {
                    try
                    {
                        return await recipient.MyWebView.EvaluateScript<bool>(m.Script);
                    }
                    catch
                    {
                        return false;
                    }
                }));
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

                        // 1. Делаем скриншот
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

                        if (string.IsNullOrEmpty(base64Data)) return;

                        // 2. Очищаем память в JS сразу после получения
                        MyWebView.ExecuteScript("window.vrmApp.printscreen = ''");

                        int commaIndex = base64Data.IndexOf(',');
                        string base64 = commaIndex >= 0 ? base64Data.Substring(commaIndex + 1) : base64Data;
                        byte[] bytes = Convert.FromBase64String(base64);

                        Dispatcher.UIThread.Post(() =>
                        {
                            using var ms = new MemoryStream(bytes);
                            var newBitmap = new Bitmap(ms);

                            // 3. ОСВОБОЖДАЕМ СТАРЫЙ BITMAP перед установкой нового
                            var oldBitmap = WebViewSnapshot.Source as IDisposable;
                            WebViewSnapshot.Source = newBitmap;
                            oldBitmap?.Dispose();
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
                if (MessageList.DataContext is not MainVM mainVM)
                    return;

                var sourceIndex = m.Value.sourceIndex;
                var replyIndex = m.Value.replyIndex;

                var currentOffset = ChatScrollViewer.Offset;

                double replyOffset = 0;
                for (int i = 0; i < replyIndex; i++)
                    replyOffset += mainVM.Chat[i].MessageModel.DesignHeight;

                if (replyOffset < currentOffset.Y)
                {
                    double distance = 0;
                    for (int i = replyIndex; i < m.Value.sourceIndex; i++)
                        distance += mainVM.Chat[i].MessageModel.DesignHeight;

                    // если source тоже не полностью виден
                    var sourceOffset = replyOffset + distance;
                    if (sourceOffset < currentOffset.Y)
                        distance += currentOffset.Y - sourceOffset;

                    var delta = Math.Min(currentOffset.Y, distance);

                    currentOffset = currentOffset.WithY(currentOffset.Y - delta);
                    ChatScrollViewer.Offset = currentOffset;
                }

                Dispatcher.UIThread.Post(async () =>
                    await mainVM.Chat[replyIndex].TriggerHighlightAsync(mainVM.Chat[sourceIndex])
                );
            });

            WeakReferenceMessenger.Default.Register<MainWindow, RequestMessageHeight<double>>(this, (_, m) =>
            {
                var element = MessageList.TryGetElement(m.Index);
                m.Reply(element is null ? 0 : element.Bounds.Height);
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

        private void OnBackgroundPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            RootPanel.Focus();
        }

        private void LeftThumb_OnDragCompleted(object? sender, VectorEventArgs e)
        {
            _lastLeftBarWidth = MainGrid.ColumnDefinitions[1].Width;
        }

        private void ChatOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems == null)
                return;
            Dispatcher.UIThread.Post(() =>
            {
                ChatScrollViewer.ScrollToEnd();
            });
        }

        private void Window_OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not MainVM mainVM)
                return;
            
            mainVM.Chat.CollectionChanged += ChatOnCollectionChanged;
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            SettingsVM.Instance.LastUserEntry = DateTime.UtcNow;

            if (VoiceService.PythonProcess is null || VoiceService.PythonProcess.HasExited)
                return;
            
            VoiceService.PythonProcess.Kill(entireProcessTree: true);
            VoiceService.PythonProcess.Dispose();

            Environment.Exit(0);
        }

        private void LeftToggleButton_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton btn || btn.Parent is not Panel panel)
                return;
            switch (btn.IsChecked)
            {
                case true:
                    ChatButton.IsChecked = btn == ChatButton;
                    MemoryButton.IsChecked = btn == MemoryButton;
                    PersonalityButton.IsChecked = btn == PersonalityButton;
                    ParametersButton.IsChecked = btn == ParametersButton;
                    ModelButton.IsChecked = btn == ModelButton;

                    MainGrid.ColumnDefinitions[1].MinWidth = 400;
                    MainGrid.ColumnDefinitions[1].Width = _lastLeftBarWidth;
                    break;
                case false:
                    MainGrid.ColumnDefinitions[1].MinWidth = 0;
                    MainGrid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Pixel);
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
            Model3DService.SetBackground();
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

        private void OnElementPrepared(object? sender, ItemsRepeaterElementPreparedEventArgs e)
        {
            var element = e.Element;

            element.SizeChanged += OnElementSizeChanged;
        }

        private void OnElementSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (sender is not Control control || 
                control.DataContext is not MessageVM msg ||
                msg.MessageModel.Role == "temp") 
                return;
            
            double actualHeight = e.NewSize.Height;
            
            if (Math.Abs(msg.MessageModel.DesignHeight - actualHeight) < 0.01)
                return;
            msg.MessageModel.DesignHeight = actualHeight;
            SettingsVM.Instance.MessagesWithNewHeights.Add(msg.MessageModel);
        }

        private void OnElementClearing(object? sender, ItemsRepeaterElementClearingEventArgs e)
        {
            e.Element.SizeChanged -= OnElementSizeChanged;
        }
    }
}