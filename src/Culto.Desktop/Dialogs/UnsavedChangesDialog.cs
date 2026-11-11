using System.Windows;

namespace Culto.Desktop.Dialogs;

public sealed class UnsavedChangesDialog : Window
{
    public UnsavedChangesDialog()
    {
        var panel = DialogUi.Panel(this, "Cambios sin guardar",
            "No se pudo guardar el espacio local. Cerrar sin guardar puede perder los cambios del orden, biblioteca, botones y configuración. " +
            "Una copia exportada del culto solo conserva el orden; no respalda la biblioteca, el mando ni los archivos multimedia.");
        var cancel = DialogUi.Button("Cancelar y seguir trabajando", () => DialogResult = false, true);
        cancel.Name = "KeepOpenButton";
        cancel.IsCancel = true;
        cancel.IsDefault = true;
        panel.Children.Add(cancel);
        var close = DialogUi.Button("Cerrar sin guardar localmente", () => DialogResult = true);
        close.Name = "CloseWithoutSavingButton";
        panel.Children.Add(close);
        Loaded += (_, _) => cancel.Focus();
    }
}
