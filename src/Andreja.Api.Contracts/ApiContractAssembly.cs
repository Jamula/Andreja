using System.Reflection;

namespace Andreja.Api.Contracts;

public static class ApiContractAssembly
{
    public static Assembly Reference => typeof(ApiContractAssembly).Assembly;
}
