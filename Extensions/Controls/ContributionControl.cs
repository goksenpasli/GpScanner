using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Extensions
{
    public class ContributionControl : Control, INotifyPropertyChanged
    {
        public static readonly DependencyProperty ContributionColorProperty = DependencyProperty.Register("ContributionColor", typeof(Color), typeof(ContributionControl), new PropertyMetadata(Colors.Green));
        public static readonly DependencyProperty ContributionColumnCountProperty = DependencyProperty.Register("ContributionColumnCount", typeof(int), typeof(ContributionControl), new PropertyMetadata(7));
        public static readonly DependencyProperty ContributionRowCountProperty = DependencyProperty.Register("ContributionRowCount", typeof(int), typeof(ContributionControl), new PropertyMetadata(53));
        public static readonly DependencyProperty ContributionsProperty = DependencyProperty.Register("Contributions", typeof(ObservableCollection<ContributionData>), typeof(ContributionControl), new PropertyMetadata(null, Changed));
        public static readonly DependencyProperty SelectedContributionProperty = DependencyProperty.Register("SelectedContribution", typeof(ContributionData), typeof(ContributionControl), new PropertyMetadata(null));
        public static readonly DependencyProperty ZeroContributionColorProperty = DependencyProperty.Register("ZeroContributionColor", typeof(Color), typeof(ContributionControl), new PropertyMetadata(Colors.Gray));

        static ContributionControl() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ContributionControl), new FrameworkPropertyMetadata(typeof(ContributionControl))); }

        public ContributionControl()
        {
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                Contributions =
                [
                    new() { Count = 0, ContrubutionDate = DateTime.Today },
                    new() { Count = 0, ContrubutionDate = DateTime.Today.AddDays(1) },
                    new() { Count = 0, ContrubutionDate = DateTime.Today.AddDays(2) },
                    new() { Count = 0, ContrubutionDate = DateTime.Today.AddDays(3) },
                    new() { Count = 0, ContrubutionDate = DateTime.Today.AddDays(4) },
                    new() { Count = 0, ContrubutionDate = DateTime.Today.AddDays(5) },
                    new() { Count = 0, ContrubutionDate = DateTime.Today.AddDays(6) },
                ];
            }
            PropertyChanged += ContributionControl_PropertyChanged;

            ResetDate = new RelayCommand<object>(
                parameter =>
                {
                    SelectedDay = null;
                    SelectedMonth = null;
                },
                parameter => !string.IsNullOrEmpty(SelectedMonth) || !string.IsNullOrEmpty(SelectedDay));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public Color ContributionColor { get => (Color)GetValue(ContributionColorProperty); set => SetValue(ContributionColorProperty, value); }

        public int ContributionColumnCount { get => (int)GetValue(ContributionColumnCountProperty); set => SetValue(ContributionColumnCountProperty, value); }

        public int ContributionRowCount { get => (int)GetValue(ContributionRowCountProperty); set => SetValue(ContributionRowCountProperty, value); }

        public ObservableCollection<ContributionData> Contributions { get => (ObservableCollection<ContributionData>)GetValue(ContributionsProperty); set => SetValue(ContributionsProperty, value); }

        [Browsable(false)]
        public IEnumerable<string> Days
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(Days));
                }
            }
        }

        [Browsable(false)]
        public int MaxContribution
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(MaxContribution));
                }
            }
        }

        [Browsable(false)]

        public int? MonthDateTotalContribution
        {
            get;

            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(MonthDateTotalContribution));
                }
            }
        }

        [Browsable(false)]
        public IEnumerable<string> Months
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(Months));
                }
            }
        }

        [Browsable(false)]
        public int? MonthTotalContribution
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(MonthTotalContribution));
                }
            }
        }

        public RelayCommand<object> ResetDate { get; }

        public ContributionData SelectedContribution { get => (ContributionData)GetValue(SelectedContributionProperty); set => SetValue(SelectedContributionProperty, value); }

        [Browsable(false)]
        public string SelectedDay
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(SelectedDay));
                }
            }
        }

        [Browsable(false)]
        public string SelectedMonth
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(SelectedMonth));
                }
            }
        }

        public Color ZeroContributionColor { get => (Color)GetValue(ZeroContributionColorProperty); set => SetValue(ZeroContributionColorProperty, value); }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return;
            }

            if (Dispatcher.CheckAccess())
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            else
            {
                Dispatcher.Invoke(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
            }
        }

        private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ContributionControl contributionControl && e.NewValue is ObservableCollection<ContributionData> contributiondata && contributiondata.Count > 0)
            {
                contributionControl.MaxContribution = contributiondata.Max(z => z.Count);
                contributionControl.Days = contributiondata.Take(7).Select(z => z.ContrubutionDate.Value.ToString("ddd"));
                contributionControl.Months = contributiondata.Select(z => z.ContrubutionDate.Value.ToString("MMM")).Distinct();
            }
        }

        private void ContributionControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Contributions?.Any() == true && e.PropertyName is "SelectedMonth" or "SelectedDay")
            {
                List<ContributionData> contributionsForSelectedMonth = [.. Contributions.Where(z => z.ContrubutionDate.Value.ToString("MMM") == SelectedMonth)];
                foreach (ContributionData item in Contributions)
                {
                    item.Stroke = item.ContrubutionDate.HasValue && item.ContrubutionDate.Value.ToString("MMM") == SelectedMonth ? Brushes.Red : null;
                }
                MonthTotalContribution = contributionsForSelectedMonth.Sum(z => z.Count);
                List<ContributionData> contributionsForSelectedDay = [.. contributionsForSelectedMonth.Where(z => z.ContrubutionDate.Value.ToString("ddd") == SelectedDay)];
                MonthDateTotalContribution = contributionsForSelectedDay.Sum(z => z.Count);
            }
        }
    }
}
