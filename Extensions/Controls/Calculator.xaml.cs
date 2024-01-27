using System;
using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Extensions.Controls
{
    public partial class Calculator : UserControl
    {
        private readonly StringBuilder displayTextBuilder = new();
        private double memory;
        private double result;

        public Calculator() { InitializeComponent(); }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string value = ((Button)sender).Content.ToString();
            _ = displayTextBuilder.Append(value);
            InputTextBox.Text = displayTextBuilder.ToString();
        }

        private void Clear_Click(object sender, RoutedEventArgs e) => ClearDisplay();

        private void ClearDisplay()
        {
            _ = displayTextBuilder.Clear();
            InputTextBox.Text = string.Empty;
        }

        private void ClearEntry_Click(object sender, RoutedEventArgs e) => displayTextBuilder.Clear();

        private void DisplayError(string message)
        {
            _ = MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Information);
            ClearDisplay();
        }

        private void DisplayResult(double value)
        {
            _ = displayTextBuilder.Clear();
            _ = displayTextBuilder.Append(value);
            InputTextBox.Text = displayTextBuilder.ToString();
        }

        private void Equals_Click(object sender, RoutedEventArgs e)
        {
            string expression = displayTextBuilder.ToString();
            if (string.IsNullOrWhiteSpace(expression))
            {
                return;
            }

            try
            {
                result = Convert.ToDouble(new DataTable().Compute(expression.Replace(',', '.'), null));
                DisplayResult(result);
            }
            catch (Exception ex)
            {
                DisplayError(ex.Message);
            }
        }

        private void MemoryAdd_Click(object sender, RoutedEventArgs e) => UpdateMemoryOperation((double currentValue) => memory += currentValue);

        private void MemoryClear_Click(object sender, RoutedEventArgs e) => memory = 0;

        private void MemoryRecall_Click(object sender, RoutedEventArgs e) => DisplayResult(memory);

        private void MemorySubtract_Click(object sender, RoutedEventArgs e) => UpdateMemoryOperation((double currentValue) => memory -= currentValue);

        private void Negate_Click(object sender, RoutedEventArgs e)
        {
            if (displayTextBuilder.Length == 0 || displayTextBuilder.ToString() == "0")
            {
                return;
            }

            if (displayTextBuilder.ToString().StartsWith("-"))
            {
                _ = displayTextBuilder.Remove(0, 1);
            }
            else
            {
                _ = displayTextBuilder.Insert(0, '-');
            }

            InputTextBox.Text = displayTextBuilder.ToString();
        }

        private void Percentage_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(displayTextBuilder.ToString(), out double currentValue))
            {
                DisplayResult(currentValue / 100);
            }
            else
            {
                DisplayError("Invalid Input");
            }
        }

        private void PerformUnaryOperation(Func<double, double> operation)
        {
            if (double.TryParse(displayTextBuilder.ToString(), out double currentValue))
            {
                DisplayResult(operation(currentValue));
            }
            else
            {
                DisplayError("Invalid Input");
            }
        }

        private void Reciprocal_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(displayTextBuilder.ToString(), out double currentValue))
            {
                if (currentValue == 0)
                {
                    DisplayError("Cannot divide by zero");
                    return;
                }

                DisplayResult(1 / currentValue);
            }
            else
            {
                DisplayError("Invalid Input");
            }
        }

        private void Square_Click(object sender, RoutedEventArgs e) => PerformUnaryOperation((double currentValue) => currentValue * currentValue);

        private void SquareRoot_Click(object sender, RoutedEventArgs e) => PerformUnaryOperation(Math.Sqrt);

        private void TrigonometricFunction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button trigButton)
            {
                return;
            }

            string functionName = trigButton.Content.ToString();
            if (double.TryParse(displayTextBuilder.ToString(), out double currentValue))
            {
                switch (functionName)
                {
                    case "Sin":
                        DisplayResult(Math.Sin(currentValue));
                        break;
                    case "Cos":
                        DisplayResult(Math.Cos(currentValue));
                        break;
                    case "Tan":
                        DisplayResult(Math.Tan(currentValue));
                        break;
                    case "Log":
                        DisplayResult(Math.Log(currentValue));
                        break;
                }
            }
            else
            {
                DisplayError("Invalid Input");
            }
        }

        private void UpdateMemoryOperation(Action<double> operation)
        {
            if (double.TryParse(displayTextBuilder.ToString(), out double currentValue))
            {
                operation(currentValue);
            }
            else
            {
                DisplayError("Invalid Input");
            }
        }
    }
}
