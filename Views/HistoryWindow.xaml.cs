using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ParadoxusBrowser.Data;
using ParadoxusBrowser.Data.Models;

namespace ParadoxusBrowser.Views
{
    public class HistoryGroupModel
    {
        public string DateHeader { get; set; } = string.Empty;
        public ObservableCollection<HistoryItem> Items { get; set; } = new();
    }

    public partial class HistoryWindow : Window
    {
        private List<HistoryItem> _allHistory = new();
        private readonly Action<string>? _onNavigate;

        public HistoryWindow(Action<string>? onNavigate = null)
        {
            InitializeComponent();
            _onNavigate = onNavigate;
            Loaded += async (s, e) => await LoadHistoryDataAsync();
        }

        private async Task LoadHistoryDataAsync()
        {
            _allHistory = await DatabaseContext.Instance.GetHistoryAsync(500);
            ApplyFilter(SearchTextBox.Text);
        }

        private void ApplyFilter(string filter)
        {
            string query = (filter ?? string.Empty).Trim().ToLowerInvariant();
            ClearFilterButton.Visibility = string.IsNullOrEmpty(query) ? Visibility.Collapsed : Visibility.Visible;

            var filtered = string.IsNullOrEmpty(query)
                ? _allHistory
                : _allHistory.Where(h =>
                    (!string.IsNullOrEmpty(h.Title) && h.Title.ToLowerInvariant().Contains(query)) ||
                    (!string.IsNullOrEmpty(h.Url) && h.Url.ToLowerInvariant().Contains(query))
                  ).ToList();

            if (filtered.Count == 0)
            {
                EmptyStatePanel.Visibility = Visibility.Visible;
                HistoryScrollViewer.Visibility = Visibility.Collapsed;
                GroupsItemsControl.ItemsSource = null;
                StatusTextBlock.Text = "Всего записей: 0";
                return;
            }

            EmptyStatePanel.Visibility = Visibility.Collapsed;
            HistoryScrollViewer.Visibility = Visibility.Visible;
            StatusTextBlock.Text = $"Всего записей: {filtered.Count}";

            // Group by dates
            DateTime today = DateTime.Today;
            DateTime yesterday = today.AddDays(-1);
            DateTime thisWeek = today.AddDays(-7);

            var groups = new List<HistoryGroupModel>();

            var todayItems = filtered.Where(h => h.VisitedAt.Date == today).ToList();
            if (todayItems.Count > 0)
            {
                groups.Add(new HistoryGroupModel
                {
                    DateHeader = "Сегодня",
                    Items = new ObservableCollection<HistoryItem>(todayItems)
                });
            }

            var yesterdayItems = filtered.Where(h => h.VisitedAt.Date == yesterday).ToList();
            if (yesterdayItems.Count > 0)
            {
                groups.Add(new HistoryGroupModel
                {
                    DateHeader = "Вчера",
                    Items = new ObservableCollection<HistoryItem>(yesterdayItems)
                });
            }

            var thisWeekItems = filtered.Where(h => h.VisitedAt.Date < yesterday && h.VisitedAt.Date >= thisWeek).ToList();
            if (thisWeekItems.Count > 0)
            {
                groups.Add(new HistoryGroupModel
                {
                    DateHeader = "На этой неделе",
                    Items = new ObservableCollection<HistoryItem>(thisWeekItems)
                });
            }

            var olderItems = filtered.Where(h => h.VisitedAt.Date < thisWeek).ToList();
            if (olderItems.Count > 0)
            {
                groups.Add(new HistoryGroupModel
                {
                    DateHeader = "Ранее",
                    Items = new ObservableCollection<HistoryItem>(olderItems)
                });
            }

            GroupsItemsControl.ItemsSource = groups;
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter(SearchTextBox.Text);
        }

        private void OnClearFilterClick(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = string.Empty;
        }

        private void OnHistoryRowClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is HistoryItem item)
            {
                _onNavigate?.Invoke(item.Url);
                Close();
            }
        }

        private async void OnDeleteHistoryItemClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is HistoryItem item)
            {
                await DatabaseContext.Instance.DeleteHistoryItemAsync(item.Id);
                _allHistory.Remove(item);
                ApplyFilter(SearchTextBox.Text);
            }
        }

        private async void OnClearAllHistoryClick(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show(
                "Вы уверены, что хотите безвозвратно удалить всю историю просмотров?",
                "Очистка истории",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (res == MessageBoxResult.Yes)
            {
                await DatabaseContext.Instance.ClearHistoryAsync();
                _allHistory.Clear();
                ApplyFilter(string.Empty);
            }
        }
    }
}