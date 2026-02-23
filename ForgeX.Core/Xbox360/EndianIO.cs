using ForgeX.Core.IO;

namespace ForgeX.Core.Xbox360;

/// <summary>
/// Manages a paired EndianReader/EndianWriter over a FileStream.
/// </summary>
public class EndianIO
{
    public EndianReader Reader { get; set; } = null!;
    public EndianWriter Writer { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public EndianType EndianType { get; set; }
    public FileStream Stream { get; set; } = null!;

    public EndianIO(string fileName, EndianType type, bool keepOpen)
    {
        EndianType = type;
        FileName = fileName;

        Stream = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite);
        Reader = new EndianReader(Stream, EndianType);
        Writer = new EndianWriter(Stream, EndianType);

        if (!keepOpen)
            Close();
    }

    public void Open()
    {
        Stream = new FileStream(FileName, FileMode.Open, FileAccess.ReadWrite);
        Reader = new EndianReader(Stream, EndianType);
        Writer = new EndianWriter(Stream, EndianType);
    }

    public void Close()
    {
        Stream.Close();
    }
}
