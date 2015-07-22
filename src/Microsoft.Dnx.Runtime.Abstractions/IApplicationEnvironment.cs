// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;

namespace Microsoft.Dnx.Runtime
{
    /// <summary>
    /// Provides access to common application information.
    /// </summary>
    public interface IApplicationEnvironment
    {
        /// <summary>
        /// Gets the application name.
        /// </summary>
        string ApplicationName { get; }

        /// <summary>
        /// Gets the version of the application, as specified in the project.json file.
        /// </summary>
        string ApplicationVersion { get; }

        /// <summary>
        /// Gets the base directory of the application, defined as the path to the directory containing the project.json file.
        /// </summary>
        string ApplicationBasePath { get; }

        /// <summary>
        /// Gets the configuration. This should only be used for runtime compilation.
        /// </summary>
        string Configuration { get; }

        /// <summary>
        /// Gets the target version and profile of the .NET Framework for the application.
        /// </summary>
        FrameworkName RuntimeFramework { get; }

        /// <summary>
        /// Gets the specified value from Application Global Data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Application Global Data is a collection of name-value pairs that is stored in a global set of data shared by the entire
        /// application. On Desktop CLR, this is backed by the <see cref="System.AppDomain.GetData(string)"/> and <see cref="System.AppDomain.SetData(string, object)"/>
        /// methods, and provides access to the same data. In other environments, such as CoreCLR, where AppDomains are not available,
        /// this data set is backed by a specially created global dictionary controlled by the implementor of this interface.
        /// </para>
        /// <para>
        /// On Desktop CLR, this method can be used to retrieve predefined application domain properties. See <see cref="System.AppDomain.GetData(string)"/>
        /// for a complete list.
        /// </para>
        /// </remarks>
        /// <param name="name">The name of the Application Global Data item to retrieve.</param>
        /// <returns>The value of the item identified by <paramref name="name"/>, or null if no item exists.</returns>
        object GetData(string name);

        /// <summary>
        /// Sets the specified value in Application Global Data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Application Global Data is a collection of name-value pairs that is stored in a global set of data shared by the entire
        /// application. On Desktop CLR, this is backed by the <see cref="System.AppDomain.GetData(string)"/> and <see cref="System.AppDomain.SetData(string, object)"/>
        /// methods, and provides access to the same data. In other environments, such as CoreCLR, where AppDomains are not available,
        /// this data set is backed by a specially created global dictionary controlled by the implementor of this interface.
        /// </para>
        /// <para>
        /// On Desktop CLR, this method can be used to modify SOME predefined application domain properties. See <see cref="System.AppDomain.SetData(string, object)"/>
        /// for a complete list.
        /// </para>
        /// </remarks>
        /// <param name="name">The name of the Application Global Data item to set.</param>
        /// <param name="value">The value to store in Application Global Data.</param>
        void SetData(string name, object value);
    }
}
