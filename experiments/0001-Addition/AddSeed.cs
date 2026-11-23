public static class AddSeed
{
    public static int Checked(int a, int b) => checked(a + b);
    public static int Wrapping(int a, int b) => unchecked(a + b);

    public static int Constrained(int a, int b)
    {
        var result = checked(a + b);
        if (result < 0)
            throw new PostconditionViolationException();
        return result;
    }
}

public sealed class PostconditionViolationException : Exception;
