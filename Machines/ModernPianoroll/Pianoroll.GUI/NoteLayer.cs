using System.Windows;
using System.Windows.Media;

namespace Pianoroll.GUI
{
    internal class NoteLayer : FrameworkElement
    {
        List<Visual> children = new List<Visual>();
        Editor ped;
        NotePanel insertNotePanel;
        const int BeatsPerPanel = 2;
        public NotePanel InsertNotePanel { get { return insertNotePanel; } }
        public IEnumerable<NotePanel> Panels { get { return children.Cast<NotePanel>(); } }

        public NoteLayer(Editor ped)
        {
            this.ped = ped;
            insertNotePanel = new NotePanel(ped, 0);
            AddVisualChild(insertNotePanel);
            Recreate();
        }

        public void Recreate()
        {
            foreach (Visual child in children) RemoveVisualChild(child);
            children.Clear();
            int count = (int)Math.Ceiling((double)ped.PatDim.BeatCount / BeatsPerPanel);
            for (int i = 0; i < count; i++) children.Add(new NotePanel(ped, i * BeatsPerPanel));
            if (ped.pattern != null)
                for (int track = 0; track < ped.pattern.ReBuzzPattern.Machine.TrackCount; track++)
                    foreach (var note in ped.pattern.GetAllNotes(track)) AddNote(note);
            foreach (Visual child in children) AddVisualChild(child);
        }

        NotePanel GetNotePanel(NoteEvent note)
        {
            int index = note.Time / (BeatsPerPanel * PianorollGlobal.TicksPerBeat);
            return index < 0 || index >= children.Count ? null : (NotePanel)children[index];
        }

        public void AddNote(NoteEvent note) { var panel = GetNotePanel(note); if (panel != null) { panel.AddNote(note, false); panel.InvalidateMeasure(); } }
        public void SelectNotes(Int32Rect rect, NoteSet notes, bool add) { foreach (NotePanel panel in children) panel.SelectNotes(rect, notes, add); }
        public void SelectNotes(int time, NoteSet notes, bool add) { foreach (NotePanel panel in children) panel.SelectNotes(time, notes, add); }
        public void SelectNote(NoteEvent note, NoteSet notes, bool add) { foreach (NotePanel panel in children) panel.SelectNote(note, notes, add); }
        public void DeleteNotes(IEnumerable<NoteEvent> notes) { foreach (var note in notes.ToArray()) GetNotePanel(note)?.DeleteNote(note); }
        public void SetVelocity(IEnumerable<NoteEvent> notes, int velocity) { foreach (var note in notes) GetNotePanel(note)?.SetVelocity(note, velocity); }
        protected override int VisualChildrenCount { get { return children.Count + 1; } }
        protected override Visual GetVisualChild(int index) { if (index == children.Count) return insertNotePanel; if (index < 0 || index >= children.Count) throw new ArgumentOutOfRangeException(); return children[index]; }
        protected override Size MeasureOverride(Size availableSize) { Size size = new Size(double.PositiveInfinity, double.PositiveInfinity); foreach (UIElement child in children) child.Measure(size); insertNotePanel.Measure(size); return new Size(); }
        protected override Size ArrangeOverride(Size finalSize) { for (int i = 0; i < children.Count; i++) { var panel = (NotePanel)children[i]; panel.Arrange(new Rect(0, i * BeatsPerPanel * ped.PatDim.BeatHeight, panel.DesiredSize.Width, panel.DesiredSize.Height)); } insertNotePanel.Arrange(new Rect(new Point(0, 0), insertNotePanel.DesiredSize)); return finalSize; }
    }
}
