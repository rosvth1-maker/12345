using System.Text;

namespace TsmServer.Protocol;

public static class GameCharsets
{
    static GameCharsets()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try
        {
            // Windows-874 / TIS-620 Thai Character Encoding for TS Online Thailand
            Thai = Encoding.GetEncoding(874);
        }
        catch
        {
            Thai = Encoding.UTF8;
        }

        try
        {
            Big5 = Encoding.GetEncoding("big5");
        }
        catch
        {
            Big5 = Encoding.UTF8;
        }
    }

    /// <summary>
    /// Thai Windows-874 / TIS-620 Encoding (100% Thai standard for TS Online)
    /// </summary>
    public static Encoding Thai { get; }

    /// <summary>
    /// Default Game Encoding (100% Thai)
    /// </summary>
    public static Encoding Default => Thai;

    public static Encoding Big5 { get; }
    public static Encoding Utf8 => Encoding.UTF8;
}
