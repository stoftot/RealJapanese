using System.Reflection;

// Execute the real utility in a child process, catching expected failures before Windows opens a crash dialog.
try
{
    var entryPoint = Assembly.Load("CheckDataForDuplicates").EntryPoint!;
    return (int)entryPoint.Invoke(null, [Array.Empty<string>()])!;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception is TargetInvocationException { InnerException: { } cause } ? cause : exception);
    return 1;
}
