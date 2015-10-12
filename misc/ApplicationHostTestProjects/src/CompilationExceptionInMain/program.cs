using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Dnx.Compilation;

class DummyCompilationException : Exception, ICompilationException
{
    public IEnumerable<CompilationFailure> CompilationFailures => Enumerable.Empty<CompilationFailure>();
}

class Program
{
    public static void Main()
    {
        throw new DummyCompilationException();
    }
}