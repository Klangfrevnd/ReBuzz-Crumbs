using System.Windows;
using System.Windows.Media;

namespace Pianoroll.GUI
{
    internal class NotePanel : FrameworkElement
    {
        List<NoteVisual> children;
        Editor ped;
        int firstBeat;
        public IEnumerable<NoteVisual> NoteVisuals { get { return children; } }
        public bool IsEmpty { get { return children.Count == 0; } }

        public NotePanel(Editor ped, int firstBeat) { this.ped = ped; this.firstBeat = firstBeat; children = new List<NoteVisual>(); }
        public NoteVisual GetNoteVisual(int key) { return children.FirstOrDefault(n => n.Note.Note == key); }
        public void RemoveNoteVisual(NoteVisual visual) { children.Remove(visual); RemoveVisualChild(visual); InvalidateMeasure(); }
        public void Clear() { foreach (var visual in children) RemoveVisualChild(visual); children.Clear(); InvalidateMeasure(); }
        public void AddNote(NoteEvent note, bool newnote) { var old = children.FirstOrDefault(n => n.Note.Time == note.Time && n.Note.Note == note.Note); if (old != null) { children.Remove(old); RemoveVisualChild(old); } var visual = new NoteVisual(note, ped, newnote); children.Add(visual); AddVisualChild(visual); InvalidateMeasure(); }
        public void UpdateNote(NoteEvent original, NoteEvent updated) { var visual = children.FirstOrDefault(n => n.Note.Equals(original)); if (visual != null) { visual.Note = updated; visual.InvalidateVisual(); InvalidateMeasure(); } }
        public void SelectNotes(Int32Rect rect, NoteSet notes, bool add) { foreach (var visual in children) { if (visual.Note.InRect(rect)) { if (!notes.Contains(visual.Note)) notes.Add(visual.Note); visual.Selected = true; } else if (!add) visual.Selected = false; } }
        public void SelectNotes(int time, NoteSet notes, bool add) { foreach (var visual in children) { if (visual.Note.Time == time) { if (!notes.Contains(visual.Note)) notes.Add(visual.Note); visual.Selected = true; } else if (!add) visual.Selected = false; } }
        public void SelectNote(NoteEvent note, NoteSet notes, bool add) { foreach (var visual in children) { if (visual.Note.Equals(note)) { if (!notes.Contains(visual.Note)) notes.Add(visual.Note); visual.Selected = true; } else if (!add) visual.Selected = false; } }
        public void DeleteNote(NoteEvent note) { var visual = children.FirstOrDefault(n => n.Note.Equals(note)); if (visual != null) { children.Remove(visual); RemoveVisualChild(visual); InvalidateMeasure(); } }
        public void SetVelocity(NoteEvent note, int velocity) { var visual = children.FirstOrDefault(n => n.Note.Equals(note)); if (visual != null) visual.Velocity = velocity; }
        protected override int VisualChildrenCount { get { return children.Count; } }
        protected override Visual GetVisualChild(int index) { if (index < 0 || index >= children.Count) throw new ArgumentOutOfRangeException(); return children[index]; }
        protected override Size MeasureOverride(Size availableSize) { Size size = new Size(double.PositiveInfinity, double.PositiveInfinity); foreach (UIElement child in children) child.Measure(size); return new Size(); }
        protected override Size ArrangeOverride(Size finalSize) { foreach (var visual in children) { double x = (visual.Note.Note - ped.PatDim.FirstMidiNote) * ped.PatDim.NoteWidth; double y = visual.Note.Time * ped.PatDim.BeatHeight / PianorollGlobal.TicksPerBeat - firstBeat * ped.PatDim.BeatHeight; visual.Arrange(new Rect(new Point(x, y), visual.DesiredSize)); } return finalSize; }
    }
}
