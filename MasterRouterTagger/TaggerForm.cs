using System.Text.Json;

namespace MasterRouterTagger;

public sealed class TaggerForm : Form {
    private readonly ComboBox modeComboBox = new();
    private readonly ComboBox tagComboBox = new();
    private readonly CheckBox overwriteCheckBox = new();
    private readonly Button okButton = new();
    private readonly Button cancelButton = new();
    private readonly Dictionary<string, string> presetByDisplay = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> categoryHeaders = new(StringComparer.Ordinal);
    private readonly HashSet<int> categoryHeaderIndices = new();
    private int lastValidPresetIndex = -1;

    public string SelectedTag { get; private set; } = string.Empty;
    public bool OverwriteExistingTag => overwriteCheckBox.Checked;
    public TaggingMode SelectedMode { get; private set; } = TaggingMode.AddRouteTag;

    public TaggerForm() {
        Text = "Unofficial Master Router Tagger";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(640, 240);

        var titleLabel = new Label {
            Text = "비공식(서드파티) 플러그인: 선택 노트에 :태그:를 추가/제거합니다.",
            AutoSize = true,
            Location = new Point(16, 16),
        };
        Controls.Add(titleLabel);

        var modeLabel = new Label {
            Text = "동작:",
            AutoSize = true,
            Location = new Point(16, 52),
        };
        Controls.Add(modeLabel);

        modeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        modeComboBox.Location = new Point(16, 72);
        modeComboBox.Size = new Size(256, 28);
        modeComboBox.Items.Add("태그 추가");
        modeComboBox.Items.Add("태그 제거");
        modeComboBox.SelectedIndex = 0;
        modeComboBox.SelectedIndexChanged += (_, _) => RefreshModeState();
        Controls.Add(modeComboBox);

        var tagLabel = new Label {
            Text = "태그 / 프리셋:",
            AutoSize = true,
            Location = new Point(16, 108),
        };
        Controls.Add(tagLabel);

        tagComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        tagComboBox.DrawMode = DrawMode.OwnerDrawFixed;
        tagComboBox.Location = new Point(16, 128);
        tagComboBox.Size = new Size(608, 28);
        tagComboBox.DrawItem += OnTagComboDrawItem;
        tagComboBox.SelectionChangeCommitted += OnTagComboSelectionCommitted;
        Controls.Add(tagComboBox);

        overwriteCheckBox.Text = "기존 라우팅 태그가 있어도 덮어쓰기";
        overwriteCheckBox.AutoSize = true;
        overwriteCheckBox.Location = new Point(16, 164);
        overwriteCheckBox.Checked = false;
        Controls.Add(overwriteCheckBox);

        okButton.Text = "적용";
        okButton.Location = new Point(448, 200);
        okButton.Size = new Size(84, 30);
        okButton.Click += (_, _) => OnSubmit();
        Controls.Add(okButton);

        cancelButton.Text = "취소";
        cancelButton.Location = new Point(540, 200);
        cancelButton.Size = new Size(84, 30);
        cancelButton.DialogResult = DialogResult.Cancel;
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        LoadPresets();
        RefreshModeState();
    }

    private void RefreshModeState() {
        SelectedMode = modeComboBox.SelectedIndex == 1
            ? TaggingMode.RemoveRouteTag
            : TaggingMode.AddRouteTag;
        var addMode = SelectedMode == TaggingMode.AddRouteTag;
        tagComboBox.Enabled = addMode;
        overwriteCheckBox.Enabled = addMode;
    }

