using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using WaifuAI.ViewModels;
using WaifuAI.Models;

namespace WaifuAI.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            _lastLeftBarWidth = MainGrid.ColumnDefinitions[1].Width;
            _lastRightBarWidth = MainGrid.ColumnDefinitions[3].Width;
        }

        private GridLength _lastLeftBarWidth;
        private GridLength _lastRightBarWidth;

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

        private void Control_OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not MainVM mainVM)
                return;
            mainVM.Chat.CollectionChanged += ChatOnCollectionChanged;
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
    }
}