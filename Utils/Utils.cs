public static class Utils
{
    public static int PositiveMod(int value, int mod)
    {
        int r = value % mod;         // ������ ��� (������ �� ����)
        return r < 0 ? r + mod : r;  // ������ mod ���ؼ� ����� ��ȯ
    }
}