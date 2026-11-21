using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Pianoroll.GUI
{
    public class NoteVisual : UIElement
    {
        public NoteEvent Note;
        Editor editor;
        bool selected;
        bool newnote;
        public bool Selected { get { return selected; } set { if (value != selected) { selected = value; InvalidateVisual(); } } }
        public int Velocity { get { return Note.Velocity; } set { if (value != Note.Velocity) { Note.Velocity = value; InvalidateVisual(); } } }

        public NoteVisual(NoteEvent ne, Editor editor, bool newnote) { Note = ne; this.editor = editor; this.newnote = newnote; }

        static Brush[] brush = new Brush[3];
        static Brush[] borderBrush = new Brush[3];
        static Brush[] highlightBrush = new Brush[3];
        static Brush[] shadowBrush = new Brush[3];
        static Typeface font = new Typeface("Verdana");
        static CultureInfo cultureInfo = CultureInfo.GetCultureInfo("en-us");

        protected override void OnRender(DrawingContext dc)
        {
            if (Note.Length <= 0) return;
            if (brush[0] == null)
            {
                brush[0] = (Brush)editor.FindResource("NoteBrush"); borderBrush[0] = (Brush)editor.FindResource("NoteBorderBrush"); highlightBrush[0] = (Brush)editor.FindResource("NoteHighlightBrush"); shadowBrush[0] = (Brush)editor.FindResource("NoteShadowBrush");
                brush[1] = (Brush)editor.FindResource("NewNoteBrush"); borderBrush[1] = (Brush)editor.FindResource("NewNoteBorderBrush"); highlightBrush[1] = (Brush)editor.FindResource("NewNoteHighlightBrush"); shadowBrush[1] = (Brush)editor.FindResource("NewNoteShadowBrush");
                brush[2] = (Brush)editor.FindResource("SelNoteBrush"); borderBrush[2] = (Brush)editor.FindResource("SelNoteBorderBrush"); highlightBrush[2] = (Brush)editor.FindResource("SelNoteHighlightBrush"); shadowBrush[2] = (Brush)editor.FindResource("SelNoteShadowBrush");
                for (int i = 0; i < brush.Length; i++) { brush[i].Freeze(); borderBrush[i].Freeze(); highlightBrush[i].Freeze(); shadowBrush[i].Freeze(); }
            }
            var dimensions = editor.PatDim;
            double height = Math.Max(1, Note.Length * dimensions.BeatHeight / PianorollGlobal.TicksPerBeat);
            int index = selected ? 2 : (newnote ? 1 : 0);
            dc.DrawRectangle(borderBrush[index], null, new Rect(0, 0, dimensions.NoteWidth + 1, height + 1));
            if (height > 1) dc.DrawRectangle(brush[index], null, new Rect(1, 1, dimensions.NoteWidth - 1, height - 1));
            dc.DrawRectangle(highlightBrush[index], null, new Rect(1, 1, dimensions.NoteWidth - 2, 1));
            if (height > 3) dc.DrawRectangle(highlightBrush[index], null, new Rect(1, 2, 1, height - 3));
            dc.DrawRectangle(shadowBrush[index], null, new Rect(2, height - 1, dimensions.NoteWidth - 2, 1));
            if (height > 3) dc.DrawRectangle(shadowBrush[index], null, new Rect(dimensions.NoteWidth - 1, 2, 1, height - 3));
            var text = new FormattedText(Note.Velocity.ToString(), cultureInfo, FlowDirection.LeftToRight, font, dimensions.NoteFontSize, Brushes.Black);
            text.MaxTextWidth = dimensions.NoteWidth; text.TextAlignment = TextAlignment.Center; text.MaxTextHeight = height;
            dc.DrawText(text, new Point(0, 0));
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            editor.pianoroll.NoteClicked(this, true);
            Point p = e.GetPosition(editor.pianoroll.canvas);
            if (Mouse.OverrideCursor == Cursors.SizeNS) editor.pianoroll.BeginSetNoteLength(this, p); else editor.pianoroll.BeginMoveSelectedNotes(this, p);
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Point p = e.GetPosition(editor.pianoroll.canvas);
            if (editor.pianoroll.mode == PianorollCtrl.Mode.SetNoteLength) editor.pianoroll.UpdateSetNoteLength(p);
            else if (editor.pianoroll.mode == PianorollCtrl.Mode.MoveSelectedNotes) editor.pianoroll.UpdateMoveSelectedNotes(p);
            else
            {
                double height = Note.Length * editor.PatDim.BeatHeight / PianorollGlobal.TicksPerBeat;
                Mouse.OverrideCursor = p.Y >= height - 3 ? Cursors.SizeNS : Cursors.Hand;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (editor.pianoroll.mode == PianorollCtrl.Mode.SetNoteLength) editor.pianoroll.EndSetNoteLength();
            else if (editor.pianoroll.mode == PianorollCtrl.Mode.MoveSelectedNotes) editor.pianoroll.EndMoveSelectedNotes();
            editor.pianoroll.NoteClicked(this, false);
            base.OnMouseLeftButtonUp(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e) { if (editor.pianoroll.mode == PianorollCtrl.Mode.Normal) Mouse.OverrideCursor = null; base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Right)
            {
                editor.pianoroll.DeleteNotes(Selected ? editor.pianoroll.selectedNotes : new NoteSet(Note));
                e.Handled = true;
            }
            base.OnMouseDown(e);
        }
    }
}
