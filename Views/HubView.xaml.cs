using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ParadoxusBrowser.Data.Models;
using ParadoxusBrowser.ViewModels;

namespace ParadoxusBrowser.Views
{
    public partial class HubView : UserControl
    {
        public HubView()
        {
            InitializeComponent();
        }

        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            if (e.Key == Key.Down)
            {
                if (vm.IsHubSuggestionsOpen && vm.HubSuggestions.Count > 0)
                {
                    int index = vm.SelectedHubSuggestion != null ? vm.HubSuggestions.IndexOf(vm.SelectedHubSuggestion) : -1;
                    if (index < vm.HubSuggestions.Count - 1)
                    {
                        vm.SelectedHubSuggestion = vm.HubSuggestions[index + 1];
                    }
                    else
                    {
                        vm.SelectedHubSuggestion = vm.HubSuggestions[0];
                    }
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Key == Key.Up)
            {
                if (vm.IsHubSuggestionsOpen && vm.HubSuggestions.Count > 0)
                {
                    int index = vm.SelectedHubSuggestion != null ? vm.HubSuggestions.IndexOf(vm.SelectedHubSuggestion) : vm.HubSuggestions.Count;
                    if (index > 0)
                    {
                        vm.SelectedHubSuggestion = vm.HubSuggestions[index - 1];
                    }
                    else
                    {
                        vm.SelectedHubSuggestion = vm.HubSuggestions[^1];
                    }
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Key == Key.Escape)
            {
                if (vm.IsHubSuggestionsOpen)
                {
                    vm.IsHubSuggestionsOpen = false;
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Key == Key.Enter)
            {
                if (vm.IsHubSuggestionsOpen && vm.SelectedHubSuggestion != null)
                {
                    vm.SelectHubSuggestionCommand.Execute(vm.SelectedHubSuggestion);
                    e.Handled = true;
                    return;
                }

                vm.IsHubSuggestionsOpen = false;
                vm.ExecuteHubSearchCommand.Execute(null);
                e.Handled = true;
            }
        }

                private void OnHubSearchPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                _ = vm.LoadHubSuggestionsAsync(HubSearchTextBox.Text);
            }
        }

        private void OnHubSearchGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                _ = vm.LoadHubSuggestionsAsync(HubSearchTextBox.Text);
            }
        }

        private void OnHubSearchLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            if (e.NewFocus is DependencyObject newFocus)
            {
                if (FindVisualParent<ListBoxItem>(newFocus) != null || FindVisualParent<ListBox>(newFocus) != null)
                    return;
            }

            vm.IsHubSuggestionsOpen = false;
        }

        private void HubView_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            if (vm.IsHubSuggestionsOpen && e.OriginalSource is DependencyObject dep)
            {
                var parentBorder = FindVisualParent<Border>(dep);
                bool inSearch = false;
                while (parentBorder != null)
                {
                    if (parentBorder.Name == "HubSearchBorder")
                    {
                        inSearch = true;
                        break;
                    }
                    parentBorder = FindVisualParent<Border>(VisualTreeHelper.GetParent(parentBorder));
                }

                if (!inSearch && FindVisualParent<ListBoxItem>(dep) == null && FindVisualParent<ListBox>(dep) == null)
                {
                    vm.IsHubSuggestionsOpen = false;
                }
            }
        }

        private void OnHubSearchGotFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                _ = vm.LoadHubSuggestionsAsync(HubSearchTextBox.Text);
            }
        }

        private void OnHubSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            if (HubSearchTextBox.IsFocused && DataContext is MainViewModel vm)
            {
                _ = vm.LoadHubSuggestionsAsync(HubSearchTextBox.Text);
            }
        }

        private void OnHubSuggestionClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                if (FindVisualParent<Button>(dep) != null)
                    return;

                var item = FindVisualParent<ListBoxItem>(dep);
                if (item?.DataContext is SearchHistoryItem historyItem && DataContext is MainViewModel vm)
                {
                    vm.SelectHubSuggestionCommand.Execute(historyItem);
                }
            }
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T typed) return typed;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private void OnFavoriteClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is FavoriteItem item)
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.NavigateFromHubCommand.Execute(item.Url);
                }
            }
        }

        private void OnAddSiteTileClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.OpenAddFavoriteDialogCommand.Execute(null);
            }
        }

        private void OnCancelAddFavoriteClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.CancelAddFavoriteCommand.Execute(null);
            }
        }
    }
}
