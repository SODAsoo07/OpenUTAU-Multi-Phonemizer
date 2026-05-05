namespace MasterRouterTagger;

static class Program {
    [STAThread]
    static void Main(string[] args) {
        ApplicationConfiguration.Initialize();
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0])) {
            MessageBox.Show(
                "Please run this plugin from OpenUtau.",
                "Unofficial Master Router Tagger",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var tempFilePath = args[0];
        using var form = new TaggerForm();
        if (form.ShowDialog() != DialogResult.OK) {
            return;
        }

        try {
            var lang = form.CurrentLanguage;
            var result = TaggingProcessor.Apply(
                tempFilePath,
                form.SelectedTag,
                form.OverwriteExistingTag,
                form.SelectedMode);
            switch (result.Status) {
                case ApplyStatus.NoSelectedRange:
                    MessageBox.Show(
                        lang == UiLanguage.Korean
                            ? "선택된 노트가 없습니다. 태그를 적용할 노트를 먼저 선택하세요.\n(전체 트랙 범위는 안전상 자동 적용하지 않습니다.)"
                            : "No notes selected. Select notes first.\n(Whole-track range is blocked for safety.)",
                        "Unofficial Master Router Tagger",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                case ApplyStatus.NoNotes:
                    MessageBox.Show(
                        lang == UiLanguage.Korean
                            ? "처리할 노트를 찾지 못했습니다."
                            : "No notes found to process.",
                        "Unofficial Master Router Tagger",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                default:
                    MessageBox.Show(
                        lang == UiLanguage.Korean
                            ? (result.Changed > 0
                                ? $"적용 완료: {result.Changed}개 노트"
                                : "변경된 노트가 없습니다.")
                            : (result.Changed > 0
                                ? $"Done: {result.Changed} note(s) updated."
                                : "No notes were changed."),
                        "Unofficial Master Router Tagger",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
            }
        } catch (Exception ex) {
            MessageBox.Show(
                $"Error while applying.\n{ex.Message}",
                "Unofficial Master Router Tagger",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
