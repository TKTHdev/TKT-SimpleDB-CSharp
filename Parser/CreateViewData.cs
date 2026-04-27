namespace DBSharp.Parser;

public class CreateViewData
{
    private string _viewname;
    private QueryData _qrydata;

    public CreateViewData(string viewname, QueryData qrydata)
    {
        _viewname = viewname;
        _qrydata = qrydata;
    }

    public string ViewName() => _viewname;
    public string ViewDef() => _qrydata.ToString();
}
