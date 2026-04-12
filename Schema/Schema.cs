using System.Reflection;
using Microsoft.VisualBasic.CompilerServices;

namespace DBSharp.Schema;

public class Schema
{
    public static class  SqlType
    {
        public const int INTEGER = 0;
        public const int VARCHAR = 1;
    }
    private List<string> fields = new List<string>();
    private Dictionary<string, FieldInfo> info = new  Dictionary<string, FieldInfo>();

    public void AddField(string fieldname, int type, int length)
    {
        throw new NotImplementedException();
    }

    public void AddIntField(string fieldname)
    {
        throw new NotImplementedException();
    }

    public void AddStringField(string fieldname, int length)
    {
        throw new NotImplementedException();
    }

    public void Add(string fieldname, Schema sch)
    {
        throw new NotImplementedException();
    }

    public void AddAll(Schema sch)
    {
        throw new NotImplementedException();
    }

    public List<string> Fields()
    {
        throw new NotImplementedException();
    }

    public bool HasField(string fieldname)
    {
        throw new NotImplementedException();
    }

    public int Type(string fieldname)
    {
        throw new NotImplementedException();
    }

    public int Length(string fieldname)
    {
        throw new NotImplementedException();
    }
    class FieldInfo
    {
        int type, length;
        public FieldInfo(int type, int length)
        {
            this.type = type;
            this.length = length;
        }
    }
}