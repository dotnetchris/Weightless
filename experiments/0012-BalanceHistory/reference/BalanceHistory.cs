namespace Weightless.Research.BalanceHistory;

public static class BalanceHistory
{
    public static int Append(int opening, int[] deltas, List<int> history)
    {
        int balance = opening;
        foreach (int delta in deltas)
        {
            balance = checked(balance + delta);
            history.Add(balance);
        }
        return balance;
    }
}
