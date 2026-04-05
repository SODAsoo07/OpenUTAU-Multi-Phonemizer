using System;
using System.Linq;
using OpenUtau.Core;
using OpenUtau.Core.Editing;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;

namespace MasterRouter {
    public class ApplyMasterRouterTagPrefix : BatchEdit {
        public string Name => "Unofficial Master Router: Apply :tag: to selected notes";

        public void Run(UProject project, UVoicePart part, System.Collections.Generic.List<UNote> selectedNotes, DocManager docManager) {
            if (selectedNotes.Count == 0) {
                return;
            }
            var config = MasterRouterConfig.Load(PathManager.Inst.PluginsPath);
            var tag = config.GetQuickTagOrDefault();
            if (string.IsNullOrWhiteSpace(tag)) {
                tag = "ja";
            }

            var notes = selectedNotes.ToArray();
            var lyrics = notes.Select(note => ApplyTagPrefix(note.lyric ?? string.Empty, tag)).ToArray();

            docManager.StartUndoGroup("command.batch.lyric", true);
            docManager.ExecuteCmd(new ChangeNoteLyricCommand(part, notes, lyrics));
            docManager.EndUndoGroup();
        }

        private static string ApplyTagPrefix(string lyric, string tag) {
            var body = RouteTagParser.StripIfTagged(lyric);
            return $":{tag}:{body}";
        }
    }
}

