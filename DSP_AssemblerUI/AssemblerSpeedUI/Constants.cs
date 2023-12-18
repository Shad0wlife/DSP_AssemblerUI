namespace DSP_AssemblerUI.AssemblerSpeedUI
{
    internal static class Constants
    {
        public static readonly string BaseWindowsPath = "UI Root/Overlay Canvas/In Game/Windows";
        public static readonly string AssemblerWindowPath = $"{BaseWindowsPath}/Assembler Window";
        public static readonly string AssemblerWindowSpeedTextPath = $"{AssemblerWindowPath}/produce/speed/speed-text";
        public static readonly string MinerWindowPath = $"{BaseWindowsPath}/Miner Window";
        public static readonly string MinerWindowSpeedTextPath = $"{MinerWindowPath}/produce/speed/speed-text";

        public const uint MIN_DECIMAL_PLACES = 0;
        public const uint MAX_DECIMAL_PLACES = 3;


        public const float INPUT_YPOS = 25f;
        public const float OUTPUT_YPOS = -50f;

        public const float INPUT_FIRST_X_OFFSET = 130f; //Text width/2 is deducted from this so that text is justified right
        public const float OUTPUT_FIRST_X_OFFSET = 1f;

        public const float INPUT_X_ITEMSIZE = 50f;
        public const float OUTPUT_X_ITEMSIZE = 64f;

        public const float INPUT_MAXWIDTH = 48f;
        public const float OUTPUT_MAXWIDTH = 62f;

        public const int MAX_FONTSIZE = 12;
    }
}