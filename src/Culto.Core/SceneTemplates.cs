namespace Culto.Core;

public sealed record SceneTemplate(string Id, string Name, string Description);

public static class SceneTemplates
{
    public static IReadOnlyList<SceneTemplate> Catalog { get; } = Array.AsReadOnly(new[]
    {
        new SceneTemplate("welcome", "Bienvenida", "Saludo y presentación del culto."),
        new SceneTemplate("reading", "Lectura bíblica", "Pasaje y referencia para la lectura."),
        new SceneTemplate("sermon", "Sermón", "Tema y nombre del predicador."),
        new SceneTemplate("announcement", "Anuncio", "Título, mensaje y fecha de una actividad.")
    });

    public static SceneComposition Create(string id, Cue? background = null)
    {
        var scene = new SceneComposition { Background = "#101B2D" };
        if (!Catalog.Any(template => template.Id == id)) throw new ArgumentException("La plantilla no existe.", nameof(id));
        if (background is not null)
        {
            if (background.Kind is not (CueKind.Image or CueKind.Video))
                throw new ArgumentException("El fondo debe ser una imagen o un video.", nameof(background));
            scene.Layers.Add(new SceneLayer
            {
                Name = "Fondo", SourceId = background.Id.ToString("N"), Source = background.Copy(),
                Muted = background.Kind == CueKind.Video, Loop = background.Kind == CueKind.Video
            });
        }

        switch (id)
        {
            case "welcome":
                AddText(scene, "Bienvenidos", 300, 200, "Zoom", 0);
                AddText(scene, "Nos alegra compartir este culto contigo", 530, 160, "Rise", 250);
                break;
            case "reading":
                AddText(scene, "Lectura bíblica", 150, 160, "Fade", 0);
                AddText(scene, "Tu palabra es una lámpara a mis pies y una luz en mi camino.", 340, 350, "Rise", 200);
                AddText(scene, "Salmos 119:105", 730, 130, "SlideLeft", 400);
                break;
            case "sermon":
                AddText(scene, "El mensaje de hoy", 560, 160, "SlideLeft", 0);
                AddText(scene, "Nombre del predicador", 750, 130, "Rise", 250);
                break;
            case "announcement":
                AddText(scene, "Próxima actividad", 200, 180, "Zoom", 0);
                AddText(scene, "Escribe aquí los detalles del anuncio", 420, 240, "Rise", 200);
                AddText(scene, "Fecha · Hora · Lugar", 720, 130, "Fade", 400);
                break;
        }
        return scene.Normalize();
    }

    private static void AddText(SceneComposition scene, string text, double y, double height, string entrance, int delay)
    {
        // Text sources use a 16:9 viewport; preserve it while positioning their centered text.
        var width = scene.Layers.Any(layer => layer.Source.Kind == CueKind.Text) ? 1440d : 1600d;
        var actualHeight = width * 9 / 16;
        scene.Layers.Add(new SceneLayer
        {
            Name = text, Source = new Cue { Kind = CueKind.Text, Title = text, Body = text },
            X = (scene.Width - width) / 2, Y = y + height / 2 - actualHeight / 2,
            Width = width, Height = actualHeight,
            Animation = new SceneAnimationOptions { Entrance = entrance, Exit = "Fade", DelayMilliseconds = delay }
        });
    }
}
