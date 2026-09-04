namespace jedjoud.VoxelTerrain.Generation {
    public class KeywordGuards {
        public string[] keywords;
        
        public KeywordGuards(params string[] keywords) {
            this.keywords = keywords;
        }

        public string BeginGuard() {
            if (keywords.Length == 0) {
                throw new System.Exception("erm... what the sigma?");
            }

            string line = "#if ";

            for (int i = 0; i < keywords.Length; i++) {
                line += $"defined({keywords[i]})";

                if (i != keywords.Length - 1) {
                    line += " || ";
                }
            }

            return line;
        }

        public string EndGuard() {
            return $"#endif";
        }
    }
}