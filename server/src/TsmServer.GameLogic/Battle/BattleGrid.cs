namespace TsmServer.GameLogic.Battle;

public class BattleGrid
{
    // Side 0: Player Team (2x5), Side 1: Enemy Team (2x5)
    public const int Rows = 2;
    public const int Cols = 5;

    public static (int Row, int Col) IndexToPos(int index) => (index / Cols, index % Cols);
    public static int PosToIndex(int row, int col) => row * Cols + col;
}
