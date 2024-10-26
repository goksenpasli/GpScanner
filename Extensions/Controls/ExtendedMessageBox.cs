using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Extensions
{
    public class ExtendedMessageBox : Control
    {
        public static readonly DependencyProperty CustomContentProperty = DependencyProperty.Register("CustomContent", typeof(object), typeof(ExtendedMessageBox), new PropertyMetadata(null));
        public static readonly DependencyProperty IsCustomContentVisibleProperty = DependencyProperty.Register("IsCustomContentVisible", typeof(Visibility), typeof(ExtendedMessageBox), new PropertyMetadata(Visibility.Collapsed));
        public static readonly DependencyProperty MessageProperty = DependencyProperty.Register("Message", typeof(string), typeof(ExtendedMessageBox), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty ShowNoButtonProperty = DependencyProperty.Register("ShowNoButton", typeof(Visibility), typeof(ExtendedMessageBox), new PropertyMetadata(Visibility.Collapsed));
        public static readonly DependencyProperty ShowYesButtonProperty = DependencyProperty.Register("ShowYesButton", typeof(Visibility), typeof(ExtendedMessageBox), new PropertyMetadata(Visibility.Visible));
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(ExtendedMessageBox), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty YesButtonContentProperty = DependencyProperty.Register("YesButtonContent", typeof(string), typeof(ExtendedMessageBox), new PropertyMetadata("Yes"));
        private static Grid _overlayGrid;
        private Button _noButton;
        private Button _yesButton;
        private ExtendedMessageBox dialog;

        static ExtendedMessageBox() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ExtendedMessageBox), new FrameworkPropertyMetadata(typeof(ExtendedMessageBox))); }

        public object CustomContent { get => GetValue(CustomContentProperty); set => SetValue(CustomContentProperty, value); }

        public Visibility IsCustomContentVisible { get => (Visibility)GetValue(IsCustomContentVisibleProperty); set => SetValue(IsCustomContentVisibleProperty, value); }

        public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

        public Visibility ShowNoButton { get => (Visibility)GetValue(ShowNoButtonProperty); set => SetValue(ShowNoButtonProperty, value); }

        public Visibility ShowYesButton { get => (Visibility)GetValue(ShowYesButtonProperty); set => SetValue(ShowYesButtonProperty, value); }

        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

        public string YesButtonContent { get => (string)GetValue(YesButtonContentProperty); set => SetValue(YesButtonContentProperty, value); }

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

        public void ShowDialog(Window window, string message, string title, Visibility yesbuttonvisibility = Visibility.Visible, Visibility nobuttonvisibility = Visibility.Collapsed, Action onYesAction = null, Action onNoAction = null)
        {
            dialog = new()
            {
                CustomContent = CustomContent,
                IsCustomContentVisible = IsCustomContentVisible,
                Message = message,
                Title = title,
                ShowYesButton = yesbuttonvisibility,
                ShowNoButton = nobuttonvisibility,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _overlayGrid = window.FindVisualChildren<Grid>().FirstOrDefault();
            if (_overlayGrid is not null)
            {
                _ = (_overlayGrid?.Children.Add(dialog));
                dialog.OnYesAction = onYesAction;
                dialog.OnNoAction = onNoAction;
            }
        }

        private void CloseDialog() => _overlayGrid?.Children?.Remove(this);

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