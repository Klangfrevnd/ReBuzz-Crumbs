using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using WDE.ModernPatternEditor.MPEStructures;

namespace Pianoroll.GUI
{
    public class Pattern
    {
        private MPEPattern mpePattern;
        public IPattern ReBuzzPattern { get; }
        int length;

        public delegate void LengthChanged(Pattern p);
        public event LengthChanged LengthChangedEvent;

        internal int Length
        {
            get { return length; }
            set
            {
                length = value;
                ReBuzzPattern.Length = length * 4;
                LengthChangedEvent?.Invoke(this);
            }
        }

        public int LengthInTicks { get { return length * PianorollGlobal.TicksPerBeat; } }

        public int LengthInBuzzTicks
        {
            set
            {
                length = LengthFromNumBuzzTicks(value);
                LengthChangedEvent?.Invoke(this);
            }
        }

        int LengthFromNumBuzzTicks(int n) { return n; }

        public Pattern(MPEPattern mpep, IPattern pattern, int numbuzzticks)
        {
            mpePattern = mpep;
            ReBuzzPattern = pattern;
            length = LengthFromNumBuzzTicks(numbuzzticks);
        }

        public void Update() { LengthChangedEvent?.Invoke(this); }

        MPEPatternColumn GetNoteColumn(int track)
        {
            return mpePattern?.MPEPatternColumns.FirstOrDefault(c => c.Parameter.Type == ParameterType.Note && c.ParamTrack == track);
        }

        public void AddNote(int track, NoteEvent note)
        {
            var column = GetNoteColumn(track);
            if (column == null) return;
            var start = new PatternEvent { Time = GetReBuzzTime(note.Time), Duration = GetReBuzzTime(note.Length), Value = BuzzNote.FromMIDINote(note.Note) };
            var stop = new PatternEvent { Time = GetReBuzzTime(note.Time + note.Length), Duration = 0, Value = BuzzNote.Off };
            column.SetEvents(new[] { start, stop }, true, false);
        }

        public void BeginAction(string name) { }

        public void DeleteNotes()
        {
            if (mpePattern?.Pattern?.Machine == null) return;
            foreach (var column in mpePattern.MPEPatternColumns.Where(c => c.Parameter.Type == ParameterType.Note))
            {
                var events = column.GetEvents(0, int.MaxValue).ToArray();
                if (events.Length > 0) column.SetEvents(events, false, false);
            }
        }

        public void DeleteNotes(IEnumerable<NoteEvent> notes)
        {
            if (mpePattern?.Pattern?.Machine == null || notes == null) return;
            foreach (var column in mpePattern.MPEPatternColumns.Where(c => c.Parameter.Type == ParameterType.Note))
                foreach (var note in notes.ToArray()) RemoveNote(column, note);
        }

        public void DeleteNotes(int track, IEnumerable<NoteEvent> notes)
        {
            var column = GetNoteColumn(track);
            if (column == null || notes == null) return;
            foreach (var note in notes.ToArray()) RemoveNote(column, note);
        }

        void RemoveNote(MPEPatternColumn column, NoteEvent note)
        {
            int startTime = GetReBuzzTime(note.Time);
            int value = BuzzNote.FromMIDINote(note.Note);
            var events = column.GetEvents(0, int.MaxValue).OrderBy(e => e.Time).ToArray();
            int index = Array.FindIndex(events, e => e.Time == startTime && e.Value == value);
            if (index < 0) return;
            var remove = new List<PatternEvent> { events[index] };
            if (index + 1 < events.Length && events[index + 1].Value == BuzzNote.Off) remove.Add(events[index + 1]);
            column.SetEvents(remove.ToArray(), false, false);
        }

        public NoteEvent GetAdjacentNote(int track, NoteEvent note, bool next)
        {
            var notes = GetAllNotes(track).OrderBy(n => n.Time).ThenBy(n => n.Note).ToList();
            if (notes.Count == 0) return NoteEvent.Invalid;
            if (note.IsInvalid) return next ? notes[0] : notes[notes.Count - 1];
            if (next) return notes.FirstOrDefault(n => n.Time > note.Time || (n.Time == note.Time && n.Note > note.Note), NoteEvent.Invalid);
            return notes.LastOrDefault(n => n.Time < note.Time || (n.Time == note.Time && n.Note < note.Note), NoteEvent.Invalid);
        }

