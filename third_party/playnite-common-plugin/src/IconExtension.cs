using System.Globalization;
using System.Windows.Markup;

namespace CommonPlugin;

public class IconExtension : MarkupExtension
{
    public IconExtension(string code)
    {
        Code = code;
    }

    public IconExtension(string code, string text)
    {
        Code = code;
        Text = text;
    }

    public IconExtension()
    {
    }

    public string? Code { get; set; }
    public string? Text { get; set; }

    public override object? ProvideValue(IServiceProvider serviceProvider)
    {
        var finalResult = "";
        if (Code != null)
        {
            finalResult = char.ConvertFromUtf32(int.Parse(Code, NumberStyles.HexNumber));
            if (!string.IsNullOrEmpty(Text))
            {
                finalResult += $" {Text}";
            }
        }

        return finalResult;
    }
}