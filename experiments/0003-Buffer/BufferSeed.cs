public static class BufferSeed
{
    public static int Run(int[] buffer, int storeIndex, int loadIndex, bool shouldStore, int value,
        Action<int, int> observeWrite) => UpdateAndLoad(buffer, storeIndex, loadIndex, shouldStore, value, observeWrite);

    private static int UpdateAndLoad(int[] buffer, int storeIndex, int loadIndex, bool shouldStore, int value,
        Action<int, int> observeWrite)
    {
        if (shouldStore)
        {
            var adjusted = checked(value + 1);
            buffer[storeIndex] = adjusted;
            observeWrite(storeIndex, adjusted);
        }

        return buffer[loadIndex];
    }
}
