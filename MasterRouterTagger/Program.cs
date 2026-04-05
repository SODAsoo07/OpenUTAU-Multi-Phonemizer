namespace MasterRouterTagger;

static class Program {
    [STAThread]
    static void Main(string[] args) {
        ApplicationConfiguration.Initialize();
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0])) {
            MessageBox.Show(
                "OpenUtau에서 실행해 주세요.",
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
            var result = TaggingProcessor.Apply(
                tempFilePath,
                form.SelectedTag,
                form.OverwriteExistingTag,
                form.SelectedMode);
            switch (result.Status) {
                case ApplyStatus.NoSelectedRange:
                    MessageBox.Show(
                        "선택된 노트가 없습니다. 태그를 적용할 노트를 먼저 선택하세요.\n(전체 트랙 범위는 안전상 자동 적용하지 않습니다.)",
                        "Unofficial Master Router Tagger",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                case ApplyStatus.NoNotes:
                    MessageBox.Show(
                        "처리할 노트를 찾지 못했습니다.",
                        "Unofficial Master Router Tagger",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                default:
                    MessageBox.Show(
                        result.Changed > 0
                            ? $"적용 완료: {result.Changed}개 노트"
                            : "변경된 노트가 없습니다.",
                        "Unofficial Master Router Tagger",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
            }
        } catch (Exception ex) {
            MessageBox.Show(
                $"적용 중 오류가 발생했습니다.\n{ex.Message}",
                "Unofficial Master Router Tagger",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
