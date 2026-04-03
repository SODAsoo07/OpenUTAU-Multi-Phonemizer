using System.Text.Json;

namespace MasterRouterTagger;

public sealed class TaggerForm : Form {
    private readonly ComboBox tagComboBox = new();
    private readonly CheckBox overwriteCheckBox = new();
    private readonly Button okButton = new();
    private readonly Button cancelButton = new();
    private readonly Dictionary<string, string> presetByDisplay = new(StringComparer.OrdinalIgnoreCase);

    public string SelectedTag { get; private set; } = string.Empty;
    public bool OverwriteExistingTag => overwriteCheckBox.Checked;

    public TaggerForm() {
        Text = "Master Router Tagger";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(560, 180);

        var titleLabel = new Label {
            Text = "선택한 노트의 가사 앞에 :태그:를 추가합니다.",
            AutoSize = true,
            Location = new Point(16, 16),
        };
        Controls.Add(titleLabel);

        var tagLabel = new Label {
            Text = "태그 / 프리셋:",
            AutoSize = true,
            Location = new Point(16, 52),
        };
        Controls.Add(tagLabel);

        tagComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        tagComboBox.Location = new Point(16, 72);
        tagComboBox.Size = new Size(528, 28);
        Controls.Add(tagComboBox);

        overwriteCheckBox.Text = "기존 라우팅 태그가 있어도 덮어쓰기";
        overwriteCheckBox.AutoSize = true;
        overwriteCheckBox.Location = new Point(16, 108);
        overwriteCheckBox.Checked = false;
        Controls.Add(overwriteCheckBox);

        okButton.Text = "적용";
        okButton.Location = new Point(368, 136);
        okButton.Size = new Size(84, 30);
        okButton.Click += (_, _) => OnSubmit();
        Controls.Add(okButton);

        cancelButton.Text = "취소";
        cancelButton.Location = new Point(460, 136);
        cancelButton.Size = new Size(84, 30);
        cancelButton.DialogResult = DialogResult.Cancel;
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        LoadPresets();
    }