    private void OnSubmit() {
        if (SelectedMode == TaggingMode.RemoveRouteTag) {
            SelectedTag = string.Empty;
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        var input = tagComboBox.Text.Trim();
        if (categoryHeaders.Contains(input)) {
            MessageBox.Show("카테고리 항목이 아니라 실제 프리셋/태그를 선택해 주세요.", "Unofficial Master Router Tagger", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (presetByDisplay.TryGetValue(input, out var mapped)) {
            input = mapped;
        }
        input = NormalizeTagInput(input);
        if (string.IsNullOrWhiteSpace(input)) {
            MessageBox.Show("태그를 입력하거나 프리셋에서 선택해 주세요.", "Unofficial Master Router Tagger", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        SelectedTag = input;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void OnTagComboSelectionCommitted(object? sender, EventArgs e) {
        if (tagComboBox.SelectedIndex < 0) {
            return;
        }
        if (categoryHeaderIndices.Contains(tagComboBox.SelectedIndex)) {
            System.Media.SystemSounds.Beep.Play();
            if (lastValidPresetIndex >= 0 && lastValidPresetIndex < tagComboBox.Items.Count) {
                tagComboBox.SelectedIndex = lastValidPresetIndex;
            } else {
                tagComboBox.SelectedIndex = -1;
                tagComboBox.Text = string.Empty;
            }
            return;
        }
        lastValidPresetIndex = tagComboBox.SelectedIndex;
    }

    private void OnTagComboDrawItem(object? sender, DrawItemEventArgs e) {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= tagComboBox.Items.Count) {
            return;
        }
        var text = tagComboBox.Items[e.Index]?.ToString() ?? string.Empty;
        var isHeader = categoryHeaderIndices.Contains(e.Index);
        var baseFont = e.Font ?? tagComboBox.Font;
        if (baseFont == null) {
            return;
        }
        var createdFont = false;
        var font = baseFont;
        if (isHeader) {
            font = new Font(baseFont, FontStyle.Bold);
            createdFont = true;
        }
        var color = isHeader ? Color.DimGray : e.ForeColor;
        TextRenderer.DrawText(
            e.Graphics,
            text,
            font,
            e.Bounds,
            color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (createdFont) {
            font.Dispose();
        }
        e.DrawFocusRectangle();
    }

    private void LoadPresets() {
        var grouped = BuiltinPresets()
            .GroupBy(x => x.Category)
            .OrderBy(g => CategoryOrder(g.Key))
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var displays = new List<string>();
        foreach (var group in grouped) {
            var header = $"=== {group.Key} ===";
            categoryHeaders.Add(header);
            categoryHeaderIndices.Add(displays.Count);
            displays.Add(header);

            foreach (var preset in group.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)) {
                var display = $"{preset.Name} ({preset.Tag})";
                if (!presetByDisplay.ContainsKey(display)) {
                    presetByDisplay[display] = preset.Tag;
                    displays.Add(display);
                }
            }
        }

        var config = LoadRouterConfig();
        if (!string.IsNullOrWhiteSpace(config.QuickTag)) {
            tagComboBox.Text = config.QuickTag!;
        } else if (!string.IsNullOrWhiteSpace(config.Primary)) {
            tagComboBox.Text = config.Primary!;
        }

        var aliasHeader = "=== Aliases ===";
        displays.Add(aliasHeader);
        categoryHeaders.Add(aliasHeader);
        categoryHeaderIndices.Add(displays.Count - 1);
        foreach (var alias in config.Aliases.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)) {
            var display = $"{alias.Key} (alias -> {alias.Value})";
            if (!presetByDisplay.ContainsKey(display)) {
                presetByDisplay[display] = alias.Key;
                displays.Add(display);
            }
        }

        tagComboBox.Items.AddRange(displays.Cast<object>().ToArray());
        if (tagComboBox.SelectedIndex >= 0 && !categoryHeaderIndices.Contains(tagComboBox.SelectedIndex)) {
            lastValidPresetIndex = tagComboBox.SelectedIndex;
        }
    }

    private static int CategoryOrder(string category) {
        return category switch {
            "General" => 0,
            "Japanese (JA)" => 1,
            "Korean (KO)" => 2,
            "English (EN)" => 3,
            "Chinese (ZH)" => 4,
            "Cantonese (ZH-YUE)" => 5,
            "Vietnamese (VIE)" => 6,
            "Spanish (ES)" => 7,
            "French (FR)" => 8,
            "German (DE)" => 9,
            "Italian (IT)" => 10,
            "Portuguese (PT)" => 11,
            "Russian (RU)" => 12,
            "Polish (PL)" => 13,
            "Thai (TH)" => 14,
            "Turkish (TR)" => 15,
            "Filipino (FIL)" => 16,
            _ => 100,
        };
    }

    private static string Categorize(string tag) {
        var t = (tag ?? string.Empty).ToUpperInvariant();
        if (t.Contains("DEFAULT")) return "General";
        if (t.Contains("ZH-YUE")) return "Cantonese (ZH-YUE)";
        if (t.Contains("JA")) return "Japanese (JA)";
        if (t.Contains("KO")) return "Korean (KO)";
        if (t.Contains("EN")) return "English (EN)";
        if (t.Contains("ZH")) return "Chinese (ZH)";
        if (t.Contains("VIE")) return "Vietnamese (VIE)";
        if (t.Contains("ES")) return "Spanish (ES)";
        if (t.Contains("FR")) return "French (FR)";
        if (t.Contains("DE")) return "German (DE)";
        if (t.Contains("IT")) return "Italian (IT)";
        if (t.Contains("PT")) return "Portuguese (PT)";
        if (t.Contains("RU")) return "Russian (RU)";
        if (t.Contains("PL")) return "Polish (PL)";
        if (t.Contains("TH")) return "Thai (TH)";
        if (t.Contains("TR")) return "Turkish (TR)";
        if (t.Contains("FIL")) return "Filipino (FIL)";
        return "Other";
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

    private static IEnumerable<(string Name, string Tag, string Category)> BuiltinPresets() {
        var raw = new[] {
            ("Arpasing+ Phonemizer", "EN ARPA+"),
            ("Brazilian Portuguese CVC Phonemizer", "PT-BR CVC"),
            ("Cantonese CVVC Phonemizer", "ZH-YUE CVVC"),
            ("Cantonese Syo-Style Phonemizer", "ZH-YUE SYO"),
            ("Chinese CVV (Legacy) Phonemizer", "ZH CVV"),
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
        return raw.Select(p => (p.Item1, p.Item2, Categorize(p.Item2)));
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
