using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunContentDragDropTests(MainWindow window)
    {
        var plan = Field<ObservableCollection<Cue>>(window, "_plan");
        var list = (ListBox)window.FindName("PlanList");
        var library = (ListBox)window.FindName("LibraryList");
        var bible = (ListBox)window.FindName("BibleResults");
        var session = Field<PresentationSession>(window, "_session");
        var savedPlan = plan.ToArray();
        var savedLibrary = library.ItemsSource;
        var savedBible = bible.ItemsSource;
        var savedSource = Field<int>(window, "_activeContentSource");
        var savedCatalog = Field<BibleCatalog?>(window, "_bible");
        var hymn = new Cue { Title = "Himno de prueba", HymnNumber = 42, Body = "Letra sintética", Notes = "Nota de fuente" };
        var other = new Cue { Title = "Otra selección", Body = "No arrastrar" };
        var video = new Cue { Title = "Himno en video de prueba", HymnNumber = 43, Kind = CueKind.Video,
            FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
        var verse = new Cue { Title = "Juan 3:1", Kind = CueKind.Bible, Reference = "Juan 3:1", Version = "Prueba", Body = "Versículo sintético" };
        var result = new BibleResult(new BibleVerse("Juan", 3, 1, verse.Body), verse);
        IDataObject Data(ListBox source, Cue cue) => (IDataObject)Call(window, "CreateContentDragData", source, cue)!;
        DragDropEffects Drop(IDataObject data, int index) => (DragDropEffects)Call(window, "ApplyContentDrop", data, index)!;
        void ResetPlan(params Cue[] cues)
        {
            plan.Clear(); foreach (var cue in cues) plan.Add(cue);
            Call(window, "UpdatePlanCount"); window.UpdateLayout();
        }
        ListBoxItem Row(ListBox source, object item)
        {
            source.ScrollIntoView(item); window.UpdateLayout();
            return (ListBoxItem)source.ItemContainerGenerator.ContainerFromItem(item);
        }
        void Press(ListBox source, object? row) => source.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent, Source = row ?? source });
        try
        {
            library.ItemsSource = new[] { hymn, other, video };
            SetField(window, "_bible", null);
            bible.ItemsSource = new[] { result };

            Test("Drag card identifies the grabbed content and closes on Escape without changing the order", () =>
            {
                Call(window, "SelectContentSource", 0);
                ResetPlan(other);
                var live = session.Live;
                Data(library, video);
                Call(window, "ShowContentDragPreview");
                var preview = Field<object>(window, "_contentDragPreview");
                var popup = Field<Popup>(preview, "_popup");
                PumpFor(80);
                Check(popup.IsOpen && popup.Child is FrameworkElement { ActualWidth: > 0, ActualHeight: > 0 }, "Drag card did not appear");
                Check(Descendants(popup.Child).OfType<TextBlock>().Any(text => text.Text == video.Title), "Drag card shows a different title");
                Check(Descendants(popup.Child).OfType<MediaThumbnail>().Single().Cue == video, "Drag card lost the grabbed media thumbnail");
                Check(!popup.IsHitTestVisible && window.IsActive, "Drag card intercepts input or steals focus");
                Check(GetDragCursorPos(out var pointer), "Cannot read drag pointer position");
                var grabbed = ((FrameworkElement)popup.Child).PointFromScreen(new Point(pointer.X, pointer.Y));
                Check(grabbed.X > 0 && grabbed.Y > 0 && grabbed.X < ((FrameworkElement)popup.Child).ActualWidth
                    && grabbed.Y < ((FrameworkElement)popup.Child).ActualHeight, "Drag card floats beside the cursor instead of underneath it");
                Check(((FrameworkElement)popup.Child).ActualHeight <= 80, "Drag card is a tall tooltip rather than a compact block");
                var feedback = (GiveFeedbackEventArgs)Activator.CreateInstance(typeof(GiveFeedbackEventArgs),
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { DragDropEffects.Copy, true }, null)!;
                feedback.RoutedEvent = DragDrop.GiveFeedbackEvent; library.RaiseEvent(feedback);
                Check(!feedback.UseDefaultCursors, "Dragging still uses the detached default OLE cursor");
                var cancel = (QueryContinueDragEventArgs)Activator.CreateInstance(typeof(QueryContinueDragEventArgs),
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { true, DragDropKeyStates.LeftMouseButton }, null)!;
                cancel.RoutedEvent = DragDrop.QueryContinueDragEvent;
                library.RaiseEvent(cancel);
                Check(!popup.IsOpen && popup.Child is null && Field<object?>(window, "_contentDragPreview") is null,
                    "Escape left a floating drag card behind");
                Check(plan.Count == 1 && plan[0] == other && ReferenceEquals(live, session.Live), "Cancelling changed the order or live output");
            });
            Test("Drag block preserves different grab positions without intercepting the drop", () =>
            {
                foreach (var anchor in new[] { new Point(.15, .25), new Point(.8, .75) })
                {
                    Data(library, video); SetField(window, "_contentDragAnchor", anchor);
                    Call(window, "ShowContentDragPreview"); PumpFor(60);
                    var popup = Field<Popup>(Field<object>(window, "_contentDragPreview"), "_popup");
                    var block = (FrameworkElement)popup.Child;
                    Check(GetDragCursorPos(out var pointer), "Cannot read pointer");
                    var point = block.PointFromScreen(new Point(pointer.X, pointer.Y));
                    Check(Math.Abs(point.X / block.ActualWidth - anchor.X) < .04
                        && Math.Abs(point.Y / block.ActualHeight - anchor.Y) < .04, "Block changed the grabbed point");
                    Check(!popup.IsHitTestVisible, "Block intercepts its drop target");
                    Call(window, "ClearContentDrag");
                    Check(!popup.IsOpen && popup.Child is null, "Released drag retained its block");
                }
            });
            Test("Order selection uses a background without a green outline", () =>
            {
                list.SelectedItem = other; window.UpdateLayout();
                var row = Row(list, other);
                var border = (Border)row.Template.FindName("Row", row);
                Check(border.BorderBrush is System.Windows.Media.SolidColorBrush { Color.A: 0 }
                    || border.BorderThickness == new Thickness(0), "Selected order row still has a visible outline");
            });
            Test("Content drag uses the pressed library row, ignores blank space, and clears on release or cancel", () =>
            {
                Call(window, "SelectContentSource", 0); library.SelectedItem = other;
                Press(library, Row(library, hymn));
                Check(ReferenceEquals(Field<Cue?>(window, "_contentDragCue"), hymn), "Drag took the previously selected cue");
                library.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent });
                Check(Field<Cue?>(window, "_contentDragCue") is null, "Released press remained armed");
                Check(!ReferenceEquals(Mouse.Captured, library), "Source capture survived mouse release");
                Press(library, Row(library, hymn));
                // WPF constructs this event internally during the OLE drag loop.
                var cancel = (QueryContinueDragEventArgs)Activator.CreateInstance(typeof(QueryContinueDragEventArgs),
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { true, DragDropKeyStates.LeftMouseButton }, null)!;
                cancel.RoutedEvent = DragDrop.QueryContinueDragEvent;
                library.RaiseEvent(cancel);
                Check(cancel.Action == DragAction.Cancel && Field<Cue?>(window, "_contentDragCue") is null, "Escape left a drag armed");
                Check(!ReferenceEquals(Mouse.Captured, library), "Escape retained source capture");
                Press(library, null);
                Check(Field<Cue?>(window, "_contentDragCue") is null, "Blank space dragged the selection");
            });
            Test("Hymn drop into empty order creates a selectable independent persisted copy without projecting", () =>
            {
                ResetPlan();
                var live = session.Live;
                var sourceData = Data(library, hymn);
                var token = sourceData.GetData("CultoStudio.ContentCue", false);
                Check(token is string, "OLE payload requires serializing application objects");
                var transported = new DataObject("CultoStudio.ContentCue", token!, false);
                Check(Drop(transported, 0) == DragDropEffects.Copy, "Library drag did not copy after token transport");
                var copy = plan.Single();
                Check(copy.Id != hymn.Id && !ReferenceEquals(copy, hymn) && copy.HymnNumber == 42 && copy.Body == hymn.Body && copy.Notes == hymn.Notes,
                    "Hymn copy lost metadata or shares identity");
                Check(ReferenceEquals(list.SelectedItem, copy) && session.Prepared?.Id == copy.Id && ReferenceEquals(live, session.Live),
                    "Dropped hymn was not prepared independently of live output");
                copy.Body = "Edición del orden";
                Check(hymn.Body == "Letra sintética", "Plan edit changed the source hymn");
                var restored = Field<WorkspaceStore>(window, "_store").Load().Plan.Items.Single();
                Check(restored.Id == copy.Id && restored.HymnNumber == 42 && restored.Body == hymn.Body, "Drop was not saved");
            });
            Test("Bible result drag unwraps the verse and preserves metadata at the chosen position", () =>
            {
                Call(window, "SelectContentSource", 1);
                Press(bible, Row(bible, result));
                Check(ReferenceEquals(Field<Cue?>(window, "_contentDragCue"), verse), "Bible drag did not unwrap its result");
                bible.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent });
                ResetPlan(other);
                Check(Drop(Data(bible, verse), 0) == DragDropEffects.Copy, "Bible drag did not copy");
                var copy = plan[0];
                Check(copy.Id != verse.Id && copy.Kind == CueKind.Bible && copy.Reference == verse.Reference
                    && copy.Version == verse.Version && copy.Body == verse.Body && ReferenceEquals(plan[1], other), "Bible insertion changed content or position");
                copy.Notes = "Nota independiente";
                Check(verse.Notes.Length == 0, "Bible source shares mutable plan state");
            });
            Test("Repeated copies insert at the beginning middle and tail with distinct identities", () =>
            {
                ResetPlan(other);
                // Each copy is a separate gesture. A completed/released OLE drag's
                // token may be cleared by normal input and cannot represent the next drag.
                Check(Drop(Data(library, hymn), 0) == DragDropEffects.Copy, "Beginning copy was rejected");
                Check(Drop(Data(library, hymn), 1) == DragDropEffects.Copy, "Middle copy was rejected");
                Check(Drop(Data(library, hymn), plan.Count) == DragDropEffects.Copy, "Tail copy was rejected");
                Check(plan.Count == 4 && ReferenceEquals(plan[2], other), "Copy insertion ignored the requested slot");
                Check(plan.Select(cue => cue.Id).Distinct().Count() == 4, "Repeated drops reused IDs");
            });
            Test("Order drag moves identity correctly in both directions and at the tail", () =>
            {
                var a = new Cue { Title = "A" }; var b = new Cue { Title = "B" }; var c = new Cue { Title = "C" };
                ResetPlan(a, b, c);
                Check(Drop(Data(list, a), plan.Count) == DragDropEffects.Move && plan.SequenceEqual(new[] { b, c, a }), "Tail move copied or used the wrong index");
                Drop(Data(list, a), 0);
                Check(plan.SequenceEqual(new[] { a, b, c }), "Upward move used the wrong index");
                Drop(Data(list, b), 2);
                Check(plan.SequenceEqual(new[] { a, b, c }), "Dropping after itself changed order");
                var persisted = Field<WorkspaceStore>(window, "_store").Load().Plan.Items;
                Check(persisted.Select(cue => cue.Id).SequenceEqual(plan.Select(cue => cue.Id)), "Reordered IDs were not saved");
            });
            Test("Drop geometry uses row halves and blank space appends, including an empty order", () =>
            {
                ResetPlan(other);
                var row = Row(list, other);
                var top = row.TranslatePoint(new Point(12, 0), list);
                Check((int)Call(window, "PlanInsertionIndex", new Point(top.X, top.Y + row.ActualHeight * .25))! == 0, "Upper half did not insert before");
                Check((int)Call(window, "PlanInsertionIndex", new Point(top.X, top.Y + row.ActualHeight * .75))! == 1, "Lower half did not insert after");
                Check((int)Call(window, "PlanInsertionIndex", new Point(12, list.ActualHeight - 8))! == 1, "Blank space did not append");
                ResetPlan();
                Check((int)Call(window, "PlanInsertionIndex", new Point(12, 12))! == 0, "Empty order rejected insertion");
            });
            Test("Unsupported data and a removed plan source cannot mutate the order", () =>
            {
                ResetPlan(other);
                var stale = Data(list, other);
                ResetPlan();
                Check(Drop(stale, 0) == DragDropEffects.None && Drop(new DataObject(DataFormats.Text, "No es contenido"), 0) == DragDropEffects.None
                    && plan.Count == 0, "Invalid drop changed the plan");
                Check(Call(window, "CreateContentDragData", library, verse) is null, "Source accepted a cue outside its list");
                Check(Drop(new DataObject("CultoStudio.ContentCue", Guid.NewGuid().ToString("N"), false), 0) == DragDropEffects.None,
                    "Foreign drag token was accepted");
                var cancelled = Data(library, hymn);
                Call(window, "ClearContentDrag");
                Check(Drop(cancelled, 0) == DragDropEffects.None && plan.Count == 0, "Cancelled token remained usable");
            });
            Test("Dropped video and Bible copies project through normal order double click", () =>
            {
                ResetPlan();
                Drop(Data(library, video), 0);
                var copy = plan.Single();
                Check(copy.FilePath == video.FilePath && copy.Kind == CueKind.Video && copy.HymnNumber == 43, "Video drop lost its playable source");
                list.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = Control.MouseDoubleClickEvent, Source = Row(list, copy) });
                Check(session.Live?.Id == copy.Id, "Order double click did not project the dropped copy");
                Until(() => Field<ProjectionSurface?>(window, "_output") is { IsPlaying: true, Duration: > 0 }, "Dropped video did not play");
                Drop(Data(bible, verse), plan.Count);
                var copiedVerse = plan[^1];
                list.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = Control.MouseDoubleClickEvent, Source = Row(list, copiedVerse) });
                Check(session.Live?.Id == copiedVerse.Id && session.Live.Body == verse.Body && session.Live.Reference == verse.Reference,
                    "Dropped Bible copy did not project its verse");
            });
        }
        finally
        {
            Call(window, "ClearContentDrag");
            Call(window, "CloseOutput");
            library.ItemsSource = savedLibrary; bible.ItemsSource = savedBible;
            SetField(window, "_bible", savedCatalog);
            ResetPlan(savedPlan); Call(window, "SelectContentSource", savedSource);
            if (plan.Count > 0) list.SelectedIndex = 0;
            Call(window, "SaveWorkspace");
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct DragCursorPoint { public int X; public int Y; }
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetCursorPos")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetDragCursorPos(out DragCursorPoint point);
}
