using Cultos.Core;
using System.Windows;
using System.Windows.Media;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
namespace Cultos.App;
public partial class OutputWindow : Window
{
 public OutputWindow()=>InitializeComponent();
 public void Render(PresentationSnapshot snapshot){Root.Background=snapshot.State==PresentationState.Black?MediaBrushes.Black:new SolidColorBrush(MediaColor.FromRgb(21,25,21));OutputText.Text=snapshot.State switch{PresentationState.Empty=>"",PresentationState.Logo=>"◇\nIglesia local",_=>snapshot.Content};OutputText.Visibility=snapshot.State==PresentationState.Black?Visibility.Collapsed:Visibility.Visible;}
}
