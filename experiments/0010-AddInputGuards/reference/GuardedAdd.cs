namespace Weightless.Research.InputGuards;

public static class GuardedAdd
{
    public static int WithIf(int a, int b)
    {
        if (a < -1)
            throw new ArgumentOutOfRangeException(nameof(a), a, null);
        if (b < -1)
            throw new ArgumentOutOfRangeException(nameof(b), b, null);
        return checked(a + b);
    }

    public static int WithHelper(int a, int b)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(a, -1);
        ArgumentOutOfRangeException.ThrowIfLessThan(b, -1);
        return checked(a + b);
    }
}