    private void OnSubmit() {
        var input = tagComboBox.Text.Trim();
        if (presetByDisplay.TryGetValue(input, out var mapped)) {
            input = mapped;
        }
        input = NormalizeTagInput(input);
        if (string.IsNullOrWhiteSpace(input)) {
            MessageBox.Show("태그를 입력하거나 프리셋에서 선택해 주세요.", "Master Router Tagger", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        SelectedTag = input;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void LoadPresets() {
        var displays = new List<string>();
        foreach (var (name, tag) in BuiltinPresets()) {
            var display = $"{name} ({tag})";
            if (!presetByDisplay.ContainsKey(display)) {
                presetByDisplay[display] = tag;
                displays.Add(display);
            }
        }

        var config = LoadRouterConfig();
        if (!string.IsNullOrWhiteSpace(config.QuickTag)) {
            tagComboBox.Text = config.QuickTag!;
        } else if (!string.IsNullOrWhiteSpace(config.Primary)) {
            tagComboBox.Text = config.Primary!;
        }
        foreach (var alias in config.Aliases.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)) {
            var display = $"{alias.Key} (alias -> {alias.Value})";
            if (!presetByDisplay.ContainsKey(display)) {
                presetByDisplay[display] = alias.Key;
                displays.Add(display);
            }
        }
        tagComboBox.Items.AddRange(displays.Cast<object>().ToArray());
    }

    private static string NormalizeTagInput(string input) {
        input = input.Trim();
        while (input.StartsWith(':')) {
            input = input[1..];
        }
        while (input.EndsWith(':')) {
            input = input[..^1];
        }
        return input.Trim();
    }

    private static IEnumerable<(string Name, string Tag)> BuiltinPresets() {
        return new[] {
            ("Arpasing+ Phonemizer", "EN ARPA+"),
            ("Brazilian Portuguese CVC Phonemizer", "PT-BR CVC"),
            ("Cantonese CVVC Phonemizer", "ZH-YUE CVVC"),
            ("Cantonese Syo-Style Phonemizer", "ZH-YUE SYO"),
            ("Chinese CVV (蜊∵怦蠑乗紛髻ｳ謇ｩ蠑) Phonemizer", "ZH CVV"),
            ("Chinese CVV Plus Phonemizer", "ZH CVV+"),
            ("Chinese CVVC Phonemizer", "ZH CVVC"),
            ("Default Phonemizer", "DEFAULT"),
            ("DiffSinger Chinese Phonemizer", "DIFFS ZH"),
            ("DiffSinger English Phonemizer", "DIFFS EN"),
            ("DiffSinger English+ Phonemizer", "DIFFS EN+"),
            ("DiffSinger Filipino Phonemizer", "DIFFS FIL"),
            ("DiffSinger French Millefeuille Phonemizer", "DIFFS FR MILLE"),
            ("DiffSinger German Marzipan Phonemizer", "DIFFS DE MARZ"),
            ("DiffSinger German Phonemizer", "DIFFS DE"),
            ("DiffSinger Italian Phonemizer", "DIFFS IT"),
            ("DiffSinger Japanese Phonemizer", "DIFFS JA"),
            ("DiffSinger Jyutping Phonemizer", "DIFFS ZH-YUE"),
            ("DiffSinger Korean G2P Phonemizer", "DIFFS KO"),
            ("DiffSinger Korean Phonemizer", "DIFFS KO"),
            ("DiffSinger Phonemizer", "DIFFS"),
            ("DiffSinger Portuguese Phonemizer", "DIFFS PT"),
            ("DiffSinger Rhythmizer Phonemizer", "DIFFS RHY"),
            ("DiffSinger Russian Phonemizer", "DIFFS RU"),
            ("DiffSinger Spanish Phonemizer", "DIFFS ES"),
            ("English Arpasing Phonemizer", "EN ARPA"),
            ("English C+V Phonemizer", "EN C+V"),
            ("English to Japanese Phonemizer", "EN to JA"),
            ("English VCCV Phonemizer", "EN VCCV"),
            ("English X-SAMPA phonemizer", "EN X-SAMPA"),
            ("Enunu English Phonemizer", "ENUNU EN"),
            ("Enunu Korean Phonemizer", "ENUNU KO"),
            ("Enunu Onnx English Phonemizer", "ENUNU X EN"),
            ("Enunu Onnx Phonemizer", "ENUNU X"),
            ("Enunu Phonemizer", "ENUNU"),
            ("Filipino Phonemizer", "FIL VCV & CVVC"),
            ("Filipino to Japanese Phonemizer", "FIL to JA"),
            ("French CMUSphinx Phonemizer", "FR SPHINX"),
            ("French CVVC Phonemizer", "FR CVVC"),
            ("French VCCV m2RUg Phonemizer", "FR VCCV"),
            ("German Diphone Phonemizer", "DE DIPHONE"),
            ("German VCCV Phonemizer", "DE VCCV"),
            ("Italian CVVC Phonemizer", "IT CVVC"),
            ("Italian Syllable-Based Phonemizer", "IT SYL"),
            ("Japanese CVVC Phonemizer (legacy)", "JA CVVC"),
            ("Japanese presamp Phonemizer", "JA VCV & CVVC"),
            ("Japanese VCV Phonemizer (legacy)", "JA VCV"),
            ("KO to JA Phonemizer", "KO to JA"),
            ("Korean CBNN Phonemizer", "KO CBNN"),
            ("Korean CV Phonemizer", "KO CV"),
            ("Korean CVCCV Phonemizer", "KO CVCCV"),
            ("Korean CVVC Phonemizer", "KO CVVC"),
            ("Korean VCV Phonemizer", "KO VCV"),
            ("KoreanCVCPhonemizer", "KO CVC"),
            ("Polish CVC Phonemizer", "PL CVC"),
            ("Presamp Sample Phonemizer", "ZH CVVC"),
            ("Russian CVC Phonemizer", "RU CVC"),
            ("Russian VCCV Phonemizer", "RU VCCV"),
            ("Simple Voicevox ENtoJA Phonemizer", "S-VOICEVOX EN to JA"),
            ("Simple Voicevox Japanese Phonemizer", "S-VOICEVOX JA"),
            ("Spanish Makkusan Phonemizer", "ES MAKKU"),
            ("Spanish Syllable-Based Phonemizer", "ES SYL"),
            ("Spanish to Japanese Phonemizer", "ES to JA"),
            ("Spanish VCCV Phonemizer", "ES VCCV"),
            ("Thai VCCV Phonemizer", "TH VCCV"),
            ("Turkish CVVC Phonemizer", "TR CVVC"),
            ("Vietnamese CVVC Phonemizer", "VIE CVVC"),
            ("Vietnamese VCV Phonemizer", "VIE VCV"),
            ("Vietnamese VINA Phonemizer", "VIE VINA"),
            ("Vogen Chinese Mandarin Phonemizer", "VOGEN ZH"),
            ("Vogen Chinese Yue Phonemizer", "VOGEN ZH-YUE"),
            ("Voicevox Japanese Phonemizer", "VOICEVOX JA"),
        };
    }

    private static RouterConfig LoadRouterConfig() {
        var paths = CandidateConfigPaths().Where(File.Exists).ToArray();
        foreach (var path in paths) {
            try {
                var json = File.ReadAllText(path);
                var model = JsonSerializer.Deserialize<RouterConfigModel>(json, new JsonSerializerOptions {
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                });
                var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (model?.Aliases != null) {
                    foreach (var pair in model.Aliases) {
                        var key = pair.Key?.Trim();
                        var value = pair.Value?.Trim();
                        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value)) {
                            aliases[key] = value;
                        }
                    }
                }
                return new RouterConfig(model?.Primary?.Trim(), model?.QuickTag?.Trim(), aliases);
            } catch {
                // Ignore broken config and continue.
            }
        }
        return new RouterConfig(null, null, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> CandidateConfigPaths() {
        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "master-router.config.json");
        var parent = Directory.GetParent(baseDir)?.FullName;
        if (!string.IsNullOrWhiteSpace(parent)) {
            yield return Path.Combine(parent, "master-router.config.json");
        }
    }

    private sealed record RouterConfig(string? Primary, string? QuickTag, Dictionary<string, string> Aliases);

    private sealed class RouterConfigModel {
        public string? Primary { get; set; }
        public string? QuickTag { get; set; }
        public Dictionary<string, string>? Aliases { get; set; }
    }
}

