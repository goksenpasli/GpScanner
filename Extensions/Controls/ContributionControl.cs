using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
        private IEnumerable<string> days;
        private int maxContribution;
        private IEnumerable<string> months;
        private int? monthTotalContribution;
        private string selectedMonth;

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
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public Color ContributionColor { get => (Color)GetValue(ContributionColorProperty); set => SetValue(ContributionColorProperty, value); }

        public int ContributionColumnCount { get => (int)GetValue(ContributionColumnCountProperty); set => SetValue(ContributionColumnCountProperty, value); }

        public int ContributionRowCount { get => (int)GetValue(ContributionRowCountProperty); set => SetValue(ContributionRowCountProperty, value); }

        public ObservableCollection<ContributionData> Contributions { get => (ObservableCollection<ContributionData>)GetValue(ContributionsProperty); set => SetValue(ContributionsProperty, value); }

        [Browsable(false)]
        public IEnumerable<string> Days
        {
            get => days;
            set
            {
                if (days != value)
                {
                    days = value;
                    OnPropertyChanged(nameof(Days));
                }
            }
        }

        [Browsable(false)]
        public int MaxContribution
        {
            get => maxContribution;
            set
            {
                if (maxContribution != value)
                {
                    maxContribution = value;
                    OnPropertyChanged(nameof(MaxContribution));
                }
            }
        }

        [Browsable(false)]
        public IEnumerable<string> Months
        {
            get => months;
            set
            {
                if (months != value)
                {
                    months = value;
                    OnPropertyChanged(nameof(Months));
                }
            }
        }

        [Browsable(false)]
        public int? MonthTotalContribution
        {
            get => monthTotalContribution;
            set
            {
                if (monthTotalContribution != value)
                {
                    monthTotalContribution = value;
                    OnPropertyChanged(nameof(MonthTotalContribution));
                }
            }
        }

        public ContributionData SelectedContribution { get => (ContributionData)GetValue(SelectedContributionProperty); set => SetValue(SelectedContributionProperty, value); }

        [Browsable(false)]
        public string SelectedMonth
        {
            get => selectedMonth;
            set
            {
                if (selectedMonth != value)
                {
                    selectedMonth = value;
                    OnPropertyChanged(nameof(SelectedMonth));
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName = null) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

        private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ContributionControl contributionControl && e.NewValue is not null)
            {
                contributionControl.MaxContribution = (e.NewValue as ObservableCollection<ContributionData>).Max(z => z.Count);
                contributionControl.Days = (e.NewValue as ObservableCollection<ContributionData>).Take(7).Select(z => z.ContrubutionDate.Value.ToString("ddd"));
                contributionControl.Months = (e.NewValue as ObservableCollection<ContributionData>).Select(z => z.ContrubutionDate.Value.ToString("MMM")).Distinct();
            }
        }

        private void ContributionControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "SelectedMonth" && Contributions is not null)
            {
                foreach (ContributionData item in Contributions)
                {
                    item.Stroke = item.ContrubutionDate.Value.ToString("MMM") == SelectedMonth ? Brushes.Red : null;
                }
                MonthTotalContribution = Contributions.Where(z => z.ContrubutionDate.Value.ToString("MMM") == SelectedMonth).Sum(z => z.Count);
            }
        }
    }
}
