using ForgeX.Core.IO;

namespace ForgeX.Core.Halo3;

public class TagIndexEntry
{
    public Tag? Tag { get; set; }
    public int Ident { get; set; }
    public int Offset { get; set; }
    public byte RunTimeMinimum { get; set; }
    public byte RunTimeMaximum { get; set; }
    public byte CountOnMap { get; set; }
    public byte DesignTimeMaximum { get; set; }
    public float Cost { get; set; }
    public List<PlacementChunk> PlacedItems { get; set; } = new();

    public void Read(EndianReader reader, TagDatabase? tags)
    {
        Offset = (int)reader.BaseStream.Position;
        Ident = reader.ReadInt32();
        Tag = tags?.FindTag(Ident);
        RunTimeMinimum = reader.ReadByte();
        RunTimeMaximum = reader.ReadByte();
        CountOnMap = reader.ReadByte();
        DesignTimeMaximum = reader.ReadByte();
        Cost = reader.ReadSingle();
    }

    public void Write(EndianWriter writer)
    {
        writer.BaseStream.Position = Offset;
        writer.WriteIdent(Ident);
        writer.Write(RunTimeMinimum);
        writer.Write(RunTimeMaximum);
        writer.Write(CountOnMap);
        writer.Write(DesignTimeMaximum);
        writer.WriteFloat(Cost);
    }
}
