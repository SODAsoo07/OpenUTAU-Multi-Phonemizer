using System.Linq;
using OpenUtau.Core;
using OpenUtau.Core.Editing;
using OpenUtau.Core.Ustx;

namespace MasterRouterTagger;

public class MasterRouterTaggerBatchEdit : BatchEdit {
    public string Name => "Unofficial Master Router Tagger (GUI)";

    public void Run(UProject project, UVoicePart part, System.Collections.Generic.List<UNote> selectedNotes, DocManager docManager) {
        if (selectedNotes == null || selectedNotes.Count == 0) {
            MessageBox.Show(
                "선택된 노트가 없습니다. 먼저 노트를 선택해 주세요.\nNo notes selected. Please select notes first.",
                "Unofficial Master Router Tagger",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        using var form = new TaggerForm();
        if (form.ShowDialog() != DialogResult.OK) {
            return;
        }

        var notes = selectedNotes.ToArray();
        var originalLyrics = notes.Select(note => note.lyric ?? string.Empty).ToArray();
        var lyrics = originalLyrics
            .Select(lyric => TaggingProcessor.ApplyToLyric(lyric, form.SelectedTag, form.OverwriteExistingTag, form.SelectedMode))
            .ToArray();
        var changed = originalLyrics.Where((orig, i) => !string.Equals(orig, lyrics[i], StringComparison.Ordinal)).Count();

        if (changed == 0) {
            MessageBox.Show(
                form.CurrentLanguage == UiLanguage.Korean
                    ? "변경된 노트가 없습니다."
                    : "No notes were changed.",
                "Unofficial Master Router Tagger",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        docManager.StartUndoGroup("command.batch.lyric", true);
        docManager.ExecuteCmd(new ChangeNoteLyricCommand(part, notes, lyrics));
        docManager.EndUndoGroup();

        MessageBox.Show(
            form.CurrentLanguage == UiLanguage.Korean
                ? $"적용 완료: {changed}개 노트"
                : $"Done: {changed} note(s) updated.",
            "Unofficial Master Router Tagger",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
