using System.Windows;

namespace Cultos.App;

public partial class TextEditorWindow : Window
{
    public string ValueTitle => TitleBox.Text.Trim();
    public string ValueContent => ContentBox.Text.Trim();

    public TextEditorWindow(string heading,string title="",string content="",bool showContent=true,string titleLabel="Título",string contentLabel="Contenido")
    {
        InitializeComponent();
        HeadingText.Text=heading;
        TitleLabel.Text=titleLabel;
        ContentLabel.Text=contentLabel;
        TitleBox.Text=title;
        ContentBox.Text=content;
        if(!showContent)
        {
            ContentLabel.Visibility=Visibility.Collapsed;
            ContentBox.Visibility=Visibility.Collapsed;
            Height=240;
            MinHeight=220;
        }
        Loaded+=(_,_)=>{TitleBox.Focus();TitleBox.SelectAll();};
    }

    private void Save_Click(object sender,RoutedEventArgs e)
    {
        if(string.IsNullOrWhiteSpace(ValueTitle))
        {
            System.Windows.MessageBox.Show("Escribe un nombre o título.","Dato requerido",MessageBoxButton.OK,MessageBoxImage.Information);
            TitleBox.Focus();
            return;
        }
        DialogResult=true;
    }

    private void Cancel_Click(object sender,RoutedEventArgs e)=>DialogResult=false;
}
