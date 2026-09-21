using Cultos.Core;
using System.Windows;
using System.Windows.Media;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using System.Windows.Media.Imaging;
using System.IO;
namespace Cultos.App;
public partial class OutputWindow : Window
{
 public OutputWindow()=>InitializeComponent();
 public void Render(PresentationSnapshot snapshot, ContentType? type=null)
 {
  OutputVideo.Stop();OutputVideo.Source=null;OutputVideo.Visibility=Visibility.Collapsed;OutputImage.Source=null;OutputImage.Visibility=Visibility.Collapsed;OutputText.Visibility=Visibility.Visible;
  Root.Background=snapshot.State==PresentationState.Black?MediaBrushes.Black:new SolidColorBrush(MediaColor.FromRgb(21,25,21));
  if(snapshot.State==PresentationState.Black){OutputText.Visibility=Visibility.Collapsed;return;}
  if(type==ContentType.Image&&File.Exists(snapshot.MediaPath)){OutputText.Visibility=Visibility.Collapsed;OutputImage.Source=new BitmapImage(new Uri(snapshot.MediaPath!));OutputImage.Visibility=Visibility.Visible;return;}
  if(type==ContentType.Video&&File.Exists(snapshot.MediaPath)){OutputText.Visibility=Visibility.Collapsed;OutputVideo.Source=new Uri(snapshot.MediaPath!);OutputVideo.Visibility=Visibility.Visible;OutputVideo.Play();return;}
  OutputText.Text=snapshot.State switch{PresentationState.Empty=>"",PresentationState.Logo=>"◇\nIglesia local",_=>snapshot.Content};
 }
 public void PlayMedia()=>OutputVideo.Play();
 public void PauseMedia()=>OutputVideo.Pause();
 public void StopMedia()=>OutputVideo.Stop();
}
