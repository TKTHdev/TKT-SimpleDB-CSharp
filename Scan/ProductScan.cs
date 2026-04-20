using DBSharp.TmpClass;

namespace DBSharp.Scan;

public class ProductScan : IScan
{
    private IScan _s1, _s2;

    public ProductScan(IScan s1, IScan s2)
    {
        _s1 = s1;
        _s2 = s2;
        _s1.Next();
    }
}
