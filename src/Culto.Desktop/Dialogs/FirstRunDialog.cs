using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Culto.Desktop.Dialogs;

public sealed class FirstRunDialog : Window
{
    private readonly TextBox _churchName;
    public string ChurchName => _churchName.Text.Trim();

    public FirstRunDialog(string churchName = "", bool firstRun = true)
    {
        var panel = DialogUi.Panel(this, firstRun ? "Bienvenidos a Culto Studio" : "Datos de la iglesia",
            "Prepara y presenta los cultos de tu congregación desde este equipo. El nombre de la iglesia es opcional y puedes cambiarlo después en Configuración.");
        FontFamily = new FontFamily("Segoe UI"); Width = 560;
        _churchName = new TextBox { Name = "ChurchName", MaxLength = 100, Text = churchName ?? "", MinHeight = 38 };
        DialogUi.Label(panel, "Nombre de la iglesia (opcional)", _churchName); panel.Children.Add(_churchName);
        var guide = new StackPanel();
        guide.Children.Add(new TextBlock { Text = "Tu espacio, tu contenido", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
        foreach (var text in new[]
        {
            "Los cultos, la biblioteca y los ajustes se guardan localmente en este equipo.",
            "La instalación completa incluye himnario, Biblias y fondos para trabajar sin internet. También puedes importar contenido desde Biblioteca.",
            "Añade el logo de tu iglesia en Configuración → Logo y elige la pantalla en Pantallas y salida.",
            "Conserva una copia de tus archivos multimedia junto con tus respaldos. No se necesita una cuenta para comenzar."
        })
            guide.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10), Style = (Style)FindResource("MutedText") });
        var card = DialogUi.Card(guide); card.Margin = new Thickness(0, 18, 0, 0); panel.Children.Add(card);
        var actions = DialogUi.Actions(panel);
        var cancel = DialogUi.Button(firstRun ? "Ahora no" : "Cancelar", () => DialogResult = false);
        cancel.Name = "CancelChurchSetup"; cancel.IsCancel = true; cancel.Margin = new Thickness(0, 0, 8, 0); actions.Children.Add(cancel);
        var accept = DialogUi.Button(firstRun ? "Comenzar" : "Guardar", () => DialogResult = true, true);
        accept.Name = "CompleteChurchSetup"; accept.IsDefault = true; accept.Margin = new Thickness(0); actions.Children.Add(accept);
        Loaded += (_, _) => { _churchName.Focus(); _churchName.SelectAll(); };
    }
}
