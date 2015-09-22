// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Dnx.DesignTimeHost.FunctionalTests.Infrastructure;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Dnx.DesignTimeHost.FunctionalTests.Util
{
    public static class DthMessageExtension
    {
        public static JObject RetrieveDependency(this DthMessage message, string dependencyName)
        {
            Assert.NotNull(message);
            Assert.Equal("Dependencies", message.MessageType);

            var payload = message.Payload as JObject;
            Assert.NotNull(payload);

            var dependency = payload["Dependencies"][dependencyName] as JObject;
            Assert.NotNull(dependency);
            Assert.Equal(dependencyName, dependency["Name"].Value<string>());

            return dependency;
        }

        public static JObject EnsureObjectPayload(this DthMessage message)
        {
            Assert.NotNull(message);
            Assert.True(message.Payload is JObject);

            return (JObject)message.Payload;
        }

        public static JArray EnsurePropertyAsArray(this JObject json, string propertyName)
        {
            Assert.NotNull(json);

            var property = json[propertyName];
            Assert.NotNull(property);
            Assert.True(property is JArray);

            return (JArray)property;
        }

        public static JArray AssertJArrayCount(this JArray array, int expectedCount)
        {
            Assert.NotNull(array);
            Assert.Equal(expectedCount, array.Count);

            return array;
        }

        public static JArray AssertJArrayElement<T>(this JArray array, int index, T expectedElementValue)
        {
            Assert.NotNull(array);

            var element = array[index];
            Assert.NotNull(element);
            Assert.Equal(expectedElementValue, element.Value<T>());

            return array;
        }


        public static JObject AssertProperty<T>(this JObject json, string propertyName, T expectedPropertyValue)
        {
            Assert.NotNull(json);

            var property = json[propertyName];
            Assert.NotNull(property);
            Assert.Equal(expectedPropertyValue, property.Value<T>());

            return json;
        }
    }
}
