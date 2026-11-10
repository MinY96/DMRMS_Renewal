using System.Globalization;
using System.Text.RegularExpressions;

namespace DM_RMS.Wpf.Behaviors;

public static class NumericTextBoxValidationBehavior
{
    public static readonly System.Windows.DependencyProperty NumericTypeProperty =
        System.Windows.DependencyProperty.RegisterAttached(
            "NumericType",
            typeof(string),
            typeof(NumericTextBoxValidationBehavior),
            new System.Windows.PropertyMetadata(string.Empty, OnNumericTypeChanged));

    public static string GetNumericType(System.Windows.DependencyObject obj) => (string)obj.GetValue(NumericTypeProperty);
    public static void SetNumericType(System.Windows.DependencyObject obj, string value) => obj.SetValue(NumericTypeProperty, value);

    private static void OnNumericTypeChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (d is not System.Windows.Controls.TextBox textBox) return;

        textBox.PreviewTextInput -= OnPreviewTextInput;
        textBox.PreviewTextInput += OnPreviewTextInput;
        System.Windows.DataObject.RemovePastingHandler(textBox, OnPaste);
        System.Windows.DataObject.AddPastingHandler(textBox, OnPaste);
    }

    private static void OnPreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox) return;

        var nextText = GetNextText(textBox, e.Text);
        e.Handled = !IsValidPartial(GetNumericType(textBox), nextText);
    }

    private static void OnPaste(object sender, System.Windows.DataObjectPastingEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox) return;
        if (!e.DataObject.GetDataPresent(typeof(string)))
        {
            e.CancelCommand();
            return;
        }

        var pasteText = e.DataObject.GetData(typeof(string)) as string ?? string.Empty;
        var nextText = GetNextText(textBox, pasteText);
        if (!IsValidPartial(GetNumericType(textBox), nextText))
            e.CancelCommand();
    }

    private static string GetNextText(System.Windows.Controls.TextBox textBox, string input)
    {
        var text = textBox.Text ?? string.Empty;
        var start = textBox.SelectionStart;
        var length = textBox.SelectionLength;
        if (length > 0)
            text = text.Remove(start, length);
        return text.Insert(start, input);
    }

    private static bool IsValidPartial(string numericType, string value)
    {
        numericType = (numericType ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrEmpty(value) || value == "-" || value == "+") return true;

        if (numericType.Contains("integer"))
            return Regex.IsMatch(value, "^[+-]?[0-9]*$");

        if (numericType.Contains("double"))
        {
            if (!Regex.IsMatch(value, "^[+-]?([0-9]*)(\\.[0-9]*)?$")) return false;
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _) || value.EndsWith(".", StringComparison.Ordinal);
        }

        return true;
    }
}
