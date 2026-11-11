using NetConfigPro.Models;

namespace NetConfigPro.Generators;

public static class GeneratorFactory
{
    private static readonly Dictionary<Vendor, VendorGenerator> Cache = new()
    {
        [Vendor.Huawei] = new HuaweiGenerator(),
        [Vendor.H3C] = new H3cGenerator(),
        [Vendor.Cisco] = new CiscoGenerator(),
        [Vendor.Ruijie] = new RuijieGenerator()
    };

    public static VendorGenerator Get(Vendor vendor) => Cache[vendor];
}
