using System.Reflection;

namespace Andreja.Platform.Contracts;

public static class PlatformContractAssembly
{
  // DISPOSABLE NEGATIVE ENFORCEMENT TEST — DO NOT MERGE.
    public static Assembly Reference => typeof(PlatformContractAssembly).Assembly;
}
