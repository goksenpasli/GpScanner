using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Extensions
{
    public class KeyboardControl : Control
    {
        public static readonly DependencyProperty KeyboardAttachedTextBoxProperty = DependencyProperty.Register("KeyboardAttachedTextBox", typeof(TextBox), typeof(KeyboardControl), new PropertyMetadata(null));
        private bool isShiftPressed;

        static KeyboardControl() { DefaultStyleKeyProperty.OverrideMetadata(typeof(KeyboardControl), new FrameworkPropertyMetadata(typeof(KeyboardControl))); }

        public TextBox KeyboardAttachedTextBox { get => (TextBox)GetValue(KeyboardAttachedTextBoxProperty); set => SetValue(KeyboardAttachedTextBoxProperty, value); }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            foreach (FrameworkElement child in GetTemplateChildren())
            {
                if (child is Button button)
                {
                    button.Click += Button_Click;
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            string key = button.Content?.ToString();

            if (key is null)
            {
                return;
            }

            if (button.Tag?.ToString() == "Shift")
            {
                isShiftPressed = !isShiftPressed;
                UpdateKeysForShift();
                return;
            }

            if (isShiftPressed)
            {
                key = key.ToUpper();
                isShiftPressed = false;
                UpdateKeysForShift();
            }

            if (KeyboardAttachedTextBox is TextBox textBox)
            {
                if (button.Tag?.ToString() == "Backspace")
                {
                    if (textBox.Text.Length > 0)
                    {
                        textBox.Text = textBox.Text.Substring(0, textBox.Text.Length - 1);
                    }
                }
                else
                {
                    textBox.Text += key;
                }
            }
        }

        private IEnumerable<FrameworkElement> GetTemplateChildren()
        {
            if (GetTemplateChild("PART_Grid") is Panel templateRoot)
            {
                foreach (FrameworkElement child in templateRoot.Children)
                {
                    yield return child;
                }
            }
        }

        private void UpdateKeysForShift()
        {
            foreach (FrameworkElement child in GetTemplateChildren())
            {
                if (child is Button button)
                {
                    string content = button?.Content?.ToString();
                    if (content?.Length == 1 && char.IsLetter(content[0]))
                    {
                        button.Content = isShiftPressed ? content?.ToUpper() : content?.ToLower();
                    }
                }
            }
        }
    }
}
