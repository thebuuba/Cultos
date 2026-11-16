using System.Text;

namespace Culto.Desktop.Services;

public sealed partial class PowerPointPresentation
{
    public string ReadSlideNotes(int index)
    {
        if (_presentation is null || index < 1 || index > SlideCount) throw new ArgumentOutOfRangeException(nameof(index));
        dynamic? slides = null, slide = null, notes = null, shapes = null;
        var result = new StringBuilder();
        try
        {
            slides = _presentation.Slides;
            slide = slides[index];
            notes = slide.NotesPage;
            shapes = notes.Shapes;
            for (var i = 1; i <= (int)shapes.Count; i++)
            {
                dynamic? shape = null, placeholder = null, frame = null, text = null;
                try
                {
                    shape = shapes[i];
                    if ((int)shape.Type != 14) continue; // msoPlaceholder
                    placeholder = shape.PlaceholderFormat;
                    if ((int)placeholder.Type != 2 || (int)shape.HasTextFrame != -1) continue; // ppPlaceholderBody only, no headers/footer.
                    frame = shape.TextFrame;
                    text = frame.TextRange;
                    var body = ((string)text.Text).Replace("\r\n", "\n").Replace('\r', '\n').Replace('\v', '\n').Trim();
                    if (body.Length > 0) result.AppendLine(body);
                }
                finally { Release(text); Release(frame); Release(placeholder); Release(shape); }
            }
            return result.ToString().Trim();
        }
        finally { Release(shapes); Release(notes); Release(slide); Release(slides); }
    }

    public void ExportSlidePreview(int index, string path)
    {
        if (_presentation is null || index < 1 || index > SlideCount) throw new ArgumentOutOfRangeException(nameof(index));
        dynamic? slides = null, slide = null;
        try
        {
            slides = _presentation.Slides;
            slide = slides[index];
            slide.Export(path, "PNG", 1280, 720);
        }
        finally { Release(slide); Release(slides); }
    }
}
