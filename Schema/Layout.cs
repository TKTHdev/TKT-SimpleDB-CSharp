using System.Xml.Schema;
using DBSharp.File;

namespace DBSharp.Schema;

public class Layout
{
    private Schema schema;
    private Dictionary<string, int> offsets;
    private int slotsize;

    public Layout(Schema schema)
    {
        this.schema = schema;
        offsets = new Dictionary<string, int>();
        int pos = sizeof(int); // space for the empty/inuse flag
        foreach (string fieldname in schema.Fields())
        {
           offsets.Add(fieldname, pos);
           pos += lengthInBytes(fieldname);
        }
        slotsize = pos;
    }

    public Layout(Schema schema, Dictionary<string, int> offsets, int slotsize)
    {
        this.schema = schema;
        this.offsets = offsets;
        this.slotsize = slotsize;
    }

    public Schema GetSchema()
    {
        return schema;
    }

    public int GetOffset(string fieldname)
    {
        // maybe need to be modified 
        // since it might not be handled properly when offsets[fieldname] = null?
        return offsets[fieldname];
    }

    public int GetSlotSize()
    {
        return slotsize;
    }
    

    private int lengthInBytes(string fieldname)
    {
        int fieldtype = schema.Type(fieldname);
        if (fieldtype == Schema.SqlType.INTEGER)
            return sizeof(int);
        else // fieldtype == VARCHAR
            return Page.MaxLength(schema.Length(fieldname));
    }
}