        public List<NoteEvent> GetAllNotes(int track)
        {
            var result = new List<NoteEvent>();
            var column = GetNoteColumn(track);
            if (column == null) return result;
            var events = column.GetEvents(0, int.MaxValue).OrderBy(e => e.Time).ToArray();
            for (int i = 0; i < events.Length; i++)
            {
                var current = events[i];
                if (current.Value == BuzzNote.Off) continue;
                int end = i + 1 < events.Length && events[i + 1].Value == BuzzNote.Off
                    ? events[i + 1].Time
                    : mpePattern.Pattern.Length * PatternEvent.TimeBase;
                result.Add(new NoteEvent(GetPianorollTime(current.Time), GetPianorollTime(Math.Max(0, end - current.Time)), BuzzNote.ToMIDINote(current.Value), 127));
            }
            return result;
        }

        public void MoveNotes(int track, IEnumerable<NoteEvent> notes, int dx, int dy)
        {
            if (notes == null) return;
            foreach (var original in notes.ToArray())
            {
                var updated = original;
                updated.Note = Math.Max(0, Math.Min(119, updated.Note + dx));
                updated.Time = Math.Max(0, updated.Time + dy);
                ReplaceNote(original, updated);
            }
        }

        void ReplaceNote(NoteEvent original, NoteEvent updated)
        {
            foreach (var column in mpePattern.MPEPatternColumns.Where(c => c.Parameter.Type == ParameterType.Note))
            {
                var events = column.GetEvents(0, int.MaxValue).OrderBy(e => e.Time).ToArray();
                int index = Array.FindIndex(events, e => e.Time == GetReBuzzTime(original.Time) && e.Value == BuzzNote.FromMIDINote(original.Note));
                if (index < 0) continue;
                var remove = new List<PatternEvent> { events[index] };
                if (index + 1 < events.Length && events[index + 1].Value == BuzzNote.Off) remove.Add(events[index + 1]);
                column.SetEvents(remove.ToArray(), false, false);
                var start = new PatternEvent { Time = GetReBuzzTime(updated.Time), Duration = GetReBuzzTime(updated.Length), Value = BuzzNote.FromMIDINote(updated.Note) };
                var stop = new PatternEvent { Time = GetReBuzzTime(updated.Time + updated.Length), Duration = 0, Value = BuzzNote.Off };
                column.SetEvents(new[] { start, stop }, true, false);
                return;
            }
        }

        public void SetLength(int track, IEnumerable<NoteEvent> notes, int newLength)
        {
            if (notes == null) return;
            foreach (var original in notes.ToArray())
            {
                var updated = original;
                updated.Length = Math.Max(1, newLength);
                ReplaceNote(original, updated);
            }
        }

        public void SetVelocity(int track, IEnumerable<NoteEvent> notes, int velocity) { }

        int GetReBuzzTime(int time) { return time * PatternEvent.TimeBase * mpePattern.RowsPerBeat / PianorollGlobal.TicksPerBeat / 4; }
        int GetPianorollTime(int time) { return 4 * time / PatternEvent.TimeBase * PianorollGlobal.TicksPerBeat / mpePattern.RowsPerBeat; }

        internal int GetAvailableTrack(NoteEvent note)
        {
            int time = GetReBuzzTime(note.Time);
            var machine = mpePattern.Pattern.Machine;
            for (int track = 0; track < machine.TrackCount; track++)
            {
                var column = GetNoteColumn(track);
                if (column == null) continue;
                var events = column.GetEvents(0, int.MaxValue).OrderBy(e => e.Time).ToArray();
                bool available = true;
                foreach (var e in events)
                {
                    if (e.Time > time) break;
                    available = e.Value == BuzzNote.Off;
                }
                if (available) return track;
            }
            return Math.Max(0, machine.TrackCount - 1);
        }
    }
}
