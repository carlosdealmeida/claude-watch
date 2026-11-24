using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClaudeWatch.Infrastructure;

namespace ClaudeWatch.Widget;

/// <summary>
/// Pergunta, na primeira execução, de onde o ClaudeWatch deve ler os limites.
/// Montado em código: é uma janela de duas opções e não justifica um XAML.
/// </summary>
public sealed class SourceDialog : Window
{
    public string? Choice { get; private set; }

    public SourceDialog()
    {
        Title = "ClaudeWatch — de onde ler seus limites";
        Width = 460; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(0x20, 0x24, 0x2A));

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(Text("De onde o ClaudeWatch deve ler seus limites?", 15, FontWeights.SemiBold, "#FFFFFF"));
        panel.Children.Add(Text("Dá para trocar depois no menu do ícone, em Fonte dos dados.", 12, FontWeights.Normal, "#A9B0B8", 4));

        panel.Children.Add(Option(
            "Statusline do Claude Code (recomendado)",
            "Caminho oficial e documentado. O ClaudeWatch vira a statusline do Claude Code e recebe a sessão de 5h e a semana. " +
            "Atualiza enquanto você usa o Claude Code. Não traz o limite do Fable.",
            Sources.Statusline));
        panel.Children.Add(Option(
            "API de uso (não oficial)",
            "Traz tudo, inclusive o limite do Fable e de onde veio o consumo. Usa um endpoint sem documentação, " +
            "que pode mudar sem aviso, com o token do Claude Code — e os termos da Anthropic restringem o uso desse token fora do Claude Code.",
            Sources.Api));

        Content = panel;
    }

    private Button Option(string title, string body, string value)
    {
        var content = new StackPanel();
        content.Children.Add(Text(title, 13, FontWeights.SemiBold, "#FFFFFF"));
        content.Children.Add(Text(body, 11.5, FontWeights.Normal, "#C9CED4", 4));
        var b = new Button
        {
            Content = content, Margin = new Thickness(0, 14, 0, 0), Padding = new Thickness(14, 12, 14, 12),
            HorizontalContentAlignment = HorizontalAlignment.Left, Cursor = System.Windows.Input.Cursors.Hand,
            Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x31, 0x39)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
        };
        b.Click += (_, _) => { Choice = value; DialogResult = true; };
        return b;
    }

    private static TextBlock Text(string s, double size, FontWeight weight, string color, double top = 0) => new()
    {
        Text = s, FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap,
        Foreground = (Brush)new BrushConverter().ConvertFromString(color)!, Margin = new Thickness(0, top, 0, 0),
    };
}
