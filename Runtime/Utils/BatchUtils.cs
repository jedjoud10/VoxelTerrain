namespace jedjoud.VoxelTerrain {
    public static class BatchUtils {
        public const int BATCH = VoxelUtils.VOLUME;
        public const int SMALLER_BATCH = VoxelUtils.VOLUME / 2;
        public const int EVEN_SMALLER_BATCH = VoxelUtils.VOLUME / 4;
        public const int SMALLEST_BATCH = VoxelUtils.VOLUME / 8;
        public const int SKIRT_BATCH = VoxelUtils.SKIRT_FACE * 6;
        public const int SMALLER_SKIRT_BATCH = VoxelUtils.SKIRT_FACE / 2;
        public const int PER_SKIRT_FACE_BATCH = VoxelUtils.SKIRT_FACE / 6;
        public const int PER_SKIRT_FACE_SMALLER_BATCH = VoxelUtils.SKIRT_FACE / 12;

        public const int VERTEX_BATCH = 4096;
        public const int SMALLEST_VERTEX_BATCH = 32;
    }
}