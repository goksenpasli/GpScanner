using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Extensions
{
    public class ExtendedMessageBox : Control
    {
        public static readonly DependencyProperty CheckDescriptionProperty = DependencyProperty.Register("CheckDescription", typeof(string), typeof(ExtendedMessageBox), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty CheckVisibilityProperty = DependencyProperty.Register("CheckVisibility", typeof(Visibility), typeof(ExtendedMessageBox), new PropertyMetadata(Visibility.Collapsed));
        public static readonly DependencyProperty CustomContentHeightProperty = DependencyProperty.Register("CustomContentHeight", typeof(double), typeof(ExtendedMessageBox), new PropertyMetadata(96d));
        public static readonly DependencyProperty CustomContentProperty = DependencyProperty.Register("CustomContent", typeof(object), typeof(ExtendedMessageBox), new PropertyMetadata(null));
        public static readonly DependencyProperty CustomContentVisibleProperty = DependencyProperty.Register("CustomContentVisible", typeof(Visibility), typeof(ExtendedMessageBox), new PropertyMetadata(Visibility.Collapsed));
        public static readonly DependencyProperty CustomContentWidthProperty = DependencyProperty.Register("CustomContentWidth", typeof(double), typeof(ExtendedMessageBox), new PropertyMetadata(96d));
        public static readonly DependencyProperty HiddenCaptionProperty = DependencyProperty.Register("HiddenCaption", typeof(string), typeof(ExtendedMessageBox), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty HiddenDescriptionExpandedProperty = DependencyProperty.Register("HiddenDescriptionExpanded", typeof(bool), typeof(ExtendedMessageBox), new PropertyMetadata(false));
        public static readonly DependencyProperty HiddenDescriptionProperty = DependencyProperty.Register("HiddenDescription", typeof(string), typeof(ExtendedMessageBox), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty HiddenDescriptionVisibilityProperty = DependencyProperty.Register("HiddenDescriptionVisibility", typeof(Visibility), typeof(ExtendedMessageBox), new PropertyMetadata(Visibility.Collapsed));
        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register("IsChecked", typeof(bool), typeof(ExtendedMessageBox), new PropertyMetadata(false));
        public static readonly DependencyProperty IsDraggableProperty = DependencyProperty.Register("IsDraggable", typeof(bool), typeof(ExtendedMessageBox), new PropertyMetadata(false));
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
        private Point _dragStartPoint;
        private Thickness _initialMargin;
        private bool _isDragging;
        private Button _noButton;
        private Button _yesButton;
        private ExtendedMessageBox dialog;

        static ExtendedMessageBox() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ExtendedMessageBox), new FrameworkPropertyMetadata(typeof(ExtendedMessageBox))); }

        public string CheckDescription { get => (string)GetValue(CheckDescriptionProperty); set => SetValue(CheckDescriptionProperty, value); }

        public Visibility CheckVisibility { get => (Visibility)GetValue(CheckVisibilityProperty); set => SetValue(CheckVisibilityProperty, value); }

        public object CustomContent { get => GetValue(CustomContentProperty); set => SetValue(CustomContentProperty, value); }

        public double CustomContentHeight { get => (double)GetValue(CustomContentHeightProperty); set => SetValue(CustomContentHeightProperty, value); }

        public Visibility CustomContentVisible { get => (Visibility)GetValue(CustomContentVisibleProperty); set => SetValue(CustomContentVisibleProperty, value); }

        public double CustomContentWidth { get => (double)GetValue(CustomContentWidthProperty); set => SetValue(CustomContentWidthProperty, value); }

        public string HiddenCaption { get => (string)GetValue(HiddenCaptionProperty); set => SetValue(HiddenCaptionProperty, value); }

        public string HiddenDescription { get => (string)GetValue(HiddenDescriptionProperty); set => SetValue(HiddenDescriptionProperty, value); }

        public bool HiddenDescriptionExpanded { get => (bool)GetValue(HiddenDescriptionExpandedProperty); set => SetValue(HiddenDescriptionExpandedProperty, value); }

        public Visibility HiddenDescriptionVisibility { get => (Visibility)GetValue(HiddenDescriptionVisibilityProperty); set => SetValue(HiddenDescriptionVisibilityProperty, value); }

        public bool IsChecked { get => (bool)GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

        public bool IsDraggable { get => (bool)GetValue(IsDraggableProperty); set => SetValue(IsDraggableProperty, value); }

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
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
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
                VerticalAlignment = VerticalAlignment.Center,
                CustomContentHeight = CustomContentHeight,
                CustomContentWidth = CustomContentWidth,
                IsDraggable = IsDraggable,
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

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && IsDraggable)
            {
                _isDragging = true;
                _dragStartPoint = e.GetPosition(_overlayGrid);
                _initialMargin = Margin;
                _ = CaptureMouse();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                Point currentPosition = e.GetPosition(_overlayGrid);
                double offsetX = currentPosition.X - _dragStartPoint.X;
                double offsetY = currentPosition.Y - _dragStartPoint.Y;
                double newLeft = _initialMargin.Left + offsetX;
                double newTop = _initialMargin.Top + offsetY;
                newLeft = Math.Max(-_overlayGrid.ActualWidth + ActualWidth, Math.Min(newLeft, _overlayGrid.ActualWidth - ActualWidth));
                newTop = Math.Max(-_overlayGrid.ActualHeight + ActualHeight, Math.Min(newTop, _overlayGrid.ActualHeight - ActualHeight));
                Margin = new Thickness(newLeft, newTop, 0, 0);
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
            }
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