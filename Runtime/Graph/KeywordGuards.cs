using NUnit.Framework;

namespace jedjoud.VoxelTerrain.Generation {
    public class KeywordGuards {
        public string keyword;
        public bool invert;

        public KeywordGuards(string keyword, bool invert) {
            this.keyword = keyword;
            this.invert = invert;
        }

        public string BeginGuard() {
            string begin = invert ? "ifndef" : "ifdef";
            return $"#{begin} {keyword}";
        }

        public string EndGuard() {
            return $"#endif";
        }
    }
}