using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Extensions
{
    public class ExtendedMessageBox : Control
    {
        public static readonly DependencyProperty CheckDescriptionProperty = DependencyProperty.Register("CheckDescription", typeof(string), typeof(ExtendedMessageBox), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty CheckVisibilityProperty = DependencyProperty.Register("CheckVisibility", typeof(Visibility), typeof(ExtendedMessageBox), new PropertyMetadata(Visibility.Collapsed));
        public static readonly DependencyProperty CustomContentProperty = DependencyProperty.Register("CustomContent", typeof(object), typeof(ExtendedMessageBox), new PropertyMetadata(null));
        public static readonly DependencyProperty CustomContentVisibleProperty = DependencyProperty.Register("CustomContentVisible", typeof(Visibility), typeof(ExtendedMessageBox), new PropertyMetadata(Visibility.Collapsed));
        public static readonly DependencyProperty HiddenCaptionProperty = DependencyProperty.Register("HiddenCaption", typeof(string), typeof(ExtendedMessageBox), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty HiddenDescriptionExpandedProperty = DependencyProperty.Register("HiddenDescriptionExpanded", typeof(bool), typeof(ExtendedMessageBox), new PropertyMetadata(false));
        public static readonly DependencyProperty HiddenDescriptionProperty = DependencyProperty.Register("HiddenDescription", typeof(string), typeof(ExtendedMessageBox), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty HiddenDescriptionVisibilityProperty = DependencyProperty.Register("HiddenDescriptionVisibility", typeof(Visibility), typeof(ExtendedMessageBox), new PropertyMetadata(Visibility.Collapsed));
        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register("IsChecked", typeof(bool), typeof(ExtendedMessageBox), new PropertyMetadata(false));
        public static readonly DependencyProperty IsIndeterminateProperty = DependencyProperty.Register("IsIndeterminate", typeof(bool), typeof(ExtendedMessageBox), new PropertyMetadata(false));
        public static readonly DependencyProperty MessageProperty = DependencyProperty.Register("Message", typeof(string), typeof(ExtendedMessageBox), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty NoButtonProperty = DependencyProperty.Register("NoButton", typeof(Visibility), typeof(ExtendedMessageBox), new PropertyMetadata(Visibility.Collapsed));
        public static readonly DependencyProperty NoEnabledProperty = DependencyProperty.Register("NoEnabled", typeof(bool), typeof(ExtendedMessageBox), new PropertyMetadata(true));
        public static readonly DependencyProperty ProgressBarVisibilityProperty = DependencyProperty.Register("ProgressBarVisibility", typeof(Visibility), typeof(ExtendedMessageBox), new PropertyMetadata(Visibility.Collapsed));
        public static readonly DependencyProperty ProgressValueProperty = DependencyProperty.Register("ProgressValue", typeof(double), typeof(ExtendedMessageBox), new PropertyMetadata(0d));
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(ExtendedMessageBox), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty YesButtonProperty = DependencyProperty.Register("YesButton", typeof(Visibility), typeof(ExtendedMessageBox), new PropertyMetadata(Visibility.Visible));
        public static readonly DependencyProperty YesEnabledProperty = DependencyProperty.Register("YesEnabled", typeof(bool), typeof(ExtendedMessageBox), new PropertyMetadata(true));
        private static Grid _overlayGrid;
        private static Rectangle blockrectangle;
        private Button _noButton;
        private Button _yesButton;
        private ExtendedMessageBox dialog;

        static ExtendedMessageBox() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ExtendedMessageBox), new FrameworkPropertyMetadata(typeof(ExtendedMessageBox))); }

        public string CheckDescription { get => (string)GetValue(CheckDescriptionProperty); set => SetValue(CheckDescriptionProperty, value); }

        public Visibility CheckVisibility { get => (Visibility)GetValue(CheckVisibilityProperty); set => SetValue(CheckVisibilityProperty, value); }

        public object CustomContent { get => GetValue(CustomContentProperty); set => SetValue(CustomContentProperty, value); }

        public Visibility CustomContentVisible { get => (Visibility)GetValue(CustomContentVisibleProperty); set => SetValue(CustomContentVisibleProperty, value); }

        public string HiddenCaption { get => (string)GetValue(HiddenCaptionProperty); set => SetValue(HiddenCaptionProperty, value); }

        public string HiddenDescription { get => (string)GetValue(HiddenDescriptionProperty); set => SetValue(HiddenDescriptionProperty, value); }

        public bool HiddenDescriptionExpanded { get => (bool)GetValue(HiddenDescriptionExpandedProperty); set => SetValue(HiddenDescriptionExpandedProperty, value); }

        public Visibility HiddenDescriptionVisibility { get => (Visibility)GetValue(HiddenDescriptionVisibilityProperty); set => SetValue(HiddenDescriptionVisibilityProperty, value); }

        public bool IsChecked { get => (bool)GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

        public bool IsIndeterminate { get => (bool)GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }

        public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

        public Visibility NoButton { get => (Visibility)GetValue(NoButtonProperty); set => SetValue(NoButtonProperty, value); }

        public bool NoEnabled { get => (bool)GetValue(NoEnabledProperty); set => SetValue(NoEnabledProperty, value); }

        public Visibility ProgressBarVisibility { get => (Visibility)GetValue(ProgressBarVisibilityProperty); set => SetValue(ProgressBarVisibilityProperty, value); }

        public double ProgressValue { get => (double)GetValue(ProgressValueProperty); set => SetValue(ProgressValueProperty, value); }

        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

        public Visibility YesButton { get => (Visibility)GetValue(YesButtonProperty); set => SetValue(YesButtonProperty, value); }

        public bool YesEnabled { get => (bool)GetValue(YesEnabledProperty); set => SetValue(YesEnabledProperty, value); }

        private Action OnNoAction { get; set; }

        private Action OnYesAction { get; set; }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _yesButton = GetTemplateChild("Yes") as Button;
            _noButton = GetTemplateChild("No") as Button;
            if (_yesButton is not null)
            {
                _yesButton.Click += (s, e) => OnYesButtonClick();
            }
            if (_noButton is not null)
            {
                _noButton.Click += (s, e) => OnNoButtonClick();
            }
        }

        public void ShowDialog(Window window, string message, string title = null, Action onYesAction = null, Action onNoAction = null, bool ismodal = true)
        {
            if (window is null)
            {
                throw new ArgumentNullException(nameof(window), "The window parameter cannot be null.");
            }
            dialog = new()
            {
                CustomContent = CustomContent,
                CustomContentVisible = CustomContentVisible,
                HiddenDescription = HiddenDescription,
                HiddenDescriptionExpanded = HiddenDescriptionExpanded,
                HiddenDescriptionVisibility = HiddenDescriptionVisibility,
                HiddenCaption = HiddenCaption,
                CheckDescription = CheckDescription,
                CheckVisibility = CheckVisibility,
                IsChecked = IsChecked,
                Message = message,
                Title = title,
                YesButton = YesButton,
                NoButton = NoButton,
                YesEnabled = YesEnabled,
                NoEnabled = NoEnabled,
                ProgressBarVisibility = ProgressBarVisibility,
                ProgressValue = ProgressValue,
                IsIndeterminate = IsIndeterminate,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _overlayGrid = window.GetFirstVisualChild<Grid>();
            if (_overlayGrid is null)
            {
                throw new InvalidOperationException("window should contain at least one grid control.");
            }
            if (ismodal && blockrectangle is null)
            {
                blockrectangle = new Rectangle { Fill = Brushes.Transparent, IsHitTestVisible = true };
                Grid.SetColumnSpan(blockrectangle, _overlayGrid.ColumnDefinitions.Count);
                Grid.SetRowSpan(blockrectangle, _overlayGrid.RowDefinitions.Count);
                _ = _overlayGrid.Children.Add(blockrectangle);
            }
            Grid.SetColumnSpan(dialog, _overlayGrid.ColumnDefinitions.Count);
            Grid.SetRowSpan(dialog, _overlayGrid.RowDefinitions.Count);
            _ = _overlayGrid.Children.Add(dialog);
            dialog.OnYesAction = onYesAction;
            dialog.OnNoAction = onNoAction;
        }

        private void CloseDialog()
        {
            if (_overlayGrid.Children.OfType<ExtendedMessageBox>()?.Count() == 1)
            {
                _overlayGrid.Children.Remove(blockrectangle);
                blockrectangle = null;
            }
            _overlayGrid.Children.Remove(this);
        }

        private void OnNoButtonClick()
        {
            OnNoAction?.Invoke();
            CloseDialog();
        }

        private void OnYesButtonClick()
        {
            OnYesAction?.Invoke();
            CloseDialog();
        }
    }
}