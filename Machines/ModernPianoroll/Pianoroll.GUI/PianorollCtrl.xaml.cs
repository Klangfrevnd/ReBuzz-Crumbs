using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Pianoroll.GUI
{
    public partial class PianorollCtrl : UserControl
    {
        internal Editor editor;
        NoteLayer noteLayer;
        SelectionLayer selectionLayer = new SelectionLayer();
        Pattern pattern;

        public Pattern Pattern
        {
            set
            {
                pattern = value;
                selectedNotes.Clear();
                if (noteLayer != null) noteLayer.InsertNotePanel.Clear();
            }
        }

        public void ThemeChanged() { Resources = Editor.Theme; UpdateBackground(); UpdatePatternDimensions(); }

        public PianorollCtrl()
        {
            Resources = Editor.Theme;
            InitializeComponent();
            LayoutUpdated += PianorollCtrl_LayoutUpdated;
            scrollViewer.ScrollChanged += scrollViewer_ScrollChanged;
            PreviewKeyDown += PianorollCtrl_PreviewKeyDown;
            LostFocus += PianorollCtrl_LostFocus;
            canvas.MouseLeftButtonDown += canvas_MouseLeftButtonDown;
            canvas.MouseMove += canvas_MouseMove;
            canvas.MouseLeftButtonUp += canvas_MouseLeftButtonUp;
            canvas.MouseRightButtonDown += canvas_MouseRightButtonDown;
            Focusable = true;
        }

        bool addToSelection;
        bool painting;
        int lastPaintTime = int.MinValue, lastPaintNote = int.MinValue;
        int drawLength;

        void canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            addToSelection = Keyboard.Modifiers == ModifierKeys.Control;
            Point point = e.GetPosition(canvas);
            if (addToSelection)
            {
                selectionLayer.BeginSelect(point);
                Select(new Int32Rect(), false);
                e.Handled = true;
                return;
            }

            painting = true;
            canvas.CaptureMouse();
            lastPaintTime = int.MinValue;
            lastPaintNote = int.MinValue;
            drawLength = editor.GridValue > 0 ? Math.Max(1, PianorollGlobal.TicksPerBeat / editor.GridValue) : PianorollGlobal.TicksPerBeat / 4;
            PaintAt(point);
            e.Handled = true;
        }

        void canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            EraseAt(e.GetPosition(canvas));
            e.Handled = true;
        }

        void PaintAt(Point point)
        {
            var cell = PointToCell(point);
            if (cell.note < editor.PatDim.FirstMidiNote || cell.note >= editor.PatDim.FirstMidiNote + editor.PatDim.NoteCount || cell.time < 0 || cell.time >= pattern.LengthInTicks) return;
            if (cell.time == lastPaintTime && cell.note == lastPaintNote) return;
            lastPaintTime = cell.time;
            lastPaintNote = cell.note;
            AddNote(new NoteEvent(cell.time, drawLength, cell.note, editor.lastVelocity));
        }

        void EraseAt(Point point)
        {
            var cell = PointToCell(point);
            var candidates = new NoteSet();
            noteLayer.SelectNotes(new Int32Rect(cell.note, cell.time, 1, 1), candidates, false);
            if (!candidates.IsEmpty) DeleteNotes(candidates);
        }

        (int time, int note) PointToCell(Point point)
        {
            double y = point.Y - Canvas.GetTop(canvas.Children[0]);
            int pitch = (int)Math.Floor(point.X / editor.PatDim.NoteWidth) + editor.PatDim.FirstMidiNote;
            double ticks = Math.Max(0, y) * PianorollGlobal.TicksPerBeat / editor.PatDim.BeatHeight;
            if (editor.GridValue > 0)
            {
                int step = Math.Max(1, PianorollGlobal.TicksPerBeat / editor.GridValue);
                ticks = Math.Round(ticks / step) * step;
            }
            return ((int)Math.Round(ticks), pitch);
        }

        void canvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point p = e.GetPosition(canvas);
            if (painting && e.LeftButton == MouseButtonState.Pressed) PaintAt(p);
            if (selectionLayer.Selecting) Select(DPToLP(selectionLayer.UpdateSelect(p)), addToSelection);
        }

        void canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (painting) { painting = false; canvas.ReleaseMouseCapture(); }
            if (selectionLayer.Selecting)
            {
                Point p = e.GetPosition(canvas);
                Select(DPToLP(selectionLayer.UpdateSelect(p)), addToSelection);
                selectionLayer.EndSelect(p);
            }
        }

        Int32Rect DPToLP(Rect r)
        {
            r.Y -= Canvas.GetTop(canvas.Children[0]);
            int x1 = (int)Math.Floor(r.X / editor.PatDim.NoteWidth);
            int x2 = (int)Math.Ceiling((r.X + r.Width) / editor.PatDim.NoteWidth);
            int y1 = (int)Math.Floor(r.Y * PianorollGlobal.TicksPerBeat / editor.PatDim.BeatHeight);
            int y2 = (int)Math.Ceiling((r.Y + r.Height) * PianorollGlobal.TicksPerBeat / editor.PatDim.BeatHeight);
            return new Int32Rect(x1 + editor.PatDim.FirstMidiNote, y1, x2 - x1, y2 - y1);
        }

        Rect LPToDP(Int32Rect r)
        {
            double x = (r.X - editor.PatDim.FirstMidiNote) * editor.PatDim.NoteWidth;
            double y = r.Y * editor.PatDim.BeatHeight / PianorollGlobal.TicksPerBeat + Canvas.GetTop(canvas.Children[0]);
            return new Rect(x, y, r.Width * editor.PatDim.NoteWidth, r.Height * editor.PatDim.BeatHeight / PianorollGlobal.TicksPerBeat);
        }

        Rect NoteVisualBounds(NoteEvent ne) { return LPToDP(new Int32Rect(ne.Note, ne.Time, 1, ne.Length)); }
        int YToTime(double y) { return (int)Math.Round(y * PianorollGlobal.TicksPerBeat / editor.PatDim.BeatHeight); }
        int GetCursorTime() { return YToTime(scrollViewer.VerticalOffset); }
        void SetCursorTime(int t) { scrollViewer.ScrollToVerticalOffset(Math.Round((double)t * editor.PatDim.BeatHeight / PianorollGlobal.TicksPerBeat)); }

        double SnapToGrid(double y)
        {
            if (editor.GridValue == 0) return y;
            double ticks = y * PianorollGlobal.TicksPerBeat / editor.PatDim.BeatHeight;
            int step = Math.Max(1, PianorollGlobal.TicksPerBeat / editor.GridValue);
            ticks = Math.Round(ticks / step) * step;
            return ticks * editor.PatDim.BeatHeight / PianorollGlobal.TicksPerBeat;
        }

        internal NoteSet selectedNotes = new NoteSet();
        void Select(Point p, bool add) { Select(new Int32Rect((int)p.X, (int)p.Y, 1, 1), add); }
        void Select(Int32Rect r, bool add) { if (!add) selectedNotes.Clear(); noteLayer.SelectNotes(r, selectedNotes, add); }
        internal void Select(NoteEvent ne, bool add) { if (!add) selectedNotes.Clear(); noteLayer.SelectNote(ne, selectedNotes, add); }
        void SelectNotesStartingAt(int t) { selectedNotes.Clear(); noteLayer.SelectNotes(t, selectedNotes, false); }

        void SelectAdjacentNote(bool next, bool add)
        {
            NoteEvent ne = selectedNotes.IsEmpty ? NoteEvent.Invalid : selectedNotes.GetFirstOrLastNote(!next);
            ne = pattern.GetAdjacentNote(0, ne, next);
            if (!ne.IsInvalid) { Select(ne, add); SetCursorTime(ne.Time); }
        }

        internal void DeleteNotes(NoteSet notes)
        {
            if (notes == null || notes.IsEmpty) return;
            var deleted = new NoteSet(notes);
            noteLayer.DeleteNotes(deleted);
            pattern.BeginAction("Delete Notes");
            pattern.DeleteNotes(deleted);
            selectedNotes.Remove(deleted);
        }

        void PianorollCtrl_LostFocus(object sender, RoutedEventArgs e) { ResetInsertNotes(); }
        public void KillFocus() { ResetInsertNotes(); }

        public void CursorDown()
        {
            double y = editor.GridValue > 0 ? SnapToGrid(scrollViewer.VerticalOffset + (double)editor.PatDim.BeatHeight / editor.GridValue) : scrollViewer.VerticalOffset + 1;
            if (!noteLayer.InsertNotePanel.IsEmpty && YToTime(y) > pattern.LengthInTicks) { pattern.BeginAction("Grow"); pattern.Length++; }
            scrollViewer.ScrollToVerticalOffset(y);
            if (noteLayer.InsertNotePanel.IsEmpty) SelectNotesStartingAt(YToTime(y));
        }

        public void CursorUp()
        {
            double y = editor.GridValue > 0 ? SnapToGrid(scrollViewer.VerticalOffset - (double)editor.PatDim.BeatHeight / editor.GridValue) : scrollViewer.VerticalOffset - 1;
            y = Math.Max(0, y);
            scrollViewer.ScrollToVerticalOffset(y);
            if (noteLayer.InsertNotePanel.IsEmpty) SelectNotesStartingAt(YToTime(y));
        }

        void PianorollCtrl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                switch (e.Key)
                {
                    case Key.Down: CursorDown(); e.Handled = true; break;
                    case Key.Up: CursorUp(); e.Handled = true; break;
                    case Key.PageDown: scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + editor.PatDim.BeatHeight * 2); e.Handled = true; break;
                    case Key.PageUp: scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - editor.PatDim.BeatHeight * 2); e.Handled = true; break;
                    case Key.Left: SelectAdjacentNote(false, false); e.Handled = true; break;
                    case Key.Right: SelectAdjacentNote(true, false); e.Handled = true; break;
                    case Key.Home: scrollViewer.ScrollToVerticalOffset(0); e.Handled = true; break;
                    case Key.End: scrollViewer.ScrollToVerticalOffset(double.MaxValue); e.Handled = true; break;
                    case Key.Delete: DeleteNotes(selectedNotes); e.Handled = true; break;
                    case Key.D1: ShowNoteProperties(); e.Handled = true; break;
                }
            }
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.A) { Select(new Int32Rect(0, 0, Int32.MaxValue, Int32.MaxValue), false); e.Handled = true; }
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Shift && (e.Key == Key.Left || e.Key == Key.Right)) { SelectAdjacentNote(e.Key == Key.Right, true); e.Handled = true; }
        }

        public void PianoKeyDown(int key, int velocity)
        {
            int time = GetCursorTime();
            if (editor.GridValue > 0) { int step = Math.Max(1, PianorollGlobal.TicksPerBeat / editor.GridValue); time = (int)Math.Round((double)time / step) * step; }
            noteLayer.InsertNotePanel.AddNote(new NoteEvent(time, 0, key, velocity), true);
        }

        public void PianoKeyUp(int key)
        {
            NoteVisual visual = noteLayer.InsertNotePanel.GetNoteVisual(key);
            if (visual == null) return;
            noteLayer.InsertNotePanel.RemoveNoteVisual(visual);
            if (visual.Note.Length > 0) AddNote(visual.Note);
        }

        void AddNote(NoteEvent note)
        {
            noteLayer.AddNote(note);
            pattern.BeginAction("Add Note");
            pattern.AddNote(pattern.GetAvailableTrack(note), note);
        }

        public void AddRecordedNote(NoteEvent note) { noteLayer.AddNote(note); }

        void UpdateInsertNotes()
        {
            int time = GetCursorTime();
            foreach (var visual in noteLayer.InsertNotePanel.NoteVisuals)
            {
                int length = time - visual.Note.Time;
                if (editor.GridValue > 0) { int step = Math.Max(1, PianorollGlobal.TicksPerBeat / editor.GridValue); length = (int)Math.Round((double)length / step) * step; }
                visual.Note.Length = Math.Max(0, length);
                visual.InvalidateVisual();
            }
            if (!noteLayer.InsertNotePanel.IsEmpty) noteLayer.InsertNotePanel.InvalidateMeasure();
        }

        void ResetInsertNotes() { if (!noteLayer.InsertNotePanel.IsEmpty) { noteLayer.InsertNotePanel.Clear(); noteLayer.InsertNotePanel.InvalidateMeasure(); } }
        void scrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) { if (e.VerticalChange != 0) { UpdateInsertNotes(); UpdateBackground(); } }

        void UpdateBackground()
        {
            if (border == null) return;
            if (border.Background is ImageBrush image && image.ImageSource != null) image.Viewport = new Rect(0, -Parallax.GetScrollSpeed(image) * scrollViewer.VerticalOffset, image.ImageSource.Width, image.ImageSource.Height);
        }

        void PianorollCtrl_LayoutUpdated(object sender, EventArgs e)
        {
            if (canvas.Children.Count < 2) return;
            double y = Math.Floor(scrollViewer.ViewportHeight / 2);
            Canvas.SetTop(canvas.Children[0], y);
            Canvas.SetTop(canvas.Children[1], y);
            canvas.Height = editor.PatDim.Height + scrollViewer.ViewportHeight;
        }

        public void UpdatePatternDimensions()
        {
            Stopwatch sw = Stopwatch.StartNew();
            canvas.Children.Clear();
            canvas.Children.Add(new PatternBackgroundVisual(editor));
            if (noteLayer == null) noteLayer = new NoteLayer(editor); else noteLayer.Recreate();
            canvas.Children.Add(noteLayer);
            canvas.Width = editor.PatDim.Width;
            canvas.Children.Add(selectionLayer);
            selectedNotes.Clear();
            sw.Stop();
            editor.PianorollMachine.WriteDC(string.Format("UpdatePatternDimensions {0}ms", sw.ElapsedMilliseconds));
        }

        public void NoteClicked(NoteVisual visual, bool play)
        {
            if (!selectedNotes.Contains(visual.Note)) Select(visual.Note, Keyboard.Modifiers == ModifierKeys.Control);
            editor.PianorollMachine.PlayNoteEvents(selectedNotes, play);
        }

        public void ShowNoteProperties()
        {
            if (selectedNotes.IsEmpty) return;
            Rect r = LPToDP(selectedNotes.Bounds);
            Point p = canvas.PointToScreen(new Point(r.X + r.Width + 2, r.Y - 1));
            var window = new NotePropertiesWindow(selectedNotes.CommonVelocity) { WindowStartupLocation = WindowStartupLocation.Manual, Left = p.X, Top = p.Y };
            new WindowInteropHelper(window).Owner = ((HwndSource)PresentationSource.FromVisual(editor)).Handle;
            if (!(bool)window.ShowDialog()) return;
            pattern.BeginAction("Set Velocity");
            pattern.SetVelocity(0, selectedNotes, window.Velocity);
            noteLayer.SetVelocity(selectedNotes, window.Velocity);
            selectedNotes.CommonVelocity = window.Velocity;
            editor.lastVelocity = window.Velocity;
        }

        NoteEvent setNoteLengthOriginalEvent;
        Point dragStartPoint;
        NoteVisual dragNoteVisual;
        NoteSet moveOriginalNotes;
        public enum Mode { Normal, SetNoteLength, MoveSelectedNotes }
        internal Mode mode = Mode.Normal;

        public void BeginSetNoteLength(NoteVisual visual, Point point) { visual.CaptureMouse(); dragStartPoint = point; dragNoteVisual = visual; setNoteLengthOriginalEvent = visual.Note; mode = Mode.SetNoteLength; }

        public void UpdateSetNoteLength(Point point)
        {
            int delta = YToTime(point.Y - dragStartPoint.Y);
            int length = Math.Max(1, setNoteLengthOriginalEvent.Length + delta);
            if (editor.GridValue > 0) { int step = Math.Max(1, PianorollGlobal.TicksPerBeat / editor.GridValue); length = Math.Max(step, (int)Math.Round((double)length / step) * step); }
            dragNoteVisual.Note.Length = length;
            dragNoteVisual.InvalidateVisual();
            MakeVisible(NoteVisualBounds(dragNoteVisual.Note));
        }

        public void EndSetNoteLength()
        {
            dragNoteVisual.ReleaseMouseCapture();
            if (dragNoteVisual.Note.Length != setNoteLengthOriginalEvent.Length) { pattern.BeginAction("Set Note Length"); pattern.SetLength(0, new[] { setNoteLengthOriginalEvent }, dragNoteVisual.Note.Length); }
            mode = Mode.Normal;
            dragNoteVisual = null;
        }

        public void BeginMoveSelectedNotes(NoteVisual visual, Point point)
        {
            visual.CaptureMouse();
            dragStartPoint = point;
            dragNoteVisual = visual;
            moveOriginalNotes = selectedNotes.IsEmpty ? new NoteSet(visual.Note) : new NoteSet(selectedNotes);
            mode = Mode.MoveSelectedNotes;
        }

        public void UpdateMoveSelectedNotes(Point point)
        {
            int dx = (int)Math.Round((point.X - dragStartPoint.X) / editor.PatDim.NoteWidth);
            int dy = YToTime(point.Y - dragStartPoint.Y);
            if (editor.GridValue > 0) { int step = Math.Max(1, PianorollGlobal.TicksPerBeat / editor.GridValue); dy = (int)Math.Round((double)dy / step) * step; }
            selectedNotes.Clear();
            foreach (var original in moveOriginalNotes)
            {
                var moved = original;
                moved.Note = Math.Max(editor.PatDim.FirstMidiNote, Math.Min(editor.PatDim.FirstMidiNote + editor.PatDim.NoteCount - 1, moved.Note + dx));
                moved.Time = Math.Max(0, moved.Time + dy);
                selectedNotes.Add(moved);
                foreach (var panel in noteLayer.Panels) panel.UpdateNote(original, moved);
            }
            noteLayer.InvalidateMeasure();
        }

        public void EndMoveSelectedNotes()
        {
            dragNoteVisual.ReleaseMouseCapture();
            if (moveOriginalNotes != null && !moveOriginalNotes.IsEmpty)
            {
                Int32Rect a = moveOriginalNotes.Bounds;
                Int32Rect b = selectedNotes.Bounds;
                int dx = b.X - a.X;
                int dy = b.Y - a.Y;
                if (dx != 0 || dy != 0) { pattern.BeginAction("Move Notes"); pattern.MoveNotes(0, moveOriginalNotes, dx, dy); }
            }
            mode = Mode.Normal;
            dragNoteVisual = null;
            moveOriginalNotes = null;
        }

        void MakeVisible(Rect rect)
        {
            double y = scrollViewer.VerticalOffset;
            if (rect.Bottom > y + scrollViewer.ViewportHeight) y = rect.Bottom - scrollViewer.ViewportHeight;
            var animation = new DoubleAnimation(scrollViewer.VerticalOffset, y, new Duration(TimeSpan.FromMilliseconds(200)));
            var storyboard = new Storyboard(); storyboard.Children.Add(animation);
            Storyboard.SetTarget(animation, scrollViewer);
            Storyboard.SetTargetProperty(animation, new PropertyPath(AniScrollViewer.CurrentVerticalOffsetProperty));
            storyboard.Begin();
        }
    }
}
