using System.Reflection;

namespace Andreja.Platform.Contracts;

public static class PlatformContractAssembly
{
    public static Assembly Reference => typeof(PlatformContractAssembly).Assembly;
}
