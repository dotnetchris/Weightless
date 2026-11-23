public static class RepeatedAddition
{
    public static int Run(int value, int increment)
    {
        for (var iteration = 0; iteration < 64; iteration++)
            value = unchecked(value + increment);
        return value;
    }
}
