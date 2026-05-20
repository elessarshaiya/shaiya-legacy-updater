namespace Shaiya_Invasion_Updater
{
    public static class GameOffsets
    {
        // EP5 original offsets (commented for easy rollback if needed):
        // public const int LoginPointer = 0x0071CF94;
        // public const int PatchLoginFlow1 = 0x004BE153;
        // public const int PatchLoginFlow2 = 0x004B98AD;
        // public const int PatchLoginFlow3 = 0x004BC05A;
        // public static readonly byte[] PatchLoginFlow1Bytes = { 0xE9, 0x3E, 0x01, 0x00, 0x00, 0x90, 0x90, 0x90, 0x90, 0x90 };
        // public static readonly byte[] PatchLoginFlow2Bytes = { 0xE9, 0xD0, 0x00, 0x00, 0x00, 0x90 };
        // public static readonly byte[] PatchLoginFlow3Bytes = { 0xE9, 0x90, 0x01, 0x00, 0x00, 0x90 };
        // public const int SelectServerVTable = 0x00000000;
        // public const int SelectServerSetSelected = 0x00000000;
        // public const int SelectServerOnSelect = 0x00000000;

        // Active offsets for game-pt-ps0182.exe.
        public const int LoginPointer = 0x007C48FC;
        public const int PatchLoginFlow1 = 0x004D4EBF;
        public const int PatchLoginFlow2 = 0x004D1F5F;
        public const int PatchLoginFlow3 = 0x004D0DCD;

        public static readonly byte[] PatchLoginFlow1Bytes =
        {
            0xE9, 0x54, 0x00, 0x00, 0x00,
            0x90, 0x90, 0x90, 0x90, 0x90
        };

        public static readonly byte[] PatchLoginFlow2Bytes =
        {
            0xE9, 0x64, 0x00, 0x00, 0x00,
            0x90
        };

        public static readonly byte[] PatchLoginFlow3Bytes =
        {
            0xE9, 0x21, 0x01, 0x00, 0x00,
            0x90
        };

        public const int SelectServerVTable = 0x007519B4;
        public const int SelectServerSetSelected = 0x0050BE80;
        public const int SelectServerOnSelect = 0x0050C5F0;
        public const int SelectServerSelectedIndex1 = 0x0BD8;
        public const int SelectServerSelectedIndex2 = 0x1E98;

        // PT0182 CSelectServer::OnSelect reads global state like this:
        // state = *(0x022EED30), serverList = *(state + 0x270), serverCount = *(state + 0x274).
        // Auto-select must wait until these fields are initialized; otherwise the client crashes at 0x0050C629.
        public const int SelectServerGlobalStatePointer = 0x022EED30;
        public const int SelectServerListPointerOffset = 0x0270;
        public const int SelectServerCountOffset = 0x0274;
        public const int SelectServerEntrySize = 0x26;


        // One-shot direct server select hook (PT0182).
        // Original code:
        //   0050CC01 85 C0                 test eax,eax
        //   0050CC03 0F 85 C5 00 00 00     jne 0050CCCE
        // The updater detours this branch to a small one-shot cave:
        //   flag=1 => jump to original OnSelect block once
        //   flag=0 => run original test/jne behavior
        public const int SelectServerUpdateInputBranch = 0x0050CC01;
        public const int SelectServerUpdateInputBranchContinue = 0x0050CC09;
        public const int SelectServerUpdateCallOnSelectBlock = 0x0050CCCE;
        public static readonly byte[] SelectServerUpdateInputBranchOriginalBytes =
        {
            0x85, 0xC0, 0x0F, 0x85, 0xC5, 0x00, 0x00, 0x00
        };

        public const int UserIdOffsetFromPointer = 4;
        public const int TempPasswordOffsetFromPointer = 39;
        public const int UserIdBufferLength = 32;
        public const int TempPasswordBufferLength = 32;
    }
}